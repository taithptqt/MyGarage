export function normalizeText(value) {
  return String(value || "")
    .normalize("NFD")
    .replace(/[\u0300-\u036f]/g, "")
    .toLowerCase();
}


export function getStatusBadge(status) {
  const normalized = String(status || "").toUpperCase();
  // Workshop Statuses
  if (normalized === "NEW" || normalized === "RECEPTION") return "badge-reception";
  if (normalized === "DIAGNOSING") return "badge-diagnosing";
  if (normalized === "WAITING_APPROVAL") return "badge-warning";
  if (normalized === "REPAIRING") return "badge-info";
  if (normalized === "QC_PENDING") return "badge-qc";
  if (normalized === "READY") return "badge-success";
  if (normalized === "COMPLETED") return "badge-completed";
  if (normalized === "CANCELLED") return "badge-danger";

  // Admin/Staff Statuses (Sync with DB Check Constraint: ACTIVE, ON_LEAVE, SUSPENDED, TERMINATED)
  if (normalized === "ACTIVE" || normalized === "TRUE") return "badge-active";
  if (normalized === "ON_LEAVE") return "badge-warning";
  if (normalized === "SUSPENDED") return "badge-suspended";
  if (normalized === "TERMINATED") return "badge-deleted";
  
  const low = normalized.toLowerCase();
  if (low === "active" || low === "true") return "badge-active";
  if (low === "suspended") return "badge-suspended";
  if (low === "deleted") return "badge-deleted";
  return "badge-inactive";
}
