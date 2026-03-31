using System.Collections.Generic;
using MyGarage.Models;

namespace MyGarage.Interfaces
{
    public interface IBookingGroupStatusPolicy
    {
        BookingGroupStatusEvaluation Evaluate(IReadOnlyCollection<string> statuses, string? previousStatus);
    }
}
