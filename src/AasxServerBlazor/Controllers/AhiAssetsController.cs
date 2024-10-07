namespace AasxServerBlazor.Controllers;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AasxServer;
using AasxServerBlazor.Models;
using AasxServerBlazor.Utils;
using AHI.Infrastructure.Audit.Constant;
using AHI.Infrastructure.SharedKernel.Extension;
using AHI.Infrastructure.SharedKernel.Model;
using Extensions;
using IO.Swagger.Controllers;
using IO.Swagger.Models;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;

[ApiController]
[Route("dev/assets")]
public class AhiAssetsController(
    AssetAdministrationShellRepositoryAPIApiController aasRepoController,
    SubmodelRepositoryAPIApiController smRepoController
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
                        TimeStampCreate = DateTime.UtcNow,
                        TimeStamp = DateTime.UtcNow,
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
                        TimeStampCreate = DateTime.UtcNow,
                        TimeStamp = DateTime.UtcNow,
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

        ActionType mainAction = document.Operations.All(x => string.Equals(x.op, PatchActionConstants.REMOVE, StringComparison.InvariantCultureIgnoreCase))
                                ? ActionType.Delete
                                : ActionType.Update;

        var resultModels = new List<BaseJsonPathDocument>();
        {
            string path;
            Guid attributeId;
            var operations = document.Operations;
            int index = 0;
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
                                    valueType: ToAasDataType(attribute.DataType))
                                {
                                    DisplayName = [new LangStringNameType("en-US", attribute.Name)],
                                    IdShort = attribute.Id.ToString(),
                                    TimeStamp = DateTime.UtcNow,
                                    TimeStampCreate = DateTime.UtcNow,
                                    Value = attribute.Value,
                                    Category = AttributeTypeConstants.TYPE_STATIC
                                };
                                var encodedSmId = ConvertHelper.ToBase64(assetId.ToString());
                                smRepoController.PostSubmodelElementSubmodelRepo(property, encodedSmId, first: false);
                                break;
                            }
                        }
                        break;

                    case PatchActionConstants.EDIT:
                        break;

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

        var attributes = ToAttributes(submodels);
        return Ok(ToGetAssetDto(aas, attributes));
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
                switch (sme.Category)
                {
                    case AttributeTypeConstants.TYPE_STATIC:
                    {
                        var prop = sme as IProperty;
                        var tsDto = new TimeSeriesDto()
                        {
                            v = prop.Value,
                            q = 192, // [TODO]
                            ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                            lts = 0
                        };
                        var tsList = new List<TimeSeriesDto>() { tsDto };
                        attributes.Add(new AttributeDto
                        {
                            GapfillFunction = PostgresFunction.TIME_BUCKET_GAPFILL,
                            Quality = "Good [Non-Specific]", // [TODO]
                            QualityCode = 192, // [TODO]
                            AttributeId = Guid.Parse(prop.IdShort),
                            AttributeName = prop.DisplayName.FirstOrDefault()?.Text ?? prop.IdShort,
                            AttributeType = prop.Category,
                            Uom = null, // [TODO]
                            DecimalPlace = null, // [TODO]
                            ThousandSeparator = null, // [TODO]
                            Series = tsList,
                            DataType = ToAhiDataType(prop.ValueType)
                        });
                        break;
                    }
                    default:
                        throw new NotSupportedException();
                }
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
                switch (sme.Category)
                {
                    case AttributeTypeConstants.TYPE_STATIC:
                    {
                        var prop = sme as IProperty;
                        attributes.Add(new AssetAttributeDto
                        {
                            AssetId = Guid.Parse(sm.Id),
                            AttributeType = prop.Category,
                            CreatedUtc = prop.TimeStampCreate,
                            DataType = ToAhiDataType(prop.ValueType),
                            DecimalPlace = null, // [TODO]
                            Deleted = false,
                            Id = Guid.Parse(prop.IdShort),
                            Name = prop.DisplayName.FirstOrDefault()?.Text ?? prop.IdShort,
                            SequentialNumber = -1, // [TODO]
                            Value = prop.Value,
                            Payload = JObject.FromObject(new
                            {
                                // TemplateAttributeId = sm.Administration?.TemplateId, // [NOTE] AAS doens't have
                                prop.Value
                            }).ToObject<AttributeMapping>(),
                            ThousandSeparator = null, // [TODO]
                            Uom = null, // [TODO]
                            UomId = null, // [TODO]
                            UpdatedUtc = prop.TimeStamp
                        });
                        break;
                    }
                    default:
                        throw new NotSupportedException();
                }
            }
        }
        return attributes;
    }

    private DataTypeDefXsd ToAasDataType(string dataType)
    {
        return dataType switch
        {
            DataTypeConstants.TYPE_TEXT => DataTypeDefXsd.String,
            DataTypeConstants.TYPE_BOOLEAN => DataTypeDefXsd.Boolean,
            DataTypeConstants.TYPE_DATETIME => DataTypeDefXsd.DateTime,
            DataTypeConstants.TYPE_DOUBLE => DataTypeDefXsd.Double,
            DataTypeConstants.TYPE_INTEGER => DataTypeDefXsd.Integer,
            DataTypeConstants.TYPE_TIMESTAMP => DataTypeDefXsd.Long,
            _ => DataTypeDefXsd.String,
        };
    }

    private int? GetAttributeDecimalPlace(AssetAttributeCommand attribute)
    {
        return attribute.DataType == DataTypeConstants.TYPE_DOUBLE ? attribute.DecimalPlace : null;
    }

    private string ToAhiDataType(DataTypeDefXsd aasDataType)
    {
        return aasDataType switch
        {
            DataTypeDefXsd.String => DataTypeConstants.TYPE_TEXT,
            DataTypeDefXsd.Boolean => DataTypeConstants.TYPE_BOOLEAN,
            DataTypeDefXsd.DateTime => DataTypeConstants.TYPE_DATETIME,
            DataTypeDefXsd.Double => DataTypeConstants.TYPE_DOUBLE,
            DataTypeDefXsd.Integer => DataTypeConstants.TYPE_INTEGER,
            DataTypeDefXsd.Long => DataTypeConstants.TYPE_TIMESTAMP,
            _ => DataTypeConstants.TYPE_TEXT, // Default to TEXT for unsupported types
        };
    }

}