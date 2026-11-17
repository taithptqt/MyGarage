import { request } from "@app/apiClient";

export async function getAllStaff(branchId) {
  return request({
    url: "/api/v1/staff",
    method: "GET",
    params: branchId ? { branchId } : undefined
  });
}

export async function getStaffPage(params) {
  return request({ url: "/api/v1/staff/admin-list", method: "GET", params });
}

export async function getStaffById(id) {
  return request({ url: `/api/v1/staff/${id}`, method: "GET" });
}

export async function createStaff(payload) {
  return request({
    url: "/api/v1/staff",
    method: "POST",
    data: payload
  });
}

export async function updateStaff(id, payload) {
  return request({
    url: `/api/v1/staff/${id}`,
    method: "PUT",
    data: payload
  });
}

export async function uploadStaffAvatar(id, file) {
  const formData = new FormData();
  formData.append("file", file, "avatar.jpg");
  return request({
    url: `/api/v1/staff/${id}/avatars`,
    method: "POST",
    data: formData,
    headers: {
      "Content-Type": "multipart/form-data"
    }
  });
}

export async function deleteStaff(id) {
  await request({
    url: `/api/v1/staff/${id}`,
    method: "DELETE"
  });
  return true;
}

export async function getAllRoles() {
  return request({ url: "/api/v1/roles", method: "GET" });
}
