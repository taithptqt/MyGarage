/**
 * Map backend LinkAction to in-app navigation.
 *
 * Backend automation emits three kinds of LinkAction:
 *  - /jobs/maintenance/{vehicleId}/{dedupeToken}   → maintenance reminder
 *  - /jobs/survey/service-order/{serviceOrderId}   → post-service survey
 *  - /jobs/insurance/{vehicleInsuranceId}/{date}   → insurance expiry
 *
 * These legacy backend-internal paths must never be opened directly by the
 * frontend router. This module maps the routes we can resolve synchronously to
 * safe customer-portal paths. Legacy survey links need an API resolver because
 * the frontend cannot derive vehicleId from serviceOrderId on its own.
 *
 * @param {string|null|undefined} linkAction
 * @returns {string|null} internal route path, or null if no safe mapping exists
 */
export function resolvePortalNotificationLink(linkAction) {
  if (!linkAction || typeof linkAction !== "string") return null;
  const raw = linkAction.trim();
  if (!raw.startsWith("/")) return null;

  const lower = raw.toLowerCase();

  // ── Already-safe internal customer-portal routes ──────────────────────────
  const allowedPrefixes = [
    "/my-garage-dashboard",
    "/my-vehicles",
    "/my-notifications",
    "/my-appointments",
  ];
  if (allowedPrefixes.some((p) => lower === p || lower.startsWith(`${p}/`))) {
    return raw;
  }

  // ── /jobs/maintenance/{vehicleId}/… → /my-vehicles/{vehicleId}?tab=maintenance ──
  const MAINTENANCE_PREFIX = "/jobs/maintenance/";
  if (lower.startsWith(MAINTENANCE_PREFIX)) {
    const rest = raw.slice(MAINTENANCE_PREFIX.length);
    const vehicleIdStr = rest.split("/")[0];
    const vehicleId = Number(vehicleIdStr);
    if (Number.isFinite(vehicleId) && vehicleId > 0) {
      return `/my-vehicles/${vehicleId}?tab=maintenance`;
    }
    // fallback: vehicle list
    return "/my-vehicles";
  }

  const SURVEY_PREFIX = "/jobs/survey/service-order/";
  if (lower.startsWith(SURVEY_PREFIX)) {
    return null;
  }

  // ── /jobs/insurance/{vehicleInsuranceId}/{date} → /my-notifications ───────
  // Insurance details are not yet a first-class page; keep the customer in the
  // notifications view where the rich content text is visible.
  const INSURANCE_PREFIX = "/jobs/insurance/";
  if (lower.startsWith(INSURANCE_PREFIX)) {
    return "/my-notifications?notificationType=insurance";
  }

  // Unknown backend path — return null (caller will hide the CTA button)
  return null;
}

/**
 * Human-readable label for the CTA button based on derived notification type.
 *
 * @param {string} derivedType
 * @returns {string}
 */
export function notificationCtaLabel(derivedType) {
  switch (derivedType) {
    case "maintenance":
      return "Xem gợi ý bảo dưỡng";
    case "survey":
      return "Xem đơn dịch vụ";
    case "insurance":
      return "Xem chi tiết";
    case "service_status":
      return "Xem tiến độ";
    default:
      return "Mở liên kết";
  }
}

export function isLegacySurveyNotificationLink(linkAction) {
  if (!linkAction || typeof linkAction !== "string") return false;
  return linkAction.trim().toLowerCase().startsWith("/jobs/survey/service-order/");
}

export function extractLegacySurveyServiceOrderId(linkAction) {
  if (!isLegacySurveyNotificationLink(linkAction)) return null;
  const value = Number(linkAction.trim().slice("/jobs/survey/service-order/".length).split("/")[0]);
  return Number.isFinite(value) && value > 0 ? value : null;
}

export function notificationTone(derivedType) {
  switch (derivedType) {
    case "maintenance":
      return "maintenance";
    case "survey":
      return "survey";
    case "insurance":
      return "insurance";
    case "service_status":
      return "service-status";
    default:
      return "general";
  }
}

/**
 * Badge label shown beside the notification card type chip.
 *
 * @param {string} derivedType
 * @returns {string}
 */
export function notificationTypeLabel(derivedType) {
  switch (derivedType) {
    case "maintenance":
      return "Bảo dưỡng";
    case "survey":
      return "Khảo sát";
    case "insurance":
      return "Bảo hiểm";
    case "service_status":
      return "Trạng thái sửa chữa";
    default:
      return "Thông báo";
  }
}
