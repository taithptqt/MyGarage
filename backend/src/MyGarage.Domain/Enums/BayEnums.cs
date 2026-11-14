namespace MyGarage.Domain.Enums;

public enum BayOperationalState
{
    ACTIVE,
    IN_USE,
    UNDER_MAINTENANCE,
    BROKEN,
    SUSPENDED
}

public enum BayReservationStatusState
{
    AVAILABLE = 0,
    RESERVED = 1,
    OCCUPIED = 2
}
