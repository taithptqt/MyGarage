import { parseCustomerNotes } from "@features/workshop/utils/serviceOrderDetailUtils";

export const COLUMN_ORDER = ["WHITE", "YELLOW", "RED", "GREEN"];

export const COLOR_META = {
  APPROVAL_BUCKET: {
    label: "Cần duyệt",
    description: "Phiếu tổ trưởng đã duyệt hoặc phiếu có độ quá SLA",
    tabBg: "#eff6ff",
    tabBorder: "#93c5fd",
    tabText: "#1d4ed8",
    activeBg: "#2563eb",
    activeText: "white",
    cardAccent: "#2563eb",
    cardBg: "#eff6ff",
  },
  ALL_ITEMS: {
    label: "Tất cả",
    description: "Tổng của 4 tab còn lại trong màn hình điều phối",
    tabBg: "#f0fdfa",
    tabBorder: "#5eead4",
    tabText: "#0f766e",
    activeBg: "#0f766e",
    activeText: "white",
    cardAccent: "#0f766e",
    cardBg: "#f0fdfa",
  },
  WHITE: {
    label: "Mới tiếp nhận",
    description: "Chờ tổ trưởng nhận xe và phân công",
    tabBg: "#f8fafc",
    tabBorder: "#cbd5e1",
    tabText: "#334155",
    activeBg: "#0f172a",
    activeText: "white",
    cardAccent: "#94a3b8",
    cardBg: "white",
  },
  YELLOW: {
    label: "Đang chẩn đoán",
    description: "Đã có tổ nhận và đang xử lý",
    tabBg: "#fff8db",
    tabBorder: "#f6d365",
    tabText: "#8a6a00",
    activeBg: "#d69e2e",
    activeText: "white",
    cardAccent: "#d69e2e",
    cardBg: "#fffdf3",
  },
  RED: {
    label: "Cần can thiệp",
    description: "Có cảnh báo hỗ trợ, hủy lệnh hoặc quá SLA",
    tabBg: "#fff1f2",
    tabBorder: "#fca5a5",
    tabText: "#b91c1c",
    activeBg: "#e53e3e",
    activeText: "white",
    cardAccent: "#ef4444",
    cardBg: "#fff7f7",
  },
  GREEN: {
    label: "Đã chẩn đoán",
    description: "Đã có kết quả kỹ thuật để duyệt",
    tabBg: "#ecfdf3",
    tabBorder: "#86efac",
    tabText: "#166534",
    activeBg: "#16a34a",
    activeText: "white",
    cardAccent: "#22c55e",
    cardBg: "#f3fff8",
  },
};

export const STATUS_CARD_THEME = {
  APPROVAL_BUCKET: {
    dot: "#2563eb",
    activeBorder: "#2563eb",
    activeBg: "linear-gradient(150deg, #eff6ff 0%, #dbeafe 58%, #bfdbfe 100%)",
    activeShadow: "0 12px 26px rgba(37, 99, 235, 0.24)",
  },
  ALL_ITEMS: {
    dot: "#0f766e",
    activeBorder: "#0f766e",
    activeBg: "linear-gradient(150deg, #f0fdfa 0%, #ccfbf1 58%, #99f6e4 100%)",
    activeShadow: "0 12px 26px rgba(15, 118, 110, 0.22)",
  },
  WHITE: {
    dot: "#64748b",
    activeBorder: "#334155",
    activeBg: "linear-gradient(150deg, #ffffff 0%, #f8fafc 60%, #eef2ff 100%)",
    activeShadow: "0 12px 26px rgba(51, 65, 85, 0.14)",
  },
  YELLOW: {
    dot: "#b45309",
    activeBorder: "#d69e2e",
    activeBg: "linear-gradient(150deg, #fffdf7 0%, #fff8db 58%, #fff1c7 100%)",
    activeShadow: "0 12px 26px rgba(214, 158, 46, 0.22)",
  },
  RED: {
    dot: "#dc2626",
    activeBorder: "#e53e3e",
    activeBg: "linear-gradient(150deg, #fff8f8 0%, #fff1f2 58%, #ffe0e3 100%)",
    activeShadow: "0 12px 26px rgba(229, 62, 62, 0.22)",
  },
  GREEN: {
    dot: "#15803d",
    activeBorder: "#16a34a",
    activeBg: "linear-gradient(150deg, #f8fffb 0%, #ecfdf3 58%, #dcfce7 100%)",
    activeShadow: "0 12px 26px rgba(22, 163, 74, 0.2)",
  },
};

export const DISPATCH_TAG_OPTIONS = [
  { value: "ENGINE_CHASSIS", label: "Máy - Gầm" },
  { value: "ELECTRICAL_AC", label: "Điện - Máy lạnh" },
  { value: "BODY_PAINT", label: "Đồng sơn - Chăm sóc xe" },
  { value: "MAINTENANCE", label: "Bảo dưỡng nhanh" },
];

export const DISPATCH_TAG_LABELS = DISPATCH_TAG_OPTIONS.reduce((acc, item) => {
  acc[item.value] = item.label;
  return acc;
}, {});

