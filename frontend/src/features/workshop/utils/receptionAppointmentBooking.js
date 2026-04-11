import { saveReceptionBooking } from "@features/appointments/api/appointmentApi";
import { BOOKING_FLOW } from "@features/appointments/constants/bookingConstants";
import { buildCustomerNotesPayload } from "@features/appointments/utils/bookingCommon";
import { buildInsuranceSnapshotPayload } from "@features/appointments/utils/bookingInsurance";
import { splitBookingItemIds } from "@features/appointments/utils/bookingPackages";
import { sumServiceMinutes } from "@shared/utils/serviceDurationUtils";

const VEHICLE_KEY_GUEST = "guest";
const VEHICLE_KEY_NEW = "new-customer";

const ERRORS = {
  invalidBranch: "Chi nhánh hiện tại không hợp lệ.",
  invalidDateTime: "Ngày giờ hẹn không hợp lệ.",
  missingGuestModel: "Vui lòng chọn dòng xe cho khách vãng lai.",
  invalidCustomer: "Khách hàng không hợp lệ.",
  missingNewVehicleModel: "Vui lòng chọn dòng xe mới.",
  missingVehicle: "Vui lòng chọn xe của khách.",
  vehicleNotFound: "Không tìm thấy xe đã chọn.",
};

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

function readText(value) {
  const text = String(value || "").trim();
  return text || null;
}

function normalizeBookingFlow(value, fallback = BOOKING_FLOW.FIXED) {
  const raw = String(value || "").trim().toLowerCase();
  if (raw.includes("flex")) return BOOKING_FLOW.FLEXIBLE;
  if (raw.includes("fixed")) return BOOKING_FLOW.FIXED;
  return fallback;
}

function isFlexibleTriggerServiceCode(rawCode) {
  const normalizedCode = String(rawCode || "")
    .trim()
    .toLowerCase()
    .replace(/[_\-\s]/g, "");
  return normalizedCode.startsWith("dg");
}

function normalizeServiceLookup(services) {
  const map = new Map();
  (services || []).forEach((service) => {
    const id = Number(service?.id ?? service?.Id);
    if (!Number.isFinite(id) || id === 0) return;
    map.set(id, {
      ...service,
      id,
      code: String(service?.code ?? service?.Code ?? ""),
      minutes: Math.max(0, Number(service?.standardMinutes ?? service?.StandardMinutes ?? 0) || 0),
    });
  });
  return map;
}

function buildFlexibleTriggerServiceIdSet(serviceLookup) {
  const set = new Set();
  serviceLookup.forEach((service, id) => {
    if (isFlexibleTriggerServiceCode(service?.code)) {
      set.add(Number(id));
    }
  });
  return set;
}

function resolveVehicleBookingFlow(planFlow, serviceIds, flexibleTriggerServiceIdSet) {
  const explicitFlow = normalizeBookingFlow(planFlow, "");
  if (explicitFlow === BOOKING_FLOW.FLEXIBLE || explicitFlow === BOOKING_FLOW.FIXED) {
    return explicitFlow;
  }
  return (serviceIds || []).some((serviceId) => flexibleTriggerServiceIdSet.has(Number(serviceId)))
    ? BOOKING_FLOW.FLEXIBLE
    : BOOKING_FLOW.FIXED;
}

