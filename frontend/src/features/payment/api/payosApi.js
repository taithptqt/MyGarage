import { requestSafe } from "@app/apiClient";

export async function createPayosOrder(payload) {
  return requestSafe({
    url: "/api/orders",
    method: "POST",
    data: payload
  });
}

export async function getPayments() {
  return requestSafe({
    url: "/api/payments/mine",
    method: "GET"
  });
}

export async function getPaymentStatus(paymentId) {
  return requestSafe({
    url: `/api/payments/${paymentId}/status`,
    method: "GET"
  });
}

export async function syncOrder(orderId) {
  return requestSafe({
    url: `/api/orders/${orderId}`,
    method: "GET"
  });
}

