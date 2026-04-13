import { buildRequestedIdsByMode } from "@features/appointments/utils/bookingPackages";

export function buildRequestedServiceIdsByMode({
  serviceGroups,
  isParallel,
  activeIndex = 0,
}) {
  return buildRequestedIdsByMode({
    serviceGroups,
    isParallel,
    activeIndex,
    type: "service",
  });
}

export function buildRequestedPackageIdsByMode({
  serviceGroups,
  isParallel,
  activeIndex = 0,
}) {
  return buildRequestedIdsByMode({
    serviceGroups,
    isParallel,
    activeIndex,
    type: "package",
  });
}

export function flattenRequestedIds(requestedIds) {
  return String(requestedIds || "")
    .split("|")
    .flatMap((group) => String(group || "").split(","))
    .map((item) => String(item || "").trim())
    .filter(Boolean)
    .join(",");
}

export function flattenRequestedServiceIds(requestedServiceIds) {
  return flattenRequestedIds(requestedServiceIds);
}

export async function loadAvailabilityWithFallback({
  loadBookingAvailableSlots,
  branchId,
  date,
  serviceIds = [],
  requestedServiceIds = "",
  requestedPackageIds = "",
  bookingFlow,
  vehicleCount = 1,
  isParallel = false,
}) {
  let data = await loadBookingAvailableSlots({
    branchId,
    date,
    serviceIds,
    requestedServiceIds,
    requestedPackageIds,
    bookingFlow,
    vehicleCount,
    isParallel,
  });

  const hasGroupedServiceIds = typeof requestedServiceIds === "string" && requestedServiceIds.includes("|");
  const hasGroupedPackageIds = typeof requestedPackageIds === "string" && requestedPackageIds.includes("|");
  if (Array.isArray(data) && data.length === 0 && (hasGroupedServiceIds || hasGroupedPackageIds)) {
    data = await loadBookingAvailableSlots({
      branchId,
      date,
      serviceIds,
      requestedServiceIds: flattenRequestedIds(requestedServiceIds),
      requestedPackageIds: flattenRequestedIds(requestedPackageIds),
      bookingFlow,
      vehicleCount,
      isParallel,
    });
  }

  return Array.isArray(data) ? data : [];
}
