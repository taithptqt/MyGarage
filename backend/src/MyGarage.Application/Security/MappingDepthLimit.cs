using AutoMapper;
using AutoMapper.Internal;

namespace MyGarage.Security;

public static class MappingDepthLimit
{
    // Keep an explicit application-wide limit as defense in depth for recursive maps.
    public static void Apply(IMapperConfigurationExpression configuration)
    {
        configuration.Internal().ForAllMaps((typeMap, mapping) =>
        {
            if (typeMap.MaxDepth == 0) mapping.MaxDepth(64);
        });
    }
}
