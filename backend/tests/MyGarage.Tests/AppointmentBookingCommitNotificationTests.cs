using System.Reflection;
using MyGarage.Application.Interfaces.Stores.Appointments;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Interfaces;
using MyGarage.Models;
using MyGarage.Services;
using Xunit;

namespace MyGarage.Tests;

public sealed class AppointmentBookingCommitNotificationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GroupConfirmationEmail_IsSentOnlyAfterSuccessfulCommit(bool failCommit)
    {
        var committed = false;
        var emailCount = 0;
        var consumed = new List<string>();
        var priorTokensPerItem = new List<string[]>();
        var store = Stub<IAppointmentStore>((method, args) => method.Name switch
        {
            nameof(IAppointmentStore.ExecuteInTransactionAsync) when method.IsGenericMethod =>
                typeof(AppointmentBookingCommitNotificationTests).GetMethod(nameof(RunTransaction), BindingFlags.Static | BindingFlags.NonPublic)!
                    .MakeGenericMethod(method.GetGenericArguments()[0]).Invoke(null, new object[] { args![0]!, failCommit, (Action)(() => committed = true) })!,
            nameof(IAppointmentStore.GetAppointmentsForEmailAsync) => Task.FromResult(new List<Appointment> { new() { Id = 9 } }),
            _ => throw new NotSupportedException(method.Name)
        });
        var group = Stub<IGroupSessionService>((method, _) => method.Name switch
        {
            nameof(IGroupSessionService.ResolveStaffActorIdAsync) => Task.FromResult<long?>(null),
            nameof(IGroupSessionService.CreateAsync) => Task.FromResult(new GroupSession { Id = 7 }),
            nameof(IGroupSessionService.SyncCountersAsync) => Task.CompletedTask,
            nameof(IGroupSessionService.GetByCustomerAndIdAsync) => Task.FromResult<GroupSessionResponseDto?>(new GroupSessionResponseDto { Id = 7 }),
            _ => throw new NotSupportedException(method.Name)
        });
        var core = Stub<IAppointmentBookingCoreService>((method, args) =>
        {
            if (method.Name == nameof(IAppointmentBookingCoreService.EnsureBatchFixedLeadTimesAsync)) return Task.CompletedTask;
            if (method.Name == nameof(IAppointmentBookingCoreService.BookCoreDeferredHoldAsync))
            {
                priorTokensPerItem.Add(((IReadOnlyCollection<string>)args![3]!).ToArray());
                return Task.FromResult(new Appointment { Id = 9 });
            }
            if (method.Name == nameof(IAppointmentBookingCoreService.ConsumeSlotHold))
            {
                Assert.True(committed);
                consumed.Add((string)args![0]!);
                return null;
            }
            throw new NotSupportedException(method.Name);
        });
        var assembler = Stub<IAppointmentDetailAssembler>((method, _) => method.Name == nameof(IAppointmentDetailAssembler.AssembleManyAsync)
            ? Task.FromResult<IEnumerable<AppointmentDetailResponseDto>>(Array.Empty<AppointmentDetailResponseDto>())
            : throw new NotSupportedException(method.Name));
        var notifications = Stub<IAppointmentNotificationService>((_, _) =>
        {
            Assert.True(committed);
            emailCount++;
            return Task.CompletedTask;
        });
        var reception = Stub<IAppointmentReceptionService>((method, _) => throw new NotSupportedException(method.Name));
        var service = new AppointmentBookingService(store, group, assembler, notifications, reception, core);
        var request = new BookAppointmentBatchRequestDto { BranchId = 1, Items = new() { new BookAppointmentRequestDto { SlotHoldToken = "hold-1" }, new BookAppointmentRequestDto { SlotHoldToken = "hold-2" } } };
        if (failCommit)
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.BookBatchForCustomerAsync(100, 100, request));
        else
            Assert.Equal(7, (await service.BookBatchForCustomerAsync(100, 100, request)).Id);
        Assert.Equal(failCommit ? 0 : 1, emailCount);
        Assert.Equal(failCommit ? Array.Empty<string>() : new[] { "hold-1", "hold-2" }, consumed);
        Assert.Empty(priorTokensPerItem[0]);
        Assert.Equal(new[] { "hold-1" }, priorTokensPerItem[1]);
    }

    private static async Task<T> RunTransaction<T>(Func<Task<T>> action, bool fail, Action markCommitted)
    {
        var result = await action();
        if (fail) throw new InvalidOperationException("Simulated commit failure");
        markCommitted();
        return result;
    }

    private static T Stub<T>(Func<MethodInfo, object?[]?, object?> handler) where T : class
    {
        var instance = DispatchProxy.Create<T, TestProxy>();
        ((TestProxy)(object)instance).Handler = handler;
        return instance;
    }

    public class TestProxy : DispatchProxy
    {
        public Func<MethodInfo, object?[]?, object?> Handler { get; set; } = null!;
        protected override object? Invoke(MethodInfo? method, object?[]? args) => Handler(method!, args);
    }
}