export const CANCEL_REASONS = ["Sai chuyên môn", "Quá tải khoang", "Thiếu thiết bị", "Khác"];

export const TEAM_LEADER_ROLE_CODES = new Set(["TEAM_LEADER"]);
export const TECHNICIAN_ROLE_CODES = new Set(["TECH", "TECHNICIAN", "TEAM_LEADER", "FOREMAN"]);

export function toText(value) {
  if (value === null || value === undefined) return "";
  return String(value).trim();
}

export function readValue(source, ...keys) {
  for (const key of keys) {
    const value = source?.[key];
    if (value !== null && value !== undefined) return value;
  }
  return null;
}

export function toPositiveNumber(value) {
  const parsed = Number(value);
  return Number.isFinite(parsed) && parsed > 0 ? parsed : null;
}

export function normalizeCode(value) {
  return toText(value).toUpperCase();
}

export function resolveDispatchTagLabel(tag, fallbackTag = "") {
  const normalizedTag = normalizeCode(tag);
  if (normalizedTag) return getDispatchTagLabel(normalizedTag);

  const normalizedFallback = normalizeCode(fallbackTag);
  if (normalizedFallback) return `${getDispatchTagLabel(normalizedFallback)} (chuyên môn tổ)`;

  return getDispatchTagLabel(tag);
}

export function formatDateTime(value) {
  if (!value) return "--";
  const parsed = new Date(value);
  if (Number.isNaN(parsed.getTime())) return "--";
  const pad2 = (num) => String(num).padStart(2, "0");
  const hours = pad2(parsed.getHours());
  const minutes = pad2(parsed.getMinutes());
  const day = pad2(parsed.getDate());
  const month = pad2(parsed.getMonth() + 1);
  const year = parsed.getFullYear();
  return `${hours}:${minutes} (${day}/${month}/${year})`;
}

export function formatMinutes(value) {
  const minutes = Number(value || 0);
  if (!Number.isFinite(minutes) || minutes <= 0) return "0 phút";
  if (minutes < 60) return `${Math.floor(minutes)} phút`;
  const hours = Math.floor(minutes / 60);
  const remain = Math.floor(minutes % 60);
  return remain > 0 ? `${hours} giờ ${remain} phút` : `${hours} giờ`;
}

export function getItemNewestTimestamp(item) {
  const preferred = [
    item?.requestedAt,
    item?.supportRequestedAt,
    item?.assignedAt,
    item?.startedAt,
    item?.technicianAcceptedAt,
    item?.technicianCompletedAt,
    item?.technicalApprovedAt,
    item?.finalApprovedAt,
  ];

  for (const value of preferred) {
    const time = new Date(value || 0).getTime();
    if (Number.isFinite(time) && time > 0) return time;
  }

  return 0;
}

export function sortItemsByNewest(items) {
  return [...(Array.isArray(items) ? items : [])].sort(
    (a, b) => getItemNewestTimestamp(b) - getItemNewestTimestamp(a)
  );
}

export function getDispatchTagLabel(tag) {
  const normalized = normalizeCode(tag);
  if (!normalized) return "Chưa chọn chuyên môn";

  const splitTags = normalized
    .split(/[;,|/]/)
    .map((item) => normalizeCode(item))
    .filter(Boolean);

  const uniqueTags = splitTags.length > 0 ? Array.from(new Set(splitTags)) : [normalized];
  return uniqueTags.map((code) => DISPATCH_TAG_LABELS[code] || code).join(" + ");
}

export function dispatchTagIncludes(tagSource, targetTag) {
  const normalizedTarget = normalizeCode(targetTag);
  if (!normalizedTarget) return true;

  const normalizedSource = normalizeCode(tagSource);
  if (!normalizedSource) return false;

  const splitTags = normalizedSource
    .split(/[;,|/]/)
    .map((item) => normalizeCode(item))
    .filter(Boolean);

  if (splitTags.length === 0) {
    return normalizedSource === normalizedTarget;
  }

  return splitTags.includes(normalizedTarget);
}

export function normalizeTeamIds(ids, excludedId = null) {
  const excluded = toPositiveNumber(excludedId);
  const unique = new Set();
  (Array.isArray(ids) ? ids : []).forEach((value) => {
    const id = toPositiveNumber(value);
    if (!id) return;
    if (excluded && id === excluded) return;
    unique.add(id);
  });
  return Array.from(unique);
}

export function normalizeSupportingAssignmentMap(rawMap) {
  const source = rawMap && typeof rawMap === "object" ? rawMap : {};
  const normalized = {};

  Object.keys(source).forEach((teamIdKey) => {
    const teamId = toPositiveNumber(teamIdKey);
    if (!teamId) return;

    const row = source[teamIdKey] && typeof source[teamIdKey] === "object" ? source[teamIdKey] : {};
    const technicianId = toPositiveNumber(readValue(row, "technicianId", "TechnicianId"));
    const assistantTechnicianId = toPositiveNumber(readValue(row, "assistantTechnicianId", "AssistantTechnicianId"));

    normalized[String(teamId)] = {
      technicianId: technicianId ? String(technicianId) : "",
      assistantTechnicianId: assistantTechnicianId ? String(assistantTechnicianId) : "",
    };
  });

  return normalized;
}

