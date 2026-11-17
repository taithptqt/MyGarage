export function toPositiveNumber(value) {
  const parsed = Number(value);
  return Number.isFinite(parsed) && parsed > 0 ? parsed : null;
}

export function normalizeStatusCode(value) {
  return String(value ?? "").trim().toUpperCase();
}

export function formatDateTime(value) {
  if (!value) return "--";
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return "--";
  return date.toLocaleString("vi-VN");
}

export const SYSTEM_ASSIGNMENT_NOTE_MAP = {
  "Created diagnosis ticket": "Đã tạo phiếu chẩn đoán.",
  "Assigned diagnosis": "Đã phân công phiếu chẩn đoán.",
  "Assigned by manager override": "Quản đốc đã phân công trực tiếp phiếu chẩn đoán.",
  "Lead technician accepted diagnosis": "Thợ chính đã nhận phiếu chẩn đoán.",
  "Created supporting-team diagnosis clone": "Đã tạo phiên bản hỗ trợ cho tổ phối hợp.",
  "Canceled because customer picked up vehicle": "Đã hủy vì khách hàng đã nhận xe.",
  "Diagnosis completed": "Đã hoàn thành chẩn đoán.",
};

export function buildAutoAssignmentNote(version) {
  if (!version) return "";
  if (version?.foremanRejectReason) {
    return `Bị từ chối duyệt: ${version.foremanRejectReason}`;
  }

  const eventType = normalizeStatusCode(version?.eventType);
  if (eventType === "TECHNICAL_REJECTED" || eventType === "FINAL_REJECTED") {
    return "Phiên bản này đã bị từ chối duyệt.";
  }
  if (eventType === "TECHNICAL_APPROVED") {
    return "Tổ trưởng đã duyệt kỹ thuật.";
  }
  if (eventType === "FINAL_APPROVED") {
    return "Quản đốc đã duyệt cuối.";
  }
  if (eventType === "STARTED") {
    return "Thợ chính đã nhận phiếu chẩn đoán.";
  }
  if (eventType === "ASSIGNED") {
    return "Đã phân công phiếu chẩn đoán.";
  }
  if (eventType === "CREATED") {
    return "Đã tạo phiếu chẩn đoán.";
  }
  if (eventType === "COMPLETED") {
    return "Đã hoàn thành chẩn đoán.";
  }
  if (eventType === "CANCELED") {
    return "Phiếu đã hủy.";
  }
  return "";
}

export function resolveAssignmentNoteDisplay(rawNote, version) {
  const note = String(rawNote ?? "").trim();
  if (!note) return buildAutoAssignmentNote(version);
  return SYSTEM_ASSIGNMENT_NOTE_MAP[note] || note;
}

export function isDiagnosisApprovedAndLocked(diagnosis) {
  if (!diagnosis) return false;

  const teamLeaderApproved =
    toPositiveNumber(
      diagnosis?.approvedByTeamLeaderId ??
      diagnosis?.ApprovedByTeamLeaderId ??
      diagnosis?.technicalApprovedByTeamLeaderId ??
      diagnosis?.TechnicalApprovedByTeamLeaderId ??
      diagnosis?.teamLeaderApprovedById ??
      diagnosis?.TeamLeaderApprovedById
    ) > 0 ||
    Boolean(
      diagnosis?.approvedByTeamLeaderAt ??
      diagnosis?.ApprovedByTeamLeaderAt ??
      diagnosis?.technicalApprovedAt ??
      diagnosis?.TechnicalApprovedAt ??
      diagnosis?.finalApprovedAt ??
      diagnosis?.FinalApprovedAt
    ) ||
    normalizeStatusCode(
      diagnosis?.teamLeaderApprovalStatus ??
      diagnosis?.TeamLeaderApprovalStatus ??
      diagnosis?.technicalApprovalStatus ??
      diagnosis?.TechnicalApprovalStatus
    ) === "APPROVED";

  const foremanApproved =
    toPositiveNumber(
      diagnosis?.approvedByForemanId ??
      diagnosis?.ApprovedByForemanId ??
      diagnosis?.foremanApprovedById ??
      diagnosis?.ForemanApprovedById
    ) > 0 ||
    Boolean(
      diagnosis?.approvedByForemanAt ??
      diagnosis?.ApprovedByForemanAt ??
      diagnosis?.managerApprovedAt ??
      diagnosis?.ManagerApprovedAt
    ) ||
    normalizeStatusCode(
      diagnosis?.foremanApprovalStatus ??
      diagnosis?.ForemanApprovalStatus ??
      diagnosis?.finalApprovalStatus ??
      diagnosis?.FinalApprovalStatus
    ) === "APPROVED";

  return teamLeaderApproved || foremanApproved;
}

