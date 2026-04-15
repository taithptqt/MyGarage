import React, { useCallback, useEffect, useMemo, useRef, useState } from "react";
import {
  Alert,
  AlertIcon,
  Badge,
  Box,
  Button,
  Divider,
  FormControl,
  FormLabel,
  Grid,
  GridItem,
  Heading,
  HStack,
  Input,
  Modal,
  ModalBody,
  ModalCloseButton,
  ModalContent,
  ModalFooter,
  ModalHeader,
  ModalOverlay,
  SimpleGrid,
  Spinner,
  Stack,
  Text,
  Textarea,
  VStack,
  useToast,
} from "@chakra-ui/react";
import { ArrowBackIcon } from "@chakra-ui/icons";
import { keyframes } from "@emotion/react";
import { serviceOrderApi } from "@features/workshop/api/serviceOrderApi";
import { getAllRoles, getAllStaff } from "@features/admin/api/staffApi";
import { getAllCustomers, getAllVehicles } from "@features/admin/api/customerApi";
import { getAllVehicleBrands, getAllVehicleModels } from "@features/admin/api/vehicleCatalogApi";
import { parseCustomerNotes } from "@features/workshop/utils/serviceOrderDetailUtils";
import FilterDropdown from "@shared/components/FilterDropdown";
import { useAuth } from "@shared/core/context/AuthContext";
import { useBranch } from "@shared/core/context/BranchContext";
import { useSearch } from "@shared/core/context/SearchContext";

const COLUMN_ORDER = ["WHITE", "YELLOW", "RED", "GREEN"];

