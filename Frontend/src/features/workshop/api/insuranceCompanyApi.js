import { request } from "@app/apiClient";

export const insuranceCompanyApi = {
  getAll: () => request({ url: "/api/v1/insurance-companies", method: "GET" }),
};
