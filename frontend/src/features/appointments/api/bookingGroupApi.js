import { request } from "@app/apiClient";
import { BOOKING_GROUPS_ROUTES } from "@features/appointments/api/bookingGroupRoutes";

export async function createBookingGroup(payload) {
  return request({
    url: BOOKING_GROUPS_ROUTES.base,
    method: "POST",
    data: payload,
  });
}

export async function getMyBookingGroups() {
  return request({
    url: BOOKING_GROUPS_ROUTES.mine,
    method: "GET",
  });
}

export async function getMyBookingGroupDetail(id) {
  return request({
    url: BOOKING_GROUPS_ROUTES.detail(id),
    method: "GET",
  });
}
