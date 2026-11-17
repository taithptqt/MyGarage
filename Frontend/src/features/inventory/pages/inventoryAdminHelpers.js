export const SECTION_META = {
  warehouseStructure: {
    title: "Quản lý cấu trúc kho",
    subtitle: "Tạo/sửa/khóa kho, cấu hình loại kho, bin/location và kho mặc định",
    hint: "Bắt đầu bằng việc tạo kho theo từng chi nhánh, sau đó gán kho mặc định cho từng loại hàng."
  },
  catalogProducts: {
    title: "Quản lý sản phẩm kho",
    subtitle: "Tạo/sửa sản phẩm, quản lý SKU, đơn vị tính, hình ảnh và trạng thái kinh doanh",
    hint: "Chuẩn hóa mã SKU và danh mục trước khi nhập kho để tránh trùng lặp dữ liệu."
  },
  catalogCategories: {
    title: "Quản lý danh mục sản phẩm",
    subtitle: "Tạo/sửa nhóm hàng để phân loại vật tư rõ ràng và đồng nhất toàn hệ thống",
    hint: "Nên tạo cấu trúc danh mục ngắn gọn, dễ hiểu để người mới tìm hàng nhanh hơn."
  },
  catalogSuppliers: {
    title: "Quản lý nhà cung cấp",
    subtitle: "Lưu thông tin nhà cung cấp phục vụ truy xuất nguồn hàng và đối soát mua vào",
    hint: "Chuẩn tên nhà cung cấp ngay từ đầu để tránh trùng và dễ truy vấn lịch sử giao dịch."
  },
  catalog: {
    title: "Quản lý sản phẩm kho",
    subtitle: "Tạo/sửa sản phẩm, quản lý SKU, đơn vị tính, hình ảnh và trạng thái kinh doanh",
    hint: "Chuẩn hóa mã SKU và danh mục trước khi nhập kho để tránh trùng lặp dữ liệu."
  },
  policy: {
    title: "Cấu hình chính sách tồn kho",
    subtitle: "Min/Max, ngưỡng cảnh báo, âm kho, reserve, vượt định mức, hoàn trả, chuyển kho",
    hint: "Thiết lập chính sách chung trước, sau đó điều chỉnh tồn an toàn riêng cho từng chi nhánh."
  },
  monitoring: {
    title: "Giám sát tồn kho toàn hệ thống",
    subtitle: "Theo chi nhánh, kho, sản phẩm, hàng sắp hết, tồn chậm, reserved, giá trị tồn",
    hint: "Theo dõi các chỉ số tổng quan hàng ngày để phát hiện sớm thiếu hàng hoặc lệch tồn."
  },
  audits: {
    title: "Quản lý và duyệt kiểm kê",
    subtitle: "Theo dõi kỳ kiểm kê, chênh lệch và duyệt điều chỉnh sau kiểm kê",
    hint: "Ưu tiên xử lý các phiếu có chênh lệch lớn trước để giảm rủi ro sai lệch tồn kho."
  },
  transactions: {
    title: "Lịch sử nhập xuất và truy vết",
    subtitle: "Truy vết đầy đủ theo chi nhánh/kho/sản phẩm/loại giao dịch và chứng từ nguồn",
    hint: "Dùng bộ lọc theo loại giao dịch để kiểm tra nhanh các luồng nhập/xuất bất thường."
  },
  approvals: {
    title: "Phê duyệt ngoại lệ kho",
    subtitle: "Duyệt các case xuất vượt, điều chỉnh tồn, hoàn trả bất thường, kho lỗi",
    hint: "Chỉ duyệt ngoại lệ khi có đủ chứng từ và ghi chú rõ nguyên nhân."
  },
  alerts: {
    title: "Cảnh báo và xử lý block",
    subtitle: "Trung tâm cảnh báo tồn thấp, âm kho, chứng từ treo, giao dịch bất thường",
    hint: "Xử lý cảnh báo mức cao trước để tránh ảnh hưởng đến hoàn tất sửa chữa và xuất hàng."
  },
  reports: {
    title: "Báo cáo quản trị kho",
    subtitle: "Báo cáo tồn kho, giá trị, luân chuyển, top xuất/hoàn, chênh lệch kiểm kê và audit log",
    hint: "Có thể dùng báo cáo này để đối soát nội bộ và chuẩn bị hồ sơ kiểm toán."
  }
};

