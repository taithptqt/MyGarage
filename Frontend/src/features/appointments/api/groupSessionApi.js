import { request } from "@app/apiClient";
import { GROUP_SESSIONS_ROUTES } from "@features/appointments/api/groupSessionRoutes";

export async function createGroupSession(payload) {
  return request({
    url: GROUP_SESSIONS_ROUTES.base,
    method: "POST",
    data: payload,
  });
}

export async function getMyGroupSessions() {
  return request({
    url: GROUP_SESSIONS_ROUTES.mine,
    method: "GET",
  });
}

export async function getMyGroupSessionDetail(id) {
  return request({
    url: GROUP_SESSIONS_ROUTES.detail(id),
    method: "GET",
  });
}

export async function cancelGroupSessionAll(id, reason) {
  return request({
    url: GROUP_SESSIONS_ROUTES.cancelAll(id),
    method: "POST",
    data: { reason: reason || null },
  });
}
