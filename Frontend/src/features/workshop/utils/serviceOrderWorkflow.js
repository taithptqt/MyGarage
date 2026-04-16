import { formatLicensePlate } from "@features/workshop/utils/serviceOrderDetailUtils";
import { splitBookingItemIds } from "@features/appointments/utils/bookingPackages";
import { buildInsuranceSnapshotPayload } from "@features/appointments/utils/bookingInsurance";

const VEHICLE_KEY_GUEST = "guest";
const VEHICLE_KEY_NEW = "new-customer";
const SLOT_DURATION_MINUTES = 15;
const CHECK_IN_SETUP_MINUTES = 15;
const EXECUTION_BUFFER_MINUTES = 15;

function toNumberList(value) {
  if (!Array.isArray(value)) return [];
  return Array.from(
    new Set(
      value
        .map(Number)
        .filter((id) => Number.isFinite(id) && id !== 0)
    )
  );
}

function parsePositiveId(value) {
  const parsed = Number(value);
  return Number.isFinite(parsed) && parsed > 0 ? parsed : null;
}

function toLocalDateTimeValue(value) {
  if (!value) return "";
  const parsed = new Date(value);
  if (Number.isNaN(parsed.getTime())) return "";

  const year = parsed.getFullYear();
  const month = String(parsed.getMonth() + 1).padStart(2, "0");
  const day = String(parsed.getDate()).padStart(2, "0");
  const hour = String(parsed.getHours()).padStart(2, "0");
  const minute = String(parsed.getMinutes()).padStart(2, "0");

  return `${year}-${month}-${day}T${hour}:${minute}:00`;
}

function buildCustomerNotes(value) {
  if (Array.isArray(value)) {
    const merged = value.map((line) => String(line || "").trim()).filter(Boolean).join("; ");
    return merged || null;
  }
  const text = String(value || "").trim();
  return text || null;
}

function getVehicleIdFromKey(vehicleKey) {
  const match = String(vehicleKey || "").match(/^existing-(\d+)$/);
  return match ? Number(match[1]) : null;
}

export function calculateRoundedOccupancyMinutes(serviceMinutes) {
  const normalizedServiceMinutes = Number(serviceMinutes || 0);
  const safeServiceMinutes = Number.isFinite(normalizedServiceMinutes) && normalizedServiceMinutes > 0
    ? normalizedServiceMinutes
    : 0;
  const occupancyMinutes = CHECK_IN_SETUP_MINUTES + safeServiceMinutes + EXECUTION_BUFFER_MINUTES;
  const slotCount = Math.max(1, Math.ceil(occupancyMinutes / SLOT_DURATION_MINUTES));
  return slotCount * SLOT_DURATION_MINUTES;
}