export function parseIdList(value) {
  if (Array.isArray(value)) {
    return value
      .map((entry) => toPositiveNumber(entry))
      .filter(Boolean);
  }

  const raw = String(value ?? "").trim();
  if (!raw) return [];

  try {
    const parsed = JSON.parse(raw);
    if (Array.isArray(parsed)) {
      return parsed
        .map((entry) => toPositiveNumber(entry))
        .filter(Boolean);
    }
  } catch {
    // Fallback to CSV parser below.
  }

  return raw
    .split(",")
    .map((entry) => toPositiveNumber(entry))
    .filter(Boolean);
}

export function extractServiceId(service) {
  return toPositiveNumber(service?.id ?? service?.Id);
}

export function extractServiceName(service) {
  return String(service?.name ?? service?.Name ?? service?.serviceName ?? service?.ServiceName ?? "").trim();
}

export function extractProductName(product) {
  return String(product?.name ?? product?.Name ?? "").trim();
}

export function extractServiceCategoryId(service) {
  return toPositiveNumber(
    service?.categoryId ??
    service?.CategoryId ??
    service?.serviceCategoryId ??
    service?.ServiceCategoryId ??
    service?.serviceCategory?.id ??
    service?.ServiceCategory?.id
  );
}

export function extractServiceCategoryName(service) {
  return String(
    service?.serviceCategoryName ??
    service?.ServiceCategoryName ??
    service?.categoryName ??
    service?.CategoryName ??
    service?.serviceCategory?.categoryName ??
    service?.serviceCategory?.name ??
    service?.ServiceCategory?.categoryName ??
    service?.ServiceCategory?.name ??
    ""
  ).trim();
}

export function extractCategoryId(category) {
  return toPositiveNumber(category?.id ?? category?.Id);
}

export function extractCategoryParentId(category) {
  return toPositiveNumber(category?.parentId ?? category?.ParentId);
}

export function extractCategoryName(category) {
  return String(
    category?.categoryName ??
      category?.CategoryName ??
      category?.name ??
      category?.Name ??
      category?.code ??
      ""
  ).trim();
}

export function extractProductCategoryId(product) {
  return toPositiveNumber(
    product?.categoryId ??
      product?.CategoryId
  );
}

export function extractProductCategoryName(product) {
  return String(
    product?.categoryName ??
      product?.CategoryName ??
      ""
  ).trim();
}

export function firstNonEmptyText(...values) {
  for (const value of values) {
    const text = String(value ?? "").trim();
    if (text) return text;
  }
  return "";
}

export function normalizeLookupText(value) {
  return String(value || "")
    .trim()
    .toLowerCase()
    .normalize("NFD")
    .replace(/[\u0300-\u036f]/g, "")
    .replace(/đ/g, "d");
}

export const INTERNAL_CUSTOMER_NOTE_MARKER_REGEX = /\[(?:FLOW:[^\]]+|CLUSTER:[^\]]+|[A-Z][A-Z0-9_]{2,})\]/g;
export const EMPTY_CUSTOMER_NOTE_NORMALIZED_VALUES = new Set([
  "khong co",
  "khong co ghi chu",
  "khong co ghi chu gi",
  "khong co mo ta",
  "none",
  "no note",
  "n/a",
  "na",
]);

