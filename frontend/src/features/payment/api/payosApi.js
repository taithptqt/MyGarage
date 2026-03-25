import { requestSafe } from "../../../app/apiClient";

export async function createPayosOrder(payload) {
  return requestSafe({
    url: "/api/Order",
    method: "POST",
    data: payload
  });
}

export async function getPayments() {
  return requestSafe({
    url: "/api/Payment",
    method: "GET"
  });
}

export async function getPaymentStatus(paymentId) {
  return requestSafe({
    url: `/api/Payment/${paymentId}/status`,
    method: "GET"
  });
}

export async function syncOrder(orderId) {
  return requestSafe({
    url: `/api/Order/${orderId}`,
    method: "GET"
  });
}
