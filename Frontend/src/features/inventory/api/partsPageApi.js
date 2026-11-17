import { request } from "@app/apiClient";

export function getPartsPage(params) {
  return request({ url: "/api/v1/inventory/parts-page", method: "GET", params });
}
