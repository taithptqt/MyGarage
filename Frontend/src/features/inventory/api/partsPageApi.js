import { request } from "@app/apiClient";

export function getPartsPage(params) {
  return request({ url: "/api/inventory/parts-page", method: "GET", params });
}
