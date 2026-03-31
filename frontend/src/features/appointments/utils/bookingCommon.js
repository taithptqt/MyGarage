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
  const raw = String(value || "")
    .toUpperCase()
    .replace(/[^A-Z0-9]/g, "");

  if (!raw) return "";

  let prefix = "";
  let rest = "";

  const startsWithProvince = /^\d{2}[A-Z]/.test(raw);
  if (startsWithProvince) {
    prefix = raw.slice(0, 3);
    rest = raw.slice(3, 8);
  } else {
    prefix = raw.slice(0, Math.min(3, raw.length));
    rest = raw.slice(prefix.length, prefix.length + 5);
  }

  if (!rest) return prefix;
  if (rest.length <= 3) return `${prefix}-${rest}`;
  return `${prefix}-${rest.slice(0, 3)}.${rest.slice(3)}`;
}

export function formatVehicleDescriptor(brandName, modelName) {
  const brand = String(brandName || "").trim();
  const model = String(modelName || "").trim();
  return `${brand} ${model}`.trim();
}

export function formatStoredVehicleLabel(vehicle) {
  const plate = String(vehicle?.licensePlate || "").trim() || "Không biển số";
  const descriptor = formatVehicleDescriptor(vehicle?.brandName, vehicle?.modelName);
  return descriptor ? `${plate} - ${descriptor}` : plate;
}

export function iconByIndex(index) {
  const icons = ["🔧", "🛞", "⚙️", "🛡️", "🧰", "🔩"];
  return icons[index % icons.length];
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
