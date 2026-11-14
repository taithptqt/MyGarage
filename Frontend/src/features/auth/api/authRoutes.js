export const AUTH_BASE = "/api/auth";

export const AUTH_ROUTES = {
  login: `${AUTH_BASE}/sessions`,
  register: `${AUTH_BASE}/accounts`,
  google: `${AUTH_BASE}/external-sessions/google`,
  session: `${AUTH_BASE}/session`,
  logout: `${AUTH_BASE}/logout`,
  me: `${AUTH_BASE}/profile`,
  forgotPassword: `${AUTH_BASE}/forgot-password`,
  resetPassword: `${AUTH_BASE}/reset-password`,
  changePassword: `${AUTH_BASE}/password`,
};
