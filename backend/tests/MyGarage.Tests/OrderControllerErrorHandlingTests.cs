using System.Reflection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using MyGarage.Application.Payments.DTOs;
using MyGarage.Controllers;
using MyGarage.Interfaces;
using Xunit;

namespace MyGarage.Tests;

public class OrderControllerErrorHandlingTests
{
    [Fact]
    public async Task UnexpectedFailure_DoesNotExposeExceptionDetails()
    {
        var facade = DispatchProxy.Create<IOrderCommandFacade, OrderFacadeProxy>();
        ((OrderFacadeProxy)facade).Handler = (_, _) =>
            Task.FromException<OrderCommandResult<Order>>(new Exception("sensitive-provider-token"));
        var controller = CreateController(facade);

        var result = await controller.Get(42);

        var response = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(500, response.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(response.Value);
        Assert.DoesNotContain("sensitive-provider-token", System.Text.Json.JsonSerializer.Serialize(problem));
    }

    [Fact]
    public async Task Conflict_PreservesStatus()
    {
        var facade = DispatchProxy.Create<IOrderCommandFacade, OrderFacadeProxy>();
        ((OrderFacadeProxy)facade).Handler = (_, _) =>
            Task.FromResult(new OrderCommandResult<Order>(409, ErrorPayload: "conflict"));

        var result = await CreateController(facade).Get(42);

        Assert.Equal(409, Assert.IsType<ObjectResult>(result.Result).StatusCode);
    }

    [Fact]
    public async Task RequestCancellation_IsPropagated()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var facade = DispatchProxy.Create<IOrderCommandFacade, OrderFacadeProxy>();
        ((OrderFacadeProxy)facade).Handler = (_, _) =>
            Task.FromCanceled<OrderCommandResult<Order>>(cancellation.Token);
        var controller = CreateController(facade);
        controller.HttpContext.RequestAborted = cancellation.Token;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => controller.Get(42));
    }

    private static OrderController CreateController(IOrderCommandFacade facade) => new(facade, NullLogger<OrderController>.Instance)
    {
        ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
    };

    public class OrderFacadeProxy : DispatchProxy
    {
        public Func<MethodInfo?, object?[]?, object?> Handler { get; set; } = (_, _) => throw new NotImplementedException();
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => Handler(targetMethod, args);
    }
}
