const parseCount = (raw) => {
  const count = Number(raw);
  if (raw == null || !/^\d+$/.test(String(raw)) || !Number.isSafeInteger(count)) {
    throw new Error("Không xác định được tổng số yêu cầu lương.");
  }
  return count;
};

export function parseSalaryRequestPage(response) {
  if (!response?.ok) {
    const error = new Error(response?.data?.message || "Không tải được dữ liệu lương.");
    error.response = { status: response?.status };
    throw error;
  }
  const data = response.data;
  const isEnvelope = data && typeof data === "object" && !Array.isArray(data);
  const items = isEnvelope ? data.items : data;
  const total = parseCount(
    (isEnvelope ? (data.totalCount ?? data.total) : undefined) ?? response.headers?.["x-total-count"]
  );
  const pending = parseCount(
    (isEnvelope ? (data.pendingCount ?? data.pending) : undefined) ?? response.headers?.["x-salary-pending-count"]
  );
  const approved = parseCount(
    (isEnvelope ? (data.approvedCount ?? data.approved) : undefined) ?? response.headers?.["x-salary-approved-count"]
  );
  if (!Array.isArray(items) || items.length > total || pending + approved > total) {
    throw new Error("Không xác định được tổng số yêu cầu lương.");
  }
  return { items, total, pending, approved };
}
