import { request } from "@app/apiClient";

export const insuranceCompanyApi = {
  getAll: () => request({ url: "/api/insurance-companies", method: "GET" }),
};