export function buildSupportingAssignmentsPayload(teamIds, assignmentMap) {
  const normalizedTeamIds = normalizeTeamIds(teamIds, null);
  const map = normalizeSupportingAssignmentMap(assignmentMap);

  return normalizedTeamIds.map((teamId) => {
    const key = String(teamId);
    const row = map[key] || {};
    const technicianId = toPositiveNumber(readValue(row, "technicianId", "TechnicianId"));
    const assistantTechnicianId = toPositiveNumber(readValue(row, "assistantTechnicianId", "AssistantTechnicianId"));
    return {
      teamId,
      technicianId: technicianId || 0,
      assistantTechnicianId: assistantTechnicianId || null,
    };
  });
}

export function normalizeSupportRequestMap(rawMap) {
  const source = rawMap && typeof rawMap === "object" ? rawMap : {};
  const normalized = {};

  Object.keys(source).forEach((teamIdKey) => {
    const teamId = toPositiveNumber(teamIdKey);
    if (!teamId) return;

    const row = source[teamIdKey] && typeof source[teamIdKey] === "object" ? source[teamIdKey] : {};
    const requestedTechnicianCount = Math.max(
      1,
      Math.min(8, Number(readValue(row, "requestedTechnicianCount", "RequestedTechnicianCount") || 1))
    );

    normalized[String(teamId)] = {
      requestedTechnicianCount: String(requestedTechnicianCount),
    };
  });

  return normalized;
}

export function buildSupportRequestAssignmentsPayload(teamIds, requestMap) {
  const normalizedTeamIds = normalizeTeamIds(teamIds, null);
  const normalizedMap = normalizeSupportRequestMap(requestMap);

  return normalizedTeamIds.map((teamId) => {
    const key = String(teamId);
    const row = normalizedMap[key] || { requestedTechnicianCount: "1" };
    const requestedTechnicianCount = Math.max(
      1,
      Math.min(8, Number(readValue(row, "requestedTechnicianCount", "RequestedTechnicianCount") || 1))
    );

    return {
      teamId,
      technicianId: 0,
      assistantTechnicianId: null,
      requestedTechnicianCount,
    };
  });
}

export function normalizeSupportingAssignmentsFromBoard(rawAssignments, primaryTeamId = null) {
  if (!Array.isArray(rawAssignments)) return [];

  const excludedPrimaryTeamId = toPositiveNumber(primaryTeamId);
  const byTeam = new Map();

  rawAssignments.forEach((assignment) => {
    const teamId = toPositiveNumber(readValue(assignment, "teamId", "TeamId"));
    if (!teamId) return;
    if (excludedPrimaryTeamId && teamId === excludedPrimaryTeamId) return;
    if (byTeam.has(teamId)) return;

    byTeam.set(teamId, {
      teamId,
      teamName: toText(readValue(assignment, "teamName", "TeamName")),
      technicianId: toPositiveNumber(readValue(assignment, "technicianId", "TechnicianId")),
      technicianName: toText(readValue(assignment, "technicianName", "TechnicianName")),
      assistantTechnicianId: toPositiveNumber(readValue(assignment, "assistantTechnicianId", "AssistantTechnicianId")),
      assistantTechnicianName: toText(readValue(assignment, "assistantTechnicianName", "AssistantTechnicianName")),
      requestedTechnicianCount: Math.max(
        0,
        Number(readValue(assignment, "requestedTechnicianCount", "RequestedTechnicianCount") || 0)
      ),
    });
  });

  return Array.from(byTeam.values());
}

export function extractRequestedServiceNames(details) {
  if (!Array.isArray(details) || details.length === 0) return "";

  const names = [];
  const seen = new Set();

  details.forEach((detail) => {
    if (readValue(detail, "isDeleted", "IsDeleted") === true) return;

    const recommendationId = toPositiveNumber(readValue(detail, "diagnosisRecommendationId", "DiagnosisRecommendationId"));
    if (recommendationId) return;

    const itemType = normalizeCode(readValue(detail, "itemType", "ItemType"));
    const hasStandardService = toPositiveNumber(readValue(detail, "standardServiceId", "StandardServiceId"));
    const isServiceLike = itemType === "SERVICE" || itemType === "LABOR" || Boolean(hasStandardService);
    if (!isServiceLike) return;

    const name =
      toText(readValue(detail, "itemName", "ItemName")) ||
      toText(readValue(detail, "serviceName", "ServiceName")) ||
      toText(readValue(detail, "standardServiceName", "StandardServiceName"));

    if (!name) return;

    const key = name.toLowerCase();
    if (seen.has(key)) return;
    seen.add(key);
    names.push(name);
  });

  return names.join(", ");
}

export function normalizeServiceOrderDetailsResponse(response) {
  if (Array.isArray(response)) return response;

  const directRows = readValue(response, "items", "Items", "rows", "Rows", "data", "Data");
  if (Array.isArray(directRows)) return directRows;

  const nestedData = readValue(response, "data", "Data");
  if (nestedData && typeof nestedData === "object") {
    const nestedRows = readValue(nestedData, "items", "Items", "rows", "Rows", "data", "Data");
    if (Array.isArray(nestedRows)) return nestedRows;
  }

  return [];
}

