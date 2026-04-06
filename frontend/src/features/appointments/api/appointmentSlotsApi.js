import { request } from "@app/apiClient";
import { APPOINTMENTS_ROUTES } from "@features/appointments/api/appointmentRoutes";

export async function getAvailableSlots(branchId, date, options = {}) {
  const params = { branchId, date };
  if (options.requestedServiceIds) {
    params.requestedServiceIds = options.requestedServiceIds;
  }
  if (options.bookingFlow) {
    params.bookingFlow = options.bookingFlow;
  }
  if (Number.isFinite(Number(options.vehicleCount)) && Number(options.vehicleCount) > 0) {
    params.vehicleCount = Number(options.vehicleCount);
  }
  if (typeof options.isParallel === "boolean") {
    params.isParallel = options.isParallel;
  }

  return request({
    url: APPOINTMENTS_ROUTES.availability,
    method: "GET",
    params,
  });
}

export async function getNearestSlotSuggestion(branchId, checkInAt, options = {}) {
  const params = { checkInAt };
  if (options.requestedServiceIds) {
    params.requestedServiceIds = options.requestedServiceIds;
  }
  if (Number.isFinite(Number(options.vehicleCount)) && Number(options.vehicleCount) > 0) {
    params.vehicleCount = Number(options.vehicleCount);
  }
  if (typeof options.isParallel === "boolean") {
    params.isParallel = options.isParallel;
  }
  if (Number.isFinite(Number(options.searchDays)) && Number(options.searchDays) > 0) {
    params.searchDays = Number(options.searchDays);
  }

  return request({
    url: APPOINTMENTS_ROUTES.nearestSlot(branchId),
    method: "GET",
    params,
  });
}

export async function createSlotHold(payload) {
  return request({
    url: APPOINTMENTS_ROUTES.slotHolds,
    method: "POST",
    data: payload,
  });
}

export async function releaseSlotHold(holdToken) {
  return request({
    url: APPOINTMENTS_ROUTES.slotHoldDetail(holdToken),
    method: "DELETE",
  });
}