const COLOR_META = {
  NEEDS_APPROVAL: {
    label: "Cần duyệt",
    description: "Phiếu tổ trưởng đã duyệt hoặc phiếu cờ đỏ quá SLA",
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
    label: "Đã chuẩn đoán",
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

const STATUS_CARD_THEME = {
  NEEDS_APPROVAL: {
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

const DISPATCH_TAG_OPTIONS = [
  { value: "ENGINE_CHASSIS", label: "Máy - Gầm" },
  { value: "ELECTRICAL_AC", label: "Điện - Máy lạnh" },
  { value: "BODY_PAINT", label: "Đồng sơn - Chăm sóc xe" },
  { value: "MAINTENANCE", label: "Bảo dưỡng nhanh" },
];

const DISPATCH_TAG_LABELS = DISPATCH_TAG_OPTIONS.reduce((acc, item) => {
  acc[item.value] = item.label;
  return acc;
}, {});

const CANCEL_REASONS = ["Sai chuyên môn", "Quá tải khoang", "Thiếu thiết bị", "Khác"];

const TEAM_LEADER_ROLE_CODES = new Set(["TEAM_LEADER"]);
const TECHNICIAN_ROLE_CODES = new Set(["TECH", "TECHNICIAN", "TEAM_LEADER", "FOREMAN"]);

function toText(value) {
  if (value === null || value === undefined) return "";
  return String(value).trim();
}

function readValue(source, ...keys) {
  for (const key of keys) {
    const value = source?.[key];
    if (value !== null && value !== undefined) return value;
  }
  return null;
}

function toPositiveNumber(value) {
  const parsed = Number(value);
  return Number.isFinite(parsed) && parsed > 0 ? parsed : null;
}

function normalizeCode(value) {
  return toText(value).toUpperCase();
}

function resolveDispatchTagLabel(tag, fallbackTag = "") {
  const normalizedTag = normalizeCode(tag);
  if (normalizedTag) return getDispatchTagLabel(normalizedTag);

  const normalizedFallback = normalizeCode(fallbackTag);
  if (normalizedFallback) return `${getDispatchTagLabel(normalizedFallback)} (chuyên môn tổ)`;

  return getDispatchTagLabel(tag);
}

function formatDateTime(value) {
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

function formatMinutes(value) {
  const minutes = Number(value || 0);
  if (!Number.isFinite(minutes) || minutes <= 0) return "0 phút";
  if (minutes < 60) return `${Math.floor(minutes)} phút`;
  const hours = Math.floor(minutes / 60);
  const remain = Math.floor(minutes % 60);
  return remain > 0 ? `${hours} giờ ${remain} phút` : `${hours} giờ`;
}

function getDispatchTagLabel(tag) {
  const normalized = normalizeCode(tag);
  if (!normalized) return "Chưa chọn chuyên môn";

  const splitTags = normalized
    .split(/[;,|/]/)
    .map((item) => normalizeCode(item))
    .filter(Boolean);

  const uniqueTags = splitTags.length > 0 ? Array.from(new Set(splitTags)) : [normalized];
  return uniqueTags.map((code) => DISPATCH_TAG_LABELS[code] || code).join(" + ");
}

function dispatchTagIncludes(tagSource, targetTag) {
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

function normalizeTeamIds(ids, excludedId = null) {
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

function normalizeSupportingAssignmentMap(rawMap) {
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

function buildSupportingAssignmentsPayload(teamIds, assignmentMap) {
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

function normalizeSupportRequestMap(rawMap) {
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

function buildSupportRequestAssignmentsPayload(teamIds, requestMap) {
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

function normalizeSupportingAssignmentsFromBoard(rawAssignments, primaryTeamId = null) {
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

function extractRequestedServiceNames(details) {
  if (!Array.isArray(details) || details.length === 0) return "";

  const names = [];
  const seen = new Set();

  details.forEach((detail) => {
    if (readValue(detail, "isDeleted", "IsDeleted") === true) return;

    const recommendationId = toPositiveNumber(readValue(detail, "diagnoseRecommendationId", "DiagnoseRecommendationId"));
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

function normalizeServiceOrderDetailsResponse(response) {
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

function normalizeDiagnosisResponse(response) {
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

function isDiagnosisCompletedItem(item) {
  if (!item) return false;
  const normalizedState = normalizeCode(item.queueState);
  if (["DIAGNOSED", "COMPLETED", "DONE"].includes(normalizedState)) return true;
  if (normalizeCode(item.queueColor) === "GREEN") return true;
  return Boolean(item.technicalApprovedByTeamLeaderId || item.approvedByTeamLeaderAt);
}

function isForemanMyTaskItem(item) {
  if (!item) return false;
  const isOverSlaRed = normalizeCode(item.queueColor) === "RED";
  const isTeamLeaderApproved = Boolean(item.technicalApprovedAt || item.technicalApprovedByTeamLeaderId);
  return isOverSlaRed || isTeamLeaderApproved;
}

function resolveStaffNameById(staffs, id) {
  const staffId = toPositiveNumber(id);
  if (!staffId || !Array.isArray(staffs)) return "";
  const match = staffs.find((staff) => toPositiveNumber(readValue(staff, "id", "Id")) === staffId);
  return toText(readValue(match, "fullName", "FullName"));
}

const cancelAttentionPulse = keyframes`
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

const QUEUE_STATE_META = {
  NEW: { label: "Mới tiếp nhận", colorScheme: "gray" },
  IN_PROGRESS: { label: "Đang chẩn đoán", colorScheme: "yellow" },
  ALERT: { label: "Cần can thiệp", colorScheme: "red" },
  DIAGNOSED: { label: "Đã chuẩn đoán", colorScheme: "green" },
};

function getQueueStateMeta(queueState, queueColor) {
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

function resolveApiErrorMessage(error, fallback = "Đã có lỗi xảy ra") {
  const message = toText(readValue(error, "response")?.data?.message);
  if (message) return message;
  const raw = toText(error?.message);
  if (raw) return raw;
  return fallback;
}

function isOperationalBayAvailable(operationalStatus) {
  const normalized = normalizeCode(operationalStatus);
  if (!normalized) return true;
  return !["IN_USE", "BROKEN", "UNDER_MAINTENANCE", "MAINTENANCE", "SUSPENDED"].includes(normalized);
}

function getStaffRoleCode(staff, roleById) {
  const directRoleCode = normalizeCode(readValue(staff, "roleCode", "RoleCode"));
  if (directRoleCode) return directRoleCode;

  const roleId = toPositiveNumber(readValue(staff, "roleId", "RoleId"));
  if (!roleId) return "";

  const role = roleById.get(roleId);
  return normalizeCode(readValue(role, "roleCode", "RoleCode"));
}
function isTechnicianCandidate(staff, roleById) {
  const roleCode = getStaffRoleCode(staff, roleById);
  const roleName = toText(readValue(staff, "roleName", "RoleName")).toLowerCase();

  if (TECHNICIAN_ROLE_CODES.has(roleCode)) return true;
  return roleName.includes("kỹ thuật") || roleName.includes("ky thuat") || roleName.includes("tổ trưởng") || roleName.includes("to truong");
}

function isTeamLeader(staff, roleById) {
  const roleCode = getStaffRoleCode(staff, roleById);
  return TEAM_LEADER_ROLE_CODES.has(roleCode);
}

function isStaffActive(staff) {
  if (readValue(staff, "isDeleted", "IsDeleted") === true) return false;
  const status = normalizeCode(readValue(staff, "status", "Status"));
  if (!status) return true;
  return status !== "INACTIVE";
}

function sortStaffByName(list) {
  return [...list].sort((a, b) => {
    const aName = toText(readValue(a, "fullName", "FullName"));
    const bName = toText(readValue(b, "fullName", "FullName"));
    return aName.localeCompare(bName, "vi");
  });
}

function resolveCurrentStaff(staffs, user) {
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

function resolveCustomerName(raw, order, customerMap) {
  const boardName = toText(readValue(raw, "customerName", "CustomerName"));
  if (boardName) return boardName;

  const orderName = toText(readValue(order, "customerName", "CustomerName")) || toText(readValue(order, "guestName", "GuestName"));
  if (orderName) return orderName;

  const customerId = toPositiveNumber(readValue(order, "customerId", "CustomerId"));
  if (!customerId) return "";

  const customer = customerMap.get(customerId);
  return toText(readValue(customer, "fullName", "FullName", "name", "Name"));
}

function resolveVehiclePlate(raw, order, vehicleMap) {
  const boardPlate = toText(readValue(raw, "vehicleLicensePlate", "VehicleLicensePlate"));
  if (boardPlate) return boardPlate;

  const orderPlate = toText(readValue(order, "vehicleLicensePlate", "VehicleLicensePlate")) || toText(readValue(order, "guestVehicleInfo", "GuestVehicleInfo"));
  if (orderPlate) return orderPlate;

  const vehicleId = toPositiveNumber(readValue(order, "vehicleId", "VehicleId"));
  if (!vehicleId) return "";

  const vehicle = vehicleMap.get(vehicleId);
  return toText(readValue(vehicle, "licensePlate", "LicensePlate", "plateNumber", "PlateNumber"));
}

function resolveVehicleInfo(raw, order, vehicleMap, vehicleModelMap = new Map(), vehicleBrandMap = new Map()) {
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

function getVehicleDisplayParts(item) {
  const vin = toText(item?.vehicleVin);
  return {
    plate: toText(item?.vehicleLicensePlate) || "--",
    descriptor: toText(item?.vehicleDescriptor) || "Chưa rõ dòng xe",
    manufactureYear: toText(item?.vehicleManufactureYear) || "--",
    color: toText(item?.vehicleColor) || "--",
    vin,
  };
}

function extractAlertReasons(item) {
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
    reasons.push("Chuẩn đoán đang vượt SLA 45 phút");
  }

  return reasons;
}

function normalizeBoardItem(raw, orderMap, customerMap, vehicleMap, vehicleModelMap = new Map(), vehicleBrandMap = new Map(), teamMap = new Map()) {
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
  const assignedBayCode = toText(readValue(raw, "bayCode", "BayCode", "assignedBayCode", "AssignedBayCode"));
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
function DispatchQueueCard({
  item,
  isSelected,
  onSelect,
  onViewCancelRequest,
  onViewQuote,
  dispatchTagLabel = "",
}) {
  const meta = COLOR_META[item.queueColor] || COLOR_META.WHITE;
  const alerts = extractAlertReasons(item);
  const queueStateMeta = getQueueStateMeta(item.queueState, item.queueColor);
  const vehicle = getVehicleDisplayParts(item);
  const canViewCancelRequest = Boolean(item?.isCancelPendingApproval) && typeof onViewCancelRequest === "function";
  const canViewQuote = isDiagnosisCompletedItem(item) && typeof onViewQuote === "function";
  const statusBorderByColor = {
    WHITE: "#cbd5e1",
    YELLOW: "#f6d365",
    RED: "#fca5a5",
    GREEN: "#86efac",
  };
  const baseBorderColor = statusBorderByColor[item.queueColor] || "#dbe4f2";

  return (
    <Box
      border="2px solid"
      borderColor={isSelected ? meta.activeBg : baseBorderColor}
      borderLeftWidth="6px"
      borderLeftColor={meta.cardAccent}
      borderRadius="xl"
      bg={isSelected ? "#f8fbff" : "white"}
      p={3.5}
      cursor="pointer"
      transition="all 0.18s ease"
      boxShadow={isSelected ? "0 12px 26px rgba(37, 99, 235, 0.16)" : "0 4px 12px rgba(15,23,42,0.06)"}
      _hover={{ borderColor: meta.activeBg, transform: "translateY(-1px)" }}
      onClick={() => onSelect(item.serviceOrderId)}
    >
      <Stack spacing={3}>
        <HStack justify="space-between" align="start" spacing={3}>
          <Box minW={0}>
            <Text fontWeight="800" color="gray.800" noOfLines={1}>
              {item.serviceOrderCode}
            </Text>
            <Text fontSize="sm" color="gray.600" noOfLines={1}>
              {item.customerName || "Khách hàng chưa rõ"}
            </Text>
            <HStack mt={1} spacing={1.5} flexWrap="wrap">
              <Badge colorScheme="blue" variant="subtle" borderRadius="full" px={2.5}>
                Biển số: {vehicle.plate}
              </Badge>
              <Badge colorScheme="gray" variant="subtle" borderRadius="full" px={2.5}>
                Dòng xe: {vehicle.descriptor}
              </Badge>
            </HStack>
            <Text mt={1} fontSize="sm" color="gray.700" fontWeight="600" noOfLines={1}>
              Năm SX: {vehicle.manufactureYear} | Màu: {vehicle.color}
            </Text>
            {vehicle.vin ? (
              <Text mt={1} fontSize="sm" color="gray.700" fontWeight="600" noOfLines={1}>
                VIN: {vehicle.vin}
              </Text>
            ) : null}
          </Box>

          <Badge colorScheme={queueStateMeta.colorScheme} borderRadius="full" px={2.5} py={1}>
            {queueStateMeta.label}
          </Badge>
        </HStack>

        <HStack spacing={2} flexWrap="wrap">
          <Badge colorScheme="purple" borderRadius="full" px={2.5}>
            Chuyên môn: {dispatchTagLabel || getDispatchTagLabel(item.dispatchTag)}
          </Badge>
          <Badge colorScheme="gray" borderRadius="full" px={2.5}>
            Chờ nhận: {formatMinutes(item.pendingMinutes)}
          </Badge>
          <Badge colorScheme="orange" borderRadius="full" px={2.5}>
            Đang xử lý: {formatMinutes(item.assignedMinutes)}
          </Badge>
        </HStack>

        <Grid templateColumns={{ base: "1fr", md: "1fr 1fr" }} gap={2}>
          <Box p={2} border="1px solid" borderColor="gray.100" borderRadius="md" bg="white">
            <Text fontSize="xs" color="gray.500" textTransform="uppercase" fontWeight="700">
              Nhân sự
            </Text>
            <Text fontSize="sm" color="gray.600" noOfLines={1}>
              Tổ chính: {item.primaryTeamName || item.teamName || "Chưa giao tổ"}
            </Text>
            {Array.isArray(item.supportingTeamNames) && item.supportingTeamNames.length > 0 && (
              <Text fontSize="sm" color="gray.600" noOfLines={2}>
                Tổ phối hợp: {item.supportingTeamNames.join(", ")}
              </Text>
            )}
            <Text fontSize="sm" color="gray.600" noOfLines={1}>
              Thợ chính: {item.technicianName || ""}
            </Text>
            <Text fontSize="sm" color="gray.600" noOfLines={1}>
              Thợ phụ: {item.assistantTechnicianName || ""}
            </Text>
          </Box>
          <Box p={2} border="1px solid" borderColor="gray.100" borderRadius="md" bg="white">
            <Text fontSize="xs" color="gray.500" textTransform="uppercase" fontWeight="700">
              Tiến độ
            </Text>
            <Text fontSize="sm" color="gray.600" noOfLines={1}>
              Khoang: {item.assignedBayCode || "Chưa chỉ định"}
            </Text>
            <Text fontSize="sm" color="gray.600" noOfLines={1}>
              Yêu cầu lúc: {formatDateTime(item.requestedAt)}
            </Text>
          </Box>
        </Grid>

        {alerts.length > 0 && (
          <Box border="1px solid" borderColor="red.200" bg="red.50" borderRadius="md" p={2}>
            <Text fontSize="xs" color="red.700" textTransform="uppercase" fontWeight="700" mb={1}>
              Cần can thiệp
            </Text>
            <VStack align="start" spacing={1}>
              {alerts.slice(0, 2).map((reason) => (
                <Text key={`${item.serviceOrderId}-${reason}`} fontSize="sm" color="red.600" noOfLines={2}>
                  - {reason}
                </Text>
              ))}
            </VStack>
          </Box>
        )}

        {(canViewCancelRequest || canViewQuote) && (
          <HStack justify="flex-end" spacing={2} pt={1} borderTop="1px solid" borderColor="gray.100">
            {canViewCancelRequest && (
              <Button
                size="sm"
                colorScheme="red"
                variant="outline"
                borderRadius="full"
                fontWeight="700"
                animation={`${cancelAttentionPulse} 1.8s ease-in-out infinite`}
                onClick={(event) => {
                  event.stopPropagation();
                  onViewCancelRequest(item);
                }}
              >
                Xem yêu cầu hủy
              </Button>
            )}
            {canViewQuote && (
              <Button
                size="sm"
                colorScheme="blue"
                variant="solid"
                borderRadius="full"
                fontWeight="700"
                onClick={(event) => {
                  event.stopPropagation();
                  onViewQuote(item);
                }}
              >
                Xem chuẩn đoán
              </Button>
            )}
          </HStack>
        )}
      </Stack>
    </Box>
  );
}

function SuggestionPreview({ suggestion }) {
  if (!suggestion) return null;

  const loads = Array.isArray(suggestion.technicianLoads) ? suggestion.technicianLoads : [];
  return (
    <Box border="1px solid" borderColor="blue.100" borderRadius="xl" bg="#f8fbff" p={3}>
      <Text fontSize="sm" fontWeight="700" color="blue.700" mb={2}>
        Gợi ý phân công tự động
      </Text>
      <Stack spacing={1} fontSize="sm" color="gray.700">
        <Text>Thợ chính gợi ý: {suggestion.suggestedLeadTechnicianName || "--"}</Text>
        <Text>Thợ phụ gợi ý: {suggestion.suggestedAssistantTechnicianName || "--"}</Text>
        <Text>Khoang gợi ý: {suggestion.suggestedBayCode || "--"}</Text>
      </Stack>
      {loads.length > 0 && (
        <Box mt={3}>
          <Text fontSize="xs" color="gray.500" mb={1}>
            Tải hiện tại theo tổ
          </Text>
          <VStack align="stretch" spacing={1}>
            {loads.slice(0, 5).map((row) => (
              <HStack key={row.technicianId} justify="space-between" fontSize="xs" color="gray.600">
                <Text noOfLines={1}>{row.technicianName || `KTV #${row.technicianId}`}</Text>
                <Badge colorScheme="gray" borderRadius="full">
                  {row.activeDiagnosisCount || 0} ca
                </Badge>
              </HStack>
            ))}
          </VStack>
        </Box>
      )}
    </Box>
  );
}

export default function DispatchOpsDashboard({
  mode = "manager",
  pageMode = "board",
  assignmentServiceOrderId = null,
  assignmentFocus = "",
  onOpenAssignment = null,
  onOpenQuote = null,
  onBack = null,
}) {
  const toast = useToast();
  const { user } = useAuth();
  const { selectedBranchId } = useBranch();
  const { searchQuery } = useSearch();

  const isTeamLeaderMode = mode === "team_leader";
  const isManagerMode = mode === "manager";
  const useManagerStyleStatusCards = isManagerMode || isTeamLeaderMode;
  const isAssignmentPage = pageMode === "assignment";

  const [boardItems, setBoardItems] = useState([]);
  const [teams, setTeams] = useState([]);
  const [staffs, setStaffs] = useState([]);
  const [roles, setRoles] = useState([]);
  const [bays, setBays] = useState([]);

  const [loading, setLoading] = useState(false);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState("");

  const [teamFilter, setTeamFilter] = useState("");
  const [dispatchTagFilter, setDispatchTagFilter] = useState("");
  const [activeColorTab, setActiveColorTab] = useState(useManagerStyleStatusCards ? "ALL_ITEMS" : "WHITE");
  const [teamLeaderBoardTab, setTeamLeaderBoardTab] = useState("dispatch");
  const [selectedServiceOrderId, setSelectedServiceOrderId] = useState(
    toPositiveNumber(assignmentServiceOrderId)
  );

  const [leaderTeamId, setLeaderTeamId] = useState("");
  const [leaderLeadTechnicianId, setLeaderLeadTechnicianId] = useState("");
  const [leaderAssistantTechnicianId, setLeaderAssistantTechnicianId] = useState("");
  const [leaderBayId, setLeaderBayId] = useState("");
  const [leaderSupportTeamIds, setLeaderSupportTeamIds] = useState([]);
  const [leaderSupportRequestMap, setLeaderSupportRequestMap] = useState({});
  const [leaderSupportNote, setLeaderSupportNote] = useState("");
  const [isLeaderSupportSectionExpanded, setIsLeaderSupportSectionExpanded] = useState(false);
  const [leaderSupportResponseLeadTechnicianId, setLeaderSupportResponseLeadTechnicianId] = useState("");
  const [leaderSupportResponseAssistantTechnicianId, setLeaderSupportResponseAssistantTechnicianId] = useState("");
  const [leaderSupportResponseNote, setLeaderSupportResponseNote] = useState("");
  const [leaderCancelReason, setLeaderCancelReason] = useState(CANCEL_REASONS[0]);
  const [leaderCancelNote, setLeaderCancelNote] = useState("");

  const [managerTeamId, setManagerTeamId] = useState("");
  const [managerSupportingTeamIds, setManagerSupportingTeamIds] = useState([]);
  const [managerSupportingAssignmentMap, setManagerSupportingAssignmentMap] = useState({});
  const [managerLeadTechnicianId, setManagerLeadTechnicianId] = useState("");
  const [managerAssistantTechnicianId, setManagerAssistantTechnicianId] = useState("");
  const [managerBayId, setManagerBayId] = useState("");
  const [managerOverrideReason, setManagerOverrideReason] = useState("");
  const [managerDecisionNote, setManagerDecisionNote] = useState("");
  const [managerCancelDecisionModalOpen, setManagerCancelDecisionModalOpen] = useState(false);
  const [isManagerSupportSectionExpanded, setIsManagerSupportSectionExpanded] = useState(false);

  const [suggestion, setSuggestion] = useState(null);
  const [suggestionLoading, setSuggestionLoading] = useState(false);
  const [selectedRequestedServices, setSelectedRequestedServices] = useState("");
  const [selectedRequestedServicesLoading, setSelectedRequestedServicesLoading] = useState(false);
  const [cancelRequestDetail, setCancelRequestDetail] = useState(null);
  const [cancelRequestDetailLoading, setCancelRequestDetailLoading] = useState(false);
  const cancelRequestSectionRef = useRef(null);

  const selectedBranchRaw = Number(selectedBranchId);
  const isAllBranchesSelected = Number.isFinite(selectedBranchRaw) && selectedBranchRaw === 0;
  const selectedBranch = toPositiveNumber(selectedBranchRaw);
  const userBranchId = toPositiveNumber(readValue(user, "branchId", "BranchId"));
  const forceCurrentBranch = isManagerMode && Boolean(userBranchId);
  const branchId = forceCurrentBranch
    ? userBranchId
    : (isAllBranchesSelected ? null : selectedBranch || userBranchId);

  const roleById = useMemo(() => {
    const map = new Map();
    (roles || []).forEach((role) => {
      const id = toPositiveNumber(readValue(role, "id", "Id"));
      if (id) map.set(id, role);
    });
    return map;
  }, [roles]);

  const currentStaff = useMemo(() => resolveCurrentStaff(staffs, user), [staffs, user]);
  const currentStaffId = toPositiveNumber(readValue(currentStaff, "id", "Id")) || toPositiveNumber(readValue(user, "id", "Id"));
  const currentStaffTeamId = toPositiveNumber(readValue(currentStaff, "teamId", "TeamId"));

  const activeTeams = useMemo(() => {
    const list = (Array.isArray(teams) ? teams : []).filter((team) => {
      if (readValue(team, "isDeleted", "IsDeleted") === true) return false;
      const teamBranchId = toPositiveNumber(readValue(team, "branchId", "BranchId"));
      if (!branchId) return true;
      return teamBranchId === branchId;
    });

    return [...list].sort((a, b) => {
      const aName = toText(readValue(a, "name", "Name"));
      const bName = toText(readValue(b, "name", "Name"));
      return aName.localeCompare(bName, "vi");
    });
  }, [teams, branchId]);
  const currentTeam = useMemo(() => {
    if (!currentStaffTeamId) return null;
    return activeTeams.find((team) => toPositiveNumber(readValue(team, "id", "Id")) === currentStaffTeamId) || null;
  }, [activeTeams, currentStaffTeamId]);

  const currentTeamSpecialty = normalizeCode(readValue(currentTeam, "specialtyTag", "SpecialtyTag"));
  const currentTeamSpecialtyLabel = resolveDispatchTagLabel(currentTeamSpecialty);

  const activeTechnicians = useMemo(() => {
    const filtered = (Array.isArray(staffs) ? staffs : []).filter((staff) => {
      if (!isStaffActive(staff)) return false;
      if (!isTechnicianCandidate(staff, roleById)) return false;

      const staffBranchId = toPositiveNumber(readValue(staff, "branchId", "BranchId"));
      if (!branchId) return true;
      return staffBranchId === branchId;
    });

    return sortStaffByName(filtered);
  }, [staffs, roleById, branchId]);

  const selectedOrderBranchId = useMemo(() => {
    const targetOrderId = toPositiveNumber(selectedServiceOrderId);
    if (!targetOrderId) return null;
    const matched = boardItems.find((item) => item.serviceOrderId === targetOrderId);
    return toPositiveNumber(matched?.executeBranchId);
  }, [selectedServiceOrderId, boardItems]);

  const assignmentBranchId = selectedOrderBranchId || branchId || userBranchId || null;

  const availableBays = useMemo(() => {
    const targetBranchId = isAssignmentPage ? assignmentBranchId : branchId;
    const sorted = (Array.isArray(bays) ? bays : [])
      .filter((bay) => {
        if (readValue(bay, "isDeleted", "IsDeleted") === true) return false;

        const isActive = readValue(bay, "isActive", "IsActive");
        if (isActive === false) return false;

        const bayBranchId = toPositiveNumber(readValue(bay, "branchId", "BranchId"));
        if (!targetBranchId) return true;
        return bayBranchId === targetBranchId;
      })
      .sort((a, b) => {
        const aOccupied = Boolean(readValue(a, "currentOccupiedVehicleId", "CurrentOccupiedVehicleId"));
        const bOccupied = Boolean(readValue(b, "currentOccupiedVehicleId", "CurrentOccupiedVehicleId"));
        if (aOccupied !== bOccupied) return Number(aOccupied) - Number(bOccupied);

        const aCode = toText(readValue(a, "code", "Code", "name", "Name"));
        const bCode = toText(readValue(b, "code", "Code", "name", "Name"));
        return aCode.localeCompare(bCode, "vi");
      });

    return sorted;
  }, [bays, branchId, isAssignmentPage, assignmentBranchId]);

  const getTechniciansByTeam = useCallback(
    (targetTeamId) => {
      const parsedTeamId = toPositiveNumber(targetTeamId);
      if (!parsedTeamId) return activeTechnicians;
      return activeTechnicians.filter((staff) => toPositiveNumber(readValue(staff, "teamId", "TeamId")) === parsedTeamId);
    },
    [activeTechnicians]
  );

  const leaderTeamIdForAssignment = toPositiveNumber(leaderTeamId) || currentStaffTeamId;
  const hasLeaderTeamSelected = Boolean(leaderTeamIdForAssignment);
  const hasManagerTeamSelected = Boolean(toPositiveNumber(managerTeamId));

  const leaderTechnicianOptions = useMemo(() => getTechniciansByTeam(leaderTeamIdForAssignment), [getTechniciansByTeam, leaderTeamIdForAssignment]);
  const managerTechnicianOptions = useMemo(() => getTechniciansByTeam(managerTeamId), [getTechniciansByTeam, managerTeamId]);

  const assignmentTeamBranchId = assignmentBranchId;
  const scopedTeamsForAssignment = useMemo(() => {
    if (!assignmentTeamBranchId) return activeTeams;
    return activeTeams.filter(
      (team) => toPositiveNumber(readValue(team, "branchId", "BranchId")) === assignmentTeamBranchId
    );
  }, [activeTeams, assignmentTeamBranchId]);

  const assignmentTeamOptions = useMemo(() => {
    const options = [{ value: "", label: "-- Chọn tổ --" }];
    scopedTeamsForAssignment.forEach((team) => {
      const id = toPositiveNumber(readValue(team, "id", "Id"));
      const name = toText(readValue(team, "name", "Name"));
      if (!id) return;
      options.push({ value: String(id), label: name || `Tổ #${id}` });
    });
    return options;
  }, [scopedTeamsForAssignment]);

  const leaderSupportingTeamOptions = useMemo(() => {
    const primaryTeamId = toPositiveNumber(leaderTeamId);
    return scopedTeamsForAssignment
      .map((team) => {
        const id = toPositiveNumber(readValue(team, "id", "Id"));
        const name = toText(readValue(team, "name", "Name"));
        return { id, name: name || (id ? `Tổ #${id}` : "") };
      })
      .filter((team) => team.id && (!primaryTeamId || team.id !== primaryTeamId));
  }, [scopedTeamsForAssignment, leaderTeamId]);

  const managerSupportingTeamOptions = useMemo(() => {
    const primaryTeamId = toPositiveNumber(managerTeamId);
    return scopedTeamsForAssignment
      .map((team) => {
        const id = toPositiveNumber(readValue(team, "id", "Id"));
        const name = toText(readValue(team, "name", "Name"));
        return { id, name: name || (id ? `Tổ #${id}` : "") };
      })
      .filter((team) => team.id && (!primaryTeamId || team.id !== primaryTeamId));
  }, [scopedTeamsForAssignment, managerTeamId]);

  const leaderPrimaryTechnicianOptions = useMemo(() => {
    const options = [{ value: "", label: "-- Chọn thợ chính --" }];
    leaderTechnicianOptions.forEach((staff) => {
      const id = toPositiveNumber(readValue(staff, "id", "Id"));
      const name = toText(readValue(staff, "fullName", "FullName"));
      if (!id) return;
      options.push({ value: String(id), label: name || `Nhân sự #${id}` });
    });
    return options;
  }, [leaderTechnicianOptions]);

  const leaderAssistantTechnicianOptions = useMemo(() => {
    const selectedLeadId = toPositiveNumber(leaderLeadTechnicianId);
    const options = [{ value: "", label: "-- Không chọn --" }];
    leaderTechnicianOptions.forEach((staff) => {
      const id = toPositiveNumber(readValue(staff, "id", "Id"));
      const name = toText(readValue(staff, "fullName", "FullName"));
      if (!id) return;
      if (selectedLeadId && id === selectedLeadId) return;
      options.push({ value: String(id), label: name || `Nhân sự #${id}` });
    });
    return options;
  }, [leaderTechnicianOptions, leaderLeadTechnicianId]);

  const supportResponseTechnicians = useMemo(
    () => getTechniciansByTeam(currentStaffTeamId),
    [getTechniciansByTeam, currentStaffTeamId]
  );

  const supportResponseLeadOptions = useMemo(() => {
    const options = [{ value: "", label: "-- Chọn thợ chính tổ phụ --" }];
    supportResponseTechnicians.forEach((staff) => {
      const id = toPositiveNumber(readValue(staff, "id", "Id"));
      const name = toText(readValue(staff, "fullName", "FullName"));
      if (!id) return;
      options.push({ value: String(id), label: name || `Nhân sự #${id}` });
    });
    return options;
  }, [supportResponseTechnicians]);

  const supportResponseAssistantOptions = useMemo(() => {
    const selectedLeadId = toPositiveNumber(leaderSupportResponseLeadTechnicianId);
    const options = [{ value: "", label: "-- Không chọn --" }];
    supportResponseTechnicians.forEach((staff) => {
      const id = toPositiveNumber(readValue(staff, "id", "Id"));
      const name = toText(readValue(staff, "fullName", "FullName"));
      if (!id) return;
      if (selectedLeadId && id === selectedLeadId) return;
      options.push({ value: String(id), label: name || `Nhân sự #${id}` });
    });
    return options;
  }, [supportResponseTechnicians, leaderSupportResponseLeadTechnicianId]);

  const managerPrimaryTechnicianOptions = useMemo(() => {
    const options = [{ value: "", label: "-- Chọn thợ chính --" }];
    managerTechnicianOptions.forEach((staff) => {
      const id = toPositiveNumber(readValue(staff, "id", "Id"));
      const name = toText(readValue(staff, "fullName", "FullName"));
      if (!id) return;
      options.push({ value: String(id), label: name || `Nhân sự #${id}` });
    });
    return options;
  }, [managerTechnicianOptions]);

  const managerAssistantTechnicianOptions = useMemo(() => {
    const selectedLeadId = toPositiveNumber(managerLeadTechnicianId);
    const options = [{ value: "", label: "-- Không chọn --" }];
    managerTechnicianOptions.forEach((staff) => {
      const id = toPositiveNumber(readValue(staff, "id", "Id"));
      const name = toText(readValue(staff, "fullName", "FullName"));
      if (!id) return;
      if (selectedLeadId && id === selectedLeadId) return;
      options.push({ value: String(id), label: name || `Nhân sự #${id}` });
    });
    return options;
  }, [managerTechnicianOptions, managerLeadTechnicianId]);

  const buildTechnicianOptionsForTeam = useCallback((teamId, includeEmpty = false) => {
    const options = [];
    if (includeEmpty) {
      options.push({ value: "", label: "-- Không chọn --" });
    }

    const teamTechnicians = getTechniciansByTeam(teamId);
    teamTechnicians.forEach((staff) => {
      const id = toPositiveNumber(readValue(staff, "id", "Id"));
      const name = toText(readValue(staff, "fullName", "FullName"));
      if (!id) return;
      options.push({ value: String(id), label: name || `Nhân sự #${id}` });
    });

    return options;
  }, [getTechniciansByTeam]);

  const managerSupportingTechnicianOptionsByTeam = useMemo(() => {
    const map = {};
    managerSupportingTeamOptions.forEach((team) => {
      map[String(team.id)] = {
        lead: buildTechnicianOptionsForTeam(team.id, false),
        assistant: buildTechnicianOptionsForTeam(team.id, true),
      };
    });
    return map;
  }, [managerSupportingTeamOptions, buildTechnicianOptionsForTeam]);

  useEffect(() => {
    if (hasLeaderTeamSelected) return;
    if (toText(leaderLeadTechnicianId)) setLeaderLeadTechnicianId("");
    if (toText(leaderAssistantTechnicianId)) setLeaderAssistantTechnicianId("");
  }, [hasLeaderTeamSelected, leaderLeadTechnicianId, leaderAssistantTechnicianId, leaderBayId]);

  useEffect(() => {
    if (hasManagerTeamSelected) return;
    if (toText(managerLeadTechnicianId)) setManagerLeadTechnicianId("");
    if (toText(managerAssistantTechnicianId)) setManagerAssistantTechnicianId("");
    if (toText(managerBayId)) setManagerBayId("");
  }, [hasManagerTeamSelected, managerLeadTechnicianId, managerAssistantTechnicianId, managerBayId]);

  useEffect(() => {
    const leadId = toPositiveNumber(leaderLeadTechnicianId);
    if (!leadId) {
      if (toText(leaderAssistantTechnicianId)) setLeaderAssistantTechnicianId("");
      return;
    }
    const assistantId = toPositiveNumber(leaderAssistantTechnicianId);
    if (assistantId && assistantId === leadId) {
      setLeaderAssistantTechnicianId("");
    }
  }, [leaderLeadTechnicianId, leaderAssistantTechnicianId, leaderBayId]);

  useEffect(() => {
    const leadId = toPositiveNumber(managerLeadTechnicianId);
    if (!leadId) {
      if (toText(managerAssistantTechnicianId)) setManagerAssistantTechnicianId("");
      if (toText(managerBayId)) setManagerBayId("");
      return;
    }
    const assistantId = toPositiveNumber(managerAssistantTechnicianId);
    if (assistantId && assistantId === leadId) {
      setManagerAssistantTechnicianId("");
    }
  }, [managerLeadTechnicianId, managerAssistantTechnicianId, managerBayId]);

  useEffect(() => {
    const leadId = toPositiveNumber(leaderSupportResponseLeadTechnicianId);
    const assistantId = toPositiveNumber(leaderSupportResponseAssistantTechnicianId);
    if (leadId && assistantId && leadId === assistantId) {
      setLeaderSupportResponseAssistantTechnicianId("");
    }
  }, [leaderSupportResponseLeadTechnicianId, leaderSupportResponseAssistantTechnicianId]);

  const baySelectionOptions = useMemo(() => {
    const options = [{ value: "", label: "-- Chọn khoang (bắt buộc) --" }];
    availableBays.forEach((bay) => {
      const id = toPositiveNumber(readValue(bay, "id", "Id"));
      const code = toText(readValue(bay, "code", "Code")) || toText(readValue(bay, "name", "Name")) || `Khoang #${id}`;
      const occupied = Boolean(readValue(bay, "currentOccupiedVehicleId", "CurrentOccupiedVehicleId"));
      const operational = isOperationalBayAvailable(readValue(bay, "operationalStatus", "OperationalStatus"));
      const disabled = occupied || !operational;
      if (!id) return;
      options.push({
        value: String(id),
        label: `${code} ${disabled ? "(Đang bận)" : "(Trống)"}`,
        disabled,
      });
    });
    return options;
  }, [availableBays]);

  const cancelReasonOptions = useMemo(
    () => CANCEL_REASONS.map((reason) => ({ value: reason, label: reason })),
    []
  );

  const teamFilterOptions = useMemo(() => {
    const options = [{ value: "", label: "Tất cả tổ" }];
    activeTeams.forEach((team) => {
      const id = toPositiveNumber(readValue(team, "id", "Id"));
      const name = toText(readValue(team, "name", "Name"));
      options.push({
        value: id ? String(id) : "",
        label: name || `Tổ #${id}`,
      });
    });
    return options;
  }, [activeTeams]);

  const dispatchTagFilterOptions = useMemo(
    () => [{ value: "", label: "Tất cả chuyên môn" }, ...DISPATCH_TAG_OPTIONS.map((item) => ({ value: item.value, label: item.label }))],
    []
  );

  const fetchData = useCallback(async () => {
    setLoading(true);
    setError("");

    const boardParams = { includeCompleted: isTeamLeaderMode || isAssignmentPage || isManagerMode };
    const teamFilterId = toPositiveNumber(teamFilter);
    if (branchId) {
      boardParams.branchId = branchId;
    }
    if (teamFilterId) {
      boardParams.teamId = teamFilterId;
    }

    try {
      const [
        boardRes,
        ordersRes,
        customersRes,
        vehiclesRes,
        vehicleModelsRes,
        vehicleBrandsRes,
        teamsRes,
        staffsRes,
        rolesRes,
        baysRes,
      ] = await Promise.all([
        serviceOrderApi.getDiagnosisDispatchBoard(boardParams),
        serviceOrderApi.getAll(),
        getAllCustomers(branchId || undefined),
        getAllVehicles(),
        getAllVehicleModels(),
        getAllVehicleBrands(),
        serviceOrderApi.getTeams(),
        getAllStaff(branchId || undefined),
        getAllRoles(),
        serviceOrderApi.getBays(),
      ]);

      const orderList = Array.isArray(ordersRes) ? ordersRes : [];
      const customerList = Array.isArray(customersRes) ? customersRes : [];
      const vehicleList = Array.isArray(vehiclesRes) ? vehiclesRes : [];
      const vehicleModelList = Array.isArray(vehicleModelsRes) ? vehicleModelsRes : [];
      const vehicleBrandList = Array.isArray(vehicleBrandsRes) ? vehicleBrandsRes : [];

      setTeams(Array.isArray(teamsRes) ? teamsRes : []);
      setStaffs(Array.isArray(staffsRes) ? staffsRes : []);
      setRoles(Array.isArray(rolesRes) ? rolesRes : []);
      setBays(Array.isArray(baysRes) ? baysRes : []);

      const orderMap = new Map(orderList.map((item) => [toPositiveNumber(readValue(item, "id", "Id")) || 0, item]));
      const customerMap = new Map(customerList.map((item) => [toPositiveNumber(readValue(item, "id", "Id")) || 0, item]));
      const vehicleMap = new Map(vehicleList.map((item) => [toPositiveNumber(readValue(item, "id", "Id")) || 0, item]));
      const vehicleModelMap = new Map(vehicleModelList.map((item) => [toPositiveNumber(readValue(item, "id", "Id")) || 0, item]));
      const vehicleBrandMap = new Map(vehicleBrandList.map((item) => [toPositiveNumber(readValue(item, "id", "Id")) || 0, item]));
      const teamMap = new Map(
        (Array.isArray(teamsRes) ? teamsRes : []).map((team) => [toPositiveNumber(readValue(team, "id", "Id")) || 0, team])
      );

      const normalized = (Array.isArray(boardRes) ? boardRes : [])
        .map((item) => normalizeBoardItem(item, orderMap, customerMap, vehicleMap, vehicleModelMap, vehicleBrandMap, teamMap))
        .filter((item) => item.serviceOrderId > 0);

      setBoardItems(normalized);
    } catch (err) {
      setError(resolveApiErrorMessage(err, "Không thể tải bảng điều phối."));
    } finally {
      setLoading(false);
    }
  }, [branchId, teamFilter]);

  useEffect(() => {
    fetchData();
  }, [fetchData]);

  useEffect(() => {
    if (!isTeamLeaderMode) return;
    if (!currentStaffTeamId) return;
    if (!toPositiveNumber(teamFilter)) {
      setTeamFilter(String(currentStaffTeamId));
    }
    if (!toPositiveNumber(leaderTeamId)) {
      setLeaderTeamId(String(currentStaffTeamId));
    }
  }, [isTeamLeaderMode, currentStaffTeamId, teamFilter, leaderTeamId]);

  useEffect(() => {
    const selectedTeamId = toPositiveNumber(teamFilter);
    if (!selectedTeamId) return;
    const isExisting = activeTeams.some((team) => toPositiveNumber(readValue(team, "id", "Id")) === selectedTeamId);
    if (!isExisting) {
      setTeamFilter("");
    }
  }, [activeTeams, teamFilter]);

  const scopedItems = useMemo(() => {
    return boardItems.filter((item) => {
      if (!isTeamLeaderMode || !currentStaffTeamId) return true;

      if (item.teamId === currentStaffTeamId) return true;
      if (Array.isArray(item.supportingTeamIds) && item.supportingTeamIds.includes(currentStaffTeamId)) return true;

      if (!item.teamId) {
        if (item.queueColor !== "WHITE" && item.queueColor !== "RED") return false;
        if (currentTeamSpecialty && item.dispatchTag && !dispatchTagIncludes(item.dispatchTag, currentTeamSpecialty)) return false;
        return true;
      }

      return false;
    });
  }, [boardItems, isTeamLeaderMode, currentStaffTeamId, currentTeamSpecialty]);

  const visibleItems = useMemo(() => {
    const query = toText(searchQuery).toLowerCase();
    const tagFilterCode = normalizeCode(dispatchTagFilter);

    return scopedItems.filter((item) => {
      if (tagFilterCode && !dispatchTagIncludes(item.dispatchTag, tagFilterCode)) return false;
      if (!query) return true;

      const haystack = [
        item.serviceOrderCode,
        item.customerName,
        item.vehicleLicensePlate,
        item.teamName,
        Array.isArray(item.supportingTeamNames) ? item.supportingTeamNames.join(" ") : "",
        item.technicianName,
        item.assistantTechnicianName,
        getDispatchTagLabel(item.dispatchTag),
      ]
        .map((value) => toText(value).toLowerCase())
        .join(" ");

      return haystack.includes(query);
    });
  }, [scopedItems, searchQuery, dispatchTagFilter]);

  const managerScopedItems = useMemo(() => {
    if (!isManagerMode) return visibleItems;
    return visibleItems;
  }, [isManagerMode, visibleItems]);

  const boardByColor = useMemo(() => {
    const grouped = { NEEDS_APPROVAL: [], ALL_ITEMS: [], WHITE: [], YELLOW: [], RED: [], GREEN: [] };
    const source = (isManagerMode ? managerScopedItems : visibleItems).filter((item) => {
      const queueState = normalizeCode(item?.queueState);
      const queueColor = normalizeCode(item?.queueColor);
      if (queueColor === "GREEN") return false;
      if (["DIAGNOSED", "COMPLETED", "DONE", "APPROVED"].includes(queueState)) return false;
      return true;
    });

    if (isManagerMode) {
      grouped.NEEDS_APPROVAL = source.filter(isForemanMyTaskItem);
      const needApprovalIds = new Set(
        grouped.NEEDS_APPROVAL
          .map((item) => toPositiveNumber(item?.serviceOrderId))
          .filter(Boolean)
      );

      source.forEach((item) => {
        const itemId = toPositiveNumber(item?.serviceOrderId);
        if (itemId && needApprovalIds.has(itemId)) return;
        const color = COLUMN_ORDER.includes(item.queueColor) ? item.queueColor : "WHITE";
        if (color === "WHITE" || color === "YELLOW" || color === "RED") {
          grouped[color].push(item);
        }
      });

      grouped.ALL_ITEMS = [
        ...grouped.NEEDS_APPROVAL,
        ...grouped.WHITE,
        ...grouped.YELLOW,
        ...grouped.RED,
      ];
      return grouped;
    }

    source.forEach((item) => {
      const color = COLUMN_ORDER.includes(item.queueColor) ? item.queueColor : "WHITE";
      if (color === "WHITE" || color === "YELLOW" || color === "RED") {
        grouped[color].push(item);
      }
    });

    grouped.ALL_ITEMS = [...grouped.WHITE, ...grouped.YELLOW, ...grouped.RED];
    return grouped;
  }, [isManagerMode, managerScopedItems, visibleItems]);

  const teamLeaderSupportNotifications = useMemo(() => {
    if (!isTeamLeaderMode || !currentStaffTeamId) return [];

    return visibleItems
      .map((item) => {
        const supportingAssignments = Array.isArray(item.supportingAssignments) ? item.supportingAssignments : [];
        const assignment = supportingAssignments.find(
          (row) => toPositiveNumber(row?.teamId) === currentStaffTeamId
        );
        if (!assignment) return null;

        const requestedCount = Math.max(0, Number(assignment?.requestedTechnicianCount || 0));
        const hasSelectedLead = Boolean(toPositiveNumber(assignment?.technicianId));
        if (requestedCount <= 0 || hasSelectedLead) return null;

        return {
          ...item,
          requestedTechnicianCount: requestedCount,
          supportRequestedAt: item.supportRequestedAt || item.requestedAt,
          supportRequestNote: item.supportRequestNote || "",
        };
      })
      .filter(Boolean)
      .sort((a, b) => new Date(b.supportRequestedAt || 0).getTime() - new Date(a.supportRequestedAt || 0).getTime());
  }, [isTeamLeaderMode, currentStaffTeamId, visibleItems]);

  const totalStats = useMemo(
    () => ({
      NEEDS_APPROVAL: boardByColor.NEEDS_APPROVAL.length,
      ALL_ITEMS:
        boardByColor.NEEDS_APPROVAL.length +
        boardByColor.WHITE.length +
        boardByColor.YELLOW.length +
        boardByColor.RED.length,
      WHITE: boardByColor.WHITE.length,
      YELLOW: boardByColor.YELLOW.length,
      RED: boardByColor.RED.length,
      GREEN: boardByColor.GREEN.length,
    }),
    [boardByColor]
  );

  const dashboardTabOrder = useMemo(() => {
    if (isManagerMode) {
      // Manager uses a single-row 5-tab layout.
      return ["ALL_ITEMS", "NEEDS_APPROVAL", "WHITE", "YELLOW", "RED"];
    }
    if (isTeamLeaderMode) {
      // Team leader uses the same 5-tab overview style as foreman.
      return ["ALL_ITEMS", "NEEDS_APPROVAL", "WHITE", "YELLOW", "RED"];
    }
    return ["WHITE", "YELLOW", "RED"];
  }, [isManagerMode, isTeamLeaderMode]);

  const activeTabItems = useMemo(() => boardByColor[activeColorTab] || [], [activeColorTab, boardByColor]);
  const activeBoardListItems = useMemo(() => {
    if (!isTeamLeaderMode) return activeTabItems;
    if (teamLeaderBoardTab === "support_notifications") return teamLeaderSupportNotifications;
    return activeTabItems;
  }, [isTeamLeaderMode, teamLeaderBoardTab, teamLeaderSupportNotifications, activeTabItems]);

  const selectedItem = useMemo(() => {
    const targetId = toPositiveNumber(selectedServiceOrderId);
    if (!targetId) return null;
    const sourceItems = isAssignmentPage ? scopedItems : activeTabItems;
    return sourceItems.find((item) => item.serviceOrderId === targetId) || null;
  }, [isAssignmentPage, scopedItems, activeTabItems, selectedServiceOrderId]);

  const leaderBaySelectionOptions = useMemo(() => {
    const selectedBayId = toPositiveNumber(leaderBayId);
    if (!selectedBayId) return baySelectionOptions;

    const selectedValue = String(selectedBayId);
    const hasSelectedInOptions = baySelectionOptions.some((option) => String(option?.value) === selectedValue);
    if (hasSelectedInOptions) return baySelectionOptions;

    const selectedBayCode = toText(selectedItem?.assignedBayCode);
    const selectedBayLabel = selectedBayCode || `Khoang #${selectedBayId}`;
    return [
      ...baySelectionOptions,
      { value: selectedValue, label: `${selectedBayLabel} (Đang gán)`, disabled: true },
    ];
  }, [leaderBayId, baySelectionOptions, selectedItem?.assignedBayCode]);

  useEffect(() => {
    const routeOrderId = toPositiveNumber(assignmentServiceOrderId);
    if (!routeOrderId) return;
    if (routeOrderId !== toPositiveNumber(selectedServiceOrderId)) {
      setSelectedServiceOrderId(routeOrderId);
    }
  }, [assignmentServiceOrderId, selectedServiceOrderId]);

  useEffect(() => {
    if (isAssignmentPage || typeof onOpenAssignment === "function") return;
    if (activeBoardListItems.length === 0) {
      setSelectedServiceOrderId(null);
      return;
    }

    if (!selectedItem) {
      setSelectedServiceOrderId(activeBoardListItems[0].serviceOrderId);
    }
  }, [isAssignmentPage, activeBoardListItems, selectedItem, onOpenAssignment]);

  useEffect(() => {
    if (dashboardTabOrder.includes(activeColorTab)) return;
    setActiveColorTab(dashboardTabOrder[0] || "WHITE");
  }, [dashboardTabOrder, activeColorTab]);

  useEffect(() => {
    if (!isAssignmentPage) return;
    if (!selectedItem?.queueColor) return;
    const color = COLUMN_ORDER.includes(selectedItem.queueColor) ? selectedItem.queueColor : "WHITE";
    const targetTab = dashboardTabOrder.includes(color) ? color : (dashboardTabOrder[0] || "WHITE");
    if (targetTab !== activeColorTab) setActiveColorTab(targetTab);
  }, [isAssignmentPage, selectedItem?.serviceOrderId, selectedItem?.queueColor, activeColorTab, dashboardTabOrder]);
  useEffect(() => {
    if (!selectedItem) return;

    if (isTeamLeaderMode) {
      const nextTeamId = toPositiveNumber(selectedItem.teamId) || currentStaffTeamId || toPositiveNumber(leaderTeamId);
      const nextSupportingAssignments = normalizeSupportingAssignmentsFromBoard(
        selectedItem.supportingAssignments,
        nextTeamId
      );
      const nextSupportingTeamIds = normalizeTeamIds(
        nextSupportingAssignments.map((item) => item.teamId),
        nextTeamId
      );
      const boardSupportingAssignments = normalizeSupportingAssignmentsFromBoard(
        selectedItem.supportingAssignments,
        nextTeamId
      );
      const nextSupportRequestMap = {};
      nextSupportingTeamIds.forEach((teamId) => {
        const matched = boardSupportingAssignments.find((item) => item.teamId === teamId);
        const requestedCount = Math.max(1, Number(matched?.requestedTechnicianCount || 1));
        nextSupportRequestMap[String(teamId)] = {
          requestedTechnicianCount: String(requestedCount),
        };
      });
      const incomingAssignmentForCurrentTeam = boardSupportingAssignments.find(
        (assignment) => assignment.teamId === currentStaffTeamId
      );
      setLeaderTeamId(nextTeamId ? String(nextTeamId) : "");
      setLeaderSupportTeamIds(nextSupportingTeamIds.map((id) => String(id)));
      setLeaderSupportRequestMap(nextSupportRequestMap);
      setLeaderLeadTechnicianId(selectedItem.technicianId ? String(selectedItem.technicianId) : "");
      setLeaderAssistantTechnicianId(selectedItem.assistantTechnicianId ? String(selectedItem.assistantTechnicianId) : "");
      setLeaderBayId(selectedItem.assignedBayId ? String(selectedItem.assignedBayId) : "");
      setIsLeaderSupportSectionExpanded(false);
      setLeaderSupportResponseLeadTechnicianId(
        incomingAssignmentForCurrentTeam?.technicianId ? String(incomingAssignmentForCurrentTeam.technicianId) : ""
      );
      setLeaderSupportResponseAssistantTechnicianId(
        incomingAssignmentForCurrentTeam?.assistantTechnicianId ? String(incomingAssignmentForCurrentTeam.assistantTechnicianId) : ""
      );
      setLeaderSupportResponseNote("");
      return;
    }

    const nextManagerTeamId = toPositiveNumber(selectedItem.teamId);
    const nextManagerSupportingTeamIds = normalizeTeamIds(
      selectedItem.supportingTeamIds || selectedItem.assignedTeamIds || [],
      nextManagerTeamId
    );
    const boardSupportingAssignments = normalizeSupportingAssignmentsFromBoard(
      selectedItem.supportingAssignments,
      nextManagerTeamId
    );
    const nextManagerSupportingAssignmentMap = {};
    nextManagerSupportingTeamIds.forEach((teamId) => {
      const matched = boardSupportingAssignments.find((item) => item.teamId === teamId);
      nextManagerSupportingAssignmentMap[String(teamId)] = {
        technicianId: matched?.technicianId ? String(matched.technicianId) : "",
        assistantTechnicianId: matched?.assistantTechnicianId ? String(matched.assistantTechnicianId) : "",
      };
    });
    setManagerTeamId(nextManagerTeamId ? String(nextManagerTeamId) : "");
    setManagerSupportingTeamIds(nextManagerSupportingTeamIds.map((id) => String(id)));
    setManagerSupportingAssignmentMap(nextManagerSupportingAssignmentMap);
    setManagerLeadTechnicianId(selectedItem.technicianId ? String(selectedItem.technicianId) : "");
    setManagerAssistantTechnicianId(selectedItem.assistantTechnicianId ? String(selectedItem.assistantTechnicianId) : "");
    setManagerBayId(selectedItem.assignedBayId ? String(selectedItem.assignedBayId) : "");
    setIsManagerSupportSectionExpanded(nextManagerSupportingTeamIds.length > 0);
  }, [selectedItem?.serviceOrderId]);

  useEffect(() => {
    if (!isTeamLeaderMode) return;
    const orderId = toPositiveNumber(selectedItem?.serviceOrderId);
    if (!orderId) return;
    if (toPositiveNumber(selectedItem?.assignedBayId)) return;

    let cancelled = false;
    serviceOrderApi
      .getDiagnoses(orderId)
      .then((rows) => {
        if (cancelled) return;
        const list = Array.isArray(rows) ? rows : [];
        const withBay = list
          .map((item) => ({
            bayId: toPositiveNumber(readValue(item, "assignedBayId", "AssignedBayId")),
            updatedAt: readValue(item, "updatedAt", "UpdatedAt") || readValue(item, "createdAt", "CreatedAt") || null,
          }))
          .filter((item) => item.bayId);

        if (withBay.length === 0) return;
        withBay.sort((a, b) => {
          const at = a.updatedAt ? new Date(a.updatedAt).getTime() : 0;
          const bt = b.updatedAt ? new Date(b.updatedAt).getTime() : 0;
          return bt - at;
        });
        setLeaderBayId(String(withBay[0].bayId));
      })
      .catch(() => {});

    return () => {
      cancelled = true;
    };
  }, [isTeamLeaderMode, selectedItem?.serviceOrderId, selectedItem?.assignedBayId]);

  useEffect(() => {
    const orderId = toPositiveNumber(selectedItem?.serviceOrderId);
    if (!isAssignmentPage || !orderId) {
      setSelectedRequestedServices("");
      setSelectedRequestedServicesLoading(false);
      return;
    }

    const fallbackRequestedServices = toText(selectedItem?.customerRequestedServices);
    setSelectedRequestedServices(fallbackRequestedServices);
    setSelectedRequestedServicesLoading(true);

    let cancelled = false;

    serviceOrderApi
      .getDetails(orderId)
      .then((detailsRes) => {
        if (cancelled) return;
        const normalized = normalizeServiceOrderDetailsResponse(detailsRes);
        const extracted = extractRequestedServiceNames(normalized);
        setSelectedRequestedServices(extracted || fallbackRequestedServices);
      })
      .catch(() => {
        if (!cancelled) {
          setSelectedRequestedServices(fallbackRequestedServices);
        }
      })
      .finally(() => {
        if (!cancelled) {
          setSelectedRequestedServicesLoading(false);
        }
      });

    return () => {
      cancelled = true;
    };
  }, [isAssignmentPage, selectedItem?.serviceOrderId, selectedItem?.customerRequestedServices]);

  useEffect(() => {
    const orderId = toPositiveNumber(selectedItem?.serviceOrderId);
    const shouldFocusCancel = normalizeCode(assignmentFocus) === "CANCEL-REQUEST";
    const shouldLoadCancelDetail = Boolean(selectedItem?.isCancelPendingApproval) || shouldFocusCancel;

    if (!isAssignmentPage || !orderId || !shouldLoadCancelDetail) {
      setCancelRequestDetail(null);
      setCancelRequestDetailLoading(false);
      return;
    }

    const fallbackReason = toText(selectedItem?.cancelReason);
    if (fallbackReason) {
      setCancelRequestDetail({
        cancellationReason: fallbackReason,
        cancellationRequestedAt: null,
        cancellationRequestedBy: null,
        cancellationRequestedByName: "",
      });
    } else {
      setCancelRequestDetail(null);
    }

    setCancelRequestDetailLoading(true);
    let cancelled = false;

    serviceOrderApi
      .getDiagnoses(orderId)
      .then((response) => {
        if (cancelled) return;
        const diagnoses = normalizeDiagnosisResponse(response);
        const pendingCancelRequest = diagnoses
          .map((diagnosis) => {
            const cancellationStatus = normalizeCode(
              readValue(diagnosis, "cancellationStatus", "CancellationStatus")
            );
            if (!["PENDING", "REQUESTED"].includes(cancellationStatus)) return null;

            const cancellationReason = toText(
              readValue(diagnosis, "cancellationReason", "CancellationReason")
            );
            const cancellationRequestedAt = readValue(
              diagnosis,
              "cancellationRequestedAt",
              "CancellationRequestedAt"
            );
            const cancellationRequestedBy = toPositiveNumber(
              readValue(diagnosis, "cancellationRequestedBy", "CancellationRequestedBy")
            );
            const sortTimeSource =
              cancellationRequestedAt ||
              readValue(diagnosis, "updatedAt", "UpdatedAt") ||
              readValue(diagnosis, "createdAt", "CreatedAt");
            const sortTime = new Date(sortTimeSource || 0).getTime();

            return {
              cancellationReason,
              cancellationRequestedAt,
              cancellationRequestedBy,
              cancellationRequestedByName: resolveStaffNameById(staffs, cancellationRequestedBy),
              sortTime: Number.isFinite(sortTime) ? sortTime : 0,
            };
          })
          .filter(Boolean)
          .sort((a, b) => b.sortTime - a.sortTime)[0];

        if (pendingCancelRequest) {
          setCancelRequestDetail(pendingCancelRequest);
          return;
        }

        if (!fallbackReason) {
          setCancelRequestDetail(null);
        }
      })
      .catch(() => {
        if (!cancelled && !fallbackReason) {
          setCancelRequestDetail(null);
        }
      })
      .finally(() => {
        if (!cancelled) {
          setCancelRequestDetailLoading(false);
        }
      });

    return () => {
      cancelled = true;
    };
  }, [
    assignmentFocus,
    isAssignmentPage,
    selectedItem?.serviceOrderId,
    selectedItem?.isCancelPendingApproval,
    selectedItem?.cancelReason,
    staffs,
  ]);

  useEffect(() => {
    if (!isAssignmentPage) return;
    if (normalizeCode(assignmentFocus) !== "CANCEL-REQUEST") return;
    if (!cancelRequestSectionRef.current) return;

    const timer = window.setTimeout(() => {
      cancelRequestSectionRef.current?.scrollIntoView({
        behavior: "smooth",
        block: "start",
      });
    }, 250);

    return () => window.clearTimeout(timer);
  }, [assignmentFocus, cancelRequestDetailLoading, isAssignmentPage, selectedItem?.serviceOrderId]);

  useEffect(() => {
    const primaryTeamId = toPositiveNumber(leaderTeamId);
    const normalizedIds = normalizeTeamIds(leaderSupportTeamIds, primaryTeamId).map((id) => String(id));
    const currentIds = Array.isArray(leaderSupportTeamIds) ? leaderSupportTeamIds.map((item) => String(item)) : [];
    const hasIdChanges = normalizedIds.length !== currentIds.length || normalizedIds.some((id, index) => id !== currentIds[index]);
    if (hasIdChanges) {
      setLeaderSupportTeamIds(normalizedIds);
    }

    const normalizedMap = normalizeSupportRequestMap(leaderSupportRequestMap);
    const nextMap = {};
    normalizedIds.forEach((teamId) => {
      nextMap[teamId] = normalizedMap[teamId] || { requestedTechnicianCount: "1" };
    });
    const mapChanged = JSON.stringify(nextMap) !== JSON.stringify(normalizedMap);
    if (mapChanged) {
      setLeaderSupportRequestMap(nextMap);
    }
  }, [leaderTeamId, leaderSupportTeamIds, leaderSupportRequestMap]);

  useEffect(() => {
    const primaryTeamId = toPositiveNumber(managerTeamId);
    const normalizedIds = normalizeTeamIds(managerSupportingTeamIds, primaryTeamId).map((id) => String(id));
    const currentIds = Array.isArray(managerSupportingTeamIds) ? managerSupportingTeamIds.map((item) => String(item)) : [];
    const hasIdChanges = normalizedIds.length !== currentIds.length || normalizedIds.some((id, index) => id !== currentIds[index]);
    if (hasIdChanges) {
      setManagerSupportingTeamIds(normalizedIds);
    }

    const normalizedMap = normalizeSupportingAssignmentMap(managerSupportingAssignmentMap);
    const nextMap = {};
    normalizedIds.forEach((teamId) => {
      const row = normalizedMap[teamId] || { technicianId: "", assistantTechnicianId: "" };
      const leadId = toPositiveNumber(row.technicianId);
      const assistantId = toPositiveNumber(row.assistantTechnicianId);
      nextMap[teamId] = {
        technicianId: row.technicianId || "",
        assistantTechnicianId: leadId && assistantId && leadId === assistantId ? "" : (row.assistantTechnicianId || ""),
      };
    });
    const mapChanged = JSON.stringify(nextMap) !== JSON.stringify(normalizedMap);
    if (mapChanged) {
      setManagerSupportingAssignmentMap(nextMap);
    }
  }, [managerTeamId, managerSupportingTeamIds, managerSupportingAssignmentMap]);

  const loadDispatchSuggestion = useCallback(
    async (targetMode) => {
      if (!selectedItem) {
        toast({ title: "Chưa chọn lệnh", status: "warning", duration: 2500, isClosable: true });
        return;
      }

      const teamId = targetMode === "team_leader" ? leaderTeamIdForAssignment : toPositiveNumber(managerTeamId);
      if (!teamId) {
        toast({ title: "Vui lòng chọn tổ trước khi lấy gợi ý", status: "warning", duration: 2600, isClosable: true });
        return;
      }

      setSuggestionLoading(true);
      try {
        const response = await serviceOrderApi.getDiagnosisDispatchSuggestion(selectedItem.serviceOrderId, { teamId });
        setSuggestion(response || null);

        if (targetMode === "team_leader") {
          setLeaderLeadTechnicianId(toPositiveNumber(response?.suggestedLeadTechnicianId) ? String(response.suggestedLeadTechnicianId) : "");
          setLeaderAssistantTechnicianId(toPositiveNumber(response?.suggestedAssistantTechnicianId) ? String(response.suggestedAssistantTechnicianId) : "");
        } else {
          setManagerLeadTechnicianId(toPositiveNumber(response?.suggestedLeadTechnicianId) ? String(response.suggestedLeadTechnicianId) : "");
          setManagerAssistantTechnicianId(toPositiveNumber(response?.suggestedAssistantTechnicianId) ? String(response.suggestedAssistantTechnicianId) : "");
          setManagerBayId(toPositiveNumber(response?.suggestedBayId) ? String(response.suggestedBayId) : "");
        }

        toast({ title: "Đã áp dụng gợi ý phân công", status: "success", duration: 2200, isClosable: true });
      } catch (err) {
        toast({ title: "Không thể lấy gợi ý", description: resolveApiErrorMessage(err), status: "error", duration: 3500, isClosable: true });
      } finally {
        setSuggestionLoading(false);
      }
    },
    [selectedItem, leaderTeamIdForAssignment, managerTeamId, toast]
  );

  const handleLeaderSupportRequestedCountChange = useCallback((teamId, value) => {
    const normalizedTeamId = String(toPositiveNumber(teamId) || "");
    if (!normalizedTeamId) return;

    setLeaderSupportRequestMap((prev) => {
      const normalized = normalizeSupportRequestMap(prev);
      const current = normalized[normalizedTeamId] || { requestedTechnicianCount: "1" };
      const numericValue = Math.max(1, Math.min(8, Number(value || 1)));
      return {
        ...normalized,
        [normalizedTeamId]: {
          ...current,
          requestedTechnicianCount: String(numericValue),
        },
      };
    });
  }, []);

  const handleManagerSupportingAssignmentChange = useCallback((teamId, field, value) => {
    const normalizedTeamId = String(toPositiveNumber(teamId) || "");
    if (!normalizedTeamId) return;

    setManagerSupportingAssignmentMap((prev) => {
      const normalized = normalizeSupportingAssignmentMap(prev);
      const current = normalized[normalizedTeamId] || { technicianId: "", assistantTechnicianId: "" };
      const nextValue = toText(value);
      const nextRow = {
        ...current,
        [field]: nextValue,
      };
      const leadId = toPositiveNumber(nextRow.technicianId);
      const assistantId = toPositiveNumber(nextRow.assistantTechnicianId);
      if (leadId && assistantId && leadId === assistantId) {
        nextRow.assistantTechnicianId = "";
      }
      return {
        ...normalized,
        [normalizedTeamId]: nextRow,
      };
    });
  }, []);

  const handleLeaderAssign = async () => {
    if (!selectedItem) return;

    const hasAssignedTechnicians =
      Boolean(toPositiveNumber(selectedItem?.technicianId)) ||
      Boolean(toPositiveNumber(selectedItem?.assistantTechnicianId));
    const areAllTechniciansAccepted = hasAssignedTechnicians && Boolean(selectedItem?.startedAt);
    if (areAllTechniciansAccepted) {
      toast({
        title: "Tất cả thợ đã nhận việc, không thể giao lại",
        status: "info",
        duration: 2600,
        isClosable: true,
      });
      return;
    }

    const leadId = toPositiveNumber(leaderLeadTechnicianId);
    const assistantId = toPositiveNumber(leaderAssistantTechnicianId);
    const teamId = toPositiveNumber(leaderTeamId) || currentStaffTeamId;

    if (!leadId || !teamId) {
      toast({ title: "Vui lòng chọn tổ và thợ chính", status: "warning", duration: 2600, isClosable: true });
      return;
    }

    if (assistantId && assistantId === leadId) {
      toast({ title: "Thợ phụ không được trùng thợ chính", status: "warning", duration: 2600, isClosable: true });
      return;
    }

    setSaving(true);
    try {
      await serviceOrderApi.acceptDiagnosisRequest(selectedItem.serviceOrderId, leadId, {
        teamId,
        teamLeaderId: currentStaffId,
        assistantTechnicianId: assistantId || null,
      });

      toast({ title: "Đã nhận xe và phân công ca chuẩn đoán", status: "success", duration: 2500, isClosable: true });
      await fetchData();
      setSelectedServiceOrderId(selectedItem.serviceOrderId);
    } catch (err) {
      toast({ title: "Không thể phân công", description: resolveApiErrorMessage(err), status: "error", duration: 3500, isClosable: true });
    } finally {
      setSaving(false);
    }
  };

  const handleLeaderSupport = async () => {
    if (!selectedItem || !currentStaffId) return;
    const primaryTeamId = toPositiveNumber(leaderTeamId) || currentStaffTeamId || toPositiveNumber(selectedItem?.teamId);
    const supportingTeamIds = normalizeTeamIds(leaderSupportTeamIds, primaryTeamId);
    const supportingAssignments = buildSupportRequestAssignmentsPayload(
      supportingTeamIds,
      leaderSupportRequestMap
    );

    if (supportingAssignments.length === 0) {
      toast({
        title: "Vui lòng chọn ít nhất một tổ phụ cần hỗ trợ",
        status: "warning",
        duration: 3000,
        isClosable: true,
      });
      return;
    }

    setSaving(true);
    try {
      await serviceOrderApi.requestDiagnosisSupport(selectedItem.serviceOrderId, {
        requestedByStaffId: currentStaffId,
        note: toText(leaderSupportNote) || null,
        supportingAssignments,
      });

      toast({ title: "Đã gửi yêu cầu hỗ trợ cho tổ phụ", status: "success", duration: 2500, isClosable: true });
      setLeaderSupportNote("");
      setLeaderSupportTeamIds([]);
      setLeaderSupportRequestMap({});
      setIsLeaderSupportSectionExpanded(false);
      await fetchData();
      setSelectedServiceOrderId(selectedItem.serviceOrderId);
    } catch (err) {
      toast({ title: "Không thể gửi hỗ trợ", description: resolveApiErrorMessage(err), status: "error", duration: 3500, isClosable: true });
    } finally {
      setSaving(false);
    }
  };

  const handleLeaderCancelRequest = async () => {
    if (!selectedItem || !currentStaffId) return;

    const reason = toText(leaderCancelReason);
    const note = toText(leaderCancelNote);
    const payloadReason = note ? `${reason}: ${note}` : reason;

    if (!payloadReason) {
      toast({ title: "Vui lòng nhập lý do hủy", status: "warning", duration: 2600, isClosable: true });
      return;
    }

    setSaving(true);
    try {
      await serviceOrderApi.requestDiagnosisCancel(selectedItem.serviceOrderId, {
        requestedByStaffId: currentStaffId,
        reason: payloadReason,
      });

      toast({ title: "Đã gửi yêu cầu hủy lệnh", status: "success", duration: 2500, isClosable: true });
      setLeaderCancelNote("");
      await fetchData();
      setSelectedServiceOrderId(selectedItem.serviceOrderId);
    } catch (err) {
      toast({ title: "Không thể gửi yêu cầu hủy", description: resolveApiErrorMessage(err), status: "error", duration: 3500, isClosable: true });
    } finally {
      setSaving(false);
    }
  };

  const handleManagerOverride = async () => {
    if (!selectedItem || !currentStaffId) return;

    const leadId = toPositiveNumber(managerLeadTechnicianId);
    const assistantId = toPositiveNumber(managerAssistantTechnicianId);
    const teamId = toPositiveNumber(managerTeamId);
    const supportingTeamIds = normalizeTeamIds(managerSupportingTeamIds, teamId);
    const supportingAssignments = buildSupportingAssignmentsPayload(supportingTeamIds, managerSupportingAssignmentMap);
    const bayId = toPositiveNumber(managerBayId);

    if (!teamId) {
      toast({ title: "Vui lòng chọn tổ trước khi chọn thợ", status: "warning", duration: 2500, isClosable: true });
      return;
    }

    if (!leadId) {
      toast({ title: "Vui lòng chọn thợ chính", status: "warning", duration: 2500, isClosable: true });
      return;
    }

    if (supportingTeamIds.length > 0 && !teamId) {
      toast({ title: "Vui lòng chọn tổ chính khi có tổ phối hợp", status: "warning", duration: 2600, isClosable: true });
      return;
    }

    if (assistantId && assistantId === leadId) {
      toast({ title: "Thợ phụ không được trùng thợ chính", status: "warning", duration: 2500, isClosable: true });
      return;
    }

    const invalidSupportingAssignment = supportingAssignments.find((assignment) => {
      if (!assignment || assignment.teamId <= 0) return true;
      if (assignment.technicianId <= 0) return true;
      if (assignment.assistantTechnicianId && assignment.assistantTechnicianId === assignment.technicianId) return true;
      return false;
    });
    if (invalidSupportingAssignment) {
      const invalidTeam = managerSupportingTeamOptions.find((team) => team.id === invalidSupportingAssignment.teamId);
      const invalidTeamName = invalidTeam?.name || `Tổ #${invalidSupportingAssignment.teamId}`;
      if (!invalidSupportingAssignment.technicianId) {
        toast({
          title: `Vui lòng chọn thợ chính cho ${invalidTeamName}`,
          status: "warning",
          duration: 3200,
          isClosable: true,
        });
      } else {
        toast({
          title: `Thợ phụ của ${invalidTeamName} không được trùng thợ chính`,
          status: "warning",
          duration: 3200,
          isClosable: true,
        });
      }
      return;
    }

    setSaving(true);
    try {
      await serviceOrderApi.overrideDiagnosisAssignment(selectedItem.serviceOrderId, {
        managerId: currentStaffId,
        technicianId: leadId,
        assistantTechnicianId: assistantId || null,
        teamId: teamId || null,
        supportingTeamIds,
        supportingAssignments,
        bayId: bayId || null,
        reason: toText(managerOverrideReason) || null,
      });

      toast({ title: "Đã override phân công thành công", status: "success", duration: 2500, isClosable: true });
      await fetchData();
      setSelectedServiceOrderId(selectedItem.serviceOrderId);
    } catch (err) {
      toast({ title: "Không thể override", description: resolveApiErrorMessage(err), status: "error", duration: 3500, isClosable: true });
    } finally {
      setSaving(false);
    }
  };

  const handleManagerCancelDecision = async (approve) => {
    if (!selectedItem || !currentStaffId) return;

    setSaving(true);
    try {
      await serviceOrderApi.decideDiagnosisCancel(selectedItem.serviceOrderId, {
        managerId: currentStaffId,
        approve,
        note: toText(managerDecisionNote) || null,
      });

      toast({
        title: approve ? "Đã duyệt hủy lệnh" : "Đã từ chối hủy lệnh",
        status: "success",
        duration: 2500,
        isClosable: true,
      });
      setManagerDecisionNote("");
      setManagerCancelDecisionModalOpen(false);
      await fetchData();
      setSelectedServiceOrderId(selectedItem.serviceOrderId);
    } catch (err) {
      toast({ title: "Không thể xử lý yêu cầu hủy", description: resolveApiErrorMessage(err), status: "error", duration: 3500, isClosable: true });
    } finally {
      setSaving(false);
    }
  };

  const handleLeaderSupportSelection = async () => {
    if (!selectedItem || !currentStaffId || !currentStaffTeamId) return;

    const incomingAssignment = (Array.isArray(selectedItem.supportingAssignments) ? selectedItem.supportingAssignments : [])
      .find((assignment) => toPositiveNumber(assignment?.teamId) === currentStaffTeamId);

    if (!incomingAssignment) {
      toast({
        title: "Không tìm thấy yêu cầu hỗ trợ cho tổ của bạn",
        status: "info",
        duration: 2600,
        isClosable: true,
      });
      return;
    }

    const leadId = toPositiveNumber(leaderSupportResponseLeadTechnicianId);
    const assistantId = toPositiveNumber(leaderSupportResponseAssistantTechnicianId);

    if (!leadId) {
      toast({
        title: "Vui lòng chọn thợ chính của tổ phụ",
        status: "warning",
        duration: 2600,
        isClosable: true,
      });
      return;
    }

    if (assistantId && assistantId === leadId) {
      toast({
        title: "Thợ phụ không được trùng thợ chính",
        status: "warning",
        duration: 2600,
        isClosable: true,
      });
      return;
    }

    setSaving(true);
    try {
      await serviceOrderApi.selectDiagnosisSupportingTeam(selectedItem.serviceOrderId, {
        teamLeaderId: currentStaffId,
        teamId: currentStaffTeamId,
        technicianId: leadId,
        assistantTechnicianId: assistantId || null,
        note: toText(leaderSupportResponseNote) || null,
      });

      toast({
        title: "Đã xác nhận nhân sự hỗ trợ cho tổ phụ",
        status: "success",
        duration: 2500,
        isClosable: true,
      });
      setLeaderSupportResponseNote("");
      await fetchData();
      setSelectedServiceOrderId(selectedItem.serviceOrderId);
    } catch (err) {
      toast({
        title: "Không thể xác nhận nhân sự hỗ trợ",
        description: resolveApiErrorMessage(err),
        status: "error",
        duration: 3500,
        isClosable: true,
      });
    } finally {
      setSaving(false);
    }
  };

  const handleOpenManagerCancelDecisionModal = () => {
    if (!canManagerCancelDecision) {
      toast({ title: "Không có yêu cầu hủy chờ duyệt", status: "info", duration: 2200, isClosable: true });
      return;
    }
    setManagerCancelDecisionModalOpen(true);
  };

  const handleCloseManagerCancelDecisionModal = () => {
    if (saving) return;
    setManagerCancelDecisionModalOpen(false);
  };

  const selectedPrimaryTeamId =
    toPositiveNumber(selectedItem?.primaryTeamId) || toPositiveNumber(selectedItem?.teamId);
  const canCurrentTeamLeadPrimaryFlow =
    Boolean(selectedItem) && (!selectedPrimaryTeamId || selectedPrimaryTeamId === currentStaffTeamId);
  const hasLeaderAssignedTechnicians =
    Boolean(selectedItem) &&
    (
      Boolean(toPositiveNumber(selectedItem?.technicianId)) ||
      Boolean(toPositiveNumber(selectedItem?.assistantTechnicianId))
    );
  const isLeaderAssignmentLocked = hasLeaderAssignedTechnicians && Boolean(selectedItem?.startedAt);
  const canLeaderUseSuggestion =
    Boolean(selectedItem) &&
    canCurrentTeamLeadPrimaryFlow &&
    !isLeaderAssignmentLocked &&
    hasLeaderTeamSelected;

  const canLeaderAssign =
    Boolean(selectedItem) &&
    canCurrentTeamLeadPrimaryFlow &&
    !isLeaderAssignmentLocked &&
    !selectedItem?.approvedByTeamLeaderAt;
  const canLeaderSupport =
    Boolean(selectedItem?.technicianId) &&
    canCurrentTeamLeadPrimaryFlow &&
    !selectedItem?.approvedByTeamLeaderAt;
  const canLeaderCancel =
    Boolean(selectedItem?.technicianId) &&
    canCurrentTeamLeadPrimaryFlow &&
    !selectedItem?.isCancelPendingApproval;
  const canManagerOverride = Boolean(selectedItem);
  const canManagerConfigureSupportingTeams = Boolean(toPositiveNumber(managerTeamId)) || managerSupportingTeamIds.length > 0;
  const canManagerCancelDecision = Boolean(selectedItem?.isCancelPendingApproval);
  const canViewSelectedQuote = Boolean(isAssignmentPage && selectedItem && isDiagnosisCompletedItem(selectedItem) && typeof onOpenQuote === "function");
  const selectedDispatchTagLabel = resolveDispatchTagLabel(
    selectedItem?.dispatchTag,
    isTeamLeaderMode ? currentTeamSpecialty : ""
  );
  const selectedIncomingSupportAssignment = useMemo(() => {
    if (!isTeamLeaderMode || !currentStaffTeamId || !selectedItem) return null;
    const rows = Array.isArray(selectedItem.supportingAssignments) ? selectedItem.supportingAssignments : [];
    const matched = rows.find((row) => toPositiveNumber(row?.teamId) === currentStaffTeamId);
    if (!matched) return null;
    const requestedCount = Math.max(0, Number(matched?.requestedTechnicianCount || 0));
    if (requestedCount <= 0) return null;
    return {
      ...matched,
      requestedTechnicianCount: requestedCount,
    };
  }, [isTeamLeaderMode, currentStaffTeamId, selectedItem]);
  const canLeaderSelectSupportTeam = Boolean(selectedIncomingSupportAssignment) && !toPositiveNumber(selectedIncomingSupportAssignment?.technicianId);
  const cancelRequestReasonText =
    toText(cancelRequestDetail?.cancellationReason) ||
    toText(selectedItem?.cancelReason) ||
    "Không có mô tả.";
  const cancelRequestSenderText = (() => {
    const senderNameFromDetail = toText(cancelRequestDetail?.cancellationRequestedByName);
    if (senderNameFromDetail) return senderNameFromDetail;

    const senderId = toPositiveNumber(cancelRequestDetail?.cancellationRequestedBy);
    if (senderId) {
      const senderNameByStaffList = resolveStaffNameById(staffs, senderId);
      if (senderNameByStaffList) return senderNameByStaffList;
      return `Nhân sự #${senderId}`;
    }

    return "Chưa xác định";
  })();

  const actionButtonBaseProps = {
    h: "38px",
    minH: "38px",
    px: 4,
    py: 0,
    borderRadius: "full",
    fontSize: "sm",
    fontWeight: "700",
    lineHeight: "1",
    display: "inline-flex",
    alignItems: "center",
    justifyContent: "center",
    whiteSpace: "nowrap",
  };

  const openAssignmentPage = useCallback(
    (serviceOrderId, options = {}) => {
      const targetId = toPositiveNumber(serviceOrderId);
      if (!targetId) return;

      if (!isAssignmentPage && typeof onOpenAssignment === "function") {
        onOpenAssignment(targetId, options);
        return;
      }

      setSelectedServiceOrderId(targetId);
    },
    [isAssignmentPage, onOpenAssignment]
  );

  const handleQueueCardSelect = useCallback(
    (serviceOrderId) => {
      openAssignmentPage(serviceOrderId);
    },
    [openAssignmentPage]
  );

  const handleViewCancelRequest = useCallback(
    (item) => {
      const targetId = toPositiveNumber(item?.serviceOrderId);
      if (!targetId) return;
      openAssignmentPage(targetId, { focus: "cancel-request" });
    },
    [openAssignmentPage]
  );

  const handleViewQuote = useCallback(
    (item) => {
      const targetId = toPositiveNumber(item?.serviceOrderId);
      if (!targetId || typeof onOpenQuote !== "function") return;
      onOpenQuote(targetId);
    },
    [onOpenQuote]
  );

  const handleViewSelectedQuote = useCallback(() => {
    const targetId = toPositiveNumber(selectedItem?.serviceOrderId);
    if (!targetId || typeof onOpenQuote !== "function") return;
    onOpenQuote(targetId);
  }, [onOpenQuote, selectedItem?.serviceOrderId]);

  return (
    <Box
      p={{ base: 4, md: 6 }}
      minH="100vh"
      bg="linear-gradient(180deg, #f4f7ff 0%, #eef4ff 100%)"
    >
      <VStack align="stretch" spacing={5}>
        <HStack justify="space-between" align={{ base: "start", md: "center" }} flexWrap="wrap" spacing={3}>
          <Box>
            <Heading size="lg" color="#1b2559">
              {isTeamLeaderMode ? "Điều phối chuẩn đoán - Tổ trưởng" : "Điều phối chuẩn đoán - Quản đốc"}
            </Heading>
            <Text color="gray.600" mt={1}>
              {isAssignmentPage
                ? "Trang phân công chi tiết cho từng lệnh, tập trung thao tác điều phối và phê duyệt."
                : "Theo dõi 4 giai đoạn từ tiếp nhận, phân công đến phê duyệt kỹ thuật và báo giá."}
            </Text>
            {isTeamLeaderMode && (
              <HStack mt={2} spacing={2} flexWrap="wrap">
                <Badge colorScheme="blue" variant="subtle" borderRadius="full" px={2.5} py={1}>
                  Tổ phụ trách: {toText(readValue(currentTeam, "name", "Name")) || "Chưa xác định"}
                </Badge>
                <Badge colorScheme="purple" variant="subtle" borderRadius="full" px={2.5} py={1}>
                  Chuyên môn quản lý: {currentTeamSpecialtyLabel}
                </Badge>
              </HStack>
            )}
          </Box>

          <HStack spacing={2} align="center">
            {isAssignmentPage && (
              <Button
                leftIcon={<ArrowBackIcon />}
                variant="ghost"
                colorScheme="blue"
                onClick={onBack}
              >
                Quay lại
              </Button>
            )}
            {canViewSelectedQuote && (
              <Button
                colorScheme="blue"
                onClick={handleViewSelectedQuote}
              >
                Xem chuẩn đoán
              </Button>
            )}
          </HStack>
        </HStack>

        <HStack justify="flex-end" align="center" mt={1} mb={1}>
          <Button
            variant="outline"
            colorScheme="blue"
            onClick={fetchData}
            isLoading={loading}
          >
            ⟳ Làm mới
          </Button>
        </HStack>

        {!isAssignmentPage && (
          <Box
            bg="white"
            borderRadius="2xl"
            border="1px solid"
            borderColor="#d9e2f1"
            p={{ base: 3, md: 4 }}
            boxShadow="0 12px 30px rgba(15, 23, 42, 0.08)"
          >
            <HStack justify="space-between" align={{ base: "start", md: "center" }} flexWrap="wrap" spacing={2}>
              <Box>
                <Text fontSize="sm" fontWeight="700" color="#1e3a8a">
                  Trạng thái điều phối
                </Text>
                <Text fontSize="sm" color="gray.500">
                  Nhấn vào một trạng thái để phân loại danh sách lệnh.
                </Text>
              </Box>
              <Badge
                borderRadius="full"
                px={3}
                py={1}
                bg="#e0ecff"
                color="#1d4ed8"
                fontSize="xs"
                fontWeight="700"
              >
                Đang xem: {COLOR_META[activeColorTab]?.label || "--"} ({totalStats[activeColorTab] || 0})
              </Badge>
            </HStack>

              <SimpleGrid
                columns={{
                  base: 1,
                  sm: 2,
                  md: useManagerStyleStatusCards ? 3 : 2,
                  xl: useManagerStyleStatusCards ? 5 : 4,
                }}
                spacing={3}
                mt={4}
              >
              {dashboardTabOrder.map((color) => {
                const isActive = activeColorTab === color;
                const meta = COLOR_META[color];
                const theme = STATUS_CARD_THEME[color] || STATUS_CARD_THEME.WHITE;
                return (
                  <Box
                    as="button"
                    type="button"
                    key={color}
                    aria-pressed={isActive}
                    onClick={() => setActiveColorTab(color)}
                    minH={{ base: "112px", md: "124px" }}
                    p={4}
                    borderRadius="xl"
                    border="2px solid"
                    borderColor={isActive ? theme.activeBorder : "#dbe4f2"}
                    bg={isActive ? theme.activeBg : "#ffffff"}
                    textAlign="left"
                    transition="all 0.18s ease"
                    boxShadow={isActive ? theme.activeShadow : "0 4px 14px rgba(30, 64, 175, 0.08)"}
                    _hover={{
                      borderColor: theme.activeBorder,
                      transform: "translateY(-2px)",
                    }}
                  >
                    <HStack justify="space-between" align="start">
                      <VStack align="start" spacing={1} minH="60px">
                        <HStack spacing={2}>
                          <Box
                            w="10px"
                            h="10px"
                            borderRadius="full"
                            bg={theme.dot}
                            boxShadow={isActive ? "0 0 0 4px rgba(255, 255, 255, 0.72)" : "0 0 0 4px rgba(148, 163, 184, 0.2)"}
                          />
                          <Text fontSize={{ base: "sm", md: "md" }} fontWeight="700" color={isActive ? "#0f172a" : "#334155"}>
                            {meta.label}
                          </Text>
                        </HStack>
                        <Text fontSize="xs" color={isActive ? "#475569" : "#64748b"} noOfLines={2}>
                          {meta.description}
                        </Text>
                      </VStack>
                    </HStack>

                    <Text mt={3} fontSize={{ base: "3xl", md: "4xl" }} lineHeight="1" fontWeight="800" letterSpacing="-0.02em" color={isActive ? theme.dot : "#0f172a"}>
                      {Number(totalStats[color] ?? 0)}
                    </Text>
                  </Box>
                );
              })}
            </SimpleGrid>
          </Box>
        )}

        {!isAssignmentPage && (
          isTeamLeaderMode && (
            <Box bg="white" borderRadius="xl" border="1px solid" borderColor="#d9e2f1" p={3}>
              <HStack spacing={2} align="center" flexWrap="wrap">
                <Box
                  as="button"
                  type="button"
                  onClick={() => setTeamLeaderBoardTab("dispatch")}
                  h="40px"
                  minH="40px"
                  px={5}
                  borderRadius="999px"
                  border="1px solid"
                  borderColor={teamLeaderBoardTab === "dispatch" ? "#2563eb" : "#93c5fd"}
                  bg={teamLeaderBoardTab === "dispatch" ? "#2563eb" : "#ffffff"}
                  color={teamLeaderBoardTab === "dispatch" ? "#ffffff" : "#1d4ed8"}
                  fontWeight="700"
                  fontSize="sm"
                  lineHeight="1"
                  display="inline-flex"
                  alignItems="center"
                  justifyContent="center"
                  boxSizing="border-box"
                  appearance="none"
                  verticalAlign="middle"
                  m={0}
                  alignSelf="center"
                  transition="all 0.16s ease"
                  _hover={{
                    bg: teamLeaderBoardTab === "dispatch" ? "#1d4ed8" : "#eff6ff",
                    borderColor: "#2563eb",
                  }}
                >
                  <HStack spacing={2} align="center" h="100%">
                    <Text as="span" fontSize="inherit" lineHeight="1">
                      Danh sách điều phối
                    </Text>
                    <Badge
                      visibility="hidden"
                      borderRadius="full"
                      px={2}
                      py={0.5}
                      lineHeight="1"
                      display="inline-flex"
                      alignItems="center"
                      justifyContent="center"
                    >
                      0
                    </Badge>
                  </HStack>
                </Box>
                <Box
                  as="button"
                  type="button"
                  onClick={() => setTeamLeaderBoardTab("support_notifications")}
                  h="40px"
                  minH="40px"
                  px={5}
                  borderRadius="999px"
                  border="1px solid"
                  borderColor={teamLeaderBoardTab === "support_notifications" ? "#ea580c" : "#f97316"}
                  bg={teamLeaderBoardTab === "support_notifications" ? "#ea580c" : "#ffffff"}
                  color={teamLeaderBoardTab === "support_notifications" ? "#ffffff" : "#c2410c"}
                  fontWeight="700"
                  fontSize="sm"
                  lineHeight="1"
                  display="inline-flex"
                  alignItems="center"
                  justifyContent="center"
                  boxSizing="border-box"
                  appearance="none"
                  verticalAlign="middle"
                  m={0}
                  alignSelf="center"
                  transition="all 0.16s ease"
                  _hover={{
                    bg: teamLeaderBoardTab === "support_notifications" ? "#c2410c" : "#fff7ed",
                    borderColor: "#ea580c",
                  }}
                >
                  <HStack spacing={2} align="center" h="100%">
                    <Text as="span" fontSize="inherit" lineHeight="1">
                      Thông báo hỗ trợ
                    </Text>
                    <Badge
                      bg={teamLeaderBoardTab === "support_notifications" ? "white" : "orange.100"}
                      color={teamLeaderBoardTab === "support_notifications" ? "orange.600" : "orange.700"}
                      borderRadius="full"
                      px={2}
                      py={0.5}
                      lineHeight="1"
                      display="inline-flex"
                      alignItems="center"
                      justifyContent="center"
                      animation={teamLeaderSupportNotifications.length > 0 ? `${cancelAttentionPulse} 1.8s ease-in-out infinite` : "none"}
                    >
                      {teamLeaderSupportNotifications.length}
                    </Badge>
                  </HStack>
                </Box>
              </HStack>
            </Box>
          )
        )}

        {!isAssignmentPage && !isTeamLeaderMode && (
          <Box bg="white" borderRadius="xl" border="1px solid" borderColor="gray.100" p={3}>
            <HStack spacing={3} align="center" flexWrap="wrap">
              <FilterDropdown
                id="dispatch-team-filter"
                label="Lọc theo tổ"
                value={teamFilter}
                options={teamFilterOptions}
                onChange={setTeamFilter}
                minWidth={300}
                disabled={isTeamLeaderMode && !!currentStaffTeamId}
              />
              <FilterDropdown
                id="dispatch-tag-filter"
                label="Lọc theo chuyên môn"
                value={dispatchTagFilter}
                options={dispatchTagFilterOptions}
                onChange={setDispatchTagFilter}
                minWidth={260}
              />
            </HStack>
          </Box>
        )}

        {error && (
          <Alert status="error" borderRadius="xl">
            <AlertIcon />
            {error}
          </Alert>
        )}

        <Grid templateColumns="1fr" gap={4} alignItems="start">
          {!isAssignmentPage && (
            <GridItem>
              <Box bg="white" border="1px solid" borderColor="gray.100" borderRadius="2xl" overflow="hidden" boxShadow="0 14px 32px rgba(15,23,42,0.08)">
                {loading ? (
                  <VStack py={12} spacing={3}>
                    <Spinner color="blue.500" />
                    <Text color="gray.500">Đang tải dữ liệu điều phối...</Text>
                  </VStack>
                ) : activeBoardListItems.length === 0 ? (
                  <Box py={12} textAlign="center" color="gray.400">
                    {isTeamLeaderMode && teamLeaderBoardTab === "support_notifications"
                      ? "Không có yêu cầu hỗ trợ chờ tổ của bạn xử lý"
                      : "Không có lệnh phù hợp bộ lọc"}
                  </Box>
                ) : (
                  <VStack align="stretch" spacing={3} p={3} bg="#f8fafc">
                    {isTeamLeaderMode && teamLeaderBoardTab === "support_notifications"
                      ? activeBoardListItems.map((item) => (
                        <SupportNotificationCard
                          key={`support-notification-${item.serviceOrderId}`}
                          item={item}
                          onSelect={handleQueueCardSelect}
                          dispatchTagLabel={resolveDispatchTagLabel(item.dispatchTag, isTeamLeaderMode ? currentTeamSpecialty : "")}
                        />
                      ))
                      : activeBoardListItems.map((item) => (
                        <DispatchQueueCard
                          key={`dispatch-${item.serviceOrderId}`}
                          item={item}
                          isSelected={false}
                          onSelect={handleQueueCardSelect}
                          onViewCancelRequest={isTeamLeaderMode ? null : handleViewCancelRequest}
                          onViewQuote={handleViewQuote}
                          dispatchTagLabel={resolveDispatchTagLabel(item.dispatchTag, isTeamLeaderMode ? currentTeamSpecialty : "")}
                        />
                      ))}
                  </VStack>
                )}
              </Box>
            </GridItem>
          )}

          {isAssignmentPage && (
            <GridItem>
            <Box bg="white" border="1px solid" borderColor="gray.100" borderRadius="2xl" p={4} boxShadow="0 14px 32px rgba(15,23,42,0.08)">
              {!selectedItem ? (
                <Box border="1px dashed" borderColor="gray.200" borderRadius="xl" p={8} textAlign="center" color="gray.500">
                  Không tìm thấy lệnh cần phân công trong danh sách hiện tại.
                </Box>
              ) : (
                <VStack align="stretch" spacing={4}>
                  <Box p={3} border="1px solid" borderColor="gray.100" borderRadius="xl" bg="gray.50">
                    <Text fontSize="xs" color="gray.500" mb={1}>LỆNH SỬA CHỮA</Text>
                    <Heading size="md" color="#1b2559">{selectedItem.serviceOrderCode}</Heading>
                    <HStack mt={2} spacing={2} flexWrap="wrap">
                      <Badge colorScheme={(selectedItem.queueColor === "RED" ? "red" : selectedItem.queueColor === "YELLOW" ? "yellow" : selectedItem.queueColor === "GREEN" ? "green" : "gray")} borderRadius="full">
                        {selectedItem.queueColor}
                      </Badge>
                      <Badge colorScheme="purple" borderRadius="full">Chuyên môn: {selectedDispatchTagLabel}</Badge>
                      <Badge colorScheme={getQueueStateMeta(selectedItem.queueState, selectedItem.queueColor).colorScheme} borderRadius="full">
                        {getQueueStateMeta(selectedItem.queueState, selectedItem.queueColor).label}
                      </Badge>
                    </HStack>
                  </Box>

                  <Box p={3} border="1px solid" borderColor="orange.100" borderRadius="xl" bg="#fffaf0">
                    <Text fontSize="xs" color="orange.700" fontWeight="700" mb={2}>
                      THÔNG TIN PHÂN TỔ TỪ KHÁCH HÀNG
                    </Text>
                    <Stack spacing={2} fontSize="sm" color="gray.700">
                      <Text>
                        <strong>Dịch vụ yêu cầu:</strong>{" "}
                        {selectedRequestedServicesLoading
                          ? "Đang tải..."
                          : (selectedRequestedServices || selectedItem.customerRequestedServices || "Chưa ghi rõ")}
                      </Text>
                      <Text>
                        <strong>Mô tả khách hàng:</strong>{" "}
                        {selectedItem.customerMainNotes || "Không có"}
                      </Text>
                    </Stack>
                  </Box>

                  <Box p={3} border="1px solid" borderColor="blue.100" borderRadius="xl" bg="#f8fbff">
                    <Text fontSize="sm" color="gray.600">Yêu cầu lúc: {formatDateTime(selectedItem.requestedAt)}</Text>
                    <Text fontSize="sm" color="gray.600">Nhận ca lúc: {formatDateTime(selectedItem.assignedAt)}</Text>
                    <Text fontSize="sm" color="gray.600">Tổ chính: {selectedItem.primaryTeamName || selectedItem.teamName || "Chưa giao tổ"}</Text>
                    {Array.isArray(selectedItem.supportingTeamNames) && selectedItem.supportingTeamNames.length > 0 && (
                      <Text fontSize="sm" color="gray.600">Tổ phối hợp: {selectedItem.supportingTeamNames.join(", ")}</Text>
                    )}
                    <Text fontSize="sm" color="gray.600">Chờ nhận: {formatMinutes(selectedItem.pendingMinutes)}</Text>
                    <Text fontSize="sm" color="gray.600">Đang xử lý: {formatMinutes(selectedItem.assignedMinutes)}</Text>
                    <Text fontSize="sm" color="gray.600">Duyệt kỹ thuật: {selectedItem.technicalApprovedAt ? formatDateTime(selectedItem.technicalApprovedAt) : "Chưa duyệt"}</Text>
                    <Text fontSize="sm" color="gray.600">Duyệt cuối: {selectedItem.finalApprovedAt ? formatDateTime(selectedItem.finalApprovedAt) : "Chưa duyệt"}</Text>
                  </Box>

                  {extractAlertReasons(selectedItem).length > 0 && (
                    <Alert status="error" borderRadius="xl" flexDirection="column" alignItems="start" gap={1}>
                      <HStack>
                        <AlertIcon />
                        <Text fontWeight="700">Cảnh báo cần xử lý</Text>
                      </HStack>
                      {extractAlertReasons(selectedItem).map((reason) => (
                        <Text key={`alert-${reason}`} fontSize="sm">- {reason}</Text>
                      ))}
                    </Alert>
                  )}
                  {isTeamLeaderMode ? (
                    <VStack align="stretch" spacing={4}>
                      <Divider />
                      {!canCurrentTeamLeadPrimaryFlow && (
                        <Alert status="info" borderRadius="xl" bg="blue.50" border="1px solid" borderColor="blue.100">
                          <AlertIcon color="blue.500" />
                          <Text fontSize="sm" color="blue.700">
                            Lệnh này thuộc tổ chính{" "}
                            <strong>{selectedItem.primaryTeamName || selectedItem.teamName || "--"}</strong>. Tổ của bạn chỉ xử lý phần xác nhận nhân sự hỗ trợ nếu có yêu cầu.
                          </Text>
                        </Alert>
                      )}

                      {canCurrentTeamLeadPrimaryFlow && (
                        <>
                          <Box border="1px solid" borderColor="gray.100" borderRadius="xl" p={3} bg="gray.50">
                            <Text fontWeight="700" color="gray.700" mb={2}>1) Nhận xe và phân công cặp thợ</Text>

                            <FormControl>
                              <FormLabel fontSize="sm" mb={1}>Tổ thực hiện</FormLabel>
                              <FilterDropdown
                                id="leader-assignment-team"
                                value={leaderTeamId}
                                options={assignmentTeamOptions}
                                onChange={(nextValue) => {
                                  if (String(nextValue || "") !== String(leaderTeamId || "")) {
                                    setLeaderLeadTechnicianId("");
                                    setLeaderAssistantTechnicianId("");
                                  }
                                  setLeaderTeamId(nextValue);
                                }}
                                minWidth={300}
                                disabled={Boolean(currentStaffTeamId)}
                              />
                            </FormControl>

                            <FormControl mt={2}>
                              <FormLabel fontSize="sm" mb={1}>Thợ chính</FormLabel>
                              <FilterDropdown
                                id="leader-main-tech"
                                value={leaderLeadTechnicianId}
                                options={leaderPrimaryTechnicianOptions}
                                onChange={(nextValue) => {
                                  const nextLeadId = toPositiveNumber(nextValue);
                                  const currentAssistantId = toPositiveNumber(leaderAssistantTechnicianId);
                                  setLeaderLeadTechnicianId(nextValue);
                                  if (nextLeadId && currentAssistantId && nextLeadId === currentAssistantId) {
                                    setLeaderAssistantTechnicianId("");
                                  }
                                }}
                                minWidth="100%"
                                disabled={isLeaderAssignmentLocked || !hasLeaderTeamSelected}
                              />
                            </FormControl>

                            <FormControl mt={2}>
                              <FormLabel fontSize="sm" mb={1}>Thợ phụ</FormLabel>
                              <FilterDropdown
                                id="leader-assistant-tech"
                                value={leaderAssistantTechnicianId}
                                options={leaderAssistantTechnicianOptions}
                                onChange={(nextValue) => {
                                  const nextAssistantId = toPositiveNumber(nextValue);
                                  const currentLeadId = toPositiveNumber(leaderLeadTechnicianId);
                                  if (nextAssistantId && currentLeadId && nextAssistantId === currentLeadId) {
                                    setLeaderAssistantTechnicianId("");
                                    return;
                                  }
                                  setLeaderAssistantTechnicianId(nextValue);
                                }}
                                minWidth="100%"
                                disabled={isLeaderAssignmentLocked || !hasLeaderTeamSelected || !toPositiveNumber(leaderLeadTechnicianId)}
                              />
                            </FormControl>

                            <FormControl mt={2}>
                              <FormLabel fontSize="sm" mb={1}>Khoang (chỉ xem)</FormLabel>
                              <FilterDropdown
                                id="leader-bay-select"
                                value={leaderBayId}
                                options={leaderBaySelectionOptions}
                                onChange={setLeaderBayId}
                                minWidth="100%"
                                disabled
                              />
                              <Text mt={1} fontSize="xs" color="gray.500">
                                Tổ trưởng không có quyền sửa khoang. Chỉ quản đốc được đổi khoang ở bước override.
                              </Text>
                            </FormControl>

                            <Box mt={2} p={2.5} border="1px dashed" borderColor="blue.200" borderRadius="lg" bg="blue.50">
                              <Text fontSize="xs" color="blue.700" fontWeight="700" textTransform="uppercase">
                                Lưu ý phân quyền
                              </Text>
                              <Text mt={1} fontSize="sm" color="blue.700">
                                Tổ trưởng tổ chính chỉ giao cặp thợ cho tổ chính. Nhu cầu tổ phụ sẽ được gửi riêng ở bước hỗ trợ bên dưới.
                              </Text>
                            </Box>

                            <HStack mt={3} spacing={2} align="stretch">
                              <Button
                                variant="unstyled"
                                {...actionButtonBaseProps}
                                border="2px solid"
                                borderColor="#2563eb"
                                bg="white"
                                color="#1d4ed8"
                                _hover={{ bg: "#eff6ff" }}
                                onClick={() => loadDispatchSuggestion("team_leader")}
                                isLoading={suggestionLoading}
                                isDisabled={!canLeaderUseSuggestion}
                              >
                                Gợi ý tự động
                              </Button>
                              <Button
                                variant="unstyled"
                                {...actionButtonBaseProps}
                                border="2px solid"
                                borderColor="#2563eb"
                                bg="#2563eb"
                                color="white"
                                _hover={{ bg: "#1d4ed8" }}
                                onClick={handleLeaderAssign}
                                isLoading={saving}
                                isDisabled={!canLeaderAssign || !toPositiveNumber(leaderLeadTechnicianId)}
                              >
                                Nhận xe và giao ca
                              </Button>
                            </HStack>
                            {isLeaderAssignmentLocked && (
                              <Text mt={2} fontSize="sm" color="gray.600">
                                Tất cả thợ đã nhận việc nên cụm phân công và 2 nút đã bị khóa.
                              </Text>
                            )}

                            <Box mt={3}>
                              <SuggestionPreview suggestion={suggestion} />
                            </Box>
                          </Box>

                          <Box border="1px solid" borderColor="gray.100" borderRadius="xl" p={3} bg="gray.50">
                            <Text fontWeight="700" color="gray.700" mb={2}>2) Yêu cầu hỗ trợ hội chẩn</Text>
                            <HStack justify="space-between" align="center" mb={2}>
                              <Button
                                size="sm"
                                colorScheme="orange"
                                variant="outline"
                                onClick={() => setIsLeaderSupportSectionExpanded((prev) => !prev)}
                              >
                                {isLeaderSupportSectionExpanded ? "Thu gọn hỗ trợ tổ phụ" : "Cần tổ phụ hỗ trợ"}
                              </Button>
                              {leaderSupportTeamIds.length > 0 && (
                                <Badge colorScheme="orange" borderRadius="full" px={2.5}>
                                  {leaderSupportTeamIds.length} tổ đã chọn
                                </Badge>
                              )}
                            </HStack>

                            {isLeaderSupportSectionExpanded ? (
                              <>
                                <SupportingTeamSelector
                                  mode="request"
                                  value={leaderSupportTeamIds}
                                  onChange={(values) => setLeaderSupportTeamIds((values || []).map((value) => String(value)))}
                                  options={leaderSupportingTeamOptions}
                                  requestMap={leaderSupportRequestMap}
                                  onRequestedCountChange={handleLeaderSupportRequestedCountChange}
                                  emptyText="Không có tổ phụ phù hợp trong chi nhánh hiện tại."
                                  helperText="Chọn tổ phụ và số lượng thợ cần hỗ trợ. Tổ trưởng tổ phụ sẽ tự chọn thợ của tổ mình."
                                />
                                <FormControl mt={2}>
                                  <FormLabel fontSize="sm" mb={1}>Mô tả vấn đề cần phối hợp</FormLabel>
                                  <Textarea
                                    size="sm"
                                    value={leaderSupportNote}
                                    onChange={(event) => setLeaderSupportNote(event.target.value)}
                                    placeholder="Ví dụ: cần phối hợp kiểm tra hệ thống điện khi chẩn đoán lỗi máy..."
                                  />
                                </FormControl>
                                <Button
                                  mt={3}
                                  size="sm"
                                  colorScheme="orange"
                                  variant="solid"
                                  onClick={handleLeaderSupport}
                                  isLoading={saving}
                                  isDisabled={!canLeaderSupport || leaderSupportTeamIds.length === 0}
                                  bg={!canLeaderSupport || leaderSupportTeamIds.length === 0 ? "gray.200" : "orange.500"}
                                  color={!canLeaderSupport || leaderSupportTeamIds.length === 0 ? "gray.500" : "white"}
                                  borderWidth="1px"
                                  borderColor={!canLeaderSupport || leaderSupportTeamIds.length === 0 ? "gray.300" : "orange.600"}
                                  _hover={!canLeaderSupport || leaderSupportTeamIds.length === 0 ? {} : { bg: "orange.600", transform: "translateY(-1px)" }}
                                  _active={!canLeaderSupport || leaderSupportTeamIds.length === 0 ? {} : { bg: "orange.700" }}
                                  _disabled={{ opacity: 1, cursor: "not-allowed" }}
                                >
                                  Gửi yêu cầu đến tổ phụ
                                </Button>
                              </>
                            ) : (
                              <Text fontSize="sm" color="gray.500">
                                Nhấn "Cần tổ phụ hỗ trợ" để mở danh sách tổ phụ và tạo yêu cầu phối hợp.
                              </Text>
                            )}
                          </Box>
                        </>
                      )}

                      {selectedIncomingSupportAssignment && (
                        <Box border="1px solid" borderColor="orange.200" borderRadius="xl" p={3} bg="#fffdf5">
                          <Text fontWeight="700" color="orange.700" mb={2}>
                            Xác nhận nhân sự hỗ trợ của tổ phụ
                          </Text>

                          <SimpleGrid columns={{ base: 1, md: 2 }} spacing={2.5}>
                            <Box p={2.5} border="1px solid" borderColor="orange.100" borderRadius="lg" bg="white">
                              <Text fontSize="xs" color="orange.700" fontWeight="700" textTransform="uppercase">
                                Tổ chính yêu cầu
                              </Text>
                              <Text mt={1} fontSize="sm" color="gray.700" fontWeight="700">
                                {selectedItem.primaryTeamName || selectedItem.teamName || "Chưa xác định"}
                              </Text>
                              <Text mt={1} fontSize="sm" color="gray.600">
                                Cần {selectedIncomingSupportAssignment.requestedTechnicianCount} thợ từ tổ của bạn
                              </Text>
                            </Box>
                            <Box p={2.5} border="1px solid" borderColor="gray.200" borderRadius="lg" bg="white">
                              <Text fontSize="xs" color="gray.600" fontWeight="700" textTransform="uppercase">
                                Thời điểm yêu cầu
                              </Text>
                              <Text mt={1} fontSize="sm" color="gray.700" fontWeight="700">
                                {formatDateTime(selectedItem.supportRequestedAt || selectedItem.requestedAt)}
                              </Text>
                              <Text mt={1} fontSize="xs" color="gray.500">
                                Chuyên môn: {selectedDispatchTagLabel}
                              </Text>
                            </Box>
                          </SimpleGrid>

                          {!canLeaderSelectSupportTeam && (
                            <Alert mt={2.5} status="success" borderRadius="lg" bg="green.50" border="1px solid" borderColor="green.100">
                              <AlertIcon color="green.500" />
                              <Text fontSize="sm" color="green.700">
                                Đã xác nhận thợ hỗ trợ:{" "}
                                <strong>{selectedIncomingSupportAssignment.technicianName || "Đã chọn"}</strong>
                                {selectedIncomingSupportAssignment.assistantTechnicianName
                                  ? `, phụ: ${selectedIncomingSupportAssignment.assistantTechnicianName}`
                                  : ""}
                                .
                              </Text>
                            </Alert>
                          )}

                          <SimpleGrid mt={2.5} columns={{ base: 1, md: 2 }} spacing={2.5}>
                            <FormControl>
                              <FormLabel fontSize="sm" mb={1}>Thợ chính tổ phụ</FormLabel>
                              <FilterDropdown
                                id="leader-support-response-lead"
                                value={leaderSupportResponseLeadTechnicianId}
                                options={supportResponseLeadOptions}
                                onChange={(nextValue) => {
                                  const nextLeadId = toPositiveNumber(nextValue);
                                  const currentAssistantId = toPositiveNumber(leaderSupportResponseAssistantTechnicianId);
                                  setLeaderSupportResponseLeadTechnicianId(nextValue);
                                  if (nextLeadId && currentAssistantId && nextLeadId === currentAssistantId) {
                                    setLeaderSupportResponseAssistantTechnicianId("");
                                  }
                                }}
                                minWidth="100%"
                                disabled={!canLeaderSelectSupportTeam}
                              />
                            </FormControl>
                            <FormControl>
                              <FormLabel fontSize="sm" mb={1}>Thợ phụ tổ phụ</FormLabel>
                              <FilterDropdown
                                id="leader-support-response-assistant"
                                value={leaderSupportResponseAssistantTechnicianId}
                                options={supportResponseAssistantOptions}
                                onChange={(nextValue) => {
                                  const nextAssistantId = toPositiveNumber(nextValue);
                                  const currentLeadId = toPositiveNumber(leaderSupportResponseLeadTechnicianId);
                                  if (nextAssistantId && currentLeadId && nextAssistantId === currentLeadId) {
                                    setLeaderSupportResponseAssistantTechnicianId("");
                                    return;
                                  }
                                  setLeaderSupportResponseAssistantTechnicianId(nextValue);
                                }}
                                minWidth="100%"
                                disabled={!canLeaderSelectSupportTeam}
                              />
                            </FormControl>
                          </SimpleGrid>

                          <FormControl mt={2.5}>
                            <FormLabel fontSize="sm" mb={1}>Ghi chú phản hồi của tổ phụ</FormLabel>
                            <Textarea
                              size="sm"
                              value={leaderSupportResponseNote}
                              onChange={(event) => setLeaderSupportResponseNote(event.target.value)}
                              placeholder="Ví dụ: tổ phụ tiếp nhận hỗ trợ tại khoang DN-001 trong 15 phút nữa..."
                              isDisabled={!canLeaderSelectSupportTeam}
                            />
                          </FormControl>

                          <Button
                            mt={3}
                            size="sm"
                            colorScheme="orange"
                            onClick={handleLeaderSupportSelection}
                            isLoading={saving}
                            isDisabled={!canLeaderSelectSupportTeam || !toPositiveNumber(leaderSupportResponseLeadTechnicianId)}
                          >
                            {canLeaderSelectSupportTeam ? "Xác nhận thợ hỗ trợ của tổ phụ" : "Đã xác nhận nhân sự hỗ trợ"}
                          </Button>
                        </Box>
                      )}

                      {canCurrentTeamLeadPrimaryFlow && (
                        <>
                          <Box border="1px solid" borderColor="gray.100" borderRadius="xl" p={3} bg="gray.50">
                            <Text fontWeight="700" color="gray.700" mb={2}>3) Gửi hủy lệnh (chờ duyệt quản đốc)</Text>
                            <FormControl>
                              <FormLabel fontSize="sm" mb={1}>Lý do</FormLabel>
                              <FilterDropdown
                                id="leader-cancel-reason"
                                value={leaderCancelReason}
                                options={cancelReasonOptions}
                                onChange={setLeaderCancelReason}
                                minWidth="100%"
                              />
                            </FormControl>
                            <FormControl mt={2}>
                              <FormLabel fontSize="sm" mb={1}>Ghi chú bổ sung</FormLabel>
                              <Textarea
                                size="sm"
                                value={leaderCancelNote}
                                onChange={(event) => setLeaderCancelNote(event.target.value)}
                                placeholder="Thêm chi tiết phục vụ quyết định phân lại..."
                              />
                            </FormControl>
                            <Button
                              mt={3}
                              size="sm"
                              colorScheme="red"
                              variant="outline"
                              onClick={handleLeaderCancelRequest}
                              isLoading={saving}
                              isDisabled={!canLeaderCancel}
                            >
                              Gửi yêu cầu hủy
                            </Button>
                          </Box>
                        </>
                      )}
                    </VStack>
                  ) : (
                    <VStack align="stretch" spacing={4}>
                      <Divider />
                      <SimpleGrid columns={{ base: 1, xl: 2 }} spacing={4}>

                      <Box border="1px solid" borderColor="gray.100" borderRadius="xl" p={3} bg="gray.50">
                        <Text fontWeight="700" color="gray.700" mb={2}>1) Nhận xe và override phân công</Text>

                        <FormControl>
                          <FormLabel fontSize="sm" mb={1}>Tổ thực hiện</FormLabel>
                              <FilterDropdown
                                id="manager-assignment-team"
                                value={managerTeamId}
                                options={assignmentTeamOptions}
                                onChange={(nextValue) => {
                                  if (String(nextValue || "") !== String(managerTeamId || "")) {
                                    setManagerLeadTechnicianId("");
                                    setManagerAssistantTechnicianId("");
                                    setManagerBayId("");
                                  }
                                  setManagerTeamId(nextValue);
                                }}
                                minWidth={300}
                              />
                            </FormControl>

                        <FormControl mt={2}>
                          <FormLabel fontSize="sm" mb={1}>Thợ chính</FormLabel>
                          <FilterDropdown
                            id="manager-main-tech"
                            value={managerLeadTechnicianId}
                            options={managerPrimaryTechnicianOptions}
                                onChange={(nextValue) => {
                                  const nextLeadId = toPositiveNumber(nextValue);
                                  const currentAssistantId = toPositiveNumber(managerAssistantTechnicianId);
                                  setManagerLeadTechnicianId(nextValue);
                                  if (nextLeadId && currentAssistantId && nextLeadId === currentAssistantId) {
                                    setManagerAssistantTechnicianId("");
                                  }
                                }}
                                minWidth="100%"
                                disabled={!hasManagerTeamSelected}
                              />
                            </FormControl>

                        <FormControl mt={2}>
                          <FormLabel fontSize="sm" mb={1}>Thợ phụ</FormLabel>
                          <FilterDropdown
                            id="manager-assistant-tech"
                            value={managerAssistantTechnicianId}
                            options={managerAssistantTechnicianOptions}
                                onChange={(nextValue) => {
                                  const nextAssistantId = toPositiveNumber(nextValue);
                                  const currentLeadId = toPositiveNumber(managerLeadTechnicianId);
                                  if (nextAssistantId && currentLeadId && nextAssistantId === currentLeadId) {
                                    setManagerAssistantTechnicianId("");
                                    return;
                                  }
                                  setManagerAssistantTechnicianId(nextValue);
                                }}
                                minWidth="100%"
                                disabled={!hasManagerTeamSelected || !toPositiveNumber(managerLeadTechnicianId)}
                              />
                            </FormControl>

                        <FormControl mt={2}>
                          <FormLabel fontSize="sm" mb={1}>Khoang (bắt buộc)</FormLabel>
                          <FilterDropdown
                            id="manager-bay-select"
                                value={managerBayId}
                                options={baySelectionOptions}
                                onChange={setManagerBayId}
                                minWidth="100%"
                                disabled={!hasManagerTeamSelected || !toPositiveNumber(managerLeadTechnicianId)}
                              />
                            </FormControl>

                        <FormControl mt={2}>
                          <FormLabel fontSize="sm" mb={1}>Lý do override</FormLabel>
                          <Input
                            size="sm"
                            borderRadius="xl"
                            value={managerOverrideReason}
                            onChange={(event) => setManagerOverrideReason(event.target.value)}
                            placeholder="Ví dụ: xe chờ quá 10 phút, cần điều tải..."
                          />
                        </FormControl>

                        <Box mt={2} p={2.5} border="1px dashed" borderColor="blue.200" borderRadius="lg" bg="blue.50">
                          <Text fontSize="xs" color="blue.700" fontWeight="700" textTransform="uppercase">
                            Lưu ý phân quyền
                          </Text>
                          <Text mt={1} fontSize="sm" color="blue.700">
                            Quản đốc có quyền override tổ, cặp thợ, khoang và chỉ định tổ phối hợp để cân tải nhanh theo thực tế xưởng.
                          </Text>
                        </Box>

                        <HStack mt={3} spacing={2} align="stretch">
                          <Button
                            variant="unstyled"
                            {...actionButtonBaseProps}
                            border="2px solid"
                            borderColor="#2563eb"
                            bg="white"
                            color="#1d4ed8"
                            _hover={{ bg: "#eff6ff" }}
                            onClick={() => loadDispatchSuggestion("manager")}
                            isLoading={suggestionLoading}
                            isDisabled={!selectedItem || !toPositiveNumber(managerTeamId)}
                          >
                            Gợi ý tự động
                          </Button>
                          <Button
                            variant="unstyled"
                            {...actionButtonBaseProps}
                            border="2px solid"
                            borderColor="#2563eb"
                            bg="#2563eb"
                            color="white"
                            _hover={{ bg: "#1d4ed8" }}
                            onClick={handleManagerOverride}
                            isLoading={saving}
                            isDisabled={!canManagerOverride || !toPositiveNumber(managerTeamId) || !toPositiveNumber(managerLeadTechnicianId)}
                          >
                            Giao lại ngay
                          </Button>
                        </HStack>

                        <Box mt={3}>
                          <SuggestionPreview suggestion={suggestion} />
                        </Box>
                      </Box>

                      <Box border="1px solid" borderColor="gray.100" borderRadius="xl" p={3} bg="gray.50">
                        <Text fontWeight="700" color="gray.700" mb={2}>2) Chỉ định tổ phối hợp (tuỳ chọn)</Text>
                        <HStack justify="space-between" align="center" mb={2}>
                          <Button
                            size="sm"
                            colorScheme="blue"
                            variant="outline"
                            onClick={() => setIsManagerSupportSectionExpanded((prev) => !prev)}
                            isDisabled={!canManagerConfigureSupportingTeams}
                          >
                            {isManagerSupportSectionExpanded ? "Thu gọn tổ phối hợp" : "Thêm tổ phối hợp"}
                          </Button>
                          {managerSupportingTeamIds.length > 0 && (
                            <Badge colorScheme="blue" borderRadius="full" px={2.5}>
                              {managerSupportingTeamIds.length} tổ đã chọn
                            </Badge>
                          )}
                        </HStack>

                        {isManagerSupportSectionExpanded ? (
                          <SupportingTeamSelector
                            value={managerSupportingTeamIds}
                            onChange={(values) => setManagerSupportingTeamIds((values || []).map((value) => String(value)))}
                            options={managerSupportingTeamOptions}
                            assignmentMap={managerSupportingAssignmentMap}
                            onAssignmentChange={handleManagerSupportingAssignmentChange}
                            technicianOptionsByTeam={managerSupportingTechnicianOptionsByTeam}
                            emptyText="Chưa có tổ phối hợp phù hợp."
                            helperText="Tổ chính là tổ được giao trách nhiệm chính thức."
                          />
                        ) : (
                          <Text mt={1} fontSize="sm" color="gray.500">
                            {canManagerConfigureSupportingTeams
                              ? 'Nhấn "Thêm tổ phối hợp" để chọn tổ phụ và chỉ định cặp thợ cho từng tổ.'
                              : "Vui lòng chọn tổ thực hiện trước khi chỉ định tổ phối hợp."}
                          </Text>
                        )}
                      </Box>

                      <Box ref={cancelRequestSectionRef} border="1px solid" borderColor="gray.100" borderRadius="xl" p={3} bg="gray.50">
                        <Text fontWeight="700" color="gray.700" mb={2}>3) Duyệt/Từ chối yêu cầu hủy</Text>
                        {selectedItem.isCancelPendingApproval ? (
                          <VStack align="stretch" spacing={3}>
                            <Box p={3} border="1px solid" borderColor="orange.200" borderRadius="lg" bg="orange.50">
                              <Text fontSize="xs" color="orange.700" fontWeight="700" textTransform="uppercase" mb={1}>
                                Đang chờ quản đốc quyết định
                              </Text>
                              <VStack align="start" spacing={1}>
                                <Text fontSize="sm" color="gray.700">
                                  <strong>Lý do:</strong> {cancelRequestReasonText}
                                </Text>
                                <Text fontSize="sm" color="gray.700">
                                  <strong>Người gửi:</strong> {cancelRequestDetailLoading ? "Đang tải..." : cancelRequestSenderText}
                                </Text>
                                <Text fontSize="sm" color="gray.700">
                                  <strong>Thời điểm gửi:</strong> {formatDateTime(cancelRequestDetail?.cancellationRequestedAt)}
                                </Text>
                              </VStack>
                            </Box>
                            <Button
                              size="sm"
                              colorScheme="orange"
                              variant="outline"
                              onClick={handleOpenManagerCancelDecisionModal}
                            >
                              Xem chi tiết và duyệt/hủy
                            </Button>
                          </VStack>
                        ) : (
                          <Text mt={1} fontSize="sm" color="gray.500">Không có yêu cầu hủy chờ duyệt.</Text>
                        )}
                      </Box>
                      </SimpleGrid>
                    </VStack>
                  )}
                </VStack>
              )}
            </Box>
          </GridItem>
          )}
        </Grid>
      </VStack>

      <Modal isOpen={managerCancelDecisionModalOpen} onClose={handleCloseManagerCancelDecisionModal} isCentered size="lg">
        <ModalOverlay bg="blackAlpha.500" backdropFilter="blur(2px)" />
        <ModalContent borderRadius="2xl" border="1px solid" borderColor="blue.100">
          <ModalHeader color="#1b2559" fontWeight="800">Duyệt yêu cầu hủy lệnh</ModalHeader>
          <ModalCloseButton />
          <ModalBody>
            <VStack align="stretch" spacing={4}>
              <Box border="1px solid" borderColor="gray.100" borderRadius="xl" p={3} bg="gray.50">
                <Text fontSize="xs" color="gray.500" textTransform="uppercase" fontWeight="700">Lệnh sửa chữa</Text>
                <Text mt={1} fontWeight="800" color="gray.800">{selectedItem?.serviceOrderCode || "--"}</Text>
                <Text mt={1} fontSize="sm" color="gray.600">
                  {selectedItem?.customerName || "Khách hàng chưa rõ"}
                </Text>
                <HStack mt={1} spacing={1.5} flexWrap="wrap">
                  <Badge colorScheme="blue" variant="subtle" borderRadius="full" px={2.5}>
                    Biển số: {getVehicleDisplayParts(selectedItem).plate}
                  </Badge>
                  <Badge colorScheme="gray" variant="subtle" borderRadius="full" px={2.5}>
                    Dòng xe: {getVehicleDisplayParts(selectedItem).descriptor}
                  </Badge>
                </HStack>
                <Text mt={1} fontSize="sm" color="gray.700" fontWeight="600">
                  Năm SX: {getVehicleDisplayParts(selectedItem).manufactureYear} | Màu: {getVehicleDisplayParts(selectedItem).color}
                </Text>
                {getVehicleDisplayParts(selectedItem).vin ? (
                  <Text mt={1} fontSize="sm" color="gray.700" fontWeight="600">
                    VIN: {getVehicleDisplayParts(selectedItem).vin}
                  </Text>
                ) : null}
              </Box>

              <Box border="1px solid" borderColor="orange.200" borderRadius="xl" p={3} bg="orange.50">
                <Text fontSize="xs" color="orange.700" textTransform="uppercase" fontWeight="700">Lý do hủy từ tổ trưởng</Text>
                <VStack mt={1} align="start" spacing={1}>
                  <Text fontSize="sm" color="gray.700">{cancelRequestReasonText}</Text>
                  <Text fontSize="sm" color="gray.700">
                    <strong>Người gửi:</strong> {cancelRequestDetailLoading ? "Đang tải..." : cancelRequestSenderText}
                  </Text>
                  <Text fontSize="sm" color="gray.700">
                    <strong>Thời điểm gửi:</strong> {formatDateTime(cancelRequestDetail?.cancellationRequestedAt)}
                  </Text>
                </VStack>
              </Box>

              <FormControl>
                <FormLabel fontSize="sm" mb={1} fontWeight="700">Ghi chú quyết định của quản đốc</FormLabel>
                <Textarea
                  size="sm"
                  value={managerDecisionNote}
                  onChange={(event) => setManagerDecisionNote(event.target.value)}
                  placeholder="Nêu rõ lý do đồng ý/từ chối để lưu lịch sử xử lý..."
                />
              </FormControl>
            </VStack>
          </ModalBody>
          <ModalFooter gap={2}>
            <Button variant="ghost" onClick={handleCloseManagerCancelDecisionModal} isDisabled={saving}>
              Đóng
            </Button>
            <Button
              colorScheme="red"
              variant="outline"
              onClick={() => handleManagerCancelDecision(false)}
              isLoading={saving}
              isDisabled={!canManagerCancelDecision}
            >
              Từ chối hủy
            </Button>
            <Button
              colorScheme="green"
              onClick={() => handleManagerCancelDecision(true)}
              isLoading={saving}
              isDisabled={!canManagerCancelDecision}
            >
              Đồng ý hủy
            </Button>
          </ModalFooter>
        </ModalContent>
      </Modal>
    </Box>
  );
}

function SupportNotificationCard({ item, onSelect, dispatchTagLabel = "" }) {
  const requestedCount = Math.max(1, Number(item?.requestedTechnicianCount || 1));
  const vehicle = getVehicleDisplayParts(item);

  return (
    <Box
      border="1px solid"
      borderColor="orange.200"
      borderLeftWidth="6px"
      borderLeftColor="orange.400"
      borderRadius="xl"
      bg="white"
      p={4}
      boxShadow="0 8px 24px rgba(245, 158, 11, 0.14)"
      transition="all 0.16s ease"
      _hover={{ transform: "translateY(-2px)", borderColor: "orange.300" }}
    >
      <VStack align="stretch" spacing={3}>
        <HStack justify="space-between" align="start" spacing={3}>
          <Box minW={0}>
            <Text fontWeight="800" color="#1f2937" noOfLines={1}>
              {item.serviceOrderCode}
            </Text>
            <Text fontSize="sm" color="gray.600" noOfLines={1}>
              {item.customerName || "Khách hàng chưa rõ"}
            </Text>
            <HStack mt={1} spacing={1.5} flexWrap="wrap">
              <Badge colorScheme="blue" variant="subtle" borderRadius="full" px={2.5}>
                Biển số: {vehicle.plate}
              </Badge>
              <Badge colorScheme="gray" variant="subtle" borderRadius="full" px={2.5}>
                Dòng xe: {vehicle.descriptor}
              </Badge>
            </HStack>
            <Text mt={1} fontSize="sm" color="gray.700" fontWeight="600" noOfLines={1}>
              Năm SX: {vehicle.manufactureYear} | Màu: {vehicle.color}
            </Text>
            {vehicle.vin ? (
              <Text mt={1} fontSize="sm" color="gray.700" fontWeight="600" noOfLines={1}>
                VIN: {vehicle.vin}
              </Text>
            ) : null}
          </Box>
          <Badge colorScheme="orange" borderRadius="full" px={2.5}>
            Chờ tổ phụ chọn thợ
          </Badge>
        </HStack>

        <SimpleGrid columns={{ base: 1, md: 2 }} spacing={2}>
          <Box p={2.5} border="1px solid" borderColor="orange.100" borderRadius="lg" bg="orange.50">
            <Text fontSize="xs" color="orange.700" fontWeight="700" textTransform="uppercase">
              Tổ yêu cầu hỗ trợ
            </Text>
            <Text mt={1} fontSize="sm" color="gray.700" fontWeight="700">
              {item.primaryTeamName || item.teamName || "Chưa xác định"}
            </Text>
            <Text mt={1} fontSize="sm" color="gray.600">
              Cần {requestedCount} thợ từ tổ của bạn
            </Text>
          </Box>
          <Box p={2.5} border="1px solid" borderColor="gray.100" borderRadius="lg" bg="gray.50">
            <Text fontSize="xs" color="gray.600" fontWeight="700" textTransform="uppercase">
              Thời điểm yêu cầu
            </Text>
            <Text mt={1} fontSize="sm" color="gray.700" fontWeight="700">
              {formatDateTime(item.supportRequestedAt || item.requestedAt)}
            </Text>
            <Text mt={1} fontSize="xs" color="gray.500">
              Chuyên môn: {dispatchTagLabel || getDispatchTagLabel(item.dispatchTag)}
            </Text>
          </Box>
        </SimpleGrid>

        <Box p={2.5} border="1px dashed" borderColor="orange.200" borderRadius="lg" bg="#fffbeb">
          <Text fontSize="xs" color="orange.700" fontWeight="700" textTransform="uppercase" mb={1}>
            Ghi chú từ tổ chính
          </Text>
          <Text fontSize="sm" color="gray.700">
            {toText(item.supportRequestNote) || "Không có ghi chú bổ sung."}
          </Text>
        </Box>

        <HStack justify="flex-end">
          <Button
            size="sm"
            colorScheme="orange"
            borderRadius="full"
            onClick={() => onSelect(item.serviceOrderId)}
          >
            Vào chọn thợ hỗ trợ
          </Button>
        </HStack>
      </VStack>
    </Box>
  );
}

function SupportingTeamSelector({
  mode = "assignment",
  value,
  onChange,
  options,
  assignmentMap,
  onAssignmentChange,
  technicianOptionsByTeam,
  requestMap,
  onRequestedCountChange,
  emptyText,
  helperText,
}) {
  const isRequestMode = mode === "request";
  const selectedValues = Array.isArray(value) ? value.map((item) => String(item)) : [];
  const selectedSet = new Set(selectedValues);
  const safeOptions = Array.isArray(options) ? options : [];
  const safeAssignmentMap = normalizeSupportingAssignmentMap(assignmentMap);
  const safeRequestMap = normalizeSupportRequestMap(requestMap);
  const safeTechnicianOptionsByTeam = technicianOptionsByTeam && typeof technicianOptionsByTeam === "object"
    ? technicianOptionsByTeam
    : {};
  const selectedTeams = safeOptions.filter((team) => selectedSet.has(String(team.id)));
  const requestedCountOptions = Array.from({ length: 8 }, (_, index) => ({
    value: String(index + 1),
    label: `${index + 1} thợ`,
  }));

  const handleToggle = (teamId) => {
    const normalizedTeamId = String(teamId);
    const next = selectedSet.has(normalizedTeamId)
      ? selectedValues.filter((item) => item !== normalizedTeamId)
      : [...selectedValues, normalizedTeamId];
    if (typeof onChange === "function") {
      onChange(next);
    }
  };

  return (
    <Box border="1px solid" borderColor="gray.200" borderRadius="xl" p={3} bg="white">
      <HStack justify="space-between" mb={2}>
        <Text fontSize="sm" color="gray.700" fontWeight="700">
          {isRequestMode ? "Chọn tổ phụ cần hỗ trợ" : "Chọn tổ phối hợp"}
        </Text>
        <Badge colorScheme={selectedValues.length > 0 ? (isRequestMode ? "orange" : "blue") : "gray"} borderRadius="full">
          {selectedValues.length} tổ
        </Badge>
      </HStack>

      {safeOptions.length === 0 ? (
        <Text fontSize="sm" color="gray.500">{emptyText}</Text>
      ) : (
        <VStack align="stretch" spacing={2}>
          {safeOptions.map((team) => {
            const optionValue = String(team.id);
            const isSelected = selectedSet.has(optionValue);
            return (
              <Box
                key={`support-team-option-${team.id}`}
                as="button"
                type="button"
                w="100%"
                px={3}
                py={2.5}
                borderRadius="lg"
                border="1px solid"
                borderColor={isSelected ? "blue.300" : "gray.200"}
                bg={isSelected ? "blue.50" : "gray.50"}
                _hover={{ borderColor: isSelected ? "blue.400" : "gray.300", bg: isSelected ? "blue.50" : "gray.100" }}
                transition="all 0.15s ease"
                onClick={() => handleToggle(optionValue)}
              >
                <HStack justify="space-between" spacing={3}>
                  <HStack spacing={3} minW={0}>
                    <Box
                      w="18px"
                      h="18px"
                      borderRadius="md"
                      border="1.5px solid"
                      borderColor={isSelected ? "blue.500" : "gray.400"}
                      bg={isSelected ? "blue.500" : "white"}
                      display="inline-flex"
                      alignItems="center"
                      justifyContent="center"
                      color="white"
                      fontSize="11px"
                      fontWeight="700"
                      flexShrink={0}
                    >
                      {isSelected ? "✓" : ""}
                    </Box>
                    <Text
                      fontSize="sm"
                      color="gray.800"
                      fontWeight={isSelected ? "700" : "600"}
                      textAlign="left"
                      whiteSpace="normal"
                    >
                      {team.name}
                    </Text>
                  </HStack>
                  {isSelected && (
                    <Badge colorScheme="blue" borderRadius="full" px={2}>
                      Đã chọn
                    </Badge>
                  )}
                </HStack>
              </Box>
            );
          })}
        </VStack>
      )}

      {selectedTeams.length > 0 && (
        <Box mt={3}>
          <Divider mb={3} />
          <VStack align="stretch" spacing={3}>
            {selectedTeams.map((team) => {
              const teamId = String(team.id);
              if (isRequestMode) {
                const request = safeRequestMap[teamId] || { requestedTechnicianCount: "1" };
                const requestedCount = String(
                  Math.max(
                    1,
                    Math.min(8, Number(readValue(request, "requestedTechnicianCount", "RequestedTechnicianCount") || 1))
                  )
                );

                return (
                  <Box
                    key={`support-team-request-${team.id}`}
                    border="1px solid"
                    borderColor="orange.200"
                    borderRadius="lg"
                    p={3}
                    bg="#fffaf0"
                  >
                    <SimpleGrid columns={{ base: 1, md: 2 }} spacing={2.5} alignItems="end">
                      <Box>
                        <Text fontSize="sm" fontWeight="700" color="gray.700">
                          {team.name}
                        </Text>
                        <Text mt={1} fontSize="xs" color="gray.500">
                          Tổ trưởng tổ này sẽ tự chọn thợ sau khi nhận yêu cầu.
                        </Text>
                      </Box>
                      <FormControl maxW="160px" justifySelf={{ base: "stretch", md: "end" }}>
                        <FormLabel fontSize="xs" mb={1} color="gray.600">
                          Số lượng thợ cần hỗ trợ
                        </FormLabel>
                        <FilterDropdown
                          id={`support-team-request-count-${team.id}`}
                          value={requestedCount}
                          options={requestedCountOptions}
                          onChange={(nextValue) => onRequestedCountChange?.(team.id, nextValue)}
                          minWidth={120}
                          menuMinWidth={120}
                        />
                      </FormControl>
                    </SimpleGrid>
                  </Box>
                );
              }

              const assignment = safeAssignmentMap[teamId] || { technicianId: "", assistantTechnicianId: "" };
              const teamOptions = safeTechnicianOptionsByTeam[teamId] || {};
              const leadOptions = [
                { value: "", label: "-- Chọn thợ chính --" },
                ...(Array.isArray(teamOptions.lead) ? teamOptions.lead : []),
              ];
              const selectedLeadId = toPositiveNumber(assignment.technicianId);
              const selectedAssistantId = toPositiveNumber(assignment.assistantTechnicianId);
              const assistantOptions = Array.isArray(teamOptions.assistant) && teamOptions.assistant.length > 0
                ? teamOptions.assistant.filter((item) => {
                  const optionId = toPositiveNumber(item?.value);
                  if (!optionId) return true;
                  if (!selectedLeadId) return true;
                  return optionId !== selectedLeadId;
                })
                : [{ value: "", label: "-- Không chọn --" }];
              const safeAssistantValue =
                selectedLeadId && selectedAssistantId && selectedLeadId === selectedAssistantId
                  ? ""
                  : (assignment.assistantTechnicianId || "");
              const hasLeadCandidate = leadOptions.some((item) => toPositiveNumber(item.value));

              return (
                <Box
                  key={`support-team-assignment-${team.id}`}
                  border="1px solid"
                  borderColor="gray.200"
                  borderRadius="lg"
                  p={3}
                  bg="gray.50"
                >
                  <Text fontSize="sm" fontWeight="700" color="gray.700" mb={2}>
                    {team.name}
                  </Text>
                  <SimpleGrid columns={{ base: 1, md: 2 }} spacing={2.5}>
                    <FormControl>
                      <FormLabel fontSize="xs" mb={1} color="gray.600">
                        Thợ chính
                      </FormLabel>
                      <FilterDropdown
                        id={`support-team-lead-${team.id}`}
                        value={assignment.technicianId || ""}
                        options={leadOptions}
                        onChange={(nextValue) => {
                          onAssignmentChange?.(team.id, "technicianId", nextValue);
                          const nextLeadId = toPositiveNumber(nextValue);
                          const currentAssistantId = toPositiveNumber(assignment.assistantTechnicianId);
                          if (nextLeadId && currentAssistantId && nextLeadId === currentAssistantId) {
                            onAssignmentChange?.(team.id, "assistantTechnicianId", "");
                          }
                        }}
                        minWidth="100%"
                        disabled={!hasLeadCandidate}
                      />
                    </FormControl>
                    <FormControl>
                      <FormLabel fontSize="xs" mb={1} color="gray.600">
                        Thợ phụ
                      </FormLabel>
                      <FilterDropdown
                        id={`support-team-assistant-${team.id}`}
                        value={safeAssistantValue}
                        options={assistantOptions}
                        onChange={(nextValue) => {
                          const nextAssistantId = toPositiveNumber(nextValue);
                          if (selectedLeadId && nextAssistantId && nextAssistantId === selectedLeadId) {
                            onAssignmentChange?.(team.id, "assistantTechnicianId", "");
                            return;
                          }
                          onAssignmentChange?.(team.id, "assistantTechnicianId", nextValue);
                        }}
                        minWidth="100%"
                        disabled={!hasLeadCandidate}
                      />
                    </FormControl>
                  </SimpleGrid>
                </Box>
              );
            })}
          </VStack>
        </Box>
      )}

      <Text fontSize="xs" color="gray.500" mt={2}>
        {helperText}
      </Text>
    </Box>
  );
}

