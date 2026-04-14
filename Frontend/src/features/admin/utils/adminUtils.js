export function getStatusBadge(status) {
  const normalized = String(status || "").toLowerCase();
  if (normalized === "active" || normalized === "true") return "badge-active";
  if (normalized === "suspended") return "badge-suspended";
  if (normalized === "deleted") return "badge-deleted";
  return "badge-inactive";
}
