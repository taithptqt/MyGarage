import { request } from "../../../app/apiClient";

export async function getAllBranches() {
  return request({ url: "/api/Branch", method: "GET" });
}

export async function createBranch(payload) {
  return request({
    url: "/api/Branch",
    method: "POST",
    data: payload
  });
}

export async function updateBranch(id, payload) {
  return request({
    url: `/api/Branch/${id}`,
    method: "PUT",
    data: payload
  });
}

export async function deleteBranch(id) {
  await request({
    url: `/api/Branch/${id}`,
    method: "DELETE"
  });
  return true;
}
