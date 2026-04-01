export const AUTH_BASE = "/api/auth";

export const AUTH_ROUTES = {
  login: `${AUTH_BASE}/sessions`,
  register: `${AUTH_BASE}/accounts`,
  google: `${AUTH_BASE}/external-sessions/google`,
  me: `${AUTH_BASE}/profile`,
  forgotPassword: `${AUTH_BASE}/credential-recovery-requests`,
  resetPassword: `${AUTH_BASE}/credential-recovery-finalizations`,
  changePassword: `${AUTH_BASE}/password`,
};
