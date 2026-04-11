import { formatLicensePlateByFieldName } from "@shared/utils/licensePlateInputFormatter";

export function extractField(obj, keys, fallback = null) {
  for (const key of keys) {
    if (obj?.[key] !== undefined && obj?.[key] !== null) return obj[key];
  }
  return fallback;
}

export function normalizeText(value) {
  return String(value || "").toLowerCase().trim();
}

export function formatVnd(value) {
  if (!Number.isFinite(Number(value))) return "0đ";
  return `${new Intl.NumberFormat("vi-VN").format(Math.round(Number(value)))}đ`;
}

export function formatDateVi(dateString) {
  if (!dateString) return "Chưa chọn";
  const parsed = new Date(dateString);
  if (Number.isNaN(parsed.getTime())) return "Chưa chọn";
  return parsed.toLocaleDateString("vi-VN", {
    weekday: "short",
    day: "2-digit",
    month: "2-digit",
    year: "numeric",
  });
}

export function toIsoDateInput(dateValue) {
  const d = dateValue ? new Date(dateValue) : new Date();
  if (Number.isNaN(d.getTime())) return "";
  const y = d.getFullYear();
  const m = String(d.getMonth() + 1).padStart(2, "0");
  const day = String(d.getDate()).padStart(2, "0");
  return `${y}-${m}-${day}`;
}

export function toSafeUpper(value) {
  return String(value || "").trim().toUpperCase();
}

export function timeLabel(value) {
  if (!value) return "";
  const parsed = new Date(value);
  if (!Number.isNaN(parsed.getTime())) {
    return parsed.toLocaleTimeString("vi-VN", { hour: "2-digit", minute: "2-digit" });
  }
  return String(value).slice(0, 5);
}

export function formatLicensePlateInput(value) {
  return formatLicensePlateByFieldName("licensePlate", value);
}

export function formatVehicleDescriptor(brandName, modelName) {
  const brand = String(brandName || "").trim();
  const model = String(modelName || "").trim();
  return `${brand} ${model}`.trim();
}

export function formatStoredVehicleLabel(vehicle) {
  const plate = String(vehicle?.licensePlate || "").trim() || "Không biển số";
  const brandObject = extractField(vehicle, ["brand", "Brand", "vehicleBrand", "VehicleBrand"], null);
  const modelObject = extractField(vehicle, ["model", "Model", "vehicleModel", "VehicleModel"], null);
  const brandName = String(
    extractField(vehicle, ["brandName", "BrandName", "vehicleBrandName", "VehicleBrandName"], "") ||
    extractField(brandObject, ["name", "Name", "brandName", "BrandName"], "")
  ).trim();
  const modelName = String(
    extractField(vehicle, ["modelName", "ModelName", "vehicleModelName", "VehicleModelName"], "") ||
    extractField(modelObject, ["modelName", "ModelName", "name", "Name"], "")
  ).trim();
  const descriptor = formatVehicleDescriptor(brandName, modelName);
  return `${plate} - ${descriptor || "Chưa rõ hãng - chưa rõ dòng xe"}`;
}

export function formatVehicleTabLabel(vehicle) {
  const directPlate = String(
    vehicle?.payload?.licensePlate || vehicle?.licensePlate || vehicle?.LicensePlate || ""
  ).trim();
  if (directPlate) return directPlate;

  const displayLabel = String(vehicle?.displayLabel || "").trim();
  if (!displayLabel) return "Kh\u00f4ng bi\u1ec3n s\u1ed1";

  const [platePart] = displayLabel.split(" - ");
  const plate = String(platePart || "").trim();
  return plate || displayLabel;
}

export function iconByIndex(index) {
  const icons = ["🔧", "🛞", "⚙️", "🛡️", "🧰", "🔩"];
  const safeIndex = Math.abs(Number(index) || 0);
  return icons[safeIndex % icons.length];
}

export function resolveBookingItemIcon(item) {
  const code = toSafeUpper(extractField(item, ["code", "Code"], ""));
  const name = normalizeText(extractField(item, ["name", "Name"], ""));

  if (code === "BH-CAUKE") return "🛻";

  if (code.startsWith("PKG-BH")) {
    if (code.includes("WATER") || name.includes("thủy kích") || name.includes("thuy kich")) return "🌊";
    if (code.includes("HEAVY") || name.includes("tai nạn") || name.includes("tai nan")) return "💥";
    if (code.includes("LIENKET") || name.includes("liên kết") || name.includes("lien ket")) return "🤝";
    if (code.includes("TUTUC") || name.includes("tự túc") || name.includes("tu tuc")) return "🧾";
    if (code.includes("FAST") || name.includes("xước xát") || name.includes("xuoc xat")) return "🎨";
    if (code.includes("ADVISOR") || name.includes("tư vấn") || name.includes("tu van")) return "💬";
    return "🛡️";
  }

  return iconByIndex(extractField(item, ["id", "Id"], 0));
}

export function isQuickCategory(category) {
  const raw = normalizeText(
    `${extractField(category, ["name", "Name"], "")} ${extractField(category, ["description", "Description"], "")}`
  );
  return (
    raw.includes("dich vu nhanh") ||
    raw.includes("dịch vụ nhanh") ||
    raw.includes("bao duong") ||
    raw.includes("bảo dưỡng") ||
    raw.includes("maintenance") ||
    raw.includes("quick")
  );
}

export function buildCustomerNotesPayload(symptomInputs) {
  const normalizedSymptoms = (symptomInputs || [])
    .map((x) => String(x || "").trim())
    .filter(Boolean);
  return normalizedSymptoms.length > 0
    ? JSON.stringify({ symptoms: normalizedSymptoms })
    : null;
}
