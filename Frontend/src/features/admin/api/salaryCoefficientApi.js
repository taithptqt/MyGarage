import { request } from "../../../app/apiClient";

export async function getAllSalaryCoefficients() {
  return request({ url: "/api/salary-coefficients", method: "GET" });
}

export async function createSalaryCoefficient(payload) {
  return request({
    url: "/api/salary-coefficients",
    method: "POST",
    data: payload
  });
}

export async function updateSalaryCoefficient(id, payload) {
  return request({
    url: `/api/salary-coefficients/${id}`,
    method: "PUT",
    data: payload
  });
}

export async function deleteSalaryCoefficient(id) {
  await request({
    url: `/api/salary-coefficients/${id}`,
    method: "DELETE"
  });
  return true;
}

