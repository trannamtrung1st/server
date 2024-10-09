namespace IO.Swagger.Lib.V3.Services;

using System;
using System.Collections.Generic;
using System.Linq;
using AasxServerStandardBib.Models;
using AasxServerStandardBib.Utils;
using Extensions;
using IO.Swagger.Controllers;
using IO.Swagger.Models;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public class AasApiHelperService(
    AssetAdministrationShellRepositoryAPIApiController aasRepoController,
    SubmodelRepositoryAPIApiController smRepoController)
{
    public (IAssetAdministrationShell Aas, string SubmodelId, ISubmodelElement Sme) GetAliasSme(IReferenceElement reference)
    {
        var aasId = reference.Value.Keys[0].Value;
        var aliasAas = GetById(aasId);
        var smIdRaw = reference.Value.Keys[1].Value;
        var smId = ConvertHelper.ToBase64(smIdRaw);
        var smeIdPath = reference.Value.Keys[2].Value;
        var smeResult = smRepoController.GetSubmodelElementByPathSubmodelRepo(smId, smeIdPath, LevelEnum.Deep, ExtentEnum.WithoutBlobValue) as ObjectResult;
        var aliasSme = smeResult.Value as ISubmodelElement;
        return (aliasAas, smIdRaw, aliasSme);
    }

    public (IAssetAdministrationShell Aas, string SubmodelId, ISubmodelElement Sme) GetRootAliasSme(IReferenceElement reference)
    {
        var (aliasAas, smId, aliasSme) = GetAliasSme(reference);
        while (aliasSme is IReferenceElement refElement)
            (aliasAas, smId, aliasSme) = GetAliasSme(refElement);
        return (aliasAas, smId, aliasSme);
    }

    public (IAssetAdministrationShell aas, List<ISubmodel> submodels, IEnumerable<ISubmodelElement> elements) GetFullAasById(Guid id)
    {
        var aas = GetById(id.ToString());
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

    public GetAssetSimpleDto ToGetAssetSimpleDto(IAssetAdministrationShell aas,
        IAssetAdministrationShell? parent = null,
        IEnumerable<AssetAttributeDto>? attributes = null)
    {
        return new GetAssetSimpleDto()
        {
            AssetTemplateId = Guid.TryParse(aas.Administration?.TemplateId, out var templateId) ? templateId : null, // [TODO]
            AssetTemplateName = null, // [TODO]
            Name = aas.DisplayName?.FirstOrDefault()?.Text ?? aas.IdShort,
            Attributes = attributes,
            Children = [],
            CreatedBy = aas.Administration?.Creator?.GetAsExactlyOneKey()?.Value, // [TODO]
            CreatedUtc = aas.TimeStampCreate,
            CurrentTimestamp = DateTime.UtcNow,
            CurrentUserUpn = null,
            HasWarning = false, // [TODO]
            UpdatedUtc = aas.TimeStamp,
            RetentionDays = -1, // [TODO],
            Id = Guid.TryParse(aas.Id, out var id) ? id : default,
            IsDocument = false, // [TODO]
            ParentAssetId = Guid.TryParse(aas.Extensions?.FirstOrDefault(e => e.Name == "ParentAssetId")?.Value, out var pId) ? pId : null,
            Parent = parent != null ? ToGetAssetSimpleDto(aas: parent, parent: null, attributes: null) : null,
            ResourcePath = aas.Extensions?.FirstOrDefault(e => e.Name == "ResourcePath")?.Value,
            RequestLockTimeout = null, // [TODO]
            RequestLockTimestamp = null, // [TODO]
            RequestLockUserUpn = null, // [TODO]
        };
    }

    public AssetAttributeDto ToAssetAttributeDto(ISubmodel sm, ISubmodelElement sme)
    {
        switch (sme.Category)
        {
            case AttributeTypeConstants.TYPE_STATIC:
            {
                var pStatic = sme as IProperty;
                var snapshotId = Guid.Parse(pStatic.IdShort);
                var dataType = MappingHelper.ToAhiDataType(pStatic.ValueType);
                return new AssetAttributeDto
                {
                    AssetId = Guid.Parse(sm.Id),
                    AttributeType = pStatic.Category,
                    CreatedUtc = pStatic.TimeStampCreate,
                    DataType = dataType,
                    DecimalPlace = null, // [TODO]
                    Deleted = false,
                    Id = snapshotId,
                    Name = pStatic.DisplayName.FirstOrDefault()?.Text ?? pStatic.IdShort,
                    SequentialNumber = -1, // [TODO]
                    Value = pStatic?.Value.ParseValueWithDataType(dataType, pStatic.Value, isRawData: false),
                    Payload = JObject.FromObject(new
                    {
                        // templateAttributeId = sm.Administration?.TemplateId, // [NOTE] AAS doens't have
                        id = snapshotId,
                        value = pStatic.Value
                    }).ToObject<AttributeMapping>(),
                    ThousandSeparator = null, // [TODO]
                    Uom = null, // [TODO]
                    UomId = null, // [TODO]
                    UpdatedUtc = pStatic.TimeStamp
                };
            }
            case AttributeTypeConstants.TYPE_ALIAS:
            {
                var rAlias = sme as IReferenceElement;
                var refId = Guid.Parse(rAlias.IdShort);
                var (aliasAas, _, aliasSme) = GetRootAliasSme(rAlias);
                var aliasDto = ToAssetAttributeDto(sm, aliasSme);
                if (aliasDto is null)
                    return null;
                return new AssetAttributeDto
                {
                    AssetId = Guid.Parse(sm.Id),
                    AttributeType = rAlias.Category,
                    CreatedUtc = rAlias.TimeStampCreate,
                    DataType = aliasDto.DataType,
                    DecimalPlace = aliasDto.DecimalPlace,
                    Deleted = false,
                    Id = refId,
                    Name = rAlias.DisplayName.FirstOrDefault()?.Text ?? rAlias.IdShort,
                    SequentialNumber = -1, // [TODO]
                    Value = aliasDto.Value,
                    Payload = JObject.FromObject(new
                    {
                        id = refId,
                        aliasAssetId = aliasDto.AssetId,
                        aliasAttributeId = Guid.Parse(aliasSme.IdShort),
                        aliasAssetName = aliasAas.DisplayName.FirstOrDefault()?.Text ?? aliasAas.IdShort,
                        aliasAttributeName = aliasSme.DisplayName.FirstOrDefault()?.Text ?? aliasSme.IdShort,
                    }).ToObject<AttributeMapping>(),
                    ThousandSeparator = aliasDto.ThousandSeparator,
                    Uom = aliasDto.Uom,
                    UomId = aliasDto.UomId,
                    UpdatedUtc = rAlias.TimeStamp
                };
            }
            case AttributeTypeConstants.TYPE_RUNTIME:
            {
                var smcRuntime = sme as ISubmodelElementCollection;
                var runtimeId = Guid.Parse(smcRuntime.IdShort);
                var triggerAttributeIdStr = smcRuntime.FindFirstIdShortAs<IProperty>(nameof(AssetAttributeRuntime.TriggerAttributeId))?.Value;
                Guid? triggerAttributeId = triggerAttributeIdStr != null ? Guid.Parse(triggerAttributeIdStr) : null;
                var triggerAttributeIds = smcRuntime.FindFirstIdShortAs<IProperty>(nameof(AssetAttributeRuntime.TriggerAttributeIds))?.Value;
                var enabledExpression = smcRuntime.FindFirstIdShortAs<IProperty>(nameof(AssetAttributeRuntime.EnabledExpression))?.Value;
                var expression = smcRuntime.FindFirstIdShortAs<IProperty>(nameof(AssetAttributeRuntime.Expression))?.Value;
                var expressionCompile = smcRuntime.FindFirstIdShortAs<IProperty>(nameof(AssetAttributeRuntime.ExpressionCompile))?.Value;
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
                        id = runtimeId,
                        enabledExpression = bool.TryParse(enabledExpression, out var enabled) && enabled,
                        expression,
                        expressionCompile,
                        triggerAssetId,
                        triggerAttributeId,
                        hasTriggerError
                    }).ToObject<AttributeMapping>();
                }

                var snapshotSeries = smcRuntime.GetSnapshotTimeSeries();
                var snapshotValue = snapshotSeries?.v;
                var dataType = smcRuntime.GetDataType();
                return new AssetAttributeDto
                {
                    AssetId = Guid.Parse(sm.Id),
                    AttributeType = smcRuntime.Category,
                    CreatedUtc = smcRuntime.TimeStampCreate,
                    DataType = dataType,
                    DecimalPlace = null, // [TODO]
                    Deleted = false,
                    Id = runtimeId,
                    Name = smcRuntime.DisplayName.FirstOrDefault()?.Text ?? smcRuntime.IdShort,
                    SequentialNumber = -1, // [TODO]
                    Value = snapshotValue?.ParseValueWithDataType(dataType, $"{snapshotValue}", isRawData: false),
                    Payload = payload,
                    ThousandSeparator = null, // [TODO]
                    Uom = null, // [TODO]
                    UomId = null, // [TODO]
                    UpdatedUtc = smcRuntime.TimeStamp
                };
            }
            case AttributeTypeConstants.TYPE_DYNAMIC:
            {
                var smcDynamic = sme as ISubmodelElementCollection;
                var dataType = smcDynamic.GetDataType();
                var snapshotSeries = smcDynamic.GetSnapshotTimeSeries();
                var dynamicId = Guid.Parse(smcDynamic.IdShort);
                var deviceId = smcDynamic.FindFirstIdShortAs<IProperty>("DeviceId").Value;
                var metricKey = smcDynamic.FindFirstIdShortAs<IProperty>("MetricKey").Value;
                return new AssetAttributeDto
                {
                    AssetId = Guid.Parse(sm.Id),
                    AttributeType = smcDynamic.Category,
                    CreatedUtc = smcDynamic.TimeStampCreate,
                    DataType = dataType,
                    DecimalPlace = null, // [TODO]
                    Deleted = false,
                    Id = dynamicId,
                    Name = smcDynamic.DisplayName.FirstOrDefault()?.Text ?? smcDynamic.IdShort,
                    SequentialNumber = -1, // [TODO]
                    Value = snapshotSeries?.v,
                    Payload = JObject.FromObject(new
                    {
                        id = dynamicId,
                        deviceId,
                        metricKey,
                    }).ToObject<AttributeMapping>(),
                    ThousandSeparator = null, // [TODO]
                    Uom = null, // [TODO]
                    UomId = null, // [TODO]
                    UpdatedUtc = smcDynamic.TimeStamp
                };
            }
            default:
                return null;
        }
    }

    public AttributeDto ToAttributeDto(ISubmodelElement sme)
    {
        switch (sme.Category)
        {
            case AttributeTypeConstants.TYPE_STATIC:
            {
                var pStatic = sme as IProperty;
                var dataType = MappingHelper.ToAhiDataType(pStatic.ValueType);
                var tsDto = TimeSeriesHelper.BuildSeriesDto(
                    value: pStatic.Value.ParseValueWithDataType(dataType, pStatic.Value, isRawData: false),
                    timestamp: null // [NOTE] TimeStamp is not serialized
                );
                var tsList = new List<TimeSeriesDto>() { tsDto };
                return new AttributeDto
                {
                    GapfillFunction = PostgresFunction.TIME_BUCKET_GAPFILL,
                    Quality = tsDto.q.GetQualityName(),
                    QualityCode = tsDto.q,
                    AttributeId = Guid.Parse(pStatic.IdShort),
                    AttributeName = pStatic.DisplayName.FirstOrDefault()?.Text ?? pStatic.IdShort,
                    AttributeType = pStatic.Category,
                    Uom = null, // [TODO]
                    DecimalPlace = null, // [TODO]
                    ThousandSeparator = null, // [TODO]
                    Series = tsList,
                    DataType = dataType
                };
            }
            case AttributeTypeConstants.TYPE_ALIAS:
            {
                var rAlias = sme as IReferenceElement;
                var (_, _, aliasSme) = GetRootAliasSme(rAlias);
                var aliasAttrDto = ToAttributeDto(aliasSme);
                if (aliasAttrDto is null)
                    return null;
                return new AttributeDto
                {
                    GapfillFunction = aliasAttrDto.GapfillFunction,
                    Quality = aliasAttrDto.Quality,
                    QualityCode = aliasAttrDto.QualityCode,
                    AttributeId = Guid.Parse(rAlias.IdShort),
                    AttributeName = rAlias.DisplayName.FirstOrDefault()?.Text ?? rAlias.IdShort,
                    AttributeType = rAlias.Category,
                    Uom = aliasAttrDto.Uom,
                    DecimalPlace = aliasAttrDto.DecimalPlace,
                    ThousandSeparator = aliasAttrDto.ThousandSeparator,
                    Series = aliasAttrDto.Series,
                    DataType = aliasAttrDto.DataType
                };
            }
            case AttributeTypeConstants.TYPE_RUNTIME:
            {
                var smcRuntime = sme as ISubmodelElementCollection;
                var tsList = new List<TimeSeriesDto>();
                var snapshotSeries = smcRuntime.GetSnapshotTimeSeries();
                var dataType = smcRuntime.GetDataType();
                if (snapshotSeries is not null)
                    tsList.Add(snapshotSeries);

                return new AttributeDto
                {
                    GapfillFunction = PostgresFunction.TIME_BUCKET_GAPFILL,
                    Quality = snapshotSeries?.q.GetQualityName(),
                    QualityCode = snapshotSeries?.q,
                    AttributeId = Guid.Parse(smcRuntime.IdShort),
                    AttributeName = smcRuntime.DisplayName.FirstOrDefault()?.Text ?? smcRuntime.IdShort,
                    AttributeType = smcRuntime.Category,
                    Uom = null, // [TODO]
                    DecimalPlace = null, // [TODO]
                    ThousandSeparator = null, // [TODO]
                    Series = tsList,
                    DataType = dataType
                };
            }
            case AttributeTypeConstants.TYPE_DYNAMIC:
            {
                var smcDynamic = sme as ISubmodelElementCollection;
                var tsList = new List<TimeSeriesDto>();
                var snapshotSeries = smcDynamic.GetSnapshotTimeSeries();
                var dataType = smcDynamic.GetDataType();
                if (snapshotSeries is not null)
                    tsList.Add(snapshotSeries);

                return new AttributeDto
                {
                    GapfillFunction = PostgresFunction.TIME_BUCKET_GAPFILL,
                    Quality = snapshotSeries?.q.GetQualityName(),
                    QualityCode = snapshotSeries?.q,
                    AttributeId = Guid.Parse(smcDynamic.IdShort),
                    AttributeName = smcDynamic.DisplayName.FirstOrDefault()?.Text ?? smcDynamic.IdShort,
                    AttributeType = smcDynamic.Category,
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

    public IAssetAdministrationShell GetById(string id)
    {
        var encodedId = ConvertHelper.ToBase64(id);
        var aasResult = aasRepoController.GetAssetAdministrationShellById(encodedId) as ObjectResult;
        var aas = aasResult.Value as IAssetAdministrationShell;
        return aas;
    }

    public GetAssetDto ToGetAssetDto(IAssetAdministrationShell aas, IAssetAdministrationShell? parent, IEnumerable<AssetAttributeDto>? assetAttributes)
    {
        return ToGetAssetSimpleDto(aas, parent, assetAttributes);
    }

    public IEnumerable<AssetAttributeDto> ToAttributes(IEnumerable<ISubmodel> submodel)
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

    public (string PathId, string? PathName, IAssetAdministrationShell? parentAas) BuildResourcePath(IAssetAdministrationShell currentAas, Guid? parentAssetId)
    {
        var currentName = currentAas.DisplayName?.FirstOrDefault()?.Text;
        if (parentAssetId.HasValue)
        {
            var parentAas = GetById(parentAssetId.ToString());
            var parentPathId = parentAas.Extensions.FirstOrDefault(e => e.Name == "ResourcePath").Value;
            var parentPathName = parentAas.Extensions.FirstOrDefault(e => e.Name == "ResourcePathName").Value;
            return ($"{parentPathId}/children/{currentAas.Id}", $"{parentPathName}/{currentName}", parentAas);
        }
        return ($"objects/{currentAas.Id}", currentName, null);
    }

    public IEnumerable<IAssetAdministrationShell> FilterAas(bool filterParent = false, Guid? parentId = null, IEnumerable<Guid> ids = null)
    {
        var allAasResult = aasRepoController.GetAllAssetAdministrationShells(
            assetIds: null, idShort: null, limit: null, cursor: null) as ObjectResult;
        var pagedResult = allAasResult.Value as PagedResult;
        var assets = pagedResult.result
            .OfType<IAssetAdministrationShell>().AsEnumerable()
            .Where(a => Guid.TryParse(a.Id, out _));

        if (filterParent)
        {
            assets = assets.Where(a =>
            {
                var parentAssetId = a.Extensions.FirstOrDefault(e => e.Name == "ParentAssetId")?.Value;
                return Guid.TryParse(parentAssetId, out var aPId) ? parentId == aPId : parentId == null;
            });
        }

        if (ids != null)
        {
            assets = assets.Where(a => ids.Contains(Guid.Parse(a.Id)));
        }

        return assets;
    }

    public int? GetAttributeDecimalPlace(AssetAttributeCommand attribute)
    {
        return attribute.DataType == DataTypeConstants.TYPE_DOUBLE ? attribute.DecimalPlace : null;
    }
}