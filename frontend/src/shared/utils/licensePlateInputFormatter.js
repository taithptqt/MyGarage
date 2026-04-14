import { formatVietnameseLicensePlateInput } from "@shared/utils/licensePlateValidation";

const DEFAULT_LICENSE_PLATE_FIELDS = new Set([
  "licensePlate",
  "vehicleLicensePlate",
  "guestVehiclePlate",
]);

export function isLicensePlateFieldName(fieldName, extraFieldNames = []) {
  const key = String(fieldName || "").trim();
  if (!key) return false;
  if (DEFAULT_LICENSE_PLATE_FIELDS.has(key)) return true;
  return Array.isArray(extraFieldNames) && extraFieldNames.includes(key);
}

export function formatLicensePlateByFieldName(fieldName, value, extraFieldNames = []) {
  return isLicensePlateFieldName(fieldName, extraFieldNames)
    ? formatVietnameseLicensePlateInput(value)
    : value;
}

