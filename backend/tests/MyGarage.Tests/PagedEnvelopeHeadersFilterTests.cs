using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using MyGarage.Application.Common.Pagination;
using MyGarage.Filters;
using Xunit;

namespace MyGarage.Tests;

public sealed class PagedEnvelopeHeadersFilterTests
{
    [Fact]
    public void OnResultExecuting_ForPagedEnvelope_SetsPaginationAndLinkHeaders()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Path = "/api/v1/items";
        httpContext.Request.QueryString = new QueryString("?search=oil&page=2&pageSize=10");

        var envelope = PagedEnvelope<string>.Create(new[] { "a", "b" }, totalCount: 35, page: 2, pageSize: 10);
        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
        var executingContext = new ResultExecutingContext(
            actionContext,
            Array.Empty<IFilterMetadata>(),
            new OkObjectResult(envelope),
            controller: new object());

        var filter = new PagedEnvelopeHeadersFilter();
        filter.OnResultExecuting(executingContext);

        Assert.Equal("35", httpContext.Response.Headers["X-Total-Count"].ToString());
        Assert.Equal("2", httpContext.Response.Headers["X-Page"].ToString());
        Assert.Equal("10", httpContext.Response.Headers["X-Page-Size"].ToString());
        Assert.Equal("4", httpContext.Response.Headers["X-Total-Pages"].ToString());

        var link = httpContext.Response.Headers["Link"].ToString();
        Assert.Contains("rel=\"first\"", link);
        Assert.Contains("rel=\"prev\"", link);
        Assert.Contains("rel=\"next\"", link);
        Assert.Contains("rel=\"last\"", link);
        Assert.Contains("search=oil", link);
    }
}
