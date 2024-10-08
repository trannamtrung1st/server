namespace IO.Swagger.Lib.V3.Controllers;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using AasxServer;
using AasxServerStandardBib;
using AasxServerStandardBib.Models;
using AasxServerStandardBib.Services;
using AasxServerStandardBib.Utils;
using AHI.Infrastructure.Audit.Constant;
using AHI.Infrastructure.SharedKernel.Extension;
using AHI.Infrastructure.SharedKernel.Model;
using Extensions;
using IO.Swagger.Controllers;
using IO.Swagger.Lib.V3.Services;
using IO.Swagger.Models;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

[ApiController]
[Route("dev/assets")]
public class AhiAssetsController(
    AssetAdministrationShellRepositoryAPIApiController aasRepoController,
    SubmodelRepositoryAPIApiController smRepoController,
    RuntimeAssetAttributeHandler runtimeAssetAttributeHandler,
    EventPublisher eventPublisher
) : ControllerBase
{
    [HttpPost("search")]
    public async Task<IActionResult> SearchAsync([FromBody] GetAssetByCriteria command)
    {
        var allAasResult = aasRepoController.GetAllAssetAdministrationShells(
            assetIds: null, idShort: null, limit: null, cursor: null) as ObjectResult;
        var pagedResult = allAasResult.Value as PagedResult;
        var assets = pagedResult.result
            .OfType<IAssetAdministrationShell>().AsEnumerable()
            .Where(a => Guid.TryParse(a.Id, out _)).Select(a => ToGetAssetSimpleDto(a, null)).ToArray();
        var totalCount = assets.Length;

        var ahiResp = new BaseSearchResponse<GetAssetSimpleDto>(
            duration: 0,
            totalCount: totalCount,
            pageSize: command.PageSize,
            pageIndex: command.PageIndex,
            data: assets
        );

        return Ok(ahiResp);
    }

    [HttpPatch("edit")]
    public async Task<IActionResult> UpsertAssetAsync([FromBody] JsonPatchDocument document)
    {
        var operations = document.Operations;
        var result = new UpsertAssetDto();
        var resultModels = new List<BaseJsonPathDocument>();

        foreach (var operation in operations)
        {
            string path;
            var resultModel = new BaseJsonPathDocument
            {
                OP = operation.op,
                Path = operation.path
            };

            switch (operation.op)
            {
                case "add":
                    path = operation.path.Replace("/", "");
                    var addAssetDto = JObject.FromObject(operation.value).ToObject<AddAsset>();
                    if (Guid.TryParse(path, out var parentAssetId))
                    {
                        //if elementID null => add in root
                        addAssetDto.ParentAssetId = parentAssetId;
                    }

                    var creator = new Reference(ReferenceTypes.ExternalReference, keys: [new Key(KeyTypes.Entity, value: Guid.Empty.ToString())]); // [TODO]
                    var aasId = addAssetDto.Id.ToString();
                    var aas = new AssetAdministrationShell(
                        id: aasId,
                        assetInformation: new AssetInformation(
                            assetKind: AssetKind.Instance,
                            globalAssetId: addAssetDto.Id.ToString(),
                            specificAssetIds: null,
                            assetType: "Instance")
                    )
                    {
                        Administration = new AdministrativeInformation()
                        {
                            TemplateId = addAssetDto.AssetTemplateId?.ToString(),
                            Creator = creator
                        },
                        DisplayName = [new LangStringNameType("en-US", addAssetDto.Name)],
                        IdShort = aasId,
                        Parent = null, // [TODO]
                        Submodels = []
                    };
                    aasRepoController.PostAssetAdministrationShell(aas);

                    var defaultSm = new Submodel(id: aas.Id)
                    {
                        Administration = new AdministrativeInformation()
                        {
                            TemplateId = addAssetDto.AssetTemplateId?.ToString(),
                            Creator = creator
                        },
                        DisplayName = [new LangStringNameType("en-US", "Properties")],
                        IdShort = aasId,
                        Kind = ModellingKind.Instance,
                        SubmodelElements = []
                    };
                    smRepoController.PostSubmodel(defaultSm, aasIdentifier: ConvertHelper.ToBase64(aas.Id));

                    resultModel.Values = ToGetAssetSimpleDto(aas, null);
                    break;

                case "edit":
                    break;

                case "edit_parent":
                    break;

                case "remove":
                    break;
            }

            resultModels.Add(resultModel);
        }
        result.Data = resultModels;

        Program.saveEnvDynamic(0);
        return Ok(result);
    }

    [HttpPatch("{assetId}/attributes")]
    public async Task<IActionResult> UpsertAttributesAsync(Guid assetId, [FromBody] JsonPatchDocument document)
    {
        // Including the Attribute's name come from Request of Add & Edit action - For Delete, we will only load from DB.
        var addEditAttributes = document.Operations
                                        .Where(x => x.op != PatchActionConstants.REMOVE)
                                        .Select(x => x.value.ToJson().FromJson<AssetAttributeCommand>())
                                        .Select(a => new KeyValuePair<Guid, string>(a.Id, a.Name));
        var deleteAttributes = document.Operations
                                        .Where(x => x.op == PatchActionConstants.REMOVE)
                                        .Select(x => x.value.ToJson().FromJson<DeleteAssetAttribute>())
                                        .SelectMany(a => a.Ids.Select(id => new KeyValuePair<Guid, string>(id, null)));
        var auditAttributes = addEditAttributes.Union(deleteAttributes).ToList();

        var mainAction = document.Operations.All(x => string.Equals(x.op, PatchActionConstants.REMOVE, StringComparison.InvariantCultureIgnoreCase))
                                ? ActionType.Delete
                                : ActionType.Update;

        var resultModels = new List<BaseJsonPathDocument>();
        {
            string path;
            Guid attributeId;
            var operations = document.Operations;
            var index = 0;
            var inputAttributes = operations
                .Where(x => x.op == PatchActionConstants.ADD || x.op == PatchActionConstants.EDIT)
                .Select(x => x.value.ToJson().FromJson<AssetAttributeCommand>());

            foreach (var operation in operations)
            {
                index++;
                var resultModel = new BaseJsonPathDocument
                {
                    OP = operation.op,
                    Path = operation.path
                };
                switch (operation.op)
                {
                    case PatchActionConstants.ADD:
                    {
                        var attribute = operation.value.ToJson().FromJson<AssetAttributeCommand>();
                        attribute.Id = Guid.NewGuid();
                        attribute.AssetId = assetId;
                        attribute.SequentialNumber = index;
                        attribute.DecimalPlace = GetAttributeDecimalPlace(attribute);

                        switch (attribute.AttributeType)
                        {
                            case AttributeTypeConstants.TYPE_STATIC:
                            {
                                var property = new Property(
                                    valueType: MappingHelper.ToAasDataType(attribute.DataType))
                                {
                                    DisplayName = [new LangStringNameType("en-US", attribute.Name)],
                                    IdShort = attribute.Id.ToString(),
                                    Value = attribute.Value,
                                    Category = attribute.AttributeType
                                };
                                var encodedSmId = ConvertHelper.ToBase64(assetId.ToString());
                                smRepoController.PostSubmodelElementSubmodelRepo(property, encodedSmId, first: false);
                                break;
                            }
                            case AttributeTypeConstants.TYPE_ALIAS:
                            {
                                var aliasPayload = JObject.FromObject(attribute.Payload).ToObject<AssetAttributeAlias>();

                                var aliasAasId = ConvertHelper.ToBase64(aliasPayload.AliasAssetId.ToString());
                                var aliasAasResult = aasRepoController.GetAssetAdministrationShellById(aliasAasId) as ObjectResult;
                                var aliasAas = aliasAasResult.Value as IAssetAdministrationShell;

                                var aliasSmRef = aliasAas.Submodels.First(sm => sm.GetAsExactlyOneKey().Value == aliasAas.Id);
                                var aliasSmRefKey = ConvertHelper.ToBase64(aliasSmRef.GetAsExactlyOneKey().Value);
                                var aliasSmResult = smRepoController.GetSubmodelById(aliasSmRefKey, level: LevelEnum.Deep, extent: ExtentEnum.WithBlobValue) as ObjectResult;
                                var aliasSm = aliasSmResult.Value as ISubmodel;
                                var aliasSme = aliasSm.SubmodelElements.First(sme => sme.IdShort == aliasPayload.AliasAttributeId.ToString());

                                var reference = new ReferenceElement()
                                {
                                    Category = attribute.AttributeType,
                                    DisplayName = [new LangStringNameType("en-US", attribute.Name)],
                                    IdShort = attribute.Id.ToString(),
                                    Value = new Reference(ReferenceTypes.ModelReference, [aliasAas.ToKey(), aliasSm.ToKey(), aliasSme.ToKey()])
                                };
                                var encodedSmId = ConvertHelper.ToBase64(assetId.ToString());
                                smRepoController.PostSubmodelElementSubmodelRepo(reference, encodedSmId, first: false);
                                break;
                            }
                            case AttributeTypeConstants.TYPE_RUNTIME:
                            {
                                runtimeAssetAttributeHandler.SetControllers(ahiAssetsController: this, smRepoController);
                                await runtimeAssetAttributeHandler.AddAttributeAsync(attribute, inputAttributes, cancellationToken: default);
                                break;
                            }
                        }
                        break;
                    }
                    case PatchActionConstants.EDIT:
                    {
                        path = operation.path.Replace("/", "");
                        if (Guid.TryParse(path, out attributeId))
                        {
                            var updateAttribute = operation.value.ToJson().FromJson<AssetAttributeCommand>();
                            updateAttribute.AssetId = assetId;
                            updateAttribute.Id = attributeId;
                            updateAttribute.DecimalPlace = GetAttributeDecimalPlace(updateAttribute);

                            switch (updateAttribute.AttributeType)
                            {
                                case AttributeTypeConstants.TYPE_STATIC:
                                {
                                    var smId = ConvertHelper.ToBase64(assetId.ToString());
                                    var smeIdPath = updateAttribute.Id.ToString();
                                    var smeResult = smRepoController.GetSubmodelElementByPathSubmodelRepo(smId, smeIdPath, LevelEnum.Deep, ExtentEnum.WithoutBlobValue) as ObjectResult;
                                    var property = smeResult.Value as IProperty;
                                    property.DisplayName = [new LangStringNameType("en-US", updateAttribute.Name)];
                                    property.Value = updateAttribute.Value;
                                    smRepoController.PutSubmodelElementByPathSubmodelRepo(property, smId, smeIdPath, level: LevelEnum.Deep);
                                    await eventPublisher.Publish(AasEvents.SubmodelElementUpdated, property);
                                    break;
                                }
                            }

                        }
                        break;
                    }

                    case PatchActionConstants.EDIT_TEMPLATE:
                        break;

                    case PatchActionConstants.REMOVE:
                        break;
                }

                resultModels.Add(resultModel);
            }
        }

        var result = new UpsertAssetAttributeDto
        {
            Data = resultModels
        };

        Program.saveEnvDynamic(0);
        return Ok(result);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetAssetByIdAsync([FromRoute] Guid id)
    {
        var (aas, submodels, _) = GetFullAasById(id);
        var attributes = ToAttributes(submodels);
        return Ok(ToGetAssetDto(aas, attributes));
    }

    [HttpGet("{id}/fetch")]
    public async Task<IActionResult> FetchAsync(Guid id)
    {
        var (aas, submodels, _) = GetFullAasById(id);
        var attributes = ToAttributes(submodels);
        return Ok(ToGetAssetSimpleDto(aas, attributes));
    }

    [HttpGet("{id}/snapshot")]
    public async Task<IActionResult> GetAttributeSnapshotAsync(Guid id)
    {
        var encodedId = ConvertHelper.ToBase64(id.ToString());
        var aasResult = aasRepoController.GetAssetAdministrationShellById(encodedId) as ObjectResult;
        var aas = aasResult.Value as IAssetAdministrationShell;
        var attributes = new List<AttributeDto>();

        foreach (var sm in aas.Submodels)
        {
            var smId = ConvertHelper.ToBase64(sm.GetAsExactlyOneKey().Value);
            var submodelResult = smRepoController.GetSubmodelById(smId, level: LevelEnum.Deep, extent: ExtentEnum.WithBlobValue) as ObjectResult;
            var submodel = submodelResult.Value as ISubmodel;
            var smeList = submodel.SubmodelElements;

            foreach (var sme in smeList)
            {
                var attr = ToAttributeDto(sme);
                if (attr is not null)
                    attributes.Add(attr);
            }
        }

        return Ok(new HistoricalDataDto
        {
            AssetId = id,
            AssetName = aas.DisplayName.FirstOrDefault()?.Text ?? aas.IdShort,
            Attributes = attributes
        });
    }

    [HttpPost("paths")]
    public async Task<IActionResult> GetAssetPathsAsync([FromBody] IEnumerable<Guid> ids)
    {
        var paths = new List<AssetPathDto>();
        var aassResult = aasRepoController.GetAllAssetAdministrationShells(
            assetIds: ids.Select(id => new SpecificAssetId(name: string.Empty, value: id.ToString())).ToList(),
            idShort: null, limit: null, cursor: null
        ) as ObjectResult;
        var pagedResult = aassResult.Value as PagedResult;
        var assets = pagedResult.result
            .OfType<IAssetAdministrationShell>().AsEnumerable()
            .Where(a => Guid.TryParse(a.Id, out _)).Select(a => ToGetAssetSimpleDto(a, null)).ToArray();

        foreach (var aas in assets)
        {
            // [TODO] hierarchy
            var assetPathDto = new AssetPathDto(aas.Id, pathId: null, pathName: null);
            paths.Add(assetPathDto);
        }

        return Ok(paths);
    }

    [HttpPost("attributes/validate")]
    public async Task<IActionResult> ValidateAssetAttributesAsync(ValidateAssetAttributeList command)
    {
        command.ValidationType = ValidationType.Asset;
        // [TODO]
        var response = new ValidateAssetAttributeListResponse()
        {
            Properties = []
        };
        return Ok(response);
    }

    [HttpPost("attributes/validate/multiple")]
    public async Task<IActionResult> ValidateMultipleAssetAttributesAsync(ValidateMultipleAssetAttributeList command)
    {
        command.ValidationType = ValidationType.Asset;
        // [TODO]
        var response = new List<ValidateMultipleAssetAttributeListResponse>();
        return Ok(response);
    }

    [ApiExplorerSettings(IgnoreApi = true)]
    public (IAssetAdministrationShell aas, List<ISubmodel> submodels, IEnumerable<ISubmodelElement> elements) GetFullAasById(Guid id)
    {
        var encodedId = ConvertHelper.ToBase64(id.ToString());
        var aasResult = aasRepoController.GetAssetAdministrationShellById(encodedId) as ObjectResult;
        var aas = aasResult.Value as IAssetAdministrationShell;
        var submodels = new List<ISubmodel>();

        foreach (var sm in aas.Submodels)
        {
            var smId = ConvertHelper.ToBase64(sm.GetAsExactlyOneKey().Value);
            var submodelResult = smRepoController.GetSubmodelById(smId, level: LevelEnum.Deep, extent: ExtentEnum.WithBlobValue) as ObjectResult;
            var submodel = submodelResult.Value as ISubmodel;
            submodels.Add(submodel);
        }

        var elements = submodels.SelectMany(sm => sm.SubmodelElements).ToArray();
        return (aas, submodels, elements);
    }

    private GetAssetSimpleDto ToGetAssetSimpleDto(IAssetAdministrationShell aas, IEnumerable<AssetAttributeDto> attributes)
    {
        return new GetAssetSimpleDto()
        {
            AssetTemplateId = Guid.TryParse(aas.Administration?.TemplateId, out var templateId) ? templateId : null, // [TODO]
            AssetTemplateName = null, // [TODO]
            Name = aas.DisplayName?.FirstOrDefault()?.Text ?? aas.IdShort,
            Attributes = attributes,
            Children = null, // [TODO]
            CreatedBy = aas.Administration?.Creator?.GetAsExactlyOneKey()?.Value, // [TODO]
            CreatedUtc = aas.TimeStampCreate,
            CurrentTimestamp = DateTime.UtcNow,
            CurrentUserUpn = null,
            HasWarning = false, // [TODO]
            UpdatedUtc = aas.TimeStamp,
            RetentionDays = -1, // [TODO],
            Id = Guid.TryParse(aas.Id, out var id) ? id : default,
            IsDocument = false, // [TODO]
            ParentAssetId = null, // [TODO]
            Parent = null, // [TODO]
            ResourcePath = null, // [TODO]
            RequestLockTimeout = null, // [TODO]
            RequestLockTimestamp = null, // [TODO]
            RequestLockUserUpn = null, // [TODO]
        };
    }

    private AssetAttributeDto ToAssetAttributeDto(ISubmodel sm, ISubmodelElement sme)
    {
        switch (sme.Category)
        {
            case AttributeTypeConstants.TYPE_STATIC:
            {
                var snapshot = sme as IProperty;
                var dataType = MappingHelper.ToAhiDataType(snapshot.ValueType);
                return new AssetAttributeDto
                {
                    AssetId = Guid.Parse(sm.Id),
                    AttributeType = snapshot.Category,
                    CreatedUtc = snapshot.TimeStampCreate,
                    DataType = dataType,
                    DecimalPlace = null, // [TODO]
                    Deleted = false,
                    Id = Guid.Parse(snapshot.IdShort),
                    Name = snapshot.DisplayName.FirstOrDefault()?.Text ?? snapshot.IdShort,
                    SequentialNumber = -1, // [TODO]
                    Value = snapshot?.Value.ParseValueWithDataType(dataType, snapshot.Value, isRawData: false),
                    Payload = JObject.FromObject(new
                    {
                        // templateAttributeId = sm.Administration?.TemplateId, // [NOTE] AAS doens't have
                        value = snapshot.Value
                    }).ToObject<AttributeMapping>(),
                    ThousandSeparator = null, // [TODO]
                    Uom = null, // [TODO]
                    UomId = null, // [TODO]
                    UpdatedUtc = snapshot.TimeStamp
                };
            }
            case AttributeTypeConstants.TYPE_ALIAS:
            {
                var reference = sme as IReferenceElement;
                var (aliasAas, _, aliasSme) = GetRootAliasSme(reference);
                var aliasDto = ToAssetAttributeDto(sm, aliasSme);
                if (aliasDto is null)
                    return null;
                return new AssetAttributeDto
                {
                    AssetId = Guid.Parse(sm.Id),
                    AttributeType = reference.Category,
                    CreatedUtc = reference.TimeStampCreate,
                    DataType = aliasDto.DataType,
                    DecimalPlace = aliasDto.DecimalPlace,
                    Deleted = false,
                    Id = Guid.Parse(reference.IdShort),
                    Name = reference.DisplayName.FirstOrDefault()?.Text ?? reference.IdShort,
                    SequentialNumber = -1, // [TODO]
                    Value = aliasDto.Value,
                    Payload = JObject.FromObject(new
                    {
                        id = reference.IdShort,
                        aliasAssetId = aliasDto.AssetId,
                        aliasAttributeId = Guid.Parse(aliasSme.IdShort),
                        aliasAssetName = aliasAas.DisplayName.FirstOrDefault()?.Text ?? aliasAas.IdShort,
                        aliasAttributeName = aliasSme.DisplayName.FirstOrDefault()?.Text ?? aliasSme.IdShort,
                    }).ToObject<AttributeMapping>(),
                    ThousandSeparator = aliasDto.ThousandSeparator,
                    Uom = aliasDto.Uom,
                    UomId = aliasDto.UomId,
                    UpdatedUtc = reference.TimeStamp
                };
            }
            case AttributeTypeConstants.TYPE_RUNTIME:
            {
                var smc = sme as ISubmodelElementCollection;
                var triggerAttributeIdStr = smc.FindFirstIdShortAs<IProperty>(nameof(AssetAttributeRuntime.TriggerAttributeId))?.Value;
                Guid? triggerAttributeId = triggerAttributeIdStr != null ? Guid.Parse(triggerAttributeIdStr) : null;
                var triggerAttributeIds = smc.FindFirstIdShortAs<IProperty>(nameof(AssetAttributeRuntime.TriggerAttributeIds))?.Value;
                var enabledExpression = smc.FindFirstIdShortAs<IProperty>(nameof(AssetAttributeRuntime.EnabledExpression))?.Value;
                var expression = smc.FindFirstIdShortAs<IProperty>(nameof(AssetAttributeRuntime.Expression))?.Value;
                var expressionCompile = smc.FindFirstIdShortAs<IProperty>(nameof(AssetAttributeRuntime.ExpressionCompile))?.Value;
                AttributeMapping payload;

                {
                    Guid? triggerAssetId = Guid.Parse(sm.Id);
                    bool? hasTriggerError = null;
                    if (triggerAttributeId != null)
                    {
                        var triggers = JsonConvert.DeserializeObject<IEnumerable<Guid>>(triggerAttributeIds);
                        var triggerAssetAttributeExists = triggers.Contains(triggerAttributeId.Value);
                        if (!triggerAssetAttributeExists)
                            hasTriggerError = true;
                    }
                    payload = JObject.FromObject(new
                    {
                        id = Guid.Parse(smc.IdShort),
                        enabledExpression = bool.TryParse(enabledExpression, out var enabled) && enabled,
                        expression,
                        expressionCompile,
                        triggerAssetId,
                        triggerAttributeId,
                        hasTriggerError
                    }).ToObject<AttributeMapping>();
                }

                var snapshotSmc = smc.FindFirstIdShortAs<ISubmodelElementCollection>("Snapshot");
                var snapshot = snapshotSmc.FindFirstIdShortAs<IProperty>("Value");
                var dataType = smc.FindFirstIdShortAs<IProperty>("DataType").Value;
                return new AssetAttributeDto
                {
                    AssetId = Guid.Parse(sm.Id),
                    AttributeType = smc.Category,
                    CreatedUtc = smc.TimeStampCreate,
                    DataType = dataType,
                    DecimalPlace = null, // [TODO]
                    Deleted = false,
                    Id = Guid.Parse(smc.IdShort),
                    Name = smc.DisplayName.FirstOrDefault()?.Text ?? smc.IdShort,
                    SequentialNumber = -1, // [TODO]
                    Value = snapshot?.Value.ParseValueWithDataType(dataType, snapshot.Value, isRawData: false),
                    Payload = payload,
                    ThousandSeparator = null, // [TODO]
                    Uom = null, // [TODO]
                    UomId = null, // [TODO]
                    UpdatedUtc = smc.TimeStamp
                };
            }
            default:
                return null;
        }
    }

    [ApiExplorerSettings(IgnoreApi = true)]
    public (IAssetAdministrationShell Aas, string SubmodelId, ISubmodelElement Sme) GetAliasSme(IReferenceElement reference)
    {
        var aasId = ConvertHelper.ToBase64(reference.Value.Keys[0].Value);
        var aasResult = aasRepoController.GetAssetAdministrationShellById(aasId) as ObjectResult;
        var aliasAas = aasResult.Value as IAssetAdministrationShell;
        var smIdRaw = reference.Value.Keys[1].Value;
        var smId = ConvertHelper.ToBase64(smIdRaw);
        var smeIdPath = reference.Value.Keys[2].Value;
        var smeResult = smRepoController.GetSubmodelElementByPathSubmodelRepo(smId, smeIdPath, LevelEnum.Deep, ExtentEnum.WithoutBlobValue) as ObjectResult;
        var aliasSme = smeResult.Value as ISubmodelElement;
        return (aliasAas, smIdRaw, aliasSme);
    }

    [ApiExplorerSettings(IgnoreApi = true)]
    public (IAssetAdministrationShell Aas, string SubmodelId, ISubmodelElement Sme) GetRootAliasSme(IReferenceElement reference)
    {
        var (aliasAas, smId, aliasSme) = GetAliasSme(reference);
        while (aliasSme is IReferenceElement refElement)
            (aliasAas, smId, aliasSme) = GetAliasSme(refElement);
        return (aliasAas, smId, aliasSme);
    }

    [ApiExplorerSettings(IgnoreApi = true)]
    public AttributeDto ToAttributeDto(ISubmodelElement sme)
    {
        switch (sme.Category)
        {
            case AttributeTypeConstants.TYPE_STATIC:
            {
                var snapshot = sme as IProperty;
                var dataType = MappingHelper.ToAhiDataType(snapshot.ValueType);
                var tsDto = new TimeSeriesDto()
                {
                    v = snapshot.Value.ParseValueWithDataType(dataType, snapshot.Value, isRawData: false),
                    q = 192, // [TODO]
                    ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), // [NOTE] TimeStamp is not serialized
                    lts = 0
                };
                var tsList = new List<TimeSeriesDto>() { tsDto };
                return new AttributeDto
                {
                    GapfillFunction = PostgresFunction.TIME_BUCKET_GAPFILL,
                    Quality = "Good [Non-Specific]", // [TODO]
                    QualityCode = 192, // [TODO]
                    AttributeId = Guid.Parse(snapshot.IdShort),
                    AttributeName = snapshot.DisplayName.FirstOrDefault()?.Text ?? snapshot.IdShort,
                    AttributeType = snapshot.Category,
                    Uom = null, // [TODO]
                    DecimalPlace = null, // [TODO]
                    ThousandSeparator = null, // [TODO]
                    Series = tsList,
                    DataType = dataType
                };
            }
            case AttributeTypeConstants.TYPE_ALIAS:
            {
                var reference = sme as IReferenceElement;
                var (_, _, aliasSme) = GetRootAliasSme(reference);
                var aliasAttrDto = ToAttributeDto(aliasSme);
                if (aliasAttrDto is null)
                    return null;
                return new AttributeDto
                {
                    GapfillFunction = aliasAttrDto.GapfillFunction,
                    Quality = aliasAttrDto.Quality,
                    QualityCode = aliasAttrDto.QualityCode,
                    AttributeId = Guid.Parse(reference.IdShort),
                    AttributeName = reference.DisplayName.FirstOrDefault()?.Text ?? reference.IdShort,
                    AttributeType = reference.Category,
                    Uom = aliasAttrDto.Uom,
                    DecimalPlace = aliasAttrDto.DecimalPlace,
                    ThousandSeparator = aliasAttrDto.ThousandSeparator,
                    Series = aliasAttrDto.Series,
                    DataType = aliasAttrDto.DataType
                };
            }
            case AttributeTypeConstants.TYPE_RUNTIME:
            {
                var smc = sme as ISubmodelElementCollection;
                var tsList = new List<TimeSeriesDto>();
                var snapshotSmc = smc.FindFirstIdShortAs<ISubmodelElementCollection>("Snapshot");
                var dataType = smc.FindFirstIdShortAs<IProperty>("DataType").Value;
                if (snapshotSmc.Value is not null)
                {
                    var snapshotValue = snapshotSmc.FindFirstIdShortAs<IProperty>("Value");
                    var timestamp = snapshotSmc.FindFirstIdShortAs<IProperty>("Timestamp");
                    var tsDto = new TimeSeriesDto()
                    {
                        v = snapshotValue.Value.ParseValueWithDataType(dataType, snapshotValue.Value, isRawData: false),
                        q = 192, // [TODO]
                        ts = long.Parse(timestamp.Value, CultureInfo.InvariantCulture),
                        lts = 0
                    };
                    tsList.Add(tsDto);
                }
                return new AttributeDto
                {
                    GapfillFunction = PostgresFunction.TIME_BUCKET_GAPFILL,
                    Quality = "Good [Non-Specific]", // [TODO]
                    QualityCode = 192, // [TODO]
                    AttributeId = Guid.Parse(smc.IdShort),
                    AttributeName = smc.DisplayName.FirstOrDefault()?.Text ?? smc.IdShort,
                    AttributeType = smc.Category,
                    Uom = null, // [TODO]
                    DecimalPlace = null, // [TODO]
                    ThousandSeparator = null, // [TODO]
                    Series = tsList,
                    DataType = dataType
                };
            }
            default:
                return null;
        }
    }

    private GetAssetDto ToGetAssetDto(IAssetAdministrationShell aas, IEnumerable<AssetAttributeDto> assetAttributes)
    {
        return ToGetAssetSimpleDto(aas, assetAttributes);
    }

    private IEnumerable<AssetAttributeDto> ToAttributes(IEnumerable<ISubmodel> submodel)
    {
        var attributes = new List<AssetAttributeDto>();
        foreach (var sm in submodel)
        {
            foreach (var sme in sm.SubmodelElements)
            {
                var attr = ToAssetAttributeDto(sm, sme);
                if (attr is not null)
                    attributes.Add(attr);
            }
        }
        return attributes;
    }

    private int? GetAttributeDecimalPlace(AssetAttributeCommand attribute)
    {
        return attribute.DataType == DataTypeConstants.TYPE_DOUBLE ? attribute.DecimalPlace : null;
    }
}