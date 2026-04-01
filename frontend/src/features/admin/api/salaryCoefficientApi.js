import { request } from "../../../app/apiClient";

export async function getAllSalaryCoefficients() {
  return request({ url: "/api/SalaryCoefficient", method: "GET" });
}

export async function createSalaryCoefficient(payload) {
  return request({
    url: "/api/SalaryCoefficient",
    method: "POST",
    data: payload
  });
}

export async function updateSalaryCoefficient(id, payload) {
  return request({
    url: `/api/SalaryCoefficient/${id}`,
    method: "PUT",
    data: payload
  });
}

export async function deleteSalaryCoefficient(id) {
  await request({
    url: `/api/SalaryCoefficient/${id}`,
    method: "DELETE"
  });
  return true;
}
