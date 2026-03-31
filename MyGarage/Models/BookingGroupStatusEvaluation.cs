namespace MyGarage.Models
{
    public sealed record BookingGroupStatusEvaluation(
        string Status,
        int TotalAppointments,
        int ConfirmedAppointments,
        int CanceledAppointments,
        bool ShouldSetConfirmedAt,
        bool ShouldSetCanceledAt,
        bool ShouldClearConfirmedAt,
        bool ShouldClearCanceledAt);
}
