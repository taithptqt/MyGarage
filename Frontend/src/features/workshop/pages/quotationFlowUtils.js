export function readField(source, ...keys) {
  for (const key of keys) {
    if (source && source[key] !== undefined && source[key] !== null) {
      return source[key];
    }
  }
  return null;
}

export function toPositiveNumber(value) {
  const parsed = Number(value);
  return Number.isFinite(parsed) && parsed > 0 ? parsed : null;
}

export function normalizeStatus(value) {
  return String(value || "").trim().toUpperCase();
}

export function formatDateTime(value) {
  if (!value) return "";
  const parsed = new Date(value);
  if (Number.isNaN(parsed.getTime())) return "";
  return parsed.toLocaleString("vi-VN");
}

export function formatMoney(value) {
  return Number(value || 0).toLocaleString("vi-VN");
}

export function resolveCurrentStaffId(user) {
  const candidates = [
    user?.id,
    user?.staffId,
    user?.StaffId,
    user?.Id,
    user?.profile?.id,
    user?.profile?.staffId,
  ];

  for (const candidate of candidates) {
    const parsed = Number(candidate);
    if (Number.isFinite(parsed) && parsed > 0) return parsed;
  }

  return null;
}

export function extractLatestDiagnosisId(diagnoses) {
  const list = Array.isArray(diagnoses) ? diagnoses : [];
  if (list.length === 0) return null;

  const sorted = [...list].sort((a, b) => {
    const aTime = new Date(readField(a, "updatedAt", "UpdatedAt", "createdAt", "CreatedAt") || 0).getTime();
    const bTime = new Date(readField(b, "updatedAt", "UpdatedAt", "createdAt", "CreatedAt") || 0).getTime();
    return bTime - aTime;
  });

  return toPositiveNumber(readField(sorted[0], "id", "Id"));
}

export function firstNonEmptyText(...values) {
  for (const value of values) {
    const text = String(value ?? "").trim();
    if (text) return text;
  }
  return "";
}

export function normalizeRecommendationItemType(raw) {
  const token = String(raw || "").trim().toUpperCase();
  if (token.includes("PART") || token.includes("MATERIAL") || token.includes("PRODUCT")) return "PART";
  return "SERVICE";
}

export function resolveSlaSeverity(referenceTime) {
  if (!referenceTime) return "NORMAL";
  const createdTime = new Date(referenceTime).getTime();
  if (!Number.isFinite(createdTime)) return "NORMAL";

  const elapsedMinutes = (Date.now() - createdTime) / 60000;
  if (elapsedMinutes >= 30) return "RED";
  if (elapsedMinutes >= 15) return "YELLOW";
  return "NORMAL";
}

export function resolveRecommendationStatus(row) {
  const status = normalizeStatus(readField(row, "recommendationStatus", "RecommendationStatus"));
  if (!status) return "ACTIVE";
  return status;
}