export const ITEM_TYPE_OPTIONS = [
  { value: "SELLABLE", label: "Bán được" },
  { value: "DEFECTIVE", label: "Lỗi/Hỏng" },
  { value: "CONSUMABLE", label: "Tiêu hao" },
  { value: "TRANSIT", label: "Trung chuyển" }
];

export const EMPTY_PRODUCT_FORM = {
  sku: "",
  name: "",
  unit: "Cái",
  categoryId: "",
  costPrice: "",
  retailPrice: "",
  servicePrice: "",
  taxRate: "",
  warrantyMonths: "",
  isStrictCapacityControl: false,
  isActive: true
};

export const EMPTY_CATEGORY_FORM = {
  categoryName: "",
  parentId: "",
  description: "",
  isDepositRequired: false
};

export const RESERVE_RULE_OPTIONS = [
  { value: "STRICT", label: "Nghiêm ngặt (giữ đúng số lượng để reserve)" },
  { value: "FLEXIBLE", label: "Linh hoạt (cho phép điều phối khi thiếu)" }
];

export const RETURN_RULE_OPTIONS = [
  { value: "ALLOW_WITH_REASON", label: "Cho phép, bắt buộc ghi lý do" },
  { value: "REQUIRE_APPROVAL", label: "Phải duyệt trước khi hoàn" },
  { value: "BLOCK_IF_MISSING_INFO", label: "Chặn nếu thiếu thông tin" }
];

export const TRANSFER_RULE_OPTIONS = [
  { value: "REQUIRE_APPROVAL", label: "Phải duyệt khi chuyển kho" },
  { value: "AUTO_APPROVE_INTERNAL", label: "Tự duyệt trong nội bộ chi nhánh" },
  { value: "BLOCK_NEGATIVE", label: "Chặn nếu gây âm kho" }
];

export const APPROVAL_STATUS_META = {
  PENDING: { label: "Chờ duyệt", className: "badge-suspended" },
  APPROVED: { label: "Đã duyệt", className: "badge-active" },
  REJECTED: { label: "Từ chối", className: "badge-deleted" }
};

export const AUDIT_STATUS_META = {
  DRAFT: { label: "Nháp", className: "badge-inactive" },
  PENDING: { label: "Chờ duyệt", className: "badge-suspended" },
  APPROVED: { label: "Đã duyệt", className: "badge-active" },
  REJECTED: { label: "Từ chối", className: "badge-deleted" },
  CLOSED: { label: "Đã chốt", className: "badge-active" }
};

export const ALERT_SEVERITY_META = {
  CRITICAL: { label: "Nghiêm trọng", className: "badge-deleted" },
  HIGH: { label: "Cao", className: "badge-suspended" },
  MEDIUM: { label: "Trung bình", className: "badge-inactive" },
  LOW: { label: "Thấp", className: "badge-inactive" }
};

export const SOURCE_LABELS = {
  PurchaseOrder: "Đơn mua hàng",
  RetailOrder: "Đơn bán lẻ",
  RetailReturn: "Trả hàng bán lẻ",
  MaterialRequest: "Phiếu yêu cầu vật tư",
  MaterialReturn: "Phiếu hoàn vật tư",
  Unknown: "Không rõ nguồn"
};

export const TRANSACTION_TYPE_LABELS = {
  PURCHASE_RECEIVE: "Nhập mua hàng",
  MATERIAL_RETURN: "Hoàn trả vật tư",
  SERVICE_ISSUE: "Xuất cho lệnh sửa chữa",
  RETAIL_ISSUE: "Xuất bán lẻ",
  TRANSFER: "Điều chuyển kho",
  IN: "Nhập kho",
  OUT: "Xuất kho",
  RETURN: "Hoàn trả"
};

export const APPROVAL_TYPE_LABELS = {
  OVER_ISSUE: "Xuất vượt định mức",
  OVER_SERVICE_ORDER_QTY: "Xuất vượt số lượng lệnh sửa chữa",
  AUDIT_ADJUSTMENT: "Điều chỉnh tồn sau kiểm kê",
  DEFECTIVE_RETURN: "Nhập lại hàng lỗi",
  DEFECTIVE_TRANSFER: "Chuyển hàng sang kho lỗi",
  DAMAGE_BY_STAFF: "Hỏng hàng do nhân viên",
  ABNORMAL_RETURN: "Hoàn trả bất thường",
  UNBLOCK_CASE: "Mở khóa case bị block"
};

