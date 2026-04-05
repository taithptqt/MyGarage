import { API_BASE_URL } from "@app/apiClient";

const moneyFormatter = new Intl.NumberFormat("vi-VN");

function read(value, fallback = "") {
  const text = String(value ?? "").trim();
  return text || fallback;
}

function toNumber(value) {
  const parsed = Number(value);
  return Number.isFinite(parsed) ? parsed : null;
}

function normalizeOrderType(value) {
  const normalized = read(value).toUpperCase();
  if (normalized === "RETAIL" || normalized === "SERVICE") {
    return normalized;
  }
  return "";
}

function resolveOrderCardImageUrl(value) {
  const raw = read(value);
  if (!raw) return null;

  const normalized = raw.replace(/\\/g, "/").trim();
  if (!normalized) return null;

  if (
    normalized.startsWith("http://") ||
    normalized.startsWith("https://") ||
    normalized.startsWith("data:") ||
    normalized.startsWith("blob:")
  ) {
    return normalized;
  }

  // Ignore Windows absolute file paths that the browser cannot load directly.
  if (/^[a-zA-Z]:\//.test(normalized)) {
    return null;
  }

  const path = normalized.startsWith("/") ? normalized : `/${normalized}`;
  const base = String(API_BASE_URL || "").trim().replace(/\/$/, "");
  return base ? `${base}${path}` : path;
}

function normalizeOrderCard(raw) {
  if (!raw || typeof raw !== "object") return null;

  const orderType = normalizeOrderType(raw.orderType ?? raw.OrderType);
  const orderId = Number(raw.orderId ?? raw.OrderId ?? 0);
  if (!orderType || !Number.isFinite(orderId) || orderId <= 0) {
    return null;
  }

  const status = read(raw.status ?? raw.Status).toUpperCase();
  const detailUrl = read(raw.detailUrl ?? raw.DetailUrl);
  return {
    orderType,
    orderId,
    orderCode: read(
      raw.orderCode ?? raw.OrderCode,
      orderType === "RETAIL" ? `RO-${orderId}` : `SO-${orderId}`
    ),
    title: read(raw.title ?? raw.Title, "Don hang"),
    subtitle: read(raw.subtitle ?? raw.Subtitle, "Thong tin don hang"),
    status,
    statusLabel: read(raw.statusLabel ?? raw.StatusLabel, status || "Dang xu ly"),
    totalAmount: toNumber(raw.totalAmount ?? raw.TotalAmount),
    itemCount: Number(raw.itemCount ?? raw.ItemCount ?? 0) || null,
    imageUrl: resolveOrderCardImageUrl(raw.imageUrl ?? raw.ImageUrl),
    branchName: read(raw.branchName ?? raw.BranchName) || null,
    createdAt: raw.createdAt ?? raw.CreatedAt ?? null,
    detailUrl: detailUrl && detailUrl.startsWith("/") ? detailUrl : null,
  };
}

export function getOrderCardFromMessage(message) {
  if (!message || typeof message !== "object") return null;

  const direct = normalizeOrderCard(message.orderCard ?? message.OrderCard);
  if (direct) return direct;

  const messageType = read(message.messageType ?? message.MessageType).toUpperCase();
  if (messageType !== "ORDER_CARD") return null;

  const content = read(message.content ?? message.Content);
  if (!content) return null;

  try {
    const parsed = JSON.parse(content);
    return normalizeOrderCard(parsed);
  } catch {
    return null;
  }
}

export function buildOrderCardMessagePayload(orderCard) {
  const normalized = normalizeOrderCard(orderCard);
  if (!normalized) return null;

  return {
    messageType: "ORDER_CARD",
    orderCard: {
      orderType: normalized.orderType,
      orderId: normalized.orderId,
      orderCode: normalized.orderCode,
      title: normalized.title,
      subtitle: normalized.subtitle,
      status: normalized.status,
      statusLabel: normalized.statusLabel,
      totalAmount: normalized.totalAmount,
      itemCount: normalized.itemCount,
      imageUrl: normalized.imageUrl,
      branchName: normalized.branchName,
      createdAt: normalized.createdAt,
      detailUrl: normalized.detailUrl,
    },
  };
}

export function formatOrderAmount(value) {
  const amount = toNumber(value);
  if (amount === null) return "";
  return `${moneyFormatter.format(Math.round(amount))}d`;
}

export function getOrderTypeLabel(orderType) {
  const normalized = normalizeOrderType(orderType);
  if (normalized === "RETAIL") return "Don san pham";
  if (normalized === "SERVICE") return "Don dich vu";
  return "Don hang";
}
