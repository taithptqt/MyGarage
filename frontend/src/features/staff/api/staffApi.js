import apiClient from "../../../api/api";

export async function getAllStaff() {
  const res = await apiClient.get("Staff");
  return res.data;
}

export async function getStaff(id) {
  const res = await apiClient.get(`Staff/${id}`);
  return res.data;
}

export async function createStaff(data) {
  try {
    const res = await apiClient.post("Staff", data);
    return res.data;
  } catch (error) {
    const message = error.response ? error.response.data.message : error.message;
    throw new Error(message || "Failed to create staff member.");
  }
}

export async function updateStaff(id, data) {
  try {
    const res = await apiClient.put(`Staff/${id}`, data);
    return res.data;
  } catch (error) {
    const message = error.response ? error.response.data.message : error.message;
    throw new Error(message || "Failed to update staff member.");
  }
}

export async function deleteStaff(id) {
  try {
    await apiClient.delete(`Staff/${id}`);
  } catch (error) {
    const message = error.response ? error.response.data.message : error.message;
    throw new Error(message || "Failed to delete staff member.");
  }
}
