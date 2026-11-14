namespace MyGarage.Domain.Enums;

public enum ServiceOrderStatus
{
    NEW,
    CHECKED_IN,
    WAITING,
    DIAGNOSING,
    QUOTING,
    WAITING_APPROVAL,
    APPROVED,
    WAITING_PARTS,
    IN_PROGRESS,
    REPAIRING,
    READY,
    WAITING_PAYMENT,
    CLOSED,
    CANCELED
}

public enum QcInspectionResultStatus
{
    PENDING,
    PASS,
    FAIL
}

public enum ServiceStaffRoleInOrder
{
    PRIMARY,
    ASSISTANT,
    INSPECTOR
}

public enum BayOperationalStatus
{
    ACTIVE,
    MAINTENANCE,
    INACTIVE
}