function normalizeVehicleServicePlanMaps(vehicleServicePlans, serviceLookup) {
  const byVehicleKey = new Map();
  const byVehicleId = new Map();

  (Array.isArray(vehicleServicePlans) ? vehicleServicePlans : []).forEach((rawPlan) => {
    const vehicleKey = String(rawPlan?.vehicleKey || "").trim() || null;
    const vehicleId = Number(rawPlan?.vehicleId);
    const allServiceIds = toNumberList(rawPlan?.serviceIds);
    const split = splitBookingItemIds(allServiceIds);
    const serviceIds = split.serviceIds;
    const packageIds = split.packageIds;
    const selectedServices = serviceIds
      .map((serviceId) => serviceLookup.get(serviceId))
      .filter(Boolean);
    const selectedPackages = packageIds
      .map((packageId) => serviceLookup.get(-packageId))
      .filter(Boolean);
    const computedMinutes = sumServiceMinutes(selectedServices);
    const packageMinutes = sumServiceMinutes(selectedPackages);
    const totalMinutes = Number(rawPlan?.totalMinutes);
    const resolvedMinutes = Number.isFinite(totalMinutes) && totalMinutes > 0 ? totalMinutes : computedMinutes + packageMinutes;

    const plan = {
      serviceIds: allServiceIds,
      requestedServiceIds: serviceIds.length > 0 ? serviceIds.join(",") : null,
      requestedPackageIds: packageIds.length > 0 ? packageIds.join(",") : null,
      totalMinutes: Math.max(resolvedMinutes, 30),
      bookingFlow: normalizeBookingFlow(rawPlan?.bookingFlow, ""),
      customerNotes: readText(rawPlan?.customerNotes),
      ...buildInsuranceSnapshotPayload(rawPlan),
    };

    if (vehicleKey) byVehicleKey.set(vehicleKey, plan);
    if (Number.isFinite(vehicleId) && vehicleId > 0) byVehicleId.set(vehicleId, plan);
  });

  return { byVehicleKey, byVehicleId };
}

function resolveVehiclePlan({
  vehicleKey,
  vehicleId,
  defaultServiceIds,
  defaultDuration,
  planMaps,
  flexibleTriggerServiceIdSet,
  slotBookingFlow,
}) {
  const byKeyPlan = vehicleKey ? planMaps.byVehicleKey.get(vehicleKey) : null;
  const byIdPlan = Number.isFinite(vehicleId) && vehicleId > 0 ? planMaps.byVehicleId.get(vehicleId) : null;
  const plan = byKeyPlan || byIdPlan;

  const serviceIds = plan?.serviceIds?.length > 0 ? plan.serviceIds : defaultServiceIds;
  const split = splitBookingItemIds(serviceIds);
  const requestedServiceIds = split.serviceIds.length > 0 ? split.serviceIds.join(",") : null;
  const requestedPackageIds = split.packageIds.length > 0 ? split.packageIds.join(",") : null;
  const totalMinutes = Math.max(Number(plan?.totalMinutes || defaultDuration || 30), 30);
  const bookingFlow = resolveVehicleBookingFlow(
    plan?.bookingFlow || slotBookingFlow,
    serviceIds,
    flexibleTriggerServiceIdSet
  );

  return {
    serviceIds,
    requestedServiceIds,
    requestedPackageIds,
    totalMinutes,
    bookingFlow,
    customerNotes: readText(plan?.customerNotes),
    insuranceCompanyId: plan?.insuranceCompanyId || null,
    insuranceDeductibleAmount: plan?.insuranceDeductibleAmount ?? null,
    insuranceDeductibleOption: readText(plan?.insuranceDeductibleOption),
    insuranceClaimMode: readText(plan?.insuranceClaimMode),
    insuranceQuoteState: readText(plan?.insuranceQuoteState),
    insuranceCardImageUrls: Array.isArray(plan?.insuranceCardImageUrls) ? plan.insuranceCardImageUrls : [],
    insuranceDamageImageUrls: Array.isArray(plan?.insuranceDamageImageUrls) ? plan.insuranceDamageImageUrls : [],
  };
}

function parseSlotSelectionByVehicle(slotSelections) {
  const byVehicleKey = new Map();
  const byVehicleId = new Map();

  (Array.isArray(slotSelections) ? slotSelections : []).forEach((item) => {
    const vehicleKey = String(item?.vehicleKey || "").trim();
    const vehicleId = Number(item?.vehicleId);
    const normalized = {
      startTime: readText(item?.startTime),
      endTime: readText(item?.endTime),
      bookingFlow: normalizeBookingFlow(item?.bookingFlow, ""),
      checkInWindowStart: readText(item?.checkInWindowStart),
      checkInWindowEnd: readText(item?.checkInWindowEnd),
      slotHoldToken: readText(item?.slotHoldToken),
    };
    if (vehicleKey) byVehicleKey.set(vehicleKey, normalized);
    if (Number.isFinite(vehicleId) && vehicleId > 0) byVehicleId.set(vehicleId, normalized);
  });

  return { byVehicleKey, byVehicleId };
}