export function sanitizeCustomerNoteText(value) {
  const raw = String(value || "").trim();
  if (!raw) return "";

  const cleaned = raw
    .replace(INTERNAL_CUSTOMER_NOTE_MARKER_REGEX, " ")
    .replace(/\s+/g, " ")
    .trim()
    .replace(/^[,;|.\-: ]+|[,;|.\-: ]+$/g, "")
    .trim();

  if (!cleaned) return "";

  const normalized = normalizeLookupText(cleaned);
  if (EMPTY_CUSTOMER_NOTE_NORMALIZED_VALUES.has(normalized)) {
    return "";
  }

  return cleaned;
}

export function extractVehicleModelId(order) {
  return toPositiveNumber(
    order?.vehicleModelId ??
    order?.VehicleModelId ??
    order?.customerVehicle?.vehicleModelId ??
    order?.customerVehicle?.VehicleModelId ??
    order?.vehicle?.vehicleModelId ??
    order?.vehicle?.VehicleModelId
  );
}

export function extractDiagnosisAssignmentId(diagnosis) {
  return toPositiveNumber(
    diagnosis?.diagnosisAssignmentId ??
      diagnosis?.DiagnosisAssignmentId ??
      diagnosis?.assignmentId ??
      diagnosis?.AssignmentId
  );
}

export function extractDiagnosisAssignmentVersions(diagnosis) {
  const rawVersions = diagnosis?.assignmentVersions ?? diagnosis?.AssignmentVersions;
  if (!Array.isArray(rawVersions) || rawVersions.length === 0) {
    const fallbackId = extractDiagnosisAssignmentId(diagnosis);
    if (!fallbackId) return [];
    return [
      {
        diagnosisAssignmentId: fallbackId,
        eventType: "",
        status: normalizeStatusCode(diagnosis?.status ?? diagnosis?.Status),
        teamLeaderApprovalStatus: normalizeStatusCode(diagnosis?.teamLeaderApprovalStatus ?? diagnosis?.TeamLeaderApprovalStatus),
        finalApprovalStatus: normalizeStatusCode(diagnosis?.finalApprovalStatus ?? diagnosis?.FinalApprovalStatus),
        foremanRejectReason: String(diagnosis?.foremanRejectReason ?? diagnosis?.ForemanRejectReason ?? "").trim(),
        note: String(diagnosis?.assignmentNote ?? diagnosis?.AssignmentNote ?? "").trim(),
        createdAt: diagnosis?.updatedAt ?? diagnosis?.UpdatedAt ?? diagnosis?.createdAt ?? diagnosis?.CreatedAt ?? null,
        isCurrent: true,
      },
    ];
  }

  return rawVersions
    .map((item) => {
      const diagnosisAssignmentId = toPositiveNumber(
        item?.diagnosisAssignmentId ??
        item?.DiagnosisAssignmentId ??
        item?.id ??
        item?.Id
      );
      if (!diagnosisAssignmentId) return null;
      return {
        diagnosisAssignmentId,
        eventType: String(item?.eventType ?? item?.EventType ?? "").trim().toUpperCase(),
        status: normalizeStatusCode(item?.status ?? item?.Status),
        teamLeaderApprovalStatus: normalizeStatusCode(item?.teamLeaderApprovalStatus ?? item?.TeamLeaderApprovalStatus),
        finalApprovalStatus: normalizeStatusCode(item?.finalApprovalStatus ?? item?.FinalApprovalStatus),
        foremanRejectReason: String(item?.foremanRejectReason ?? item?.ForemanRejectReason ?? "").trim(),
        note: String(item?.note ?? item?.Note ?? "").trim(),
        createdAt: item?.createdAt ?? item?.CreatedAt ?? null,
        isCurrent: Boolean(item?.isCurrent ?? item?.IsCurrent),
      };
    })
    .filter(Boolean)
    .sort((a, b) => {
      const aTime = new Date(a.createdAt || 0).getTime();
      const bTime = new Date(b.createdAt || 0).getTime();
      if (bTime !== aTime) return bTime - aTime;
      return b.diagnosisAssignmentId - a.diagnosisAssignmentId;
    });
}

