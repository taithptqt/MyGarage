async function getRequester(requester) {
  if (typeof requester === "function") return requester;
  const client = await import("@app/apiClient");
  return client.request;
}

/**
 * Executes a request targeting the modern /api/v1 route first,
 * falling back to the legacy /api route if 404 is encountered.
 */
export async function requestWithDualRoute(v1Url, legacyUrl, options = {}, requester) {
  const send = await getRequester(requester);
  try {
    return await send({ ...options, url: v1Url });
  } catch (err) {
    if ((err?.status === 404 || err?.response?.status === 404) && legacyUrl) {
      return await send({ ...options, url: legacyUrl });
    }
    throw err;
  }
}

export function getQuotationById(quotationId, requester) {
  return requestWithDualRoute(
    `/api/v1/quotations/${quotationId}`,
    `/api/v1/quotations/${quotationId}`,
    { method: "GET" },
    requester
  );
}

export function applyCustomerQuotationDecision(quotationId, data, requester) {
  return requestWithDualRoute(
    `/api/v1/quotations/${quotationId}/customer-decisions`,
    `/api/v1/quotations/${quotationId}/customer-decisions`,
    { method: "POST", data },
    requester
  );
}

export function sendCustomerQuotationDecisionOtpByToken(token, data = {}, requester) {
  return requestWithDualRoute(
    `/api/v1/quotations/public/${token}/otp-requests`,
    `/api/v1/quotations/public/${token}/otp-requests`,
    { method: "POST", data },
    requester
  );
}

export function applyCustomerQuotationDecisionByToken(token, data, requester) {
  return requestWithDualRoute(
    `/api/v1/quotations/public/${token}/decisions`,
    `/api/v1/quotations/public/${token}/decisions`,
    { method: "POST", data },
    requester
  );
}