export function normalizeDiagnosisResponse(response) {
  if (Array.isArray(response)) return response;

  const directRows = readValue(response, "items", "Items", "rows", "Rows", "data", "Data");
  if (Array.isArray(directRows)) return directRows;

  const nestedData = readValue(response, "data", "Data");
  if (nestedData && typeof nestedData === "object") {
    const nestedRows = readValue(nestedData, "items", "Items", "rows", "Rows", "data", "Data");
    if (Array.isArray(nestedRows)) return nestedRows;
  }

  return [];
}

export function isDiagnosisCompletedItem(item) {
  if (!item) return false;
  const workflowStatus = normalizeCode(item.workflowStatus);
  if ([
    "SUBMITTED",
    "TL_APPROVED",
    "ESCALATED_TO_FOREMAN",
    "FOREMAN_APPROVED_LOCKED",
    "TL_REJECTED_REDIAGNOSE",
    "FOREMAN_REJECTED_REDIAGNOSE",
  ].includes(workflowStatus)) {
    return true;
  }

  return Boolean(item.technicalApprovedByTeamLeaderId || item.approvedByTeamLeaderAt);
}

export function hasSubmittedDiagnosisVersion(item) {
  if (!item) return false;
  const workflowStatus = normalizeCode(item.workflowStatus);
  if (
    [
      "SUBMITTED",
      "TL_APPROVED",
      "ESCALATED_TO_FOREMAN",
      "FOREMAN_APPROVED_LOCKED",
      "TL_REJECTED_REDIAGNOSE",
      "FOREMAN_REJECTED_REDIAGNOSE",
    ].includes(workflowStatus)
  ) {
    return true;
  }

  return false;
}

export function canViewDiagnosisItem(item, { isTeamLeaderMode = false, isManagerMode = false } = {}) {
  if (!item) return false;
  return hasSubmittedDiagnosisVersion(item);
}

export function isForemanMyTaskItem(item) {
  if (!item) return false;
  const workflowStatus = normalizeCode(item.workflowStatus);
  const teamLeaderApprovalStatus = normalizeCode(item.teamLeaderApprovalStatus);
  const foremanApprovalStatus = normalizeCode(item.foremanApprovalStatus);
  if (["APPROVED", "REJECTED"].includes(foremanApprovalStatus)) return false;
  return (
    workflowStatus === "TL_APPROVED" ||
    workflowStatus === "ESCALATED_TO_FOREMAN" ||
    ["APPROVED", "BYPASSED", "ESCALATED"].includes(teamLeaderApprovalStatus)
  );
}

export function isTeamLeaderNeedApprovalItem(item) {
  if (!item) return false;
  const teamLeaderApprovalStatus = normalizeCode(item.teamLeaderApprovalStatus);
  const foremanApprovalStatus = normalizeCode(item.foremanApprovalStatus);
  const workflowStatus = normalizeCode(item.workflowStatus);

  if (["APPROVED", "REJECTED", "BYPASSED", "ESCALATED"].includes(teamLeaderApprovalStatus)) return false;
  if (["APPROVED", "REJECTED"].includes(foremanApprovalStatus)) return false;

  // Cần duyệt của tổ trưởng chỉ hiển thị khi đã submit phiên bản chẩn đoán.
  if (workflowStatus === "SUBMITTED") return true;

  return false;
}

export function resolveDispatchTabByStatus(item) {
  if (!item) return null;
  const dispatchBucket = normalizeCode(item.dispatchBucket);
  if (dispatchBucket === "APPROVAL_BUCKET") return "APPROVAL_BUCKET";
  if (dispatchBucket === "WHITE") return "WHITE";
  if (dispatchBucket === "YELLOW") return "YELLOW";
  if (dispatchBucket === "RED") return "RED";
  if (dispatchBucket === "GREEN") return "GREEN";
  return null;
}

export function resolveStaffNameById(staffs, id) {
  const staffId = toPositiveNumber(id);
  if (!staffId || !Array.isArray(staffs)) return "";
  const match = staffs.find((staff) => toPositiveNumber(readValue(staff, "id", "Id")) === staffId);
  return toText(readValue(match, "fullName", "FullName"));
}

export const cancelAttentionPulse = keyframes`
  0% {
    box-shadow: 0 0 0 0 rgba(239, 68, 68, 0.45);
  }
  70% {
    box-shadow: 0 0 0 10px rgba(239, 68, 68, 0);
  }
  100% {
    box-shadow: 0 0 0 0 rgba(239, 68, 68, 0);
  }
`;

export const QUEUE_STATE_META = {
  NEW: { label: "Mới tiếp nhận", colorScheme: "gray" },
  IN_PROGRESS: { label: "Đang chẩn đoán", colorScheme: "yellow" },
  ALERT: { label: "Cần can thiệp", colorScheme: "red" },
  DIAGNOSED: { label: "Đã chẩn đoán", colorScheme: "green" },
};

