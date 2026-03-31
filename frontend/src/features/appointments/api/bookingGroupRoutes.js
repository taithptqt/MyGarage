export const BOOKING_GROUPS_BASE = "/api/booking-groups";

export const BOOKING_GROUPS_ROUTES = {
  base: BOOKING_GROUPS_BASE,
  mine: `${BOOKING_GROUPS_BASE}/my`,
  detail: (id) => `${BOOKING_GROUPS_BASE}/my/${id}`,
};
