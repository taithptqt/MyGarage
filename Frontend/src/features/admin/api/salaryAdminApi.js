import { parseSalaryProfilePage } from "./salaryProfilePage.js";
import { parseSalaryRequestPage } from "./salaryRequestPage.js";

async function getRequester(requester) {
  if (typeof requester === "function") return requester;
  const client = await import("@app/apiClient");
  return client.request;
}

async function getSafeRequester(requester) {
  if (typeof requester === "function") return requester;
  const client = await import("@app/apiClient");
  return client.requestSafe;
}

/**
 * Executes a request targeting the modern /api/v1 route first,
 * falling back to the legacy /api route if 404 is encountered.
 */
export async function requestWithDualRoute(v1Url, legacyUrl, options = {}, requester) {
  const send = await getRequester(requester);
  try {
    return await send({ ...options, url: v1Url });
  } catch (err) {
    if ((err?.status === 404 || err?.response?.status === 404) && legacyUrl) {
      return await send({ ...options, url: legacyUrl });
    }
    throw err;
  }
}

/**
 * Safe version of requestWithDualRoute using requestSafe.
 */
export async function requestSafeWithDualRoute(v1Url, legacyUrl, options = {}, requester) {
  const send = await getSafeRequester(requester);
  const result = await send({ ...options, url: v1Url });
  if (!result.ok && (result.status === 404 || result.response?.status === 404) && legacyUrl) {
    return await send({ ...options, url: legacyUrl });
  }
  return result;
}

export async function getSalaryProfiles(branchId, options = {}, requester) {
  const params = { ...(options.params || {}) };
  if (branchId) params.branchId = branchId;
  return requestWithDualRoute(
    "/api/v1/salary-profiles",
    "/api/v1/salary-admin/profiles",
    { ...options, method: "GET", params },
    requester
  );
}

export async function getSalaryProfilePage(branchId, page, pageSize, requester) {
  const params = { page, pageSize };
  if (branchId) params.branchId = branchId;
  const response = await requestSafeWithDualRoute(
    "/api/v1/salary-profiles",
    "/api/v1/salary-admin/profiles",
    { method: "GET", params },
    requester
  );
  return parseSalaryProfilePage(response);
}

export async function createSalaryProfile(payload, requester) {
  return requestWithDualRoute(
    "/api/v1/salary-profiles",
    "/api/v1/salary-admin/profiles",
    { method: "POST", data: payload },
    requester
  );
}

export async function updateSalaryProfile(id, payload, requester) {
  return requestWithDualRoute(
    `/api/v1/salary-profiles/${id}`,
    `/api/v1/salary-admin/profiles/${id}`,
    { method: "PUT", data: payload },
    requester
  );
}

export async function deleteSalaryProfile(id, requester) {
  return requestWithDualRoute(
    `/api/v1/salary-profiles/${id}`,
    `/api/v1/salary-admin/profiles/${id}`,
    { method: "DELETE" },
    requester
  );
}

export async function getInitialSalaryRequests(params = {}, requester) {
  return requestWithDualRoute(
    "/api/v1/initial-salary-requests",
    "/api/v1/salary-admin/requests",
    { method: "GET", params },
    requester
  );
}

export async function getInitialSalaryRequestPage(params, requester) {
  const response = await requestSafeWithDualRoute(
    "/api/v1/initial-salary-requests",
    "/api/v1/salary-admin/requests",
    { method: "GET", params },
    requester
  );
  return parseSalaryRequestPage(response);
}

export async function approveInitialSalaryRequest(id, payload = {}, requester) {
  return requestWithDualRoute(
    `/api/v1/initial-salary-requests/${id}/approvals`,
    `/api/v1/salary-admin/requests/${id}/approvals`,
    { method: "POST", data: payload },
    requester
  );
}

export async function rejectInitialSalaryRequest(id, payload = {}, requester) {
  return requestWithDualRoute(
    `/api/v1/initial-salary-requests/${id}/rejections`,
    `/api/v1/salary-admin/requests/${id}/rejections`,
    { method: "POST", data: payload },
    requester
  );
}
