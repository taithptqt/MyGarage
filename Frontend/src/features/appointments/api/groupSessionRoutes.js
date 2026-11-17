export const GROUP_SESSIONS_BASE = "/api/v1/group-sessions";

export const GROUP_SESSIONS_ROUTES = {
  base: GROUP_SESSIONS_BASE,
  mine: `${GROUP_SESSIONS_BASE}/customers/current`,
  detail: (id) => `${GROUP_SESSIONS_BASE}/customers/current/${id}`,
  cancelAll: (id) => `${GROUP_SESSIONS_BASE}/${id}/cancellations`,
};
