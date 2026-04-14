import {
  createStaff as createStaffCore,
  deleteStaff as deleteStaffCore,
  getAllStaff as getAllStaffCore,
  getStaffById,
  updateStaff as updateStaffCore
} from "@features/admin/api/staffApi";

export async function getAllStaff() {
  return getAllStaffCore();
}

export async function getStaff(id) {
  return getStaffById(id);
}

export async function createStaff(data) {
  return createStaffCore(data);
}

export async function updateStaff(id, data) {
  return updateStaffCore(id, data);
}

export async function deleteStaff(id) {
  return deleteStaffCore(id);
}