export function buildWorkshopBatchCreatePayload({
  formData,
  suggestedFleetCompletionIso,
  effectiveBranchId,
  insuranceActive = false,
  insuranceByVehicleKey = {},
  hasMultipleSelectedVehicles,
  selectedVehicleKeys,
  plansByVehicleKey,
  vehicleNotesByKey,
}) {
  const shouldCreateGroupSession =
    Boolean(hasMultipleSelectedVehicles) &&
    !Boolean(formData?.isQuickOrder) &&
    !Boolean(formData?.isNewVehicle);
  const basePayload = {
    customerId: parsePositiveId(formData?.customerId),
    serviceAdvisorId: parsePositiveId(formData?.serviceAdvisorId),
    orderCategory: formData?.orderCategory || null,
    groupSessionId: null,
    oldPartsReturnOption: formData?.oldPartsReturnOption || "DISPOSE_ALL",
    requiredDepositAmount: Number(formData?.requiredDepositAmount || 0),
    checkInTime: formData?.checkInTime || toLocalDateTimeValue(new Date()),
    estimatedHandover: formData?.estimatedHandover || suggestedFleetCompletionIso || null,
    promisedHandoverTime: formData?.promisedHandoverTime || null,
    status: "NEW",
    executeBranchId: parsePositiveId(effectiveBranchId),
    originBranchId: parsePositiveId(effectiveBranchId),
    guestName: formData?.guestName || "",
    guestPhone: formData?.guestPhone || "",
    guestVehiclePlate: formatLicensePlate(formData?.guestVehiclePlate || ""),
    guestVehicleModelId: parsePositiveId(formData?.guestVehicleModelId),
    guestVehicleColor: formData?.guestVehicleColor || "",
    guestVehicleVin: formData?.guestVehicleVin || "",
    guestVehicleYear: parsePositiveId(formData?.guestVehicleYear),
    vehicleLicensePlate: formatLicensePlate(formData?.vehicleLicensePlate || ""),
    vehicleModelId: parsePositiveId(formData?.vehicleModelId),
    vehicleColor: formData?.vehicleColor || "",
    vehicleVin: formData?.vehicleVin || "",
    vehicleYear: parsePositiveId(formData?.vehicleYear),
  };

  const createTargets = (Array.isArray(selectedVehicleKeys) ? selectedVehicleKeys : []).map((vehicleKey) => {
    const plan = plansByVehicleKey instanceof Map
      ? (plansByVehicleKey.get(vehicleKey) || {})
      : (plansByVehicleKey?.[vehicleKey] || {});

    const split = splitBookingItemIds(toNumberList(plan.serviceIds));
    const insuranceSnapshot = insuranceActive
      ? buildInsuranceSnapshotPayload(insuranceByVehicleKey?.[vehicleKey] || {})
      : buildInsuranceSnapshotPayload();

    const payload = {
      ...basePayload,
      serviceIds: split.serviceIds,
      packageIds: split.packageIds,
      customerNotes: buildCustomerNotes(vehicleNotesByKey?.[vehicleKey]),
      ...insuranceSnapshot,
    };

    if (vehicleKey === VEHICLE_KEY_GUEST) {
      payload.customerId = null;
      payload.vehicleId = null;
      payload.isQuickOrder = true;
      payload.isNewVehicle = false;
      payload.vehicleLicensePlate = "";
      payload.vehicleModelId = null;
      payload.vehicleColor = "";
      payload.vehicleVin = "";
      payload.vehicleYear = null;
      return payload;
    }

    if (vehicleKey === VEHICLE_KEY_NEW) {
      payload.vehicleId = null;
      payload.isQuickOrder = false;
      payload.isNewVehicle = true;
      payload.guestVehiclePlate = "";
      payload.guestVehicleModelId = null;
      payload.guestVehicleColor = "";
      payload.guestVehicleVin = "";
      payload.guestVehicleYear = null;
      return payload;
    }

    payload.vehicleId = getVehicleIdFromKey(vehicleKey);
    payload.isQuickOrder = false;
    payload.isNewVehicle = false;
    payload.guestVehiclePlate = "";
    payload.guestVehicleModelId = null;
    payload.guestVehicleColor = "";
    payload.guestVehicleVin = "";
    payload.guestVehicleYear = null;
    payload.vehicleLicensePlate = "";
    payload.vehicleModelId = null;
    payload.vehicleColor = "";
    payload.vehicleVin = "";
    payload.vehicleYear = null;
    return payload;
  });

  return {
    createGroupSession: shouldCreateGroupSession,
    customerId: shouldCreateGroupSession ? Number(formData?.customerId || 0) : null,
    branchId: shouldCreateGroupSession ? Number(effectiveBranchId || 0) : null,
    customerNote: null,
    items: createTargets,
  };
}

export function buildWorkshopEditPayload({
  formData,
  suggestedFinishIso,
  insuranceActive = false,
  customerNote,
}) {
  const normalizedCustomerNote = String(customerNote || "").trim();
  const insuranceSnapshot = insuranceActive ? buildInsuranceSnapshotPayload(formData) : buildInsuranceSnapshotPayload();

  return {
    orderCategory: formData?.orderCategory || null,
    serviceAdvisorId: parsePositiveId(formData?.serviceAdvisorId),
    oldPartsReturnOption: formData?.oldPartsReturnOption || "DISPOSE_ALL",
    requiredDepositAmount: Math.max(0, Number(formData?.requiredDepositAmount || 0)),
    checkInTime: formData?.checkInTime || null,
    estimatedHandover: formData?.estimatedHandover || suggestedFinishIso || null,
    promisedHandoverTime: formData?.promisedHandoverTime || null,
    status: formData?.status || "NEW",
    serviceIds: splitBookingItemIds(toNumberList(formData?.serviceIds)).serviceIds,
    packageIds: splitBookingItemIds(toNumberList(formData?.serviceIds)).packageIds,
    ...insuranceSnapshot,
    customerNotes: normalizedCustomerNote || null,
  };
}
