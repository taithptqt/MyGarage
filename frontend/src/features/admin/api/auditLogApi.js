import { request } from "@app/apiClient";

export async function getAdminChangeLogs(params = {}) {
  const cleanedParams = Object.fromEntries(
    Object.entries(params).filter(([, value]) => value !== "" && value !== null && value !== undefined)
  );
  return request({
    url: "/api/audit-logs/administrative-change-entries",
    method: "GET",
    params: cleanedParams
  });
}
