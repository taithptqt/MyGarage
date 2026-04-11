import { getApiBaseUrl } from "@features/auth/api/authApi";

export const RETAIL_CART_STORAGE_KEY = "mygarage_retail_cart";

export const RETAIL_SELECTED_BRANCH_STORAGE_KEY = "mygarage_retail_selected_branch";

export const RETAIL_BRANCH_CHANGED_EVENT = "mygarage-retail-branch-changed";

export function readStoredRetailBranchId() {
  if (typeof window === "undefined") return "";
  try {
    const raw = localStorage.getItem(RETAIL_SELECTED_BRANCH_STORAGE_KEY);
    if (raw == null || raw === "") return "";
    const n = Number(raw);
    return Number.isFinite(n) && n > 0 ? String(Math.floor(n)) : "";
  } catch {
    return "";
  }
}

export function writeStoredRetailBranchId(branchId) {
  if (typeof window === "undefined") return;
  const s = branchId != null && branchId !== "" ? String(branchId).trim() : "";
  if (!s) {
    localStorage.removeItem(RETAIL_SELECTED_BRANCH_STORAGE_KEY);
    return;
  }
  localStorage.setItem(RETAIL_SELECTED_BRANCH_STORAGE_KEY, s);
  try {
    window.dispatchEvent(
      new CustomEvent(RETAIL_BRANCH_CHANGED_EVENT, { detail: { branchId: s } })
    );
  } catch {
    /* ignore */
  }
}

const moneyFormatter = new Intl.NumberFormat("vi-VN");
const mojibakePattern =
  /(?:Ã.|Â.|Ä‘|Æ°|Æ¡|áº|á»|â€|Ă[¡¢£¨©ª¬®°±²³´µ¶·¸¹º»¼½¾¿]|ă[¡¢£¨©ª¬®°±²³´µ¶·¸¹º»¼½¾¿])/u;
const mojibakePatternGlobal =
  /(?:Ã.|Â.|Ä‘|Æ°|Æ¡|áº|á»|â€|Ă[¡¢£¨©ª¬®°±²³´µ¶·¸¹º»¼½¾¿]|ă[¡¢£¨©ª¬®°±²³´µ¶·¸¹º»¼½¾¿])/gu;
const utf8RepairDecoder = typeof TextDecoder !== "undefined" ? new TextDecoder("utf-8") : null;

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

function countMojibakeMatches(value) {
  return [...String(value || "").matchAll(mojibakePatternGlobal)].length;
}

function decodeLatin1AsUtf8(value) {
  if (!utf8RepairDecoder || !value) {
    return String(value || "");
  }

  try {
    const bytes = Uint8Array.from(
      Array.from(String(value), (character) => character.charCodeAt(0) & 0xff)
    );

    return utf8RepairDecoder.decode(bytes);
  } catch {
    return String(value || "");
  }
}

export function repairMojibakeText(value) {
  const raw = typeof value === "string" ? value : String(value ?? "");
  if (!raw) return "";

  const trimmed = raw.trim();
  if (!trimmed) return "";
  if (!mojibakePattern.test(trimmed)) {
    return trimmed;
  }

  const repaired = decodeLatin1AsUtf8(trimmed).trim();
  if (!repaired || repaired.includes("\uFFFD")) {
    return trimmed;
  }

  return countMojibakeMatches(repaired) < countMojibakeMatches(trimmed)
    ? repaired
    : trimmed;
}

export function getDisplayText(value, fallback = "") {
  const repaired = repairMojibakeText(value);
  return repaired || fallback;
}

export function getProductName(product, fallback) {
  const resolvedFallback =
    fallback ?? `Phụ tùng #${product?.id ?? product?.productId ?? product?.ProductId ?? "?"}`;

  return getDisplayText(product?.name ?? product?.productName ?? product?.ProductName, resolvedFallback);
}

export function getProductUnitLabel(product, fallback = "Chưa cập nhật") {
  return getDisplayText(product?.unit ?? product?.Unit, fallback);
}

export function toPositiveInt(value, fallback = 1) {
  const parsed = Number(value);
  if (!Number.isFinite(parsed)) return fallback;

  const rounded = Math.floor(parsed);
  return rounded > 0 ? rounded : fallback;
}

export function toNonNegativeInt(value, fallback = 0) {
  const parsed = Number(value);
  if (!Number.isFinite(parsed)) return fallback;

  const rounded = Math.floor(parsed);
  return rounded >= 0 ? rounded : fallback;
}

export function normalizeAvailableQuantity(value) {
  return toNonNegativeInt(value, 0);
}

export function clampQuantityToAvailable(value, availableQuantity, fallback = 1) {
  const safeAvailable = normalizeAvailableQuantity(availableQuantity);
  if (safeAvailable <= 0) {
    return 0;
  }

  const normalizedValue = toPositiveInt(value, fallback);
  return Math.min(normalizedValue, safeAvailable);
}

export function normalizeText(value) {
  return repairMojibakeText(value)
    .toLowerCase()
    .normalize("NFD")
    .replace(/[\u0300-\u036f]/g, "")
    .trim();
}

export function getCategoryLabel(category) {
  const raw = category?.categoryName || category?.name || "";
  const text = getDisplayText(raw);
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

export function sortProductImages(images) {
  return [...(Array.isArray(images) ? images : [])].sort((left, right) => {
    const leftMain = left?.isMain === true ? 1 : 0;
    const rightMain = right?.isMain === true ? 1 : 0;

    if (leftMain !== rightMain) {
      return rightMain - leftMain;
    }

    const leftSort = Number.isFinite(Number(left?.sortOrder)) ? Number(left.sortOrder) : Number.MAX_SAFE_INTEGER;
    const rightSort = Number.isFinite(Number(right?.sortOrder)) ? Number(right.sortOrder) : Number.MAX_SAFE_INTEGER;
    if (leftSort !== rightSort) {
      return leftSort - rightSort;
    }

    return Number(left?.id || 0) - Number(right?.id || 0);
  });
}

export function getProductDescription(product, productImages = [], options = {}) {
  const shouldUseFallback = options?.fallback !== false;
  const directDescription = [product?.description, product?.Description].find(
    (value) => typeof value === "string" && getDisplayText(value)
  );

  if (directDescription) {
    return getDisplayText(directDescription);
  }

  const imageDescription = sortProductImages(productImages).find(
    (item) => typeof item?.description === "string" && getDisplayText(item.description)
  );

  if (imageDescription?.description) {
    return getDisplayText(imageDescription.description);
  }

  if (!shouldUseFallback) {
    return "";
  }

  return "Thông tin mô tả phụ tùng đang được cập nhật từ dữ liệu sản phẩm hiện có của MyGarage.";
}

export function getProductImageAlt(product, productImage, imageIndex = 0) {
  const productName = getProductName(product, "Phụ tùng MyGarage");
  const imageDescription = getDisplayText(productImage?.description);

  if (imageDescription) {
    return `${productName} - ${imageDescription}`;
  }

  return imageIndex > 0 ? `${productName} - ảnh ${imageIndex + 1}` : productName;
}

export function resolveProductImage(product, productImages = []) {
  const normalizedImages = sortProductImages(productImages);
  const candidate =
    normalizedImages[0]?.imageUrl ||
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

  if (branchName) return getDisplayText(branchName);
  return `Chi nhánh #${branch?.id ?? "?"}`;
}
