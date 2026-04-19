const ADMIN_FALLBACK_EMAIL = "hieuvo.23102004@gmail.com";

function normalizeRoleCode(user) {
  return String(user?.roleCode || "").toUpperCase();
}

function normalizeRoleName(user) {
  return String(user?.role || "").toLowerCase();
}

function hasAdminFallback(user) {
  const roleName = normalizeRoleName(user);
  return roleName === "admin" || String(user?.email || "").toLowerCase() === ADMIN_FALLBACK_EMAIL;
}

export function isRouteAllowed(user, allowedRoles) {
  if (!user) return false;
  if (!Array.isArray(allowedRoles) || allowedRoles.length === 0) return true;

  const roleCode = normalizeRoleCode(user);
  if (allowedRoles.includes(roleCode)) {
    return true;
  }

  if (allowedRoles.includes("ADMIN") && hasAdminFallback(user)) {
    return true;
  }

  return false;
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

