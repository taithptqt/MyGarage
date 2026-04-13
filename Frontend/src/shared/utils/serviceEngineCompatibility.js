function readField(obj, keys, fallback = null) {
  for (const key of keys) {
    if (obj?.[key] !== undefined && obj?.[key] !== null) return obj[key];
  }
  return fallback;
}

function normalizeTokenSource(value) {
  return String(value || "")
    .trim()
    .toLowerCase()
    .normalize("NFD")
    .replace(/[\u0300-\u036f]/g, "")
    .replace(/[\u0111]/g, "d")
    .replace(/[^a-z0-9]+/g, " ")
    .trim();
}

function hasKeyword(text, keywords) {
  return (Array.isArray(keywords) ? keywords : []).some((keyword) => text.includes(keyword));
}

export function parseServiceEngineFamilies(rawValue) {
  const normalized = normalizeTokenSource(rawValue);
  if (!normalized) return new Set();

  if (hasKeyword(normalized, ["all", "any", "universal", "general"])) {
    return new Set();
  }

  const families = new Set();

  if (hasKeyword(normalized, ["phev"])) {
    families.add("PHEV");
  }

  if (hasKeyword(normalized, ["hev", "hybrid"])) {
    families.add("HYBRID");
  }

  if (hasKeyword(normalized, ["ev", "electric", "dien"])) {
    families.add("EV");
  }

  if (hasKeyword(normalized, ["diesel", "dau"])) {
    families.add("DIESEL");
  }

  if (hasKeyword(normalized, ["gasoline", "petrol", "xang"])) {
    families.add("GASOLINE");
  }

  if (hasKeyword(normalized, ["cng", "lpg", "gas"])) {
    families.add("GAS");
  }

  if (hasKeyword(normalized, ["ice", "combustion", "dot trong", "dong co dot trong"])) {
    families.add("ICE");
  }

  return families;
}

export function parseVehicleEngineFamilies(rawFuelType) {
  const normalized = normalizeTokenSource(rawFuelType);
  if (!normalized) return new Set();

  const families = new Set();

  if (hasKeyword(normalized, ["phev"])) {
    families.add("PHEV");
    families.add("HYBRID");
    families.add("ICE");
  }

  if (hasKeyword(normalized, ["hev", "hybrid"])) {
    families.add("HYBRID");
    families.add("ICE");
  }

  if (hasKeyword(normalized, ["ev", "electric", "dien"])) {
    families.add("EV");
  }

  if (hasKeyword(normalized, ["diesel", "dau"])) {
    families.add("DIESEL");
    families.add("ICE");
  }

  if (hasKeyword(normalized, ["gasoline", "petrol", "xang"])) {
    families.add("GASOLINE");
    families.add("ICE");
  }

  if (hasKeyword(normalized, ["cng", "lpg", "gas"])) {
    families.add("GAS");
    families.add("ICE");
  }

  if (hasKeyword(normalized, ["ice", "combustion", "dot trong", "dong co dot trong"])) {
    families.add("ICE");
  }

  return families;
}

export function isEngineAwareVehicleModel(model) {
  return parseVehicleEngineFamilies(readField(model, ["fuelType", "FuelType"], "")).size > 0;
}

export function isServiceCompatibleWithVehicleModel(service, vehicleModel) {
  const vehicleFamilies = parseVehicleEngineFamilies(readField(vehicleModel, ["fuelType", "FuelType"], ""));
  if (vehicleFamilies.size === 0) return true;

  const serviceFamilies = parseServiceEngineFamilies(readField(service, ["engineType", "EngineType"], ""));
  if (serviceFamilies.size === 0) return true;

  return Array.from(serviceFamilies).some((family) => vehicleFamilies.has(family));
}

export function isBookableItemCompatibleWithVehicleModel(item, vehicleModel, standardServiceById = new Map()) {
  if (!item) return true;

  const packageItems = readField(item, ["items", "Items"], null);
  if (!Array.isArray(packageItems) || packageItems.length === 0) {
    return isServiceCompatibleWithVehicleModel(item, vehicleModel);
  }

  const constrainedServices = packageItems
    .map((packageItem) => {
      const standardServiceId = Number(
        readField(packageItem, ["standardServiceId", "StandardServiceId"], 0)
      );
      return standardServiceById.get(standardServiceId) || null;
    })
    .filter(Boolean)
    .filter((service) => parseServiceEngineFamilies(readField(service, ["engineType", "EngineType"], "")).size > 0);

  if (constrainedServices.length === 0) return true;

  return constrainedServices.every((service) => isServiceCompatibleWithVehicleModel(service, vehicleModel));
}

