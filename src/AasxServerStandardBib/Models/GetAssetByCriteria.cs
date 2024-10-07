using AHI.Infrastructure.SharedKernel.Model;
using Newtonsoft.Json.Linq;
using AHI.Infrastructure.Service.Dapper.Model;
using Newtonsoft.Json;
using AHI.Infrastructure.Service.Dapper.Helpers;
using AHI.Infrastructure.SharedKernel.Models;

namespace AasxServerStandardBib.Models
{
    public class GetAssetByCriteria : BaseCriteria
    {
        public GetAssetByCriteria()
        {
            Sorts = "createdUtc=asc";
        }

        public QueryCriteria ToQueryCriteria()
        {
            var queryCriteria = new QueryCriteria
            {
                Filter = Filter != null ? JsonConvert.DeserializeObject<JObject>(Filter) : null,
                PageIndex = PageIndex,
                PageSize = PageSize,
                Sorts = Sorts
            };
            if (!string.IsNullOrEmpty(Filter))
            {
                queryCriteria.Filter = DynamicCriteriaHelper.ProcessDapperQueryFilter(Filter, queryCriteria.Filter, queryCriteria, ConvertSearchFilter);
            }
            return queryCriteria;
        }

        private SearchFilter ConvertSearchFilter(SearchFilter condition, QueryCriteria queryCriteria)
        {
            var queryKeyNoSpaces = condition.QueryKey.Replace(" ", "");
            var isEqNull = queryKeyNoSpaces.EndsWith("==null");
            var isNeqNull = queryKeyNoSpaces.EndsWith("!=null");
            var isBoolType = string.Equals(condition.QueryType, AHI.Infrastructure.Service.Dapper.Enum.QueryType.BOOLEAN.ToString(), System.StringComparison.OrdinalIgnoreCase);
            if (isBoolType && (isEqNull || isNeqNull))
            {
                var passedOperator = isEqNull ? "==" : "!=";
                var queryKey = condition.QueryKey.Substring(0, condition.QueryKey.LastIndexOf(passedOperator)).Trim();
                var isOriginalTrue = string.Equals(condition.QueryValue, "true", System.StringComparison.OrdinalIgnoreCase);
                var queryValue = isOriginalTrue;
                if (condition.Operation == AHI.Infrastructure.Service.Dapper.Constant.Operation.NOT_EQUALS)
                    queryValue = !queryValue;
                if (isNeqNull)
                    queryValue = !queryValue;
                return new SearchFilter(
                    queryKey: queryKey,
                    queryValue: queryValue ? "true" : "false",
                    operation: AHI.Infrastructure.Service.Dapper.Constant.Operation.NULL,
                    queryType: AHI.Infrastructure.Service.Dapper.Enum.QueryType.NULL.ToString().ToLower()
                );
            }
            return condition;
        }
    }
}
