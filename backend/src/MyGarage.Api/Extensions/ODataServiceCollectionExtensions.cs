using System.Text.Json.Serialization;
using Microsoft.AspNetCore.OData;
using MyGarage.Filters;
using MyGarage.OData;

namespace MyGarage.Extensions;

public static class ODataServiceCollectionExtensions
{
    public static IServiceCollection AddApiControllersWithOData(this IServiceCollection services)
    {
        services
            .AddControllers(options =>
            {
                options.Filters.Add<PagedEnvelopeHeadersFilter>();
            })
            .AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
            })
            .AddOData(options =>
                options.Select()
                    .Filter()
                    .OrderBy()
                    .Count()
                    .SkipToken()
                    .SetMaxTop(100)
                    .AddRouteComponents("odata", MyGarageEdmModel.Create())
                    .AddRouteComponents("api/v1/odata", MyGarageEdmModel.Create()));

        return services;
    }
}