export function getQueueStateMeta(queueState, queueColor) {
  const normalizedState = normalizeCode(queueState);
  if (normalizedState && QUEUE_STATE_META[normalizedState]) {
    return QUEUE_STATE_META[normalizedState];
  }

  const normalizedColor = normalizeCode(queueColor);
  if (normalizedColor === "RED") return QUEUE_STATE_META.ALERT;
  if (normalizedColor === "YELLOW") return QUEUE_STATE_META.IN_PROGRESS;
  if (normalizedColor === "GREEN") return QUEUE_STATE_META.DIAGNOSED;
  return QUEUE_STATE_META.NEW;
}

export function resolveApiErrorMessage(error, fallback = "Đã có lỗi xảy ra") {
  const message = toText(readValue(error, "response")?.data?.message);
  if (message) return message;
  const raw = toText(error?.message);
  if (raw) return raw;
  return fallback;
}

export function isOperationalBayAvailable(operationalStatus) {
  const normalized = normalizeCode(operationalStatus);
  if (!normalized) return true;
  return !["IN_USE", "BROKEN", "UNDER_MAINTENANCE", "MAINTENANCE", "SUSPENDED"].includes(normalized);
}

export function getStaffRoleCode(staff, roleById) {
  const directRoleCode = normalizeCode(readValue(staff, "roleCode", "RoleCode"));
  if (directRoleCode) return directRoleCode;

  const roleId = toPositiveNumber(readValue(staff, "roleId", "RoleId"));
  if (!roleId) return "";

  const role = roleById.get(roleId);
  return normalizeCode(readValue(role, "roleCode", "RoleCode"));
}
export function isTechnicianCandidate(staff, roleById) {
  const roleCode = getStaffRoleCode(staff, roleById);
  const roleName = toText(readValue(staff, "roleName", "RoleName")).toLowerCase();

  if (TECHNICIAN_ROLE_CODES.has(roleCode)) return true;
  return roleName.includes("kỹ thuật") || roleName.includes("ky thuat") || roleName.includes("tổ trưởng") || roleName.includes("to truong");
}

export function isTeamLeader(staff, roleById) {
  const roleCode = getStaffRoleCode(staff, roleById);
  return TEAM_LEADER_ROLE_CODES.has(roleCode);
}

export function isStaffActive(staff) {
  if (readValue(staff, "isDeleted", "IsDeleted") === true) return false;
  const status = normalizeCode(readValue(staff, "status", "Status"));
  if (!status) return true;
  return status !== "INACTIVE";
}

export function sortStaffByName(list) {
  return [...list].sort((a, b) => {
    const aName = toText(readValue(a, "fullName", "FullName"));
    const bName = toText(readValue(b, "fullName", "FullName"));
    return aName.localeCompare(bName, "vi");
  });
}

export function resolveCurrentStaff(staffs, user) {
  if (!Array.isArray(staffs) || staffs.length === 0 || !user) return null;

  const userId = toPositiveNumber(readValue(user, "id", "Id"));
  if (userId) {
    const byId = staffs.find((item) => toPositiveNumber(readValue(item, "id", "Id")) === userId);
    if (byId) return byId;
  }

  const userEmail = toText(readValue(user, "email", "Email")).toLowerCase();
  if (userEmail) {
    const byEmail = staffs.find((item) => toText(readValue(item, "email", "Email")).toLowerCase() === userEmail);
    if (byEmail) return byEmail;
  }

  const userName = toText(readValue(user, "fullName", "FullName")).toLowerCase();
  if (userName) {
    const byName = staffs.find((item) => toText(readValue(item, "fullName", "FullName")).toLowerCase() === userName);
    if (byName) return byName;
  }

  return null;
}

export function resolveCustomerName(raw, order, customerMap) {
  const boardName = toText(readValue(raw, "customerName", "CustomerName"));
  if (boardName) return boardName;

  const orderName = toText(readValue(order, "customerName", "CustomerName")) || toText(readValue(order, "guestName", "GuestName"));
  if (orderName) return orderName;

  const customerId = toPositiveNumber(readValue(order, "customerId", "CustomerId"));
  if (!customerId) return "";

  const customer = customerMap.get(customerId);
  return toText(readValue(customer, "fullName", "FullName", "name", "Name"));
}

export function resolveVehiclePlate(raw, order, vehicleMap) {
  const boardPlate = toText(readValue(raw, "vehicleLicensePlate", "VehicleLicensePlate"));
  if (boardPlate) return boardPlate;

  const orderPlate = toText(readValue(order, "vehicleLicensePlate", "VehicleLicensePlate")) || toText(readValue(order, "guestVehicleInfo", "GuestVehicleInfo"));
  if (orderPlate) return orderPlate;

  const vehicleId = toPositiveNumber(readValue(order, "vehicleId", "VehicleId"));
  if (!vehicleId) return "";

  const vehicle = vehicleMap.get(vehicleId);
  return toText(readValue(vehicle, "licensePlate", "LicensePlate", "plateNumber", "PlateNumber"));
}