export const ALERT_TYPE_LABELS = {
  LOW_STOCK: "Sắp hết hàng",
  NEGATIVE_STOCK: "Âm kho",
  TX_NO_SOURCE: "Giao dịch thiếu chứng từ",
  RETURN_PENDING: "Phiếu hoàn trả treo",
  ISSUE_BLOCKED: "Phiếu xuất bị treo",
  AUDIT_GAP: "Chênh lệch kiểm kê",
  DEFECTIVE_MISMATCH: "Kho lỗi chưa đồng bộ"
};

const ITEM_TYPE_LABEL_MAP = Object.fromEntries(ITEM_TYPE_OPTIONS.map((opt) => [opt.value, opt.label]));

export function normalizeKey(value) {
  return String(value || "").trim().toUpperCase();
}

export function toNumber(value, fallback = 0) {
  const n = Number(value);
  return Number.isFinite(n) ? n : fallback;
}

export function toNullableNumber(value) {
  const raw = String(value ?? "").trim();
  if (!raw) return null;
  const parsed = Number(raw);
  if (!Number.isFinite(parsed)) return null;
  return parsed;
}

export function csvEscape(value) {
  const str = String(value ?? "");
  if (!str.includes(",") && !str.includes("\"") && !str.includes("\n")) return str;
  return `"${str.replace(/"/g, '""')}"`;
}

export function normalizeBranchStatus(status) {
  const normalized = normalizeKey(status);
  if (["ACTIVE", "ENABLED", "OPEN", "HOAT_DONG", "HOẠT_ĐỘNG"].includes(normalized)) return "Hoạt động";
  if (["INACTIVE", "DISABLED", "CLOSED", "DONG", "ĐÓNG"].includes(normalized)) return "Ngừng hoạt động";
  return status || "Chưa cấu hình";
}

export function normalizeBranchStatusClass(status) {
  const normalized = normalizeKey(status);
  if (["ACTIVE", "ENABLED", "OPEN", "HOAT_DONG", "HOẠT_ĐỘNG"].includes(normalized)) return "is-active";
  if (["INACTIVE", "DISABLED", "CLOSED", "DONG", "DONG"].includes(normalized)) return "is-inactive";
  return "is-unknown";
}

export function friendlyType(type) {
  return ITEM_TYPE_LABEL_MAP[normalizeKey(type)] || type || "-";
}

export function friendlyTransactionType(type) {
  const normalized = normalizeKey(type);
  if (!normalized) return "-";
  if (TRANSACTION_TYPE_LABELS[normalized]) return TRANSACTION_TYPE_LABELS[normalized];
  return normalized.replace(/_/g, " ");
}

export function friendlySource(source) {
  return SOURCE_LABELS[source] || source || "-";
}

export function friendlyApprovalType(type) {
  const normalized = normalizeKey(type);
  if (!normalized) return "-";
  return APPROVAL_TYPE_LABELS[normalized] || normalized.replace(/_/g, " ");
}

export function friendlyAlertType(type) {
  const normalized = normalizeKey(type);
  if (!normalized) return "-";
  return ALERT_TYPE_LABELS[normalized] || normalized.replace(/_/g, " ");
}

export function resolveStatusMeta(status, mapping) {
  const normalized = normalizeKey(status);
  const meta = mapping[normalized];
  if (meta) return meta;
  return { label: status || "Chưa rõ", className: "badge-inactive" };
}

export function paginateItems(items, currentPage, pageSize) {
  if (!Array.isArray(items) || items.length === 0) return [];
  const safePageSize = Math.max(1, Number(pageSize) || 10);
  const totalPages = Math.max(1, Math.ceil(items.length / safePageSize));
  const safePage = Math.min(Math.max(1, Number(currentPage) || 1), totalPages);
  const startIndex = (safePage - 1) * safePageSize;
  const endIndex = Math.min(startIndex + safePageSize, items.length);
  const result = [];
  for (let i = startIndex; i < endIndex; i++) {
    result.push(items[i]);
  }
  return result;
}
