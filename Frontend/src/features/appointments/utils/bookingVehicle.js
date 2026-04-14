import {
  extractField,
  formatStoredVehicleLabel,
  formatVehicleDescriptor,
} from "@features/appointments/utils/bookingCommon";
import {
  formatVietnameseLicensePlateInput,
  isValidVietnameseLicensePlate,
  normalizeVietnameseLicensePlate,
} from "@shared/utils/licensePlateValidation";
import { formatVinInput, isValidVin } from "@shared/utils/vinValidation";

export function buildCurrentVehiclePayload({
  linkedVehicles,
  vehicleMode,
  selectedExistingVehicle,
  newVehicle,
  selectedBrand,
  selectedModel,
}) {
  const hasVehicle = (linkedVehicles || []).length > 0;
  const usingNewVehicle = !hasVehicle || vehicleMode === "new";

  if (!usingNewVehicle) {
    if (!selectedExistingVehicle?.id) {
      return { error: "Vui lòng chọn xe có sẵn." };
    }

    const descriptor = formatStoredVehicleLabel(selectedExistingVehicle);
    return {
      payload: {
        vehicleId: Number(selectedExistingVehicle.id),
        vehicleModelId: Number(selectedExistingVehicle.modelId) || null,
        licensePlate: selectedExistingVehicle.licensePlate || null,
        vin: selectedExistingVehicle.vin || null,
        color: selectedExistingVehicle.color || null,
        manufactureYear: Number(selectedExistingVehicle.manufactureYear) || null,
      },
      dedupeKey: `existing-${Number(selectedExistingVehicle.id)}`,
      displayLabel: descriptor,
    };
  }

  if (!newVehicle?.brandId) return { error: "Vui lòng chọn hãng xe." };
  if (!newVehicle?.modelId) return { error: "Vui lòng chọn dòng xe." };
  if (!String(newVehicle?.licensePlate || "").trim()) return { error: "Vui lòng nhập biển số xe." };
  if (!isValidVietnameseLicensePlate(newVehicle?.licensePlate)) {
    return { error: "Biển số xe không đúng định dạng Việt Nam." };
  }
  if (!String(newVehicle?.manufactureYear || "").trim()) {
    return { error: "Vui lòng chọn năm sản xuất." };
  }
  if (String(newVehicle?.vin || "").trim() && !isValidVin(newVehicle?.vin)) {
    return { error: "Số VIN không hợp lệ (17 ký tự, không chứa I, O, Q)." };
  }

  const plate = formatVietnameseLicensePlateInput(newVehicle.licensePlate);
  const normalizedPlate = normalizeVietnameseLicensePlate(plate);
  const vin = String(newVehicle?.vin || "").trim() ? formatVinInput(newVehicle.vin) : null;
  const manufactureYear = Number(newVehicle?.manufactureYear);
  const descriptor = formatVehicleDescriptor(
    extractField(selectedBrand, ["name", "Name", "brandName", "BrandName", "displayName", "DisplayName"], ""),
    extractField(selectedModel, ["modelName", "ModelName", "name", "Name", "model", "Model", "displayName", "DisplayName"], "")
  );
  const displayLabel = descriptor ? `${plate} - ${descriptor}` : plate;

  return {
    payload: {
      vehicleId: null,
      vehicleModelId: Number(newVehicle.modelId),
      licensePlate: plate,
      vin,
      color: String(newVehicle?.color || "").trim() || null,
      manufactureYear: Number.isFinite(manufactureYear) && manufactureYear > 0 ? manufactureYear : null,
    },
    dedupeKey: `new-${normalizedPlate}-${Number(newVehicle.modelId)}`,
    displayLabel,
  };
}
