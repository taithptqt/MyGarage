import { request } from "../../../app/apiClient";

export async function getSalaryProfiles(branchId) {
  const params = {};
  if (branchId) params.branchId = branchId;
  return request({
    url: "/api/SalaryAdmin/profiles",
    method: "GET",
    params
  });
}

export async function createSalaryProfile(payload) {
  return request({
    url: "/api/SalaryAdmin/profiles",
    method: "POST",
    data: payload
  });
}

export async function updateSalaryProfile(id, payload) {
  return request({
    url: `/api/SalaryAdmin/profiles/${id}`,
    method: "PUT",
    data: payload
  });
}

export async function deleteSalaryProfile(id) {
  return request({
    url: `/api/SalaryAdmin/profiles/${id}`,
    method: "DELETE"
  });
}

export async function getInitialSalaryRequests(params = {}) {
  return request({
    url: "/api/SalaryAdmin/requests",
    method: "GET",
    params
  });
}

export async function approveInitialSalaryRequest(id, payload = {}) {
  return request({
    url: `/api/SalaryAdmin/requests/${id}/approve`,
    method: "POST",
    data: payload
  });
}

export async function rejectInitialSalaryRequest(id, payload = {}) {
  return request({
    url: `/api/SalaryAdmin/requests/${id}/reject`,
    method: "POST",
    data: payload
  });
}