export function resolveVehicleInfo(raw, order, vehicleMap, vehicleModelMap = new Map(), vehicleBrandMap = new Map()) {
  const vehicleId = toPositiveNumber(readValue(order, "vehicleId", "VehicleId"));
  const vehicle = vehicleId ? (vehicleMap.get(vehicleId) || null) : null;
  const vehicleModelId =
    toPositiveNumber(readValue(raw, "vehicleModelId", "VehicleModelId")) ||
    toPositiveNumber(readValue(order, "vehicleModelId", "VehicleModelId")) ||
    toPositiveNumber(readValue(vehicle, "modelId", "ModelId"));
  const vehicleModel = vehicleModelId ? (vehicleModelMap.get(vehicleModelId) || null) : null;
  const vehicleBrandId =
    toPositiveNumber(readValue(raw, "vehicleBrandId", "VehicleBrandId")) ||
    toPositiveNumber(readValue(order, "vehicleBrandId", "VehicleBrandId")) ||
    toPositiveNumber(readValue(vehicle, "brandId", "BrandId")) ||
    toPositiveNumber(readValue(vehicleModel, "brandId", "BrandId"));
  const vehicleBrand = vehicleBrandId ? (vehicleBrandMap.get(vehicleBrandId) || null) : null;

  const brandName =
    toText(readValue(raw, "vehicleBrandName", "VehicleBrandName")) ||
    toText(readValue(order, "vehicleBrandName", "VehicleBrandName", "guestVehicleBrandName", "GuestVehicleBrandName")) ||
    toText(readValue(vehicle, "brandName", "BrandName")) ||
    toText(readValue(vehicleBrand, "name", "Name", "brandName", "BrandName", "code", "Code"));

  const modelName =
    toText(readValue(raw, "vehicleModelName", "VehicleModelName")) ||
    toText(readValue(order, "vehicleModelName", "VehicleModelName", "guestVehicleModelName", "GuestVehicleModelName")) ||
    toText(readValue(vehicle, "modelName", "ModelName")) ||
    toText(readValue(vehicleModel, "modelName", "ModelName", "name", "Name"));

  const descriptorFromApi =
    toText(readValue(raw, "vehicleDescriptor", "VehicleDescriptor")) ||
    toText(readValue(order, "vehicleDescriptor", "VehicleDescriptor"));

  const vehicleDescriptor = descriptorFromApi || (brandName && modelName ? `${brandName} ${modelName}` : (modelName || brandName));

  const vehicleManufactureYear =
    toText(readValue(raw, "vehicleManufactureYear", "VehicleManufactureYear", "manufactureYear", "ManufactureYear")) ||
    toText(readValue(order, "vehicleManufactureYear", "VehicleManufactureYear", "manufactureYear", "ManufactureYear")) ||
    toText(readValue(vehicle, "manufactureYear", "ManufactureYear", "year", "Year"));

  const vehicleColor =
    toText(readValue(raw, "vehicleColor", "VehicleColor", "color", "Color")) ||
    toText(readValue(order, "vehicleColor", "VehicleColor", "color", "Color")) ||
    toText(readValue(vehicle, "color", "Color"));

  const vehicleVin =
    toText(readValue(raw, "vehicleVin", "VehicleVin", "vin", "Vin")) ||
    toText(readValue(order, "vehicleVin", "VehicleVin", "guestVehicleVin", "GuestVehicleVin")) ||
    toText(readValue(vehicle, "vin", "Vin"));

  return {
    vehicleBrandName: brandName,
    vehicleModelName: modelName,
    vehicleDescriptor,
    vehicleManufactureYear,
    vehicleColor,
    vehicleVin,
  };
}

export function getVehicleDisplayParts(item) {
  const vin = toText(item?.vehicleVin);
  return {
    plate: toText(item?.vehicleLicensePlate) || "--",
    descriptor: toText(item?.vehicleDescriptor) || "Chưa rõ dòng xe",
    manufactureYear: toText(item?.vehicleManufactureYear) || "--",
    color: toText(item?.vehicleColor) || "--",
    vin,
  };
}

export function extractAlertReasons(item) {
  const reasons = [];
  if (!item) return reasons;

  if (item.isSupportRequested) {
    reasons.push("Có yêu cầu hỗ trợ hội chẩn từ khoang");
  }

  if (item.isCancelPendingApproval) {
    reasons.push("Đang chờ quản đốc duyệt hủy/phân lại");
  }

  if (item.queueColor === "RED" && !item.technicianId && item.pendingMinutes >= 10) {
    reasons.push("Xe chờ nhận quá 10 phút");
  }

  if (item.queueColor === "RED" && item.technicianId && item.assignedMinutes >= 45) {
    reasons.push("Chẩn đoán đang vượt SLA 45 phút");
  }

  return reasons;
}

