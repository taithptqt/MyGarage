using System.Collections.Generic;
using MyGarage.Models;

namespace MyGarage.Interfaces
{
    public interface IGroupSessionStatusPolicy
    {
        GroupSessionStatusEvaluation Evaluate(IReadOnlyCollection<string> statuses, string? previousStatus);
    }
}
