export const VIETNAM_VIN_PATTERN = "^[A-HJ-NPR-Z0-9]{17}$";
export const VIETNAM_VIN_TITLE =
  "Số VIN hợp lệ gồm 17 ký tự chữ in hoa và số, không chứa I, O, Q.";

export function normalizeVin(value) {
  return String(value || "")
    .toUpperCase()
    .replace(/[^A-Z0-9]/g, "");
}

export function formatVinInput(value) {
  return normalizeVin(value).slice(0, 17);
}

export function isValidVin(value) {
  return /^[A-HJ-NPR-Z0-9]{17}$/.test(formatVinInput(value));
}
