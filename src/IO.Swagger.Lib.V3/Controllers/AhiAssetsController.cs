namespace IO.Swagger.Lib.V3.Controllers;

using System;
using System.Collections.Generic;
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
using Newtonsoft.Json.Linq;

[ApiController]
[Route("dev/assets")]
public class AhiAssetsController(
    AssetAdministrationShellRepositoryAPIApiController aasRepoController,
    SubmodelRepositoryAPIApiController smRepoController,
    RuntimeAssetAttributeHandler runtimeAssetAttributeHandler,
    EventPublisher eventPublisher,
    TimeSeriesService timeSeriesService,
    AasApiHelperService aasApiHelper
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
            .Where(a => Guid.TryParse(a.Id, out _)).Select(a => aasApiHelper.ToGetAssetSimpleDto(a, null)).ToArray();
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

                    resultModel.Values = aasApiHelper.ToGetAssetSimpleDto(aas, null);
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
                        attribute.DecimalPlace = aasApiHelper.GetAttributeDecimalPlace(attribute);

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
                                await runtimeAssetAttributeHandler.AddAttributeAsync(attribute, inputAttributes, cancellationToken: default);
                                break;
                            }
                            case AttributeTypeConstants.TYPE_DYNAMIC:
                            {
                                var dynamicPayload = JObject.FromObject(attribute.Payload).ToObject<AssetAttributeDynamic>();
                                if (attribute.DataType is not DataTypeConstants.TYPE_INTEGER and not DataTypeConstants.TYPE_DOUBLE)
                                {
                                    attribute.DecimalPlace = null;
                                    attribute.ThousandSeparator = null;
                                }

                                var deviceId = new Property(DataTypeDefXsd.String)
                                {
                                    DisplayName = [new LangStringNameType("en-US", "DeviceId")],
                                    IdShort = "DeviceId",
                                    Value = dynamicPayload.DeviceId
                                };
                                var metricKey = new Property(DataTypeDefXsd.String)
                                {
                                    DisplayName = [new LangStringNameType("en-US", "MetricKey")],
                                    IdShort = "MetricKey",
                                    Value = dynamicPayload.MetricKey
                                };
                                var dataType = new Property(DataTypeDefXsd.String)
                                {
                                    DisplayName = [new LangStringNameType("en-US", "DataType")],
                                    IdShort = "DataType",
                                    Value = attribute.DataType
                                };
                                var snapShot = TimeSeriesHelper.CreateEmptySnapshot(attribute.DataType);
                                var smc = new SubmodelElementCollection()
                                {
                                    DisplayName = [new LangStringNameType("en-US", attribute.Name)],
                                    IdShort = attribute.Id.ToString(),
                                    Category = attribute.AttributeType,
                                    Value = [deviceId, metricKey, dataType, snapShot]
                                };
                                var snapShotSeries = await timeSeriesService.GetDeviceMetricSnapshot(dynamicPayload.DeviceId, dynamicPayload.MetricKey);
                                if (snapShotSeries is not null)
                                    smc.UpdateSnapshot(snapShotSeries);

                                var encodedSmId = ConvertHelper.ToBase64(assetId.ToString());
                                smRepoController.PostSubmodelElementSubmodelRepo(smc, encodedSmId, first: false);
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
                            updateAttribute.DecimalPlace = aasApiHelper.GetAttributeDecimalPlace(updateAttribute);

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
        var (aas, submodels, _) = aasApiHelper.GetFullAasById(id);
        var attributes = aasApiHelper.ToAttributes(submodels);
        return Ok(aasApiHelper.ToGetAssetDto(aas, attributes));
    }

    [HttpGet("{id}/fetch")]
    public async Task<IActionResult> FetchAsync(Guid id)
    {
        var (aas, submodels, _) = aasApiHelper.GetFullAasById(id);
        var attributes = aasApiHelper.ToAttributes(submodels);
        return Ok(aasApiHelper.ToGetAssetSimpleDto(aas, attributes));
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
                var attr = aasApiHelper.ToAttributeDto(sme);
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
            .Where(a => Guid.TryParse(a.Id, out _)).Select(a => aasApiHelper.ToGetAssetSimpleDto(a, null)).ToArray();

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

    [HttpPost("attributes/runtime/publish")]
    public async Task<IActionResult> PublishRuntimeValue([FromQuery] Guid attributeId, [FromBody] TimeSeriesDto seriesDto)
    {
        var (sm, sme) = Program.FindSmeByGuid(attributeId);
        var smc = sme as ISubmodelElementCollection;
        await timeSeriesService.AddRuntimeSeries(attributeId, seriesDto);
        smc.UpdateSnapshot(seriesDto);
        var encodedSmId = ConvertHelper.ToBase64(sm.Id);
        _ = smRepoController.PutSubmodelElementByPathSubmodelRepo(sme, encodedSmId, sme.IdShort, level: LevelEnum.Deep);
        await eventPublisher.Publish(AasEvents.SubmodelElementUpdated, sme);
        Program.saveEnvDynamic(0);
        return Ok(true);
    }

    [HttpPost("device-metric-series")]
    public async Task<IActionResult> PublishDeviceMetricSeries([FromQuery] string deviceId, [FromQuery] string metricKey, [FromBody] TimeSeriesDto seriesDto)
    {
        await timeSeriesService.AddDeviceMetricSeries(deviceId, metricKey, seriesDto);
        Program.saveEnvDynamic(0);
        return Ok(true);
    }
}