import { APPOINTMENT_STATUS } from "@features/appointments/constants/appointmentStatuses";
import { BOOKING_GROUP_STATUS } from "@features/appointments/constants/bookingGroupStatuses";

export const STATUS_META = {
  [APPOINTMENT_STATUS.PENDING]: { label: "Chờ xác nhận", tone: "pending" },
  [APPOINTMENT_STATUS.CONFIRMED]: { label: "Đã xác nhận", tone: "confirmed" },
  [APPOINTMENT_STATUS.ARRIVED]: { label: "Đã đến", tone: "arrived" },
  [APPOINTMENT_STATUS.CANCELED]: { label: "Đã hủy", tone: "canceled" },
  [APPOINTMENT_STATUS.NO_SHOW]: { label: "Không đến", tone: "no-show" },
};

export const GROUP_STATUS_META = {
  [BOOKING_GROUP_STATUS.PENDING]: { label: "Đang chờ", tone: "pending" },
  [BOOKING_GROUP_STATUS.PARTIAL_CONFIRMED]: { label: "Xác nhận một phần", tone: "arrived" },
  [BOOKING_GROUP_STATUS.CONFIRMED]: { label: "Đã xác nhận", tone: "confirmed" },
  [BOOKING_GROUP_STATUS.COMPLETED]: { label: "Đã hoàn tất", tone: "confirmed" },
  [BOOKING_GROUP_STATUS.CANCELED]: { label: "Đã hủy", tone: "canceled" },
};

export const FILTERS = [
  { key: "", label: "Tất cả" },
  { key: APPOINTMENT_STATUS.PENDING, label: "Chờ xác nhận" },
  { key: APPOINTMENT_STATUS.CONFIRMED, label: "Đã xác nhận" },
  { key: APPOINTMENT_STATUS.ARRIVED, label: "Đã đến" },
  { key: APPOINTMENT_STATUS.CANCELED, label: "Đã hủy" },
  { key: APPOINTMENT_STATUS.NO_SHOW, label: "Không đến" },
];

export function toDateTime(value) {
  if (!value) return null;
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? null : date;
}

export function formatDate(value) {
  const date = toDateTime(value);
  if (!date) return "--";
  return date.toLocaleDateString("vi-VN", {
    weekday: "long",
    day: "2-digit",
    month: "2-digit",
    year: "numeric",
  });
}

export function formatTime(value) {
  const date = toDateTime(value);
  if (!date) return "--";
  return date.toLocaleTimeString("vi-VN", {
    hour: "2-digit",
    minute: "2-digit",
  });
}

export function normalizeAppointmentsResponse(payload) {
  if (Array.isArray(payload)) return payload;
  if (Array.isArray(payload?.items)) return payload.items;
  if (Array.isArray(payload?.data)) return payload.data;
  if (Array.isArray(payload?.result)) return payload.result;
  if (Array.isArray(payload?.$values)) return payload.$values;
  return [];
}

export function normalizeBookingGroupsResponse(payload) {
  if (Array.isArray(payload)) return payload;
  if (Array.isArray(payload?.items)) return payload.items;
  if (Array.isArray(payload?.data)) return payload.data;
  if (Array.isArray(payload?.result)) return payload.result;
  if (Array.isArray(payload?.$values)) return payload.$values;
  return [];
}

export function normalizeBookingGroup(item) {
  return {
    id: item?.id ?? item?.Id,
    groupCode: item?.groupCode ?? item?.GroupCode ?? "",
    status: String(item?.status ?? item?.Status ?? "").toUpperCase(),
    totalAppointments: Number(item?.totalAppointments ?? item?.TotalAppointments ?? 0),
    createdAt: item?.createdAt ?? item?.CreatedAt ?? null,
  };
}

export function extractDisplayNotes(raw) {
  const source = String(raw || "").trim();
  if (!source) return "";

  const withoutMeta = source
    .replace(/\s*\[FLOW:[^\]]*\]/gi, "")
    .replace(/\s*\[CLUSTER:[^\]]*\]/gi, "")
    .trim();

  if (!withoutMeta) return "";

  try {
    const parsed = JSON.parse(withoutMeta);
    const symptoms = Array.isArray(parsed)
      ? parsed
      : Array.isArray(parsed?.symptoms)
        ? parsed.symptoms
        : [];
    const cleaned = symptoms
      .map((entry) => String(entry || "").trim())
      .filter(Boolean)
      .filter((entry) => entry.toLowerCase() !== "không có ghi chú gì");
    return cleaned.join(", ");
  } catch {
    if (withoutMeta.toLowerCase() === "không có ghi chú gì") return "";
    return withoutMeta;
  }
}

export function normalizeAppointment(item) {
  return {
    id: item?.id ?? item?.Id,
    status: String(item?.status ?? item?.Status ?? "").toUpperCase(),
    branchName: item?.branchName ?? item?.BranchName ?? "",
    startTime: item?.startTime ?? item?.StartTime,
    endTime: item?.endTime ?? item?.EndTime,
    vehicleLicensePlate: item?.vehicleLicensePlate ?? item?.VehicleLicensePlate ?? "",
    vehicleModelName: item?.vehicleModelName ?? item?.VehicleModelName ?? "",
    serviceNames: Array.isArray(item?.serviceNames)
      ? item.serviceNames
      : Array.isArray(item?.ServiceNames)
        ? item.ServiceNames
        : [],
    customerNotes: extractDisplayNotes(item?.customerNotes ?? item?.CustomerNotes ?? ""),
    cancelReason: item?.cancelReason ?? item?.CancelReason ?? "",
  };
}

export function sortAppointments(items) {
  return [...items].sort((a, b) => {
    const timeA = toDateTime(a.startTime)?.getTime() || 0;
    const timeB = toDateTime(b.startTime)?.getTime() || 0;
    return timeB - timeA;
  });
}





