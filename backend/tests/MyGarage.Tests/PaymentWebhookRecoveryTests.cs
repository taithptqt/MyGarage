using System.Reflection;
using MyGarage.Application.Interfaces.Queries;
using MyGarage.Application.Payments;
using MyGarage.Application.Payments.DTOs;
using MyGarage.Interfaces;
using MyGarage.Models;
using Xunit;

namespace MyGarage.Tests;

public sealed class PaymentWebhookRecoveryTests
{
    [Theory]
    [InlineData(PayOsLinkStatus.Underpaid, 40L, 60L, "PENDING")]
    [InlineData(PayOsLinkStatus.Paid, 100L, 0L, "COMPLETED")]
    public async Task Recovery_UsesGatewayCumulativeAmount(PayOsLinkStatus state, long paid, long remaining, string expected)
    {
        var payment = new Payment { Id = 1, Status = "PENDING", TransactionCode = "link-1", Type = "FULL", Direction = "INFLOW" };
        decimal? actualAmount = null;
        string? actualStatus = null;
        var gateway = Proxy<IPayOsGateway>((method, _) => method.Name == nameof(IPayOsGateway.GetPaymentLinkAsync)
            ? Task.FromResult(new PayOsLinkSnapshot(state, state.ToString(), 100, paid, remaining, null, null, null, null)) : throw new NotSupportedException(method.Name));
        var data = Proxy<IWebhookPaymentDataService>((method, _) => method.Name == nameof(IWebhookPaymentDataService.FindRecoverablePaymentByLinkAsync)
            ? Task.FromResult<Payment?>(payment) : throw new NotSupportedException(method.Name));
        var confirmation = Proxy<IPaymentConfirmationService>((method, args) =>
        {
            if (method.Name != nameof(IPaymentConfirmationService.ApplyOutcomeAsync)) throw new NotSupportedException(method.Name);
            actualAmount = (decimal)args![1]!;
            actualStatus = (string)args[2]!;
            return Task.CompletedTask;
        });
        var orders = Proxy<IOrderStore>((method, _) => method.Name == nameof(IOrderStore.GetOrderByOrderCode) ? null : throw new NotSupportedException(method.Name));
        var transactions = Proxy<IOrderTransactionStore>((method, _) => throw new NotSupportedException(method.Name));
        var service = new PaymentWebhookService(gateway, orders, transactions, data, confirmation);
        await service.ProcessVerifiedPaymentAsync(new VerifiedPaymentWebhookInput(7, 40, "ref-1", "", "", "", "link-1", null, null, null, null, null, null));
        Assert.Equal(paid, actualAmount);
        Assert.Equal(expected, actualStatus);
    }

    [Fact]
    public async Task Recovery_PropagatesFailureForRetry()
    {
        var gateway = Proxy<IPayOsGateway>((method, _) => throw new NotSupportedException(method.Name));
        var data = Proxy<IWebhookPaymentDataService>((method, _) => method.Name == nameof(IWebhookPaymentDataService.FindRecoverablePaymentByLinkAsync)
            ? Task.FromException<Payment?>(new InvalidOperationException("temporary failure")) : throw new NotSupportedException(method.Name));
        var confirmation = Proxy<IPaymentConfirmationService>((method, _) => throw new NotSupportedException(method.Name));
        var orders = Proxy<IOrderStore>((method, _) => method.Name == nameof(IOrderStore.GetOrderByOrderCode) ? null : throw new NotSupportedException(method.Name));
        var transactions = Proxy<IOrderTransactionStore>((method, _) => throw new NotSupportedException(method.Name));
        var service = new PaymentWebhookService(gateway, orders, transactions, data, confirmation);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => service.ProcessVerifiedPaymentAsync(new VerifiedPaymentWebhookInput(7, 40, "ref-1", "", "", "", "link-1", null, null, null, null, null, null)));
        Assert.Equal("temporary failure", error.Message);
    }

    private static T Proxy<T>(Func<MethodInfo, object?[]?, object?> handler) where T : class
    {
        var proxy = DispatchProxy.Create<T, StubProxy<T>>();
        ((StubProxy<T>)(object)proxy).Handler = handler;
        return proxy;
    }

    public class StubProxy<T> : DispatchProxy where T : class
    {
        public Func<MethodInfo, object?[]?, object?> Handler { get; set; } = null!;
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => Handler(targetMethod!, args);
    }
}