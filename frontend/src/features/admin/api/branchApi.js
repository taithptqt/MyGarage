import { request } from "@app/apiClient";

export async function getAllBranches() {
  return request({ url: "/api/branches", method: "GET" });
}

export async function createBranch(payload) {
  return request({
    url: "/api/branches",
    method: "POST",
    data: payload
  });
}

export async function updateBranch(id, payload) {
  return request({
    url: `/api/branches/${id}`,
    method: "PUT",
    data: payload
  });
}

export async function deleteBranch(id) {
  await request({
    url: `/api/branches/${id}`,
    method: "DELETE"
  });
  return true;
}
