export const VIETNAM_LICENSE_PLATE_PATTERN =
  "^(?:[0-9]{2}[A-Z0-9]{1,2}-?[0-9]{3}(?:\\.[0-9]{1,2})?|[0-9]{2}[A-Z0-9]{1,2}-?[0-9]{4,5})$";

export const VIETNAM_LICENSE_PLATE_TITLE =
  "Biển số hợp lệ: 30A-12345 hoặc 30A-123.45";

export function normalizeVietnameseLicensePlate(value) {
  return String(value || "")
    .toUpperCase()
    .replace(/[^A-Z0-9]/g, "");
}

export function isValidVietnameseLicensePlate(value) {
  const normalized = normalizeVietnameseLicensePlate(value);
  return /^\d{2}[A-Z0-9]{1,2}\d{4,5}$/.test(normalized);
}

export function formatVietnameseLicensePlateInput(value) {
  const raw = normalizeVietnameseLicensePlate(value);
  if (!raw) return "";

  const suffixMatch = raw.match(/(\d{1,5})$/);
  if (!suffixMatch) return raw;

  const numberPart = suffixMatch[1];
  const prefix = raw.slice(0, raw.length - numberPart.length);

  if (!/^\d{2}[A-Z0-9]{1,2}$/.test(prefix)) {
    return raw;
  }

  if (numberPart.length <= 4) {
    return `${prefix}-${numberPart}`;
  }

  return `${prefix}-${numberPart.slice(0, 3)}.${numberPart.slice(3)}`;
}
