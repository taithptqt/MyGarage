import { getApiBaseUrl } from "@features/auth/api/authApi";
import { bookingAssetUrl } from "@shared/utils/bookingAssets";

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

  return resolveMediaUrl(candidate) || bookingAssetUrl("/assets/booking/showcase-2.jpg");
}

export function resolveProductGallery(product, productImages = []) {
  const sortedImages = [...productImages]
    .filter((image) => Number(image?.productId) === Number(product?.id))
    .sort((left, right) => {
      const mainDelta = Number(Boolean(right?.isMain)) - Number(Boolean(left?.isMain));
      if (mainDelta !== 0) return mainDelta;

      const orderDelta = Number(left?.sortOrder ?? Number.MAX_SAFE_INTEGER) -
        Number(right?.sortOrder ?? Number.MAX_SAFE_INTEGER);
      if (orderDelta !== 0) return orderDelta;

      return Number(left?.id ?? 0) - Number(right?.id ?? 0);
    })
    .map((image) => ({
      id: image?.id ?? image?.imageUrl ?? `image-${product?.id ?? "product"}`,
      src: resolveMediaUrl(image?.imageUrl),
      alt: image?.description || product?.name || "Phụ tùng MyGarage",
      caption: image?.description || "",
    }))
    .filter((image) => image.src);

  if (sortedImages.length > 0) {
    return sortedImages;
  }

  return [
    {
      id: `fallback-${product?.id ?? "product"}`,
      src: resolveProductImage(product),
      alt: product?.name || "Phụ tùng MyGarage",
      caption: "",
    },
  ];
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
