function normalizeText(value) {
  return String(value || "")
    .normalize("NFD")
    .replace(/[\u0300-\u036f]/g, "")
    .trim()
    .toUpperCase();
}

export function isAccountingStaff(user) {
  if (!user || user.role !== "Staff") return false;
  const roleCode = normalizeText(user.roleCode);
  const roleName = normalizeText(user.roleName);
  return roleCode === "ACCOUNTANT" || roleName.includes("KE TOAN") || roleName.includes("ACCOUNTANT");
}

export function resolveStaffHome(user) {
  if (isAccountingStaff(user)) {
    return "/accounting/dashboard";
  }
  return "/admin/default";
}
