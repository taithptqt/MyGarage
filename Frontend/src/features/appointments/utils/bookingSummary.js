import { BOOKING_FLOW } from "@features/appointments/constants/bookingConstants";
import {
  extractField,
  formatDateVi,
  formatStoredVehicleLabel,
  formatVehicleDescriptor,
  timeLabel,
  toSafeUpper,
} from "@features/appointments/utils/bookingCommon";

export function buildSummaryItems({
  customerForm,
  effectiveServices,
  selectedDate,
  selectedSlot,
  selectedExistingVehicle,
  newVehicle,
  selectedBrand,
  selectedModel,
  selectedCategoryIds,
  selectedServiceIds,
  bookingFlow,
  selectedBranchId,
  selectedBranch,
  selectedCategories,
  linkedVehicles,
  vehicleMode,
  selectedVehicleId,
  isUsingAutoDiagnostic,
  isBatchBooking,
  batchVehicles,
}) {
  const items = [];
  const customerName = String(customerForm?.fullName || "").trim();
  const customerPhone = String(customerForm?.phone || "").trim();
  const selectedServiceNames = (effectiveServices || [])
    .map((item) => extractField(item, ["name", "Name"], "").trim())
    .filter(Boolean);
  const dateLabel = selectedDate ? formatDateVi(selectedDate) : "";
  const timeRange = selectedSlot
    ? `${timeLabel(extractField(selectedSlot, ["startTime", "StartTime"]))} - ${timeLabel(extractField(selectedSlot, ["endTime", "EndTime"]))}`
    : "";

  const existingVehicleLabel = selectedExistingVehicle ? formatStoredVehicleLabel(selectedExistingVehicle) : "";
  const newVehiclePlate = toSafeUpper(newVehicle?.licensePlate);
  const newVehicleDescriptor = formatVehicleDescriptor(
    extractField(selectedBrand, ["name", "Name"], ""),
    extractField(selectedModel, ["modelName", "ModelName"], "")
  );
  const newVehicleLabel = newVehiclePlate
    ? (newVehicleDescriptor ? `${newVehiclePlate} - ${newVehicleDescriptor}` : newVehiclePlate)
    : "";

  const step1Done = (selectedCategoryIds || []).length > 0;
  const step2Done = step1Done && (bookingFlow === BOOKING_FLOW.FLEXIBLE || (selectedServiceIds || []).length > 0);
  const step3Done = step2Done && Boolean(selectedBranchId);

  const hasVehicle = (linkedVehicles || []).length > 0;
  const usingNewVehicle = !hasVehicle || vehicleMode === "new";
  const vehicleReady = usingNewVehicle
    ? Boolean(newVehicle?.brandId && newVehicle?.modelId && String(newVehicle?.licensePlate || "").trim() && String(newVehicle?.vin || "").trim())
    : Boolean(selectedVehicleId);
  const step4Done = step3Done && Boolean(customerName && customerPhone && selectedDate && selectedSlot && vehicleReady);

  if (step1Done && (selectedCategories || []).length > 0) {
    const names = selectedCategories
      .map((item) => extractField(item, ["name", "Name"], "").trim())
      .filter(Boolean);
    items.push({ key: "category", label: "Loại dịch vụ", values: names });
  }
  if (step2Done) {
    items.push({
      key: "flow",
      label: "Luồng tiếp nhận",
      value:
        bookingFlow === BOOKING_FLOW.FLEXIBLE
          ? "Check-in linh hoạt (gara tự xếp khoang)"
          : "Giờ cố định theo slot",
    });
  }
  if (step2Done && selectedServiceNames.length > 0) {
    items.push({
      key: "services",
      label: "Dịch vụ cụ thể",
      values: selectedServiceNames,
      autoNote: isUsingAutoDiagnostic,
    });
  }
  if (step3Done && selectedBranch) {
    items.push({ key: "branch", label: "Chi nhánh", value: extractField(selectedBranch, ["name", "Name"], "") });
  }
  if (step4Done && customerName) {
    items.push({ key: "customer", label: "Khách hàng", value: customerName });
  }
  if (step4Done && customerPhone) {
    items.push({ key: "phone", label: "SĐT", value: customerPhone });
  }
  if (step3Done) {
    if (isBatchBooking && (batchVehicles || []).length > 0) {
      items.push({
        key: "vehicle",
        label: "Danh sách xe",
        values: batchVehicles.map((item) => item.displayLabel),
      });
    } else {
      const vehicleLabel = usingNewVehicle ? newVehicleLabel : existingVehicleLabel;
      if (vehicleLabel) {
        items.push({ key: "vehicle", label: "Xe", value: vehicleLabel });
      }
    }
  }
  if (step4Done && dateLabel) {
    items.push({ key: "datetime", label: "Ngày giờ", value: timeRange ? `${dateLabel} - ${timeRange}` : dateLabel });
  }

  return items;
}

export function buildSelectedCategorySummaryLabel(selectedCategories) {
  const names = (selectedCategories || [])
    .map((item) => extractField(item, ["name", "Name"], "").trim())
    .filter(Boolean);
  if (names.length === 0) return "Chưa chọn";
  return names.join(", ");
}





