export const TITLES = {
  dashboard: ["Bảng điều khiển voucher", "Tổng quan hiệu quả voucher"],
  list: ["Danh sách voucher", "Tạo, sửa và quản lý vòng đời voucher"],
  campaigns: ["Chiến dịch", "Tổng hợp voucher theo chiến dịch"],
  conflicts: ["Kiểm tra xung đột", "Kiểm tra xung đột điều kiện áp dụng"],
  redemptions: ["Lịch sử sử dụng", "Theo dõi áp dụng và hoàn voucher"],
  approvals: ["Hàng chờ phê duyệt", "Xử lý yêu cầu ngoại lệ"],
  reports: ["Báo cáo hiệu quả", "Báo cáo hiệu quả và rủi ro"],
  settings: ["Thiết lập chính sách", "Chính sách vận hành voucher"]
};

export const STATUS_BADGE = {
  DRAFT: "badge-info",
  PENDING_APPROVAL: "badge-suspended",
  SCHEDULED: "badge-info",
  ACTIVE: "badge-active",
  PAUSED: "badge-suspended",
  ENDED: "badge-inactive",
  EXHAUSTED: "badge-warning",
  EXPIRED: "badge-inactive",
  REVOKED: "badge-inactive",
  ARCHIVED: "badge-inactive",
  PENDING: "badge-suspended",
  APPROVED: "badge-active",
  REJECTED: "badge-inactive",
  REDEEMED: "badge-active",
  REVERSED: "badge-warning"
};

export const STATUS_LABEL = {
  DRAFT: "Nháp",
  PENDING_APPROVAL: "Chờ duyệt",
  SCHEDULED: "Đã lên lịch",
  ACTIVE: "Đang hoạt động",
  PAUSED: "Tạm dừng",
  ENDED: "Đã kết thúc",
  EXHAUSTED: "Hết lượt",
  EXPIRED: "Hết hạn",
  REVOKED: "Đã thu hồi",
  ARCHIVED: "Lưu trữ",
  PENDING: "Chờ duyệt",
  APPROVED: "Đã duyệt",
  REJECTED: "Từ chối",
  REDEEMED: "Đã sử dụng",
  REVERSED: "Đã hoàn"
};

export const SECTION_NEEDS = {
  vouchers: new Set([]),
  campaigns: new Set([]),
  redemptions: new Set([]),
  approvals: new Set([])
};

export const up = (v) => String(v || "").trim().toUpperCase();
export const num = (v, d = 0) => (Number.isFinite(Number(v)) ? Number(v) : d);
export const tnull = (v) => {
  const s = String(v ?? "").trim();
  return s || null;
};
export const nnull = (v) => {
  const s = String(v ?? "").trim();
  if (!s) return null;
  const n = Number(s);
  return Number.isFinite(n) ? n : null;
};
export const arr = (v) => (Array.isArray(v) ? v : Array.isArray(v?.items) ? v.items : []);
export const rows = (v) => arr(v).filter((x) => x && typeof x === "object");
export const statusText = (s) => STATUS_LABEL[up(s)] || up(s) || "-";
export const pct = (a, b) => (!b || b <= 0 ? 0 : Math.max(0, Math.min(100, Math.round((a / b) * 100))));
export const dt = (v) => {
  if (!v) return "-";
  const d = new Date(v);
  return Number.isNaN(d.getTime())
    ? "-"
    : d.toLocaleString("vi-VN", { timeZone: "Asia/Ho_Chi_Minh" });
};
export const money = (v) =>
  Number.isFinite(Number(v))
    ? Number(v).toLocaleString("vi-VN", { style: "currency", currency: "VND", maximumFractionDigits: 0 })
    : "-";
export const budgetAlertText = (campaign) => {
  if (campaign?.isBudgetExceeded) return "Vượt ngân sách";
  if (campaign?.isNearBudgetLimit) return "Sắp chạm ngân sách";
  if (num(campaign?.budgetLimit, 0) <= 0) return "Không giới hạn";
  return "Bình thường";
};
export const budgetAlertClass = (campaign) => {
  if (campaign?.isBudgetExceeded) return "badge-inactive";
  if (campaign?.isNearBudgetLimit) return "badge-warning";
  if (num(campaign?.budgetLimit, 0) <= 0) return "badge-info";
  return "badge-active";
};
export const toIso = (v) => {
  const d = new Date(v);
  return Number.isNaN(d.getTime()) ? new Date().toISOString() : d.toISOString();
};
export const toInput = (v) => {
  const d = new Date(v || Date.now());
  const o = d.getTimezoneOffset() * 60000;
  return new Date(d.getTime() - o).toISOString().slice(0, 16);
};
export const withTimeout = (promise, timeoutMs, message) =>
  Promise.race([promise, new Promise((_, reject) => setTimeout(() => reject(new Error(message)), timeoutMs))]);
export const uid = () => {
  try {
    const u = JSON.parse(localStorage.getItem("mygarage_user") || "{}");
    return Number(u?.id || u?.staffId || u?.staff?.id) || null;
  } catch {
    return null;
  }
};

export function formDefault() {
  const now = new Date();
  const end = new Date(now);
  end.setDate(end.getDate() + 30);
  return {
    id: null,
    code: "",
    voucherName: "",
    type: "FIXED",
    value: "",
    minOrderValue: "",
    maxDiscountAmount: "",
    usageLimit: "",
    budgetLimit: "",
    campaignId: "",
    startDate: toInput(now),
    endDate: toInput(end),
    allowedChannels: "",
    allowedPaymentMethods: "",
    isStackable: false,
    approvalRequired: false,
    isActive: true,
    description: ""
  };
}

export const mapForm = (v) => ({
  id: v.id,
  code: v.code || "",
  voucherName: v.voucherName || "",
  type: v.type || "FIXED",
  value: v.value ?? "",
  minOrderValue: v.minOrderValue ?? "",
  maxDiscountAmount: v.maxDiscountAmount ?? "",
  usageLimit: v.usageLimit ?? "",
  budgetLimit: v.budgetLimit ?? "",
  campaignId: v.campaignId ?? "",
  startDate: toInput(v.startDate),
  endDate: toInput(v.endDate),
  allowedChannels: v.allowedChannels || "",
  allowedPaymentMethods: v.allowedPaymentMethods || "",
  isStackable: !!v.isStackable,
  approvalRequired: !!v.approvalRequired,
  isActive: v.isActive ?? true,
  description: v.description || ""
});

export function campaignDefault() {
  const now = new Date();
  const end = new Date(now);
  end.setDate(end.getDate() + 30);
  return {
    id: null,
    campaignCode: "",
    name: "",
    objective: "",
    description: "",
    startDate: toInput(now),
    endDate: toInput(end),
    budgetLimit: "",
    status: "",
    isActive: true,
    ownerStaffId: "",
    voucherIds: [],
    replaceVoucherScope: true
  };
}

export const mapCampaignForm = (item) => ({
  id: item.id ?? null,
  campaignCode: item.campaignCode || "",
  name: item.name || "",
  objective: item.objective || "",
  description: item.description || "",
  startDate: toInput(item.startDate),
  endDate: toInput(item.endDate),
  budgetLimit: item.budgetLimit ?? "",
  status: item.status || "",
  isActive: item.isActive ?? true,
  ownerStaffId: item.ownerStaffId ?? "",
  voucherIds: Array.isArray(item.voucherIds)
    ? item.voucherIds.map((x) => Number(x)).filter((x) => Number.isFinite(x))
    : [],
  replaceVoucherScope: true
});
