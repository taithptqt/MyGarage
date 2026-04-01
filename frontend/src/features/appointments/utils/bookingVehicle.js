import {
  extractField,
  formatStoredVehicleLabel,
  formatVehicleDescriptor,
  toSafeUpper,
} from "@features/appointments/utils/bookingCommon";

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
      },
      dedupeKey: `existing-${Number(selectedExistingVehicle.id)}`,
      displayLabel: descriptor,
    };
  }

  if (!newVehicle?.brandId) return { error: "Vui lòng chọn hãng xe." };
  if (!newVehicle?.modelId) return { error: "Vui lòng chọn dòng xe." };
  if (!String(newVehicle?.licensePlate || "").trim()) return { error: "Vui lòng nhập biển số xe." };
  if (!String(newVehicle?.vin || "").trim()) return { error: "Vui lòng nhập số VIN." };

  const plate = toSafeUpper(newVehicle.licensePlate);
  const vin = toSafeUpper(newVehicle.vin);
  const descriptor = formatVehicleDescriptor(
    extractField(selectedBrand, ["name", "Name"], ""),
    extractField(selectedModel, ["modelName", "ModelName"], "")
  );
  const displayLabel = descriptor ? `${plate} - ${descriptor}` : plate;

  return {
    payload: {
      vehicleId: null,
      vehicleModelId: Number(newVehicle.modelId),
      licensePlate: plate,
      vin,
      color: String(newVehicle?.color || "").trim() || null,
    },
    dedupeKey: `new-${plate}-${vin}`,
    displayLabel,
  };
}
