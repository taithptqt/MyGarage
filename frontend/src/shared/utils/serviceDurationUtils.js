function readField(source, keys, fallback = "") {
  for (const key of keys) {
    if (source?.[key] !== undefined && source?.[key] !== null) {
      return source[key];
    }
  }
  return fallback;
}

function normalizeText(value) {
  return String(value || "")
    .trim()
    .toLowerCase()
    .normalize("NFD")
    .replace(/[\u0300-\u036f]/g, "")
    .replace(/\u0111/g, "d");
}

function normalizeCode(value) {
  return String(value || "")
    .trim()
    .toUpperCase()
    .replace(/[^A-Z0-9]/g, "");
}

function normalizeLooseToken(value) {
  return normalizeText(value)
    .replace(/[_\-]/g, " ")
    .replace(/\s+/g, " ")
    .trim();
}

function readServiceIdentity(service) {
  const id = Number(readField(service, ["id", "Id"], 0));
  const code = readField(service, ["code", "Code", "packageCode", "PackageCode"], "");
  const name = readField(service, ["name", "Name", "packageName", "PackageName"], "");
  return { id, code, name };
}

export function isMiniCheckService(service) {
  const normalizedCode = normalizeCode(
    readField(service, ["code", "Code", "packageCode", "PackageCode"], "")
  );
  if (
    normalizedCode === "MINI10" ||
    normalizedCode === "MINICHECK" ||
    normalizedCode === "MINI" ||
    normalizedCode.startsWith("MNC")
  ) {
    return true;
  }

  const normalizedName = normalizeText(
    readField(service, ["name", "Name", "packageName", "PackageName"], "")
  );

  return (
    normalizedName.includes("mini check") ||
    normalizedName.includes("mini-check") ||
    normalizedName.includes("minicheck")
  );
}

export const INSPECTION_TRIAD_TYPE = {
  QUICK: "quick",
  GENERAL: "general",
  MINI: "mini",
};

export function getInspectionTriadType(service) {
  if (!service) return null;
  if (isMiniCheckService(service)) return INSPECTION_TRIAD_TYPE.MINI;

  const { code, name } = readServiceIdentity(service);
  const normalizedName = normalizeLooseToken(name);
  const normalizedCode = normalizeLooseToken(code);

  if (
    normalizedName.includes("kiem tra nhanh") ||
    normalizedCode.includes("kiem tra nhanh") ||
    normalizedCode.includes("quick check") ||
    normalizedCode.includes("quickcheck")
  ) {
    return INSPECTION_TRIAD_TYPE.QUICK;
  }

  if (
    normalizedName.includes("kiem tra tong quat") ||
    normalizedCode.includes("kiem tra tong quat") ||
    normalizedCode.includes("general check") ||
    normalizedCode.includes("generalcheck")
  ) {
    return INSPECTION_TRIAD_TYPE.GENERAL;
  }

  return null;
}

export function getInspectionTriadDisplayName(service, fallbackName = "") {
  const { name } = readServiceIdentity(service || {});
  return String(name || fallbackName || "").trim();
}

export function buildInspectionTriadTypeByServiceId(services) {
  const map = new Map();
  (services || []).forEach((service) => {
    const { id } = readServiceIdentity(service);
    if (!Number.isFinite(id) || id <= 0) return;
    const triadType = getInspectionTriadType(service);
    if (!triadType) return;
    map.set(id, triadType);
  });
  return map;
}

export function getConflictingInspectionTriadServiceIds(
  serviceIds,
  targetServiceId,
  inspectionTriadTypeByServiceId
) {
  const targetId = Number(targetServiceId);
  if (!Number.isFinite(targetId)) return [];
  if (!(inspectionTriadTypeByServiceId instanceof Map)) return [];
  if (!inspectionTriadTypeByServiceId.has(targetId)) return [];

  return Array.from(
    new Set(
      (serviceIds || [])
        .map(Number)
        .filter(
          (serviceId) =>
            Number.isFinite(serviceId) &&
            serviceId !== targetId &&
            inspectionTriadTypeByServiceId.has(serviceId)
        )
    )
  );
}

export function getServiceMinutes(service) {
  const rawMinutes = readField(
    service,
    ["minutes", "Minutes", "standardMinutes", "StandardMinutes"],
    0
  );
  const numericMinutes = Number(rawMinutes || 0);
  if (!Number.isFinite(numericMinutes) || numericMinutes <= 0) return 0;
  return numericMinutes;
}

export function sumServiceMinutes(services, options = {}) {
  const includeMiniCheck = options.includeMiniCheck === true;
  if (!Array.isArray(services)) return 0;

  return services.reduce((sum, service) => {
    if (!includeMiniCheck && isMiniCheckService(service)) return sum;
    return sum + getServiceMinutes(service);
  }, 0);
}
