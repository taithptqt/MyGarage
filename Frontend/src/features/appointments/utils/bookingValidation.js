import { isValidVietnamesePhone } from "@shared/utils/contactValidation";
import { isValidVietnameseLicensePlate } from "@shared/utils/licensePlateValidation";
import { isValidVin } from "@shared/utils/vinValidation";

export function validateBookingStep(stepNumber, data) {
  const {
    selectedCategoryIds,
    bookingFlow,
    selectedServiceIds,
    selectedBranchId,
    customerForm,
    selectedDate,
    selectedSlot,
    isBatchBooking,
    batchVehicles,
    linkedVehicles,
    vehicleMode,
    selectedVehicleId,
    newVehicle,
    BOOKING_FLOW,
  } = data;

  if (stepNumber === 1 && (selectedCategoryIds || []).length === 0) {
    return "Vui lòng chọn ít nhất một loại dịch vụ.";
  }

  if (stepNumber === 2) {
    if (bookingFlow === BOOKING_FLOW.FIXED && (selectedServiceIds || []).length === 0) {
      return "Luồng giờ cố định yêu cầu chọn ít nhất một dịch vụ cụ thể.";
    }
  }

  if (stepNumber === 3 && !selectedBranchId) {
    return "Vui lòng chọn chi nhánh tiếp nhận.";
  }

  if (stepNumber === 4) {
    if (!String(customerForm?.fullName || "").trim()) return "Vui l�ng nh?p h? t�n khách hàng.";
    if (!String(customerForm?.phone || "").trim()) return "Vui l�ng nh?p s? di?n tho?i khách hàng.";
    if (!isValidVietnamesePhone(customerForm?.phone)) return "S? di?n tho?i khách hàng không d�ng ??nh d?ng Vi?t Nam.";
    if (!selectedDate) return "Vui lòng chọn ngày đặt lịch.";
    if (!selectedSlot) return "Vui lòng chọn khung giờ khả dụng.";

    if (isBatchBooking) {
      if ((batchVehicles || []).length === 0) {
        return "Vui lòng thêm ít nhất một xe vào danh sách đặt nhiều xe.";
      }
      return "";
    }

    const hasVehicle = (linkedVehicles || []).length > 0;
    const usingNewVehicle = !hasVehicle || vehicleMode === "new";

    if (!usingNewVehicle && !selectedVehicleId) {
      return "Vui lòng chọn xe hiện có hoặc chuyển sang thêm xe mới.";
    }

    if (usingNewVehicle) {
      if (!newVehicle?.brandId) return "Vui lòng chọn hãng xe.";
      if (!newVehicle?.modelId) return "Vui lòng chọn dòng xe.";
      if (!String(newVehicle?.licensePlate || "").trim()) return "Vui lòng nhập biển số xe.";
      if (!isValidVietnameseLicensePlate(newVehicle?.licensePlate)) return "Biển số xe không đúng định dạng Việt Nam.";
      if (!String(newVehicle?.vin || "").trim()) return "Vui lòng nhập số VIN.";
      if (!isValidVin(newVehicle?.vin)) return "Số VIN không hợp lệ (17 ký tự, không chứa I, O, Q).";
    }
  }

  return "";
}