export function normalizeServiceBomItems(response) {
  const source = Array.isArray(response)
    ? response
    : Array.isArray(response?.items)
      ? response.items
      : Array.isArray(response?.data)
        ? response.data
        : Array.isArray(response?.$values)
          ? response.$values
          : Array.isArray(response?.result?.items)
            ? response.result.items
            : Array.isArray(response?.result?.data)
              ? response.result.data
              : Array.isArray(response?.result?.$values)
                ? response.result.$values
        : [];

  return source
    .map((item) => {
      const productId = toPositiveNumber(item?.productId ?? item?.ProductId ?? item?.inventoryItemId ?? item?.InventoryItemId);
      if (!productId) return null;
      const quantity = Number(
        item?.quantity ??
        item?.Quantity ??
        item?.requiredQuantity ??
        item?.RequiredQuantity ??
        item?.qty ??
        item?.Qty ??
        1
      );
      return {
        productId,
        quantity: Number.isFinite(quantity) && quantity > 0 ? quantity : 1,
        isMandatory: Boolean(item?.isMandatory ?? item?.IsMandatory),
      };
    })
    .filter(Boolean);
}

export function resolveApiErrorMessage(error) {
  const payload = error?.response?.data;
  return (
    payload?.message ||
    payload?.title ||
    error?.message ||
    "Không thể tải vật tư tự động từ BOM."
  );
}

