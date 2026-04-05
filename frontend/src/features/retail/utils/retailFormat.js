import { getApiBaseUrl } from "@features/auth/api/authApi";

export const RETAIL_CART_STORAGE_KEY = "mygarage_retail_cart";

const moneyFormatter = new Intl.NumberFormat("vi-VN");

const RETAIL_ORDER_STATUS_META = {
  DRAFT: {
    label: "Chờ thanh toán",
    tone: "draft",
    description: "Đơn đã tạo nhưng chưa xác nhận thanh toán.",
  },
  CONFIRMED: {
    label: "Đã xác nhận",
    tone: "confirmed",
    description: "Đơn đã được xác nhận và sẵn sàng xử lý.",
  },
};

export function formatVnd(value) {
  const amount = Number(value);
  return `${moneyFormatter.format(Number.isFinite(amount) ? Math.round(amount) : 0)}đ`;
}

export function toPositiveInt(value, fallback = 1) {
  const parsed = Number(value);
  if (!Number.isFinite(parsed)) return fallback;

  const rounded = Math.floor(parsed);
  return rounded > 0 ? rounded : fallback;
}

export function normalizeText(value) {
  return String(value || "")
    .toLowerCase()
    .normalize("NFD")
    .replace(/[\u0300-\u036f]/g, "")
    .trim();
}

export function getCategoryLabel(category) {
  const raw = category?.categoryName || category?.name || "";
  const text = String(raw).trim();
  return text || `Danh mục #${category?.id ?? "?"}`;
}

export function getRetailOrderStatusMeta(status) {
  const normalized = String(status || "DRAFT").trim().toUpperCase();
  return (
    RETAIL_ORDER_STATUS_META[normalized] || {
      label: normalized || "Không rõ",
      tone: "neutral",
      description: "Trạng thái hiện tại chưa được ánh xạ riêng trên frontend.",
    }
  );
}

export function resolveMediaUrl(value) {
  if (!value) return "";

  const raw = String(value).trim();
  if (!raw) return "";

  if (
    raw.startsWith("http://") ||
    raw.startsWith("https://") ||
    raw.startsWith("data:") ||
    raw.startsWith("blob:")
  ) {
    return raw;
  }

  const base = getApiBaseUrl();
  if (!base) return raw;

  return `${base}${raw.startsWith("/") ? "" : "/"}${raw}`;
}

export function resolveProductImage(product) {
  const candidate =
    product?.imageUrl ||
    product?.thumbnailUrl ||
    product?.imagePath ||
    product?.image ||
    product?.photoUrl ||
    product?.photo;

  return resolveMediaUrl(candidate) || "/assets/booking/showcase-2.jpg";
}

export function getBranchLabel(branch) {
  const branchName =
    branch?.branchName ||
    branch?.name ||
    branch?.branchCode ||
    branch?.code;

  if (branchName) return String(branchName).trim();
  return `Chi nhánh #${branch?.id ?? "?"}`;
}