function resolveSlotSelection(slotMaps, vehicleKey, vehicleId) {
  return slotMaps.byVehicleKey.get(vehicleKey) || slotMaps.byVehicleId.get(Number(vehicleId)) || null;
}

function buildVehicleNotesMap(vehicleNotePlans) {
  const map = new Map();
  (Array.isArray(vehicleNotePlans) ? vehicleNotePlans : []).forEach((item) => {
    const vehicleKey = String(item?.vehicleKey || "").trim();
    if (!vehicleKey) return;
    map.set(vehicleKey, readText(item?.customerNotes));
  });
  return map;
}

function getVehicleIdFromKey(vehicleKey) {
  const match = String(vehicleKey || "").match(/^existing-(\d+)$/);
  return match ? Number(match[1]) : 0;
}

function getSelectedVehicleKeys(formData) {
  if (formData.isQuickOrder) return [VEHICLE_KEY_GUEST];
  if (formData.isNewVehicle) return [VEHICLE_KEY_NEW];

  const selectedVehicleIds = toNumberList(formData.selectedVehicleIds);
  return selectedVehicleIds.map((vehicleId) => `existing-${vehicleId}`);
}

function splitCustomerNotesInput(value) {
  return String(value || "")
    .split(/\r?\n|;/g)
    .map((item) => String(item || "").trim())
    .filter(Boolean);
}

function resolveFallbackStartIso(formData) {
  const appointmentDate = String(formData.appointmentDate || "").trim();
  const startTime = String(formData.startTime || "").trim();
  if (!appointmentDate || !startTime) return null;

  const startAt = new Date(`${appointmentDate}T${startTime}`);
  return Number.isNaN(startAt.getTime()) ? null : startAt.toISOString();
}

function resolveDurationMinutes(slotSelection, fallbackDuration) {
  const slotStart = slotSelection?.startTime ? new Date(slotSelection.startTime) : null;
  const slotEnd = slotSelection?.endTime ? new Date(slotSelection.endTime) : null;
  if (slotStart && slotEnd && !Number.isNaN(slotStart.getTime()) && !Number.isNaN(slotEnd.getTime())) {
    const diff = Math.ceil((slotEnd.getTime() - slotStart.getTime()) / 60000);
    if (diff > 0) return Math.max(diff, 30);
  }

  return Math.max(Number(fallbackDuration || 30), 30);
}

function normalizeRequestedServiceIds(raw) {
  const ids = String(raw || "")
    .split(/[,|]/g)
    .map((item) => Number(String(item || "").trim()))
    .filter((id) => Number.isFinite(id) && id > 0);
  return ids.length > 0 ? Array.from(new Set(ids)).join(",") : null;
}

