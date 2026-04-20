export function getStatusBadge(status) {
  const normalized = String(status || "").trim().toUpperCase();
  if (normalized === "ACTIVE" || normalized === "TRUE") return "badge-active";
  if (normalized === "SUSPENDED") return "badge-suspended";
  if (normalized === "DELETED") return "badge-deleted";

  if (
    normalized === "PENDING"
    || normalized === "LATE"
    || normalized === "PARTS_PENDING"
    || normalized === "WAITING_APPROVAL"
    || normalized === "PENDING_BRANCH_MANAGER"
    || normalized === "PENDING_CUSTOMER"
    || normalized === "PENDING_INSURANCE"
    || normalized === "SENT_TO_CUSTOMER"
    || normalized === "QUOTING"
    || normalized === "DIAGNOSING"
    || normalized === "IN_PROGRESS"
    || normalized === "REPAIRING"
  ) {
    return "badge-warning";
  }

  if (normalized === "NEW" || normalized === "RECEPTION" || normalized === "LOCKED") {
    return "badge-info";
  }

  if (
    normalized === "CHECKED_IN"
    || normalized === "READY"
    || normalized === "COMPLETED"
    || normalized === "CLOSED"
    || normalized === "PAID"
    || normalized === "APPROVED"
    || normalized === "CUSTOMER_APPROVED"
    || normalized === "INSURANCE_APPROVED"
    || normalized === "ARRIVED"
    || normalized === "ARRIVED_LATE"
    || normalized === "CONFIRMED"
    || normalized === "PARTIAL_CONFIRMED"
  ) {
    return "badge-success";
  }

  if (
    normalized === "CANCELED"
    || normalized === "CANCELLED"
    || normalized === "NO_SHOW"
    || normalized === "REJECTED"
    || normalized === "CUSTOMER_REJECTED"
    || normalized === "SUPERSEDED"
    || normalized === "ESCALATED"
    || normalized === "BYPASSED"
  ) {
    return "badge-danger";
  }

  return "badge-inactive";
}
