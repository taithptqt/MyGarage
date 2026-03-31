import { request } from "../../../app/apiClient";
import { APPOINTMENTS_ROUTES } from "./appointmentRoutes";

export async function getAvailableSlots(branchId, date, options = {}) {
  const params = { branchId, date };
  if (options.requestedServiceIds) {
    params.requestedServiceIds = options.requestedServiceIds;
  }
  if (options.bookingFlow) {
    params.bookingFlow = options.bookingFlow;
  }

  return request({
    url: APPOINTMENTS_ROUTES.availability,
    method: "GET",
    params,
  });
}
