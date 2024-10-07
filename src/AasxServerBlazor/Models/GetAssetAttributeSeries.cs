using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace AasxServerBlazor.Models
{
    public class GetAssetAttributeSeries : GetAssetAttributeSeriesModel
    {
    }

    public class GetAssetAttributeSeriesModel
    {
        public string TimezoneOffset { get; set; } = "+00:00";

        public int TimeoutInSecond { get; set; }

        public int? Quality { get; set; }

        public IEnumerable<AssetAttributeSeries> Assets { get; set; }

        public Guid ActivityId { get; set; }

        public Guid WidgetId { get; set; }
    }

    public class AssetAttributeSeries
    {
        public int TimeoutInSecond { get; set; }
        public HistoricalDataType RequestType { get; set; }
        public Guid AssetId { get; set; }
        public string ProjectId { get; set; }
        public string SubscriptionId { get; set; }
        public long Start { get; set; }
        public long End { get; set; }
        public string TimeGrain { get; set; }
        public string Aggregate { get; set; }
        public IDictionary<string, string> Statics { get; set; }
        public IEnumerable<Guid> AttributeIds { get; set; }
        public bool UseCustomTimeRange { get; set; }
        public int PageSize { get; set; }
        public string GapfillFunction { get; set; } = PostgresFunction.TIME_BUCKET_GAPFILL; // valid: time_bucket_gapfill, time_bucket, default: time_bucket_gapfill

        public AssetAttributeSeries()
        {
            AttributeIds = new List<Guid>();
            Start = DateTimeOffset.UtcNow.AddHours(-1).ToUnixTimeMilliseconds();
            End = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            Aggregate = "avg";
            RequestType = HistoricalDataType.SERIES;
        }
    }

    public enum HistoricalDataType
    {
        SNAPSHOT = 0,
        SERIES = 1
    }
}
