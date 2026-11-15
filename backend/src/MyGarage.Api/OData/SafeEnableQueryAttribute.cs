using Microsoft.AspNetCore.OData.Query;

namespace MyGarage.Controllers.OData;

internal static class ODataQueryLimits
{
    public const int PageSize = 100;
    public const int MaxTop = 100;
    public const int MaxSkip = 10_000;
    public const int MaxNodeCount = 40;
    public const int MaxOrderByNodeCount = 3;

    public const AllowedQueryOptions AllowedOptions =
        AllowedQueryOptions.Select |
        AllowedQueryOptions.Filter |
        AllowedQueryOptions.OrderBy |
        AllowedQueryOptions.Skip |
        AllowedQueryOptions.SkipToken |
        AllowedQueryOptions.Top |
        AllowedQueryOptions.Count;

    public const AllowedFunctions AllowedFilterFunctions =
        AllowedFunctions.Contains |
        AllowedFunctions.StartsWith |
        AllowedFunctions.EndsWith |
        AllowedFunctions.ToLower |
        AllowedFunctions.ToUpper |
        AllowedFunctions.Trim;

    public const AllowedLogicalOperators AllowedFilterOperators =
        AllowedLogicalOperators.Equal |
        AllowedLogicalOperators.NotEqual |
        AllowedLogicalOperators.And |
        AllowedLogicalOperators.Or |
        AllowedLogicalOperators.GreaterThan |
        AllowedLogicalOperators.GreaterThanOrEqual |
        AllowedLogicalOperators.LessThan |
        AllowedLogicalOperators.LessThanOrEqual;
}

[AttributeUsage(AttributeTargets.Method)]
internal sealed class SafeEnableQueryAttribute : EnableQueryAttribute
{
    public SafeEnableQueryAttribute()
    {
        PageSize = ODataQueryLimits.PageSize;
        MaxTop = ODataQueryLimits.MaxTop;
        MaxSkip = ODataQueryLimits.MaxSkip;
        MaxExpansionDepth = 0;
        MaxNodeCount = ODataQueryLimits.MaxNodeCount;
        MaxOrderByNodeCount = ODataQueryLimits.MaxOrderByNodeCount;
        AllowedQueryOptions = ODataQueryLimits.AllowedOptions;
        AllowedFunctions = ODataQueryLimits.AllowedFilterFunctions;
        AllowedArithmeticOperators = AllowedArithmeticOperators.None;
        AllowedLogicalOperators = ODataQueryLimits.AllowedFilterOperators;
    }
}
