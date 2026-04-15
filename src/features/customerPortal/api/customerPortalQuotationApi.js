import { request } from "@app/apiClient";

export function getQuotationById(quotationId) {
  return request({ url: `/api/quotations/${quotationId}`, method: "GET" });
}

export function applyCustomerQuotationDecision(quotationId, data) {
  return request({ url: `/api/quotations/${quotationId}/customer-decision`, method: "POST", data });
}
