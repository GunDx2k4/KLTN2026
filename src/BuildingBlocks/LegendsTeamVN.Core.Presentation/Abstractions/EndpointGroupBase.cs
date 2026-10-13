using Asp.Versioning;
using Asp.Versioning.Builder;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace LegendsTeamVN.Core.Presentation.Abstractions;

public abstract class EndpointGroupBase : IEndpoint
{
    protected abstract string Name { get; }
    protected virtual string Prefix => "api";
    protected virtual string? Tag => !string.IsNullOrEmpty(Name) ? char.ToUpper(Name[0]) + Name[1..] : null;
    
    protected virtual ApiVersion[] SupportedVersions => new[] { new ApiVersion(1) };

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        var path = $"{Prefix}/v{{version:apiVersion}}/{Name}".ToLowerInvariant();
        
        ApiVersionSetBuilder versionSetBuilder = app.NewApiVersionSet();
        foreach (var version in SupportedVersions)
        {
            versionSetBuilder.HasApiVersion(version);
        }
        var versionSet = versionSetBuilder.ReportApiVersions().Build();

        var group = app.MapGroup(path)
                       .WithApiVersionSet(versionSet);

        if (!string.IsNullOrEmpty(Tag))
        {
            group = group.WithTags(Tag);
        }

        Map(group);
    }

    protected abstract void Map(RouteGroupBuilder group);
}