export function delay(ms) {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

export function normalizeImageUrls(value) {
  if (Array.isArray(value)) {
    return value
      .map((item) => String(item || "").trim())
      .filter(Boolean);
  }

  const text = String(value || "").trim();
  if (!text) return [];

  try {
    const parsed = JSON.parse(text);
    if (Array.isArray(parsed)) {
      return parsed
        .map((item) => String(item || "").trim())
        .filter(Boolean);
    }
  } catch {
    // Fallback to plain text split
  }

  return text
    .split(/[,\n;]/g)
    .map((item) => String(item || "").trim())
    .filter(Boolean);
}

export function toEvidenceMediaUrlsValue(urls) {
  const normalized = normalizeImageUrls(urls);
  return normalized.length > 0 ? JSON.stringify(normalized) : "";
}

export function normalizeQuantity(value) {
  const parsed = Number(value);
  return Number.isFinite(parsed) && parsed > 0 ? parsed : 1;
}

export function isNewRecommendationItem(item) {
  return Boolean(item?.isNew) || String(item?.id || "").startsWith("tmp-");
}

export function buildVehicleDisplay(order, modelById, brandById, vehicleContext = null) {
  const plate = firstNonEmptyText(
    vehicleContext?.vehicleLicensePlate,
    vehicleContext?.VehicleLicensePlate,
    order?.vehicleLicensePlate,
    order?.VehicleLicensePlate,
    order?.guestVehicleLicensePlate,
    order?.GuestVehicleLicensePlate,
    order?.licensePlate,
    order?.LicensePlate,
    order?.vehicle?.licensePlate,
    order?.vehicle?.LicensePlate,
    order?.customerVehicle?.licensePlate,
    order?.customerVehicle?.LicensePlate
  );

  const resolvedVehicleModelId = toPositiveNumber(
    vehicleContext?.vehicleModelId,
    vehicleContext?.VehicleModelId,
    order?.vehicleModelId,
    order?.VehicleModelId,
    order?.guestVehicleModelId,
    order?.GuestVehicleModelId,
    order?.customerVehicle?.vehicleModelId,
    order?.customerVehicle?.VehicleModelId,
    order?.vehicle?.vehicleModelId,
    order?.vehicle?.VehicleModelId,
    order?.vehicle?.modelId,
    order?.vehicle?.ModelId,
    order?.vehicle?.model?.id,
    order?.vehicle?.Model?.Id
  );
  const resolvedModel = resolvedVehicleModelId ? modelById.get(resolvedVehicleModelId) : null;

  const resolvedVehicleBrandId = toPositiveNumber(
    vehicleContext?.vehicleBrandId,
    vehicleContext?.VehicleBrandId,
    order?.vehicleBrandId,
    order?.VehicleBrandId,
    order?.guestVehicleBrandId,
    order?.GuestVehicleBrandId,
    order?.customerVehicle?.vehicleBrandId,
    order?.customerVehicle?.VehicleBrandId,
    order?.vehicle?.vehicleBrandId,
    order?.vehicle?.VehicleBrandId,
    order?.vehicle?.brandId,
    order?.vehicle?.BrandId,
    resolvedModel?.brandId,
    resolvedModel?.BrandId
  );
  const resolvedBrand = resolvedVehicleBrandId ? brandById.get(resolvedVehicleBrandId) : null;

  const brand = firstNonEmptyText(
    vehicleContext?.vehicleBrandName,
    vehicleContext?.VehicleBrandName,
    order?.vehicleBrandName,
    order?.VehicleBrandName,
    order?.guestVehicleBrandName,
    order?.GuestVehicleBrandName,
    order?.brandName,
    order?.BrandName,
    order?.vehicle?.brandName,
    order?.vehicle?.BrandName,
    order?.customerVehicle?.brandName,
    order?.customerVehicle?.BrandName,
    order?.vehicle?.brand?.name,
    order?.vehicle?.Brand?.Name,
    order?.customerVehicle?.brand?.name,
    order?.customerVehicle?.Brand?.Name,
    resolvedBrand?.name,
    resolvedBrand?.Name
  );

  const model = firstNonEmptyText(
    vehicleContext?.vehicleModelName,
    vehicleContext?.VehicleModelName,
    vehicleContext?.vehicleDescriptor,
    vehicleContext?.VehicleDescriptor,
    order?.vehicleModelName,
    order?.VehicleModelName,
    order?.guestVehicleModelName,
    order?.GuestVehicleModelName,
    order?.modelName,
    order?.ModelName,
    order?.vehicle?.modelName,
    order?.vehicle?.ModelName,
    order?.customerVehicle?.modelName,
    order?.customerVehicle?.ModelName,
    order?.vehicle?.model?.name,
    order?.vehicle?.Model?.Name,
    order?.customerVehicle?.model?.name,
    order?.customerVehicle?.Model?.Name,
    resolvedModel?.modelName,
    resolvedModel?.ModelName,
    resolvedModel?.name,
    resolvedModel?.Name
  );

  const yearRaw = firstNonEmptyText(
    vehicleContext?.vehicleManufactureYear,
    vehicleContext?.VehicleManufactureYear,
    vehicleContext?.vehicleYear,
    vehicleContext?.VehicleYear,
    order?.vehicleYear,
    order?.VehicleYear,
    order?.guestVehicleYear,
    order?.GuestVehicleYear,
    order?.manufactureYear,
    order?.ManufactureYear,
    order?.year,
    order?.Year,
    order?.vehicle?.vehicleYear,
    order?.vehicle?.VehicleYear,
    order?.vehicle?.year,
    order?.vehicle?.Year,
    order?.customerVehicle?.vehicleYear,
    order?.customerVehicle?.VehicleYear,
    order?.customerVehicle?.year,
    order?.customerVehicle?.Year
  );
  const yearNumber = Number(yearRaw);
  const year = Number.isFinite(yearNumber) && yearNumber > 1900 ? String(Math.trunc(yearNumber)) : "";

  return {
    plate: plate || "Chưa có",
    brand: brand || "Chưa có",
    model: model || "Chưa có",
    year: year || "Chưa có",
  };
}
