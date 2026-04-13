export function toPackageSelectableId(packageId) {
  const parsed = Number(packageId);
  return Number.isFinite(parsed) && parsed > 0 ? -parsed : 0;
}

export function isPackageSelectableId(value) {
  const parsed = Number(value);
  return Number.isFinite(parsed) && parsed < 0;
}

export function fromPackageSelectableId(value) {
  const parsed = Number(value);
  return Number.isFinite(parsed) && parsed < 0 ? Math.abs(parsed) : 0;
}

export function isLegacyPackageStandardService(service) {
  const code = String(service?.code ?? service?.Code ?? "").trim().toUpperCase();
  const hiddenInsuranceAdminCodes = new Set([
    "BH-MT500",
    "BH-MT1000",
    "BH-KHAUHAO",
    "BH-HOSO",
    "BH-LUUBAI",
  ]);

  return code.startsWith("PKG-") || hiddenInsuranceAdminCodes.has(code);
}

export function filterBookableStandardServices(services) {
  return (Array.isArray(services) ? services : []).filter((service) => !isLegacyPackageStandardService(service));
}

export function normalizePackageForBooking(packageItem) {
  const packageId = Number(packageItem?.id ?? packageItem?.Id ?? 0);
  const selectableId = toPackageSelectableId(packageId);
  if (!selectableId) return null;

  return {
    ...packageItem,
    id: selectableId,
    packageId,
    isPackage: true,
    code: packageItem?.code ?? packageItem?.Code ?? "",
    name: packageItem?.name ?? packageItem?.Name ?? "",
    categoryId: packageItem?.categoryId ?? packageItem?.CategoryId ?? 0,
    basePrice: packageItem?.basePrice ?? packageItem?.BasePrice ?? 0,
    standardMinutes: packageItem?.standardMinutes ?? packageItem?.StandardMinutes ?? 0,
    description: packageItem?.description ?? packageItem?.Description ?? "",
    imageUrl: packageItem?.imageUrl ?? packageItem?.ImageUrl ?? "",
  };
}

export function mergeBookableServicesAndPackages(services, packages) {
  const standardServices = filterBookableStandardServices(services);
  const packageServices = (Array.isArray(packages) ? packages : [])
    .map(normalizePackageForBooking)
    .filter(Boolean);

  return [...standardServices, ...packageServices];
}

function normalizeIds(ids, predicate, mapper = Number) {
  return Array.from(
    new Set(
      (Array.isArray(ids) ? ids : [])
        .map(Number)
        .filter((id) => Number.isFinite(id) && predicate(id))
        .map(mapper)
        .filter((id) => Number.isFinite(id) && id > 0)
    )
  );
}

export function splitBookingItemIds(ids) {
  return {
    serviceIds: normalizeIds(ids, (id) => id > 0),
    packageIds: normalizeIds(ids, (id) => id < 0, Math.abs),
  };
}

export function buildRequestedIdsByMode({
  serviceGroups,
  isParallel,
  activeIndex = 0,
  type = "service",
}) {
  const normalizedGroups = (Array.isArray(serviceGroups) ? serviceGroups : []).map((group) => {
    const split = splitBookingItemIds(group);
    return type === "package" ? split.packageIds : split.serviceIds;
  });

  if (normalizedGroups.length === 0) return "";
  if (isParallel) {
    return normalizedGroups.map((group) => group.join(",")).join("|");
  }

  const targetGroup = normalizedGroups[Math.max(0, Number(activeIndex) || 0)] || [];
  return targetGroup.join(",");
}