export function filterBookableItemsForVehicleModels(
  items,
  vehicleModelIds,
  modelInfoById = new Map(),
  standardServiceById = new Map()
) {
  const normalizedItems = Array.isArray(items) ? items : [];
  const targetModels = (Array.isArray(vehicleModelIds) ? vehicleModelIds : [])
    .map(Number)
    .filter((modelId) => Number.isFinite(modelId) && modelId > 0)
    .map((modelId) => modelInfoById.get(modelId) || null)
    .filter(Boolean);

  const engineAwareModels = targetModels.filter((model) => isEngineAwareVehicleModel(model));
  if (engineAwareModels.length === 0) return normalizedItems;

  return normalizedItems.filter((item) =>
    engineAwareModels.every((model) =>
      isBookableItemCompatibleWithVehicleModel(item, model, standardServiceById)
    )
  );
}

export function collectCompatibleCategoryIds(
  items,
  vehicleModelIds,
  modelInfoById = new Map(),
  standardServiceById = new Map()
) {
  return new Set(
    filterBookableItemsForVehicleModels(items, vehicleModelIds, modelInfoById, standardServiceById)
      .map((item) => Number(readField(item, ["categoryId", "CategoryId"], 0)))
      .filter((categoryId) => Number.isFinite(categoryId) && categoryId > 0)
  );
}

export function prunePlanByVehicleCompatibility(
  plan,
  items,
  vehicleModelIds,
  modelInfoById = new Map(),
  standardServiceById = new Map()
) {
  const normalizedPlan = {
    categoryIds: Array.isArray(plan?.categoryIds) ? plan.categoryIds.map(Number).filter(Number.isFinite) : [],
    serviceIds: Array.isArray(plan?.serviceIds) ? plan.serviceIds.map(Number).filter(Number.isFinite) : [],
    excludedSharedCategoryIds: Array.isArray(plan?.excludedSharedCategoryIds)
      ? plan.excludedSharedCategoryIds.map(Number).filter(Number.isFinite)
      : [],
    excludedSharedServiceIds: Array.isArray(plan?.excludedSharedServiceIds)
      ? plan.excludedSharedServiceIds.map(Number).filter(Number.isFinite)
      : [],
  };

  const compatibleItems = filterBookableItemsForVehicleModels(
    items,
    vehicleModelIds,
    modelInfoById,
    standardServiceById
  );
  const compatibleItemIds = new Set(
    compatibleItems
      .map((item) => Number(readField(item, ["id", "Id"], 0)))
      .filter((id) => Number.isFinite(id) && id !== 0)
  );
  const compatibleCategoryIds = collectCompatibleCategoryIds(
    items,
    vehicleModelIds,
    modelInfoById,
    standardServiceById
  );

  return {
    categoryIds: normalizedPlan.categoryIds.filter((categoryId) => compatibleCategoryIds.has(Number(categoryId))),
    serviceIds: normalizedPlan.serviceIds.filter((serviceId) => compatibleItemIds.has(Number(serviceId))),
    excludedSharedCategoryIds: normalizedPlan.excludedSharedCategoryIds.filter((categoryId) =>
      compatibleCategoryIds.has(Number(categoryId))
    ),
    excludedSharedServiceIds: normalizedPlan.excludedSharedServiceIds.filter((serviceId) =>
      compatibleItemIds.has(Number(serviceId))
    ),
  };
}

export function arePlanSelectionsEqual(leftPlan, rightPlan) {
  const normalizePlanShape = (plan) => ({
    categoryIds: Array.isArray(plan?.categoryIds) ? plan.categoryIds.map(Number).filter(Number.isFinite) : [],
    serviceIds: Array.isArray(plan?.serviceIds) ? plan.serviceIds.map(Number).filter(Number.isFinite) : [],
    excludedSharedCategoryIds: Array.isArray(plan?.excludedSharedCategoryIds)
      ? plan.excludedSharedCategoryIds.map(Number).filter(Number.isFinite)
      : [],
    excludedSharedServiceIds: Array.isArray(plan?.excludedSharedServiceIds)
      ? plan.excludedSharedServiceIds.map(Number).filter(Number.isFinite)
      : [],
  });

  const left = normalizePlanShape(leftPlan);
  const right = normalizePlanShape(rightPlan);

  return ["categoryIds", "serviceIds", "excludedSharedCategoryIds", "excludedSharedServiceIds"].every((key) => {
    const leftValues = left[key] || [];
    const rightValues = right[key] || [];
    if (leftValues.length !== rightValues.length) return false;
    return leftValues.every((value, index) => Number(value) === Number(rightValues[index]));
  });
}
