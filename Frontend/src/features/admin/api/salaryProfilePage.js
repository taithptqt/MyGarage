export function parseSalaryProfilePage(response) {
  if (!response?.ok) {
    const error = new Error(response?.data?.message || "Không tải được dữ liệu lương.");
    error.response = { status: response?.status };
    throw error;
  }
  const data = response.data;
  const isEnvelope = data && typeof data === "object" && !Array.isArray(data);
  const items = isEnvelope ? data.items : data;
  const rawTotal = (isEnvelope ? (data.totalCount ?? data.total) : undefined) ?? response.headers?.["x-total-count"];
  const total = Number(rawTotal);
  if (
    rawTotal == null ||
    !/^\d+$/.test(String(rawTotal)) ||
    !Number.isSafeInteger(total) ||
    !Array.isArray(items) ||
    items.length > total
  ) {
    throw new Error("Không xác định được tổng số bảng lương.");
  }
  return { items, total };
}
