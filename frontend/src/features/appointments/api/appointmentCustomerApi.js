import { request } from "@app/apiClient";
import { APPOINTMENTS_ROUTES } from "@features/appointments/api/appointmentRoutes";

export async function getMyAppointments() {
  return request({ url: APPOINTMENTS_ROUTES.mine, method: "GET" });
}

export async function bookAppointment(payload) {
  return request({
    url: APPOINTMENTS_ROUTES.base,
    method: "POST",
    data: payload,
  });
}

export async function bookGuestAppointment(payload) {
  return request({
    url: APPOINTMENTS_ROUTES.guest,
    method: "POST",
    data: payload,
  });
}

export async function cancelAppointment(id, reason) {
  return request({
    url: APPOINTMENTS_ROUTES.status(id),
    method: "PATCH",
    data: { status: "CANCELED", reason: reason || null },
  });
}
