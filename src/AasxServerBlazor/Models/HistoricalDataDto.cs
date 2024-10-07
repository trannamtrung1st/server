using System;
using System.Collections.Generic;
using System.Linq;
using MessagePack;

namespace AasxServerBlazor.Models
{
    public class HistoricalDataDto : HistoricalGeneralDto
    {
        public List<AttributeDto> Attributes { get; set; }

        public HistoricalDataDto() { }
        public HistoricalDataDto(long timeStart, long timeEnd, string aggregate, string timegrain, IEnumerable<AttributeDto> metrics)
        {
            Start = timeStart;
            End = timeEnd;
            Aggregate = aggregate;
            TimeGrain = timegrain;
            Attributes = metrics.ToList();
        }

        public HistoricalDataDto(long timeStart, long timeEnd, string aggregate, string timegrain, IEnumerable<AttributeDto> metrics, Guid assetId, string assetName, HistoricalDataType requestType)
            : this(timeStart, timeEnd, aggregate, timegrain, metrics)
        {
            AssetId = assetId;
            AssetName = assetName;
            QueryType = requestType;
        }
    }

    public class HistoricalGeneralDto
    {
        public Guid AssetId { get; set; }
        public string AssetName { get; set; }
        public string AssetNormalizeName { get; set; }
        public string Aggregate { get; set; }
        public string TimeGrain { get; set; }
        public long Start { get; set; }
        public long End { get; set; }
        public HistoricalDataType QueryType { get; set; }
        public string RequestType { get; set; }
        public IDictionary<string, string> Statics { get; set; }
        public string TimezoneOffset { get; set; }
    }
}