export async function saveReceptionAppointment({
  effectiveBranchId,
  formData,
  customers,
  vehicles,
  customerVehicles,
  services,
  replaceAppointmentId = 0
}) {
  if (!effectiveBranchId) throw new Error(ERRORS.invalidBranch);

  const serviceLookup = normalizeServiceLookup(services);
  const flexibleTriggerServiceIdSet = buildFlexibleTriggerServiceIdSet(serviceLookup);
  const fallbackServiceIds = toNumberList(formData.serviceIds);
  const fallbackDuration = Math.max(Number(formData.totalMinutes || 0), 30);
  const splitCustomerNotes = splitCustomerNotesInput(formData.customerNotes);
  const normalizedCustomerNotes =
    buildCustomerNotesPayload(splitCustomerNotes) || readText(formData.customerNotes) || null;
  const planMaps = normalizeVehicleServicePlanMaps(formData.vehicleServicePlans, serviceLookup);
  const selectedVehicleKeys = getSelectedVehicleKeys(formData);

  if (!formData.isQuickOrder && !formData.isNewVehicle && selectedVehicleKeys.length === 0) {
    throw new Error(ERRORS.missingVehicle);
  }

  const slotMaps = parseSlotSelectionByVehicle(formData.vehicleSlotSelections);
  const fallbackStartIso = resolveFallbackStartIso(formData);
  const vehicleNoteMap = buildVehicleNotesMap(formData.vehicleNotePlans);

  const vehiclePlans = selectedVehicleKeys.map((vehicleKey) => {
    const vehicleId = getVehicleIdFromKey(vehicleKey);
    const slotSelection = resolveSlotSelection(slotMaps, vehicleKey, vehicleId);
    const startTime = readText(slotSelection?.startTime || fallbackStartIso);
    if (!startTime) throw new Error(ERRORS.invalidDateTime);

    const plan = resolveVehiclePlan({
      vehicleKey,
      vehicleId,
      defaultServiceIds: fallbackServiceIds,
      defaultDuration: fallbackDuration,
      planMaps,
      flexibleTriggerServiceIdSet,
      slotBookingFlow: slotSelection?.bookingFlow,
    });

    const totalMinutes = resolveDurationMinutes(slotSelection, plan.totalMinutes);
    const requestedServiceIds = normalizeRequestedServiceIds(plan.requestedServiceIds);
    const requestedPackageIds = normalizeRequestedServiceIds(plan.requestedPackageIds);
    const vehicleNotes = vehicleNoteMap.get(vehicleKey);
    const customerNotes = vehicleNotes || plan.customerNotes || normalizedCustomerNotes;
    const bookingFlow = normalizeBookingFlow(plan.bookingFlow, BOOKING_FLOW.FIXED);
    const checkInWindowStart = bookingFlow === BOOKING_FLOW.FLEXIBLE
      ? readText(slotSelection?.checkInWindowStart || slotSelection?.startTime || startTime)
      : null;
    const checkInWindowEnd = bookingFlow === BOOKING_FLOW.FLEXIBLE
      ? readText(slotSelection?.checkInWindowEnd || slotSelection?.endTime || startTime)
      : null;

    return {
      vehicleKey,
      vehicleId: vehicleId > 0 ? vehicleId : null,
      bookingFlow,
      startTime,
      endTime: readText(slotSelection?.endTime),
      checkInWindowStart,
      checkInWindowEnd,
      totalMinutes,
      requestedServiceIds,
      requestedPackageIds,
      customerNotes,
      insuranceCompanyId: plan.insuranceCompanyId,
      insuranceDeductibleAmount: plan.insuranceDeductibleAmount,
      insuranceDeductibleOption: plan.insuranceDeductibleOption,
      insuranceClaimMode: plan.insuranceClaimMode,
      insuranceQuoteState: plan.insuranceQuoteState,
      insuranceCardImageUrls: Array.isArray(plan.insuranceCardImageUrls) ? plan.insuranceCardImageUrls : [],
      insuranceDamageImageUrls: Array.isArray(plan.insuranceDamageImageUrls) ? plan.insuranceDamageImageUrls : [],
      slotHoldToken: readText(slotSelection?.slotHoldToken),
      licensePlate:
        vehicleKey === VEHICLE_KEY_GUEST
          ? readText(formData.guestVehiclePlate)
          : vehicleKey === VEHICLE_KEY_NEW
            ? readText(formData.vehicleLicensePlate)
            : null,
      vehicleModelId:
        vehicleKey === VEHICLE_KEY_GUEST
          ? Number(formData.guestVehicleModelId || 0) || null
          : vehicleKey === VEHICLE_KEY_NEW
            ? Number(formData.vehicleModelId || 0) || null
            : null,
      vin:
        vehicleKey === VEHICLE_KEY_GUEST
          ? readText(formData.guestVehicleVin)
          : vehicleKey === VEHICLE_KEY_NEW
            ? readText(formData.vehicleVin)
            : null,
      color:
        vehicleKey === VEHICLE_KEY_GUEST
          ? readText(formData.guestVehicleColor)
          : vehicleKey === VEHICLE_KEY_NEW
            ? readText(formData.vehicleColor)
            : null,
      insuranceCompanyId: plan?.insuranceCompanyId || null,
      insuranceDeductibleAmount: plan?.insuranceDeductibleAmount ?? null,
      insuranceDeductibleOption: plan?.insuranceDeductibleOption || null,
      insuranceClaimMode: plan?.insuranceClaimMode || null,
      insuranceQuoteState: plan?.insuranceQuoteState || null,
    };
  });

  if (formData.isQuickOrder && !Number(formData.guestVehicleModelId || 0)) {
    throw new Error(ERRORS.missingGuestModel);
  }

  if (!formData.isQuickOrder) {
    const customerId = Number(formData.customerId || 0);
    if (!customerId) throw new Error(ERRORS.invalidCustomer);
  }

  if (formData.isNewVehicle && !Number(formData.vehicleModelId || 0)) {
    throw new Error(ERRORS.missingNewVehicleModel);
  }

  const selectedServiceIdsUnion = toNumberList([
    ...fallbackServiceIds,
    ...(Array.isArray(formData.vehicleServicePlans)
      ? formData.vehicleServicePlans.flatMap((plan) => toNumberList(plan?.serviceIds))
      : []),
  ]);
  const selectedServiceObjects = selectedServiceIdsUnion.map((serviceId) => serviceLookup.get(serviceId)).filter(Boolean);

  const response = await saveReceptionBooking({
    branchId: Number(effectiveBranchId),
    isQuickOrder: Boolean(formData.isQuickOrder),
    isNewVehicle: Boolean(formData.isNewVehicle),
    customerId: formData.customerId ? Number(formData.customerId) : null,
    customerName: readText(formData.customerName),
    customerPhone: readText(formData.customerPhone),
    guestName: readText(formData.guestName),
    guestPhone: readText(formData.guestPhone),
    guestVehiclePlate: readText(formData.guestVehiclePlate),
    guestVehicleModelId: Number(formData.guestVehicleModelId || 0) || null,
    guestVehicleColor: readText(formData.guestVehicleColor),
    guestVehicleVin: readText(formData.guestVehicleVin),
    guestVehicleYear: Number(formData.guestVehicleYear || 0) || null,
    vehicleLicensePlate: readText(formData.vehicleLicensePlate),
    vehicleModelId: Number(formData.vehicleModelId || 0) || null,
    vehicleColor: readText(formData.vehicleColor),
    vehicleVin: readText(formData.vehicleVin),
    vehicleYear: Number(formData.vehicleYear || 0) || null,
    ...buildInsuranceSnapshotPayload(formData),
    customerNotes: normalizedCustomerNotes,
    replaceAppointmentId: Number(replaceAppointmentId) > 0 ? Number(replaceAppointmentId) : null,
    vehiclePlans,
  });

  const primaryStart = response?.startTime || response?.StartTime || vehiclePlans[0]?.startTime || fallbackStartIso;
  if (!primaryStart) throw new Error(ERRORS.invalidDateTime);

  return {
    customerName:
      response?.customerName ||
      response?.CustomerName ||
      formData.customerName ||
      formData.guestName ||
      "",
    customerPhone:
      response?.customerPhone ||
      response?.CustomerPhone ||
      formData.customerPhone ||
      formData.guestPhone ||
      "",
    vehicleLicensePlate:
      response?.vehicleLicensePlate ||
      response?.VehicleLicensePlate ||
      formData.vehicleLicensePlate ||
      formData.guestVehiclePlate ||
      "",
    startTime: primaryStart,
    services: selectedServiceObjects,
    groupVehicleCount: Math.max(1, vehiclePlans.length),
    updateMode: Boolean(response?.updateMode ?? response?.UpdateMode ?? Number(replaceAppointmentId) > 0),
  };
}
