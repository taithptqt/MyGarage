namespace MyGarage.Models
{
    public sealed record GroupSessionStatusEvaluation(
        string Status,
        int TotalAppointments,
        int ConfirmedAppointments,
        int CanceledAppointments,
        bool ShouldSetConfirmedAt,
        bool ShouldSetCanceledAt,
        bool ShouldClearConfirmedAt,
        bool ShouldClearCanceledAt);
}
