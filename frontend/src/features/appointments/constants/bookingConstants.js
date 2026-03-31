export const BOOKING_STEPS = [
  "Loại dịch vụ",
  "Chọn dịch vụ",
  "Chi nhánh",
  "Thông tin đặt hẹn",
  "Hoàn thành",
];

export const BOOKING_FLOW = {
  FIXED: "FIXED",
  FLEXIBLE: "FLEXIBLE",
};

export const DEFAULT_DIAGNOSTIC_SERVICE_CODE = "CDTC-001";

export const SESSION_TABS = [
  { key: "MORNING", label: "Buổi sáng", hint: "08:00 - 12:00" },
  { key: "AFTERNOON", label: "Buổi chiều", hint: "12:00 - 17:00" },
  { key: "EVENING", label: "Buổi tối", hint: "17:00 - 20:00" },
];

export const INITIAL_ODD_SLOT_VISIBILITY = {
  MORNING: false,
  AFTERNOON: false,
  EVENING: false,
};

export const INITIAL_NEW_VEHICLE_FORM = {
  brandId: "",
  modelId: "",
  licensePlate: "",
  vin: "",
  color: "",
};
