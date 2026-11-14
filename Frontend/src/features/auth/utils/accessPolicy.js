function normalizeRoleCode(user) {
  return String(user?.roleCode || "").toUpperCase();
}

export function isRouteAllowed(user, allowedRoles) {
  if (!user) return false;
  if (!Array.isArray(allowedRoles) || allowedRoles.length === 0) return true;

  const roleCode = normalizeRoleCode(user);
  return allowedRoles.includes(roleCode);
}

export function getUnauthorizedRedirectPath(user) {
  const roleCode = normalizeRoleCode(user);
  if (roleCode === "ACCOUNTANT") {
    return "/accounting/dashboard";
  }
  if (roleCode === "ADMIN") {
    return "/admin/default";
  }
  if (["RECEPTIONIST", "TECH", "MANAGER", "QUALITY_CONTROL", "FOREMAN", "TEAM_LEADER", "STORE_KEEPER"].includes(roleCode)) {
    return "/workshop";
  }
  if (roleCode === "CUSTOMER") {
    return "/home";
  }
  return "/login";
}

