using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json.Linq;
using AHI.Infrastructure.SharedKernel.Extension;
using AasxServerStandardBib.Models;
using AasxServerStandardBib.Utils;
using IO.Swagger.Controllers;
using IO.Swagger.Lib.V3.Controllers;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Globalization;

namespace IO.Swagger.Lib.V3.Services;

public class RuntimeAssetAttributeHandler(
    ILogger<RuntimeAssetAttributeHandler> logger)
{
    private AhiAssetsController _ahiAssetsController;
    private SubmodelRepositoryAPIApiController _smRepoController;

    public void SetControllers(AhiAssetsController ahiAssetsController, SubmodelRepositoryAPIApiController smRepoController)
    {
        _ahiAssetsController = ahiAssetsController;
        _smRepoController = smRepoController;
    }

    public async Task AddAttributeAsync(AssetAttributeCommand attribute, IEnumerable<AssetAttributeCommand> inputAttributes, CancellationToken cancellationToken)
    {
        var runtimePayload = JObject.FromObject(attribute.Payload).ToObject<AssetAttributeRuntime>();
        var smc = new SubmodelElementCollection()
        {
            DisplayName = [new LangStringNameType("en-US", attribute.Name)],
            IdShort = attribute.Id.ToString(),
            Category = attribute.AttributeType,
            Extensions = [],
            Value = []
        };

        if (runtimePayload.EnabledExpression)
        {
            var (aas, _, elements) = _ahiAssetsController.GetFullAasById(attribute.AssetId);
            await ValidateRuntimeAttribute(smc, aas, elements, attribute, inputAttributes, runtimePayload);
        }

        var encodedSmId = ConvertHelper.ToBase64(attribute.AssetId.ToString());
        _smRepoController.PostSubmodelElementSubmodelRepo(smc, encodedSmId, first: false);
    }

    private async Task ValidateRuntimeAttribute(ISubmodelElementCollection smc, IAssetAdministrationShell? aas, IEnumerable<ISubmodelElement> currentSmeList, AssetAttributeCommand attribute, IEnumerable<AssetAttributeCommand> inputAttributes, AssetAttributeRuntime runtimePayload)
    {
        var assetId = attribute.AssetId;
        var targetValidateAttributes = new List<AssetTemplateAttributeValidationRequest>();
        if (inputAttributes.Any())
        {
            targetValidateAttributes.AddRange(inputAttributes.Select(item => new AssetTemplateAttributeValidationRequest()
            {
                Id = item.Id,
                DataType = item.DataType
            }));
        }

        var aliasAndTargetAliasPairs = await GetAliasTargetMappings(aas, currentSmeList);
        var attributes = currentSmeList.Select(x =>
        {
            var newAttribute = aliasAndTargetAliasPairs.FirstOrDefault(r => r.Reference.IdShort == x.IdShort);
            return newAttribute.Reference != null
                ? new { x.IdShort, Element = newAttribute.TargetElement }
                : new { x.IdShort, Element = x };
        });
        targetValidateAttributes.AddRange(attributes.Select(att => new AssetTemplateAttributeValidationRequest()
        {
            Id = Guid.Parse(att.IdShort),
            DataType = MappingHelper.ToAhiDataType((att.Element as IProperty)?.ValueType ?? DataTypeDefXsd.String)
        }));

        targetValidateAttributes.AddRange(inputAttributes.Select(att => new AssetTemplateAttributeValidationRequest()
        {
            Id = att.Id,
            DataType = att.DataType
        }));

        var request = new AssetTemplateAttributeValidationRequest()
        {
            Id = Guid.Parse(smc.IdShort),
            DataType = attribute.DataType,
            Expression = runtimePayload.Expression,
            Attributes = targetValidateAttributes
        };
        var (validateResult, expression, matchedAttributes) = await ValidateExpression(request);
        if (!validateResult)
        {
            throw new Exception("Invalid expression");
        }

        runtimePayload.ExpressionCompile = expression;
        var triggers = CreateRuntimeTriggers(runtimePayload, matchedAttributes, inputAttributes.Select(x => x.Id).Distinct());

        smc.Extensions.AddRange([
            new Extension(name: nameof(runtimePayload.Expression), valueType: DataTypeDefXsd.String, value: runtimePayload.Expression),
            new Extension(name: nameof(runtimePayload.ExpressionCompile), valueType: DataTypeDefXsd.String, value: runtimePayload.ExpressionCompile),
            new Extension(name: nameof(runtimePayload.EnabledExpression), valueType: DataTypeDefXsd.Boolean, value: runtimePayload.EnabledExpression.ToString().ToLower(CultureInfo.InvariantCulture)),
            new Extension(name: nameof(runtimePayload.TriggerAttributeIds), valueType: DataTypeDefXsd.String, value: JsonConvert.SerializeObject(triggers)) // [NOTE] temp
        ]);

        if (runtimePayload.TriggerAttributeId.HasValue)
            smc.Extensions.Add(new Extension(name: nameof(runtimePayload.TriggerAttributeId), valueType: DataTypeDefXsd.String, value: runtimePayload.TriggerAttributeId.ToString()));

        smc.Value.Add(new Property(valueType: MappingHelper.ToAasDataType(attribute.DataType))
        {
            DisplayName = [new LangStringNameType("en-US", "Snapshot")],
            IdShort = "Snapshot",
            Value = attribute.Value
        });

        smc.Value.Add(new SubmodelElementList(AasSubmodelElements.SubmodelElementCollection)
        {
            DisplayName = [new LangStringNameType("en-US", "Series")],
            IdShort = "Series",
            OrderRelevant = true,
            Value = []
        });
    }

    private async Task<IEnumerable<(IReferenceElement Reference, IAssetAdministrationShell TargetAas, string TargetSmId, ISubmodelElement TargetElement)>> GetAliasTargetMappings(IAssetAdministrationShell? aas, IEnumerable<ISubmodelElement> currentSmeList)
    {
        var validAliasAssetAttributes = currentSmeList.OfType<IReferenceElement>().ToArray();
        var aliasAndTargetAliasPairs = new List<(IReferenceElement Reference, IAssetAdministrationShell TargetAas, string TargetSmId, ISubmodelElement TargetElement)>();
        foreach (var reference in validAliasAssetAttributes)
        {
            var (aliasAas, smId, aliasSme) = _ahiAssetsController.GetRootAliasSme(reference);
            if (aliasSme == null)
                continue;
            var pair = (reference, aliasAas, smId, aliasSme);
            aliasAndTargetAliasPairs.Add(pair);
        }
        return aliasAndTargetAliasPairs;
    }

    private IEnumerable<Guid> CreateRuntimeTriggers(
        AssetAttributeRuntime runtimePayload,
        IEnumerable<Guid> matchedAttributes,
        IEnumerable<Guid> triggerAttributeIds
    )
    {
        var triggers = new List<Guid>();
        if (runtimePayload.TriggerAttributeId != null)
        {
            var exist = triggerAttributeIds.Contains(runtimePayload.TriggerAttributeId.Value);
            if (!exist)
            {
                throw new Exception("Trigger not found");
            }

            Guid? triggerAttributeId = runtimePayload.TriggerAttributeId.Value;
            if (!matchedAttributes.Contains(triggerAttributeId.Value))
            {
                triggers.Add(triggerAttributeId.Value);
            }
        }

        foreach (var attributeId in matchedAttributes)
        {
            triggers.Add(attributeId);
        }
        return triggers;
    }

    public async Task<(bool, string, HashSet<Guid>)> ValidateExpression(AssetTemplateAttributeValidationRequest request)
    {
        var expressionValidate = request.Expression;

        // *** TODO: NOW VALUE WILL NOT IN VALUE COLUMN ==> now alway true
        if (string.IsNullOrWhiteSpace(expressionValidate))
            return (false, null, null);

        var matchedAttributes = new HashSet<Guid>();
        TryParseIdProperty(expressionValidate, matchedAttributes);
        if (matchedAttributes.Contains(request.Id))
        {
            // cannot self reference
            return (false, null, null);
        }

        //must not include command attribute in expression
        if (request.Attributes.Any(x => matchedAttributes.Contains(x.Id) && x.AttributeType == AttributeTypeConstants.TYPE_COMMAND))
        {
            throw new Exception("Invalid expression");
        }

        if (matchedAttributes.Any(id => !request.Attributes.Select(t => t.Id).Contains(id)))
        {
            throw new Exception("Target attributes not found");
        }

        var dataType = request.DataType;
        var dictionary = new Dictionary<string, object>();
        expressionValidate = BuildExpression(expressionValidate, request, dictionary);

        if (dataType == DataTypeConstants.TYPE_TEXT
            && !request.Attributes.Any(x => expressionValidate.Contains($"request[\"{x.Id}\"]")))
        {
            //if expression contain special character, we need to escape it one more time
            expressionValidate = expressionValidate.ToJson();
        }

        try
        {
            logger.LogTrace(expressionValidate);
            var value = await CSharpScript.EvaluateAsync(expressionValidate, globals: new RuntimeExpressionGlobals { request = dictionary });
            if (!string.IsNullOrWhiteSpace(value.ToString()))
            {
                var result = value.ParseResultWithDataType(dataType);
                return (result, expressionValidate, matchedAttributes);
            }
        }
        catch (Exception exc)
        {
            logger.LogError(exc, exc.Message);
        }
        return (false, null, null);
    }

    private bool TryParseIdProperty(string expressionValidate, HashSet<Guid> matchedAttributes)
    {
        var m = Regex.Match(expressionValidate, RegexConstants.PATTERN_EXPRESSION_KEY, RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(10));
        while (m.Success)
        {
            if (!Guid.TryParse(m.Groups[1].Value, out var idProperty))
                return false;
            if (!matchedAttributes.Contains(idProperty))
                matchedAttributes.Add(idProperty);
            m = m.NextMatch();
        }
        return true;
    }

    private string BuildExpression(string expressionValidate, AssetTemplateAttributeValidationRequest request, Dictionary<string, object> dictionary)
    {
        foreach (var element in request.Attributes)
        {
            object value = null;
            switch (element.DataType?.ToLower())
            {
                case DataTypeConstants.TYPE_DOUBLE:
                    expressionValidate = expressionValidate.Replace($"${{{element.Id}}}$", $"Convert.ToDouble(request[\"{element.Id}\"])");
                    value = 1.0;
                    break;
                case DataTypeConstants.TYPE_INTEGER:
                    expressionValidate = expressionValidate.Replace($"${{{element.Id}}}$", $"Convert.ToInt32(request[\"{element.Id}\"])");
                    value = 1;
                    break;
                case DataTypeConstants.TYPE_BOOLEAN:
                    expressionValidate = expressionValidate.Replace($"${{{element.Id}}}$", $"Convert.ToBoolean(request[\"{element.Id}\"])");
                    value = true;
                    break;
                case DataTypeConstants.TYPE_TIMESTAMP:
                    expressionValidate = expressionValidate.Replace($"${{{element.Id}}}$", $"Convert.ToDouble(request[\"{element.Id}\"])");
                    value = (double)1;
                    break;
                case DataTypeConstants.TYPE_DATETIME:
                    expressionValidate = expressionValidate.Replace($"${{{element.Id}}}$", $"Convert.ToDateTime(request[\"{element.Id}\"])");
                    value = new DateTime(1970, 1, 1);
                    break;
                case DataTypeConstants.TYPE_TEXT:
                    expressionValidate = expressionValidate.Replace($"${{{element.Id}}}$", $"request[\"{element.Id}\"].ToString()");
                    value = "default";
                    break;
            }
            dictionary[element.Id.ToString()] = value;
        }
        return expressionValidate;
    }
}