export const AUTH_BASE = "/api/v1/auth";

export const AUTH_ROUTES = {
  login: `${AUTH_BASE}/sessions`,
  register: `${AUTH_BASE}/accounts`,
  google: `${AUTH_BASE}/external-sessions/google`,
  session: `${AUTH_BASE}/session`,
  logout: `${AUTH_BASE}/session-revocations`,
  me: `${AUTH_BASE}/profile`,
  forgotPassword: `${AUTH_BASE}/credential-recovery-requests`,
  resetPassword: `${AUTH_BASE}/credential-recovery-finalizations`,
  changePassword: `${AUTH_BASE}/password`,
};
