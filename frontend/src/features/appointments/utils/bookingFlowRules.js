import { BOOKING_FLOW } from "@features/appointments/constants/bookingConstants";

export const WORKING_DAY_MINUTES = 12 * 60;

export function normalizeServiceCode(rawCode) {
  return String(rawCode || "")
    .trim()
    .toLowerCase()
    .replace(/[_\-\s]/g, "");
}

export function isFlexibleTriggerServiceCode(rawCode) {
  return normalizeServiceCode(rawCode).startsWith("dg");
}

export function buildFlexibleTriggerServiceIdSet(services) {
  const result = new Set();
  (Array.isArray(services) ? services : []).forEach((service) => {
    const serviceId = Number(service?.id ?? service?.Id);
    if (!Number.isFinite(serviceId) || serviceId <= 0) return;
    const serviceCode = service?.code ?? service?.Code ?? "";
    if (isFlexibleTriggerServiceCode(serviceCode)) {
      result.add(serviceId);
    }
  });
  return result;
}

export function resolveServiceDrivenBookingFlow(
  serviceIds,
  flexibleTriggerServiceIdSet,
  fallback = BOOKING_FLOW.FIXED
) {
  const hasFlexibleTrigger = (Array.isArray(serviceIds) ? serviceIds : []).some((serviceId) =>
    flexibleTriggerServiceIdSet?.has(Number(serviceId))
  );
  return hasFlexibleTrigger ? BOOKING_FLOW.FLEXIBLE : fallback;
}

export function resolveBookingFlowByServicesAndDuration({
  serviceIds,
  flexibleTriggerServiceIdSet,
  executionMinutes,
  workingDayMinutes = WORKING_DAY_MINUTES,
}) {
  const isServiceDrivenFlexibleFlow =
    resolveServiceDrivenBookingFlow(serviceIds, flexibleTriggerServiceIdSet) === BOOKING_FLOW.FLEXIBLE;
  const isLongDurationFlow = Number(executionMinutes || 0) > Number(workingDayMinutes || WORKING_DAY_MINUTES);
  const bookingFlow =
    isServiceDrivenFlexibleFlow || isLongDurationFlow ? BOOKING_FLOW.FLEXIBLE : BOOKING_FLOW.FIXED;

  return {
    bookingFlow,
    isLongDurationFlow,
    isServiceDrivenFlexibleFlow,
  };
}

export function buildSlotSelectionGuide({
  bookingFlow,
  isLongDurationFlow = false,
  isServiceDrivenFlexibleFlow = false,
  inProgress = false,
}) {
  const checkInText = inProgress
    ? "Đang chọn khung giờ check-in (tiếp nhận)."
    : "Chọn khung giờ check-in (tiếp nhận).";
  const repairText = inProgress ? "Đang chọn khung giờ sửa chữa." : "Chọn khung giờ sửa chữa.";

  if (bookingFlow !== BOOKING_FLOW.FLEXIBLE) return repairText;
  if (isLongDurationFlow && !isServiceDrivenFlexibleFlow) {
    return "Tổng thời lượng vượt 12 giờ/ngày, hệ thống chuyển sang chọn khung giờ check-in (tiếp nhận).";
  }
  return checkInText;
}

export function formatCompletionWarningTime(value) {
  if (!value) return "";
  const parsed = new Date(value);
  if (Number.isNaN(parsed.getTime())) return "";

  const hour = String(parsed.getHours()).padStart(2, "0");
  const minute = String(parsed.getMinutes()).padStart(2, "0");
  const day = String(parsed.getDate()).padStart(2, "0");
  const month = String(parsed.getMonth() + 1).padStart(2, "0");
  const year = String(parsed.getFullYear());
  return `${hour} giờ ${minute} phút ngày ${day}/${month}/${year}`;
}

export function buildFlexibleCompletionWarningMessage({ vehicleLabel, completionValue }) {
  const completionText = formatCompletionWarningTime(completionValue);
  if (!completionText) return "";
  const normalizedVehicleLabel = String(vehicleLabel || "").trim() || "xe chưa xác định";
  return `Thời gian hoàn thành dự kiến cho xe ${normalizedVehicleLabel} là ${completionText}. Thời gian hoàn thiện ca sửa thực tế sẽ được tư vấn viên cập nhật sau khi xe vào khoang.`;
}