export function normalizeBoardItem(
  raw,
  orderMap,
  customerMap,
  vehicleMap,
  vehicleModelMap = new Map(),
  vehicleBrandMap = new Map(),
  teamMap = new Map(),
  bayMap = new Map()
) {
  const serviceOrderId = toPositiveNumber(readValue(raw, "serviceOrderId", "ServiceOrderId")) || 0;
  const order = orderMap.get(serviceOrderId) || null;
  const teamId = toPositiveNumber(readValue(raw, "teamId", "TeamId"));
  const teamName = toText(readValue(raw, "teamName", "TeamName"));
  const primaryTeamId = toPositiveNumber(readValue(raw, "primaryTeamId", "PrimaryTeamId")) || teamId;
  const primaryTeamName =
    toText(readValue(raw, "primaryTeamName", "PrimaryTeamName")) ||
    teamName ||
    (primaryTeamId ? toText(readValue(teamMap.get(primaryTeamId), "name", "Name")) : "");
  const rawAssignedTeamIds = Array.isArray(readValue(raw, "assignedTeamIds", "AssignedTeamIds"))
    ? readValue(raw, "assignedTeamIds", "AssignedTeamIds")
    : [];
  const assignedTeamIds = normalizeTeamIds(
    [primaryTeamId, teamId, ...rawAssignedTeamIds],
    null
  );
  const rawAssignedTeamNames = Array.isArray(readValue(raw, "assignedTeamNames", "AssignedTeamNames"))
    ? readValue(raw, "assignedTeamNames", "AssignedTeamNames")
    : [];
  const normalizedAssignedTeamNames = rawAssignedTeamNames
    .map((name) => toText(name))
    .filter(Boolean);
  const supportingAssignmentsFromApi = normalizeSupportingAssignmentsFromBoard(
    readValue(raw, "supportingAssignments", "SupportingAssignments"),
    primaryTeamId
  );
  const supportingTeamIds = supportingAssignmentsFromApi.length > 0
    ? normalizeTeamIds(supportingAssignmentsFromApi.map((item) => item.teamId), primaryTeamId)
    : normalizeTeamIds(assignedTeamIds, primaryTeamId);
  const supportingTeamNamesFromApi = normalizedAssignedTeamNames.filter(
    (name) => normalizeCode(name) !== normalizeCode(primaryTeamName)
  );
  const supportingTeamNamesFromAssignments = supportingAssignmentsFromApi
    .map((item) => toText(item.teamName))
    .filter(Boolean);
  const supportingTeamNamesFromMap = supportingTeamIds
    .map((id) => toText(readValue(teamMap.get(id), "name", "Name")))
    .filter(Boolean);
  const supportingTeamNames = supportingTeamNamesFromAssignments.length > 0
    ? supportingTeamNamesFromAssignments
    : (supportingTeamNamesFromApi.length > 0 ? supportingTeamNamesFromApi : supportingTeamNamesFromMap);
  const supportingAssignments = supportingAssignmentsFromApi.length > 0
    ? supportingAssignmentsFromApi
    : supportingTeamIds.map((id) => ({
      teamId: id,
      teamName: toText(readValue(teamMap.get(id), "name", "Name")),
      technicianId: null,
      technicianName: "",
      assistantTechnicianId: null,
      assistantTechnicianName: "",
    }));
  const technicianId = toPositiveNumber(readValue(raw, "technicianId", "TechnicianId"));
  const technicianName = toText(readValue(raw, "technicianName", "TechnicianName"));
  const assistantTechnicianId = toPositiveNumber(readValue(raw, "assistantTechnicianId", "AssistantTechnicianId"));
  const assistantTechnicianName = toText(readValue(raw, "assistantTechnicianName", "AssistantTechnicianName"));
  const assignedBayId = toPositiveNumber(readValue(raw, "bayId", "BayId", "assignedBayId", "AssignedBayId"));
  const assignedBayFromMap = assignedBayId > 0 ? bayMap.get(assignedBayId) : null;
  const assignedBayCode = toText(readValue(raw, "bayCode", "BayCode", "assignedBayCode", "AssignedBayCode")) ||
    toText(readValue(assignedBayFromMap, "code", "Code", "name", "Name"));
  const assignedAt = readValue(raw, "assignedAt", "AssignedAt");
  const startedAt = readValue(raw, "technicianAcceptedAt", "TechnicianAcceptedAt", "startedAt", "StartedAt");
  const technicianCompletedAt = readValue(raw, "technicianCompletedAt", "TechnicianCompletedAt");
  const customerNotesRaw = toText(readValue(order, "customerNotes", "CustomerNotes"));
  const parsedCustomerNotes = parseCustomerNotes(customerNotesRaw);
  const vehicleInfo = resolveVehicleInfo(raw, order, vehicleMap, vehicleModelMap, vehicleBrandMap);

  // Guard rail: if no real assignment signal exists, keep assignee fields empty.
  const hasRealAssignment = Boolean(primaryTeamId || assignedAt || startedAt || assignedBayId);
  const hasTechnicianDisplayContext = Boolean(
    assignedAt ||
    startedAt ||
    readValue(raw, "technicalApprovedAt", "TechnicalApprovedAt") ||
    readValue(raw, "finalApprovedAt", "FinalApprovedAt")
  );
  const hasLeadTechnicianAssigned = Boolean(technicianId) && hasTechnicianDisplayContext;
  const hasAssistantTechnicianAssigned = Boolean(assistantTechnicianId) && hasTechnicianDisplayContext;

  return {
    serviceOrderId,
    executeBranchId:
      toPositiveNumber(readValue(raw, "executeBranchId", "ExecuteBranchId")) ||
      toPositiveNumber(readValue(order, "executeBranchId", "ExecuteBranchId")) ||
      null,
    serviceOrderCode:
      toText(readValue(raw, "serviceOrderCode", "ServiceOrderCode")) ||
      toText(readValue(order, "code", "Code")) ||
      `SO-${serviceOrderId}`,
    serviceOrderStatus: toText(readValue(raw, "serviceOrderStatus", "ServiceOrderStatus")) || toText(readValue(order, "status", "Status")),
    queueColor: normalizeCode(readValue(raw, "queueColor", "QueueColor")) || "WHITE",
    queueState: toText(readValue(raw, "queueState", "QueueState")),
    dispatchTag: normalizeCode(readValue(raw, "dispatchTag", "DispatchTag")),
    teamId: hasRealAssignment ? primaryTeamId : null,
    teamName: hasRealAssignment ? primaryTeamName : "",
    primaryTeamId: hasRealAssignment ? primaryTeamId : null,
    primaryTeamName: hasRealAssignment ? primaryTeamName : "",
    assignedTeamIds: hasRealAssignment ? assignedTeamIds : [],
    supportingTeamIds: hasRealAssignment ? supportingTeamIds : [],
    supportingTeamNames: hasRealAssignment ? supportingTeamNames : [],
    supportingAssignments: hasRealAssignment ? supportingAssignments : [],
    technicianId: hasRealAssignment && hasLeadTechnicianAssigned ? technicianId : null,
    technicianName: hasRealAssignment && hasLeadTechnicianAssigned ? technicianName : "",
    assistantTechnicianId: hasRealAssignment && hasAssistantTechnicianAssigned ? assistantTechnicianId : null,
    assistantTechnicianName: hasRealAssignment && hasAssistantTechnicianAssigned ? assistantTechnicianName : "",
    assignedBayId: hasRealAssignment ? assignedBayId : null,
    assignedBayCode: hasRealAssignment ? assignedBayCode : "",
    requestedAt: readValue(raw, "requestedAt", "RequestedAt"),
    assignedAt: hasRealAssignment ? assignedAt : null,
    startedAt: hasRealAssignment ? startedAt : null,
    technicianAcceptedAt: hasRealAssignment ? startedAt : null,
    technicianCompletedAt: hasRealAssignment ? technicianCompletedAt : null,
    pendingMinutes: Number(readValue(raw, "pendingMinutes", "PendingMinutes") || 0),
    assignedMinutes: Number(readValue(raw, "assignedMinutes", "AssignedMinutes") || 0),
    isSupportRequested: Boolean(readValue(raw, "isSupportRequested", "IsSupportRequested")),
    supportRequestedAt: readValue(raw, "supportRequestedAt", "SupportRequestedAt"),
    supportRequestedBy: toPositiveNumber(readValue(raw, "supportRequestedBy", "SupportRequestedBy")),
    supportRequestNote: toText(readValue(raw, "supportRequestNote", "SupportRequestNote")),
    isCancelPendingApproval: Boolean(readValue(raw, "isCancelPendingApproval", "IsCancelPendingApproval")),
    cancelReason: toText(readValue(raw, "cancelReason", "CancelReason")),
    technicalApprovedByTeamLeaderId: toPositiveNumber(readValue(raw, "technicalApprovedByTeamLeaderId", "TechnicalApprovedByTeamLeaderId")),
    technicalApprovedAt: readValue(raw, "technicalApprovedAt", "TechnicalApprovedAt"),
    approvedByTeamLeaderAt: readValue(raw, "approvedByTeamLeaderAt", "ApprovedByTeamLeaderAt", "finalApprovedAt", "FinalApprovedAt"),
    finalApprovedAt: readValue(raw, "finalApprovedAt", "FinalApprovedAt", "approvedByTeamLeaderAt", "ApprovedByTeamLeaderAt"),
    teamLeaderApprovalStatus: toText(readValue(raw, "teamLeaderApprovalStatus", "TeamLeaderApprovalStatus")),
    foremanApprovalStatus: toText(readValue(raw, "foremanApprovalStatus", "ForemanApprovalStatus")),
    workflowStatus: toText(readValue(raw, "workflowStatus", "WorkflowStatus")),
    dispatchBucket: toText(readValue(raw, "dispatchBucket", "DispatchBucket")),
    teamLeaderNote: toText(readValue(raw, "teamLeaderNote", "TeamLeaderNote")),
    foremanNote: toText(readValue(raw, "foremanNote", "ForemanNote")),
    customerName: resolveCustomerName(raw, order, customerMap),
    vehicleLicensePlate: resolveVehiclePlate(raw, order, vehicleMap),
    vehicleBrandName: vehicleInfo.vehicleBrandName,
    vehicleModelName: vehicleInfo.vehicleModelName,
    vehicleDescriptor: vehicleInfo.vehicleDescriptor,
    vehicleManufactureYear: vehicleInfo.vehicleManufactureYear,
    vehicleColor: vehicleInfo.vehicleColor,
    vehicleVin: vehicleInfo.vehicleVin,
    customerNotes: customerNotesRaw,
    customerMainNotes: toText(parsedCustomerNotes?.mainNotes),
    customerRequestedServices: toText(parsedCustomerNotes?.requestedServices),
  };
}
