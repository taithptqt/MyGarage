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
    label: "Cáº§n duyá»‡t",
    description: "Phiáº¿u tá»• trÆ°á»Ÿng Ä‘Ã£ duyá»‡t hoáº·c phiáº¿u cá» Ä‘á» quÃ¡ SLA",
    tabBg: "#eff6ff",
    tabBorder: "#93c5fd",
    tabText: "#1d4ed8",
    activeBg: "#2563eb",
    activeText: "white",
    cardAccent: "#2563eb",
    cardBg: "#eff6ff",
  },
  ALL_ITEMS: {
    label: "Táº¥t cáº£",
    description: "Tá»•ng cá»§a 4 tab cÃ²n láº¡i trong mÃ n hÃ¬nh Ä‘iá»u phá»‘i",
    tabBg: "#f0fdfa",
    tabBorder: "#5eead4",
    tabText: "#0f766e",
    activeBg: "#0f766e",
    activeText: "white",
    cardAccent: "#0f766e",
    cardBg: "#f0fdfa",
  },
  WHITE: {
    label: "Má»›i tiáº¿p nháº­n",
    description: "Chá» tá»• trÆ°á»Ÿng nháº­n xe vÃ  phÃ¢n cÃ´ng",
    tabBg: "#f8fafc",
    tabBorder: "#cbd5e1",
    tabText: "#334155",
    activeBg: "#0f172a",
    activeText: "white",
    cardAccent: "#94a3b8",
    cardBg: "white",
  },
  YELLOW: {
    label: "Äang cháº©n Ä‘oÃ¡n",
    description: "ÄÃ£ cÃ³ tá»• nháº­n vÃ  Ä‘ang xá»­ lÃ½",
    tabBg: "#fff8db",
    tabBorder: "#f6d365",
    tabText: "#8a6a00",
    activeBg: "#d69e2e",
    activeText: "white",
    cardAccent: "#d69e2e",
    cardBg: "#fffdf3",
  },
  RED: {
    label: "Cáº§n can thiá»‡p",
    description: "CÃ³ cáº£nh bÃ¡o há»— trá»£, há»§y lá»‡nh hoáº·c quÃ¡ SLA",
    tabBg: "#fff1f2",
    tabBorder: "#fca5a5",
    tabText: "#b91c1c",
    activeBg: "#e53e3e",
    activeText: "white",
    cardAccent: "#ef4444",
    cardBg: "#fff7f7",
  },
  GREEN: {
    label: "ÄÃ£ chuáº©n Ä‘oÃ¡n",
    description: "ÄÃ£ cÃ³ káº¿t quáº£ ká»¹ thuáº­t Ä‘á»ƒ duyá»‡t",
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
  { value: "ENGINE_CHASSIS", label: "MÃ¡y - Gáº§m" },
  { value: "ELECTRICAL_AC", label: "Äiá»‡n - MÃ¡y láº¡nh" },
  { value: "BODY_PAINT", label: "Äá»“ng sÆ¡n - ChÄƒm sÃ³c xe" },
  { value: "MAINTENANCE", label: "Báº£o dÆ°á»¡ng nhanh" },
];

const DISPATCH_TAG_LABELS = DISPATCH_TAG_OPTIONS.reduce((acc, item) => {
  acc[item.value] = item.label;
  return acc;
}, {});

const CANCEL_REASONS = ["Sai chuyÃªn mÃ´n", "QuÃ¡ táº£i khoang", "Thiáº¿u thiáº¿t bá»‹", "KhÃ¡c"];

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
  if (normalizedFallback) return `${getDispatchTagLabel(normalizedFallback)} (chuyÃªn mÃ´n tá»•)`;

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
  if (!Number.isFinite(minutes) || minutes <= 0) return "0 phÃºt";
  if (minutes < 60) return `${Math.floor(minutes)} phÃºt`;
  const hours = Math.floor(minutes / 60);
  const remain = Math.floor(minutes % 60);
  return remain > 0 ? `${hours} giá» ${remain} phÃºt` : `${hours} giá»`;
}

function getDispatchTagLabel(tag) {
  const normalized = normalizeCode(tag);
  if (!normalized) return "ChÆ°a chá»n chuyÃªn mÃ´n";

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
  NEW: { label: "Má»›i tiáº¿p nháº­n", colorScheme: "gray" },
  IN_PROGRESS: { label: "Äang cháº©n Ä‘oÃ¡n", colorScheme: "yellow" },
  ALERT: { label: "Cáº§n can thiá»‡p", colorScheme: "red" },
  DIAGNOSED: { label: "ÄÃ£ chuáº©n Ä‘oÃ¡n", colorScheme: "green" },
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

function resolveApiErrorMessage(error, fallback = "ÄÃ£ cÃ³ lá»—i xáº£y ra") {
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
  return roleName.includes("ká»¹ thuáº­t") || roleName.includes("ky thuat") || roleName.includes("tá»• trÆ°á»Ÿng") || roleName.includes("to truong");
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
    descriptor: toText(item?.vehicleDescriptor) || "ChÆ°a rÃµ dÃ²ng xe",
    manufactureYear: toText(item?.vehicleManufactureYear) || "--",
    color: toText(item?.vehicleColor) || "--",
    vin,
  };
}

function extractAlertReasons(item) {
  const reasons = [];
  if (!item) return reasons;

  if (item.isSupportRequested) {
    reasons.push("CÃ³ yÃªu cáº§u há»— trá»£ há»™i cháº©n tá»« khoang");
  }

  if (item.isCancelPendingApproval) {
    reasons.push("Äang chá» quáº£n Ä‘á»‘c duyá»‡t há»§y/phÃ¢n láº¡i");
  }

  if (item.queueColor === "RED" && !item.technicianId && item.pendingMinutes >= 10) {
    reasons.push("Xe chá» nháº­n quÃ¡ 10 phÃºt");
  }

  if (item.queueColor === "RED" && item.technicianId && item.assignedMinutes >= 45) {
    reasons.push("Chuáº©n Ä‘oÃ¡n Ä‘ang vÆ°á»£t SLA 45 phÃºt");
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
              {item.customerName || "KhÃ¡ch hÃ ng chÆ°a rÃµ"}
            </Text>
            <HStack mt={1} spacing={1.5} flexWrap="wrap">
              <Badge colorScheme="blue" variant="subtle" borderRadius="full" px={2.5}>
                Biá»ƒn sá»‘: {vehicle.plate}
              </Badge>
              <Badge colorScheme="gray" variant="subtle" borderRadius="full" px={2.5}>
                DÃ²ng xe: {vehicle.descriptor}
              </Badge>
            </HStack>
            <Text mt={1} fontSize="sm" color="gray.700" fontWeight="600" noOfLines={1}>
              NÄƒm SX: {vehicle.manufactureYear} | MÃ u: {vehicle.color}
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
            ChuyÃªn mÃ´n: {dispatchTagLabel || getDispatchTagLabel(item.dispatchTag)}
          </Badge>
          <Badge colorScheme="gray" borderRadius="full" px={2.5}>
            Chá» nháº­n: {formatMinutes(item.pendingMinutes)}
          </Badge>
          <Badge colorScheme="orange" borderRadius="full" px={2.5}>
            Äang xá»­ lÃ½: {formatMinutes(item.assignedMinutes)}
          </Badge>
        </HStack>

        <Grid templateColumns={{ base: "1fr", md: "1fr 1fr" }} gap={2}>
          <Box p={2} border="1px solid" borderColor="gray.100" borderRadius="md" bg="white">
            <Text fontSize="xs" color="gray.500" textTransform="uppercase" fontWeight="700">
              NhÃ¢n sá»±
            </Text>
            <Text fontSize="sm" color="gray.600" noOfLines={1}>
              Tá»• chÃ­nh: {item.primaryTeamName || item.teamName || "ChÆ°a giao tá»•"}
            </Text>
            {Array.isArray(item.supportingTeamNames) && item.supportingTeamNames.length > 0 && (
              <Text fontSize="sm" color="gray.600" noOfLines={2}>
                Tá»• phá»‘i há»£p: {item.supportingTeamNames.join(", ")}
              </Text>
            )}
            <Text fontSize="sm" color="gray.600" noOfLines={1}>
              Thá»£ chÃ­nh: {item.technicianName || ""}
            </Text>
            <Text fontSize="sm" color="gray.600" noOfLines={1}>
              Thá»£ phá»¥: {item.assistantTechnicianName || ""}
            </Text>
          </Box>
          <Box p={2} border="1px solid" borderColor="gray.100" borderRadius="md" bg="white">
            <Text fontSize="xs" color="gray.500" textTransform="uppercase" fontWeight="700">
              Tiáº¿n Ä‘á»™
            </Text>
            <Text fontSize="sm" color="gray.600" noOfLines={1}>
              Khoang: {item.assignedBayCode || "ChÆ°a chá»‰ Ä‘á»‹nh"}
            </Text>
            <Text fontSize="sm" color="gray.600" noOfLines={1}>
              YÃªu cáº§u lÃºc: {formatDateTime(item.requestedAt)}
            </Text>
          </Box>
        </Grid>

        {alerts.length > 0 && (
          <Box border="1px solid" borderColor="red.200" bg="red.50" borderRadius="md" p={2}>
            <Text fontSize="xs" color="red.700" textTransform="uppercase" fontWeight="700" mb={1}>
              Cáº§n can thiá»‡p
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
                Xem yÃªu cáº§u há»§y
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
                Xem chuáº©n Ä‘oÃ¡n
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
        Gá»£i Ã½ phÃ¢n cÃ´ng tá»± Ä‘á»™ng
      </Text>
      <Stack spacing={1} fontSize="sm" color="gray.700">
        <Text>Thá»£ chÃ­nh gá»£i Ã½: {suggestion.suggestedLeadTechnicianName || "--"}</Text>
        <Text>Thá»£ phá»¥ gá»£i Ã½: {suggestion.suggestedAssistantTechnicianName || "--"}</Text>
        <Text>Khoang gá»£i Ã½: {suggestion.suggestedBayCode || "--"}</Text>
      </Stack>
      {loads.length > 0 && (
        <Box mt={3}>
          <Text fontSize="xs" color="gray.500" mb={1}>
            Táº£i hiá»‡n táº¡i theo tá»•
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
    const options = [{ value: "", label: "-- Chá»n tá»• --" }];
    scopedTeamsForAssignment.forEach((team) => {
      const id = toPositiveNumber(readValue(team, "id", "Id"));
      const name = toText(readValue(team, "name", "Name"));
      if (!id) return;
      options.push({ value: String(id), label: name || `Tá»• #${id}` });
    });
    return options;
  }, [scopedTeamsForAssignment]);

  const leaderSupportingTeamOptions = useMemo(() => {
    const primaryTeamId = toPositiveNumber(leaderTeamId);
    return scopedTeamsForAssignment
      .map((team) => {
        const id = toPositiveNumber(readValue(team, "id", "Id"));
        const name = toText(readValue(team, "name", "Name"));
        return { id, name: name || (id ? `Tá»• #${id}` : "") };
      })
      .filter((team) => team.id && (!primaryTeamId || team.id !== primaryTeamId));
  }, [scopedTeamsForAssignment, leaderTeamId]);

  const managerSupportingTeamOptions = useMemo(() => {
    const primaryTeamId = toPositiveNumber(managerTeamId);
    return scopedTeamsForAssignment
      .map((team) => {
        const id = toPositiveNumber(readValue(team, "id", "Id"));
        const name = toText(readValue(team, "name", "Name"));
        return { id, name: name || (id ? `Tá»• #${id}` : "") };
      })
      .filter((team) => team.id && (!primaryTeamId || team.id !== primaryTeamId));
  }, [scopedTeamsForAssignment, managerTeamId]);

  const leaderPrimaryTechnicianOptions = useMemo(() => {
    const options = [{ value: "", label: "-- Chá»n thá»£ chÃ­nh --" }];
    leaderTechnicianOptions.forEach((staff) => {
      const id = toPositiveNumber(readValue(staff, "id", "Id"));
      const name = toText(readValue(staff, "fullName", "FullName"));
      if (!id) return;
      options.push({ value: String(id), label: name || `NhÃ¢n sá»± #${id}` });
    });
    return options;
  }, [leaderTechnicianOptions]);

  const leaderAssistantTechnicianOptions = useMemo(() => {
    const selectedLeadId = toPositiveNumber(leaderLeadTechnicianId);
    const options = [{ value: "", label: "-- KhÃ´ng chá»n --" }];
    leaderTechnicianOptions.forEach((staff) => {
      const id = toPositiveNumber(readValue(staff, "id", "Id"));
      const name = toText(readValue(staff, "fullName", "FullName"));
      if (!id) return;
      if (selectedLeadId && id === selectedLeadId) return;
      options.push({ value: String(id), label: name || `NhÃ¢n sá»± #${id}` });
    });
    return options;
  }, [leaderTechnicianOptions, leaderLeadTechnicianId]);

  const supportResponseTechnicians = useMemo(
    () => getTechniciansByTeam(currentStaffTeamId),
    [getTechniciansByTeam, currentStaffTeamId]
  );

  const supportResponseLeadOptions = useMemo(() => {
    const options = [{ value: "", label: "-- Chá»n thá»£ chÃ­nh tá»• phá»¥ --" }];
    supportResponseTechnicians.forEach((staff) => {
      const id = toPositiveNumber(readValue(staff, "id", "Id"));
      const name = toText(readValue(staff, "fullName", "FullName"));
      if (!id) return;
      options.push({ value: String(id), label: name || `NhÃ¢n sá»± #${id}` });
    });
    return options;
  }, [supportResponseTechnicians]);

  const supportResponseAssistantOptions = useMemo(() => {
    const selectedLeadId = toPositiveNumber(leaderSupportResponseLeadTechnicianId);
    const options = [{ value: "", label: "-- KhÃ´ng chá»n --" }];
    supportResponseTechnicians.forEach((staff) => {
      const id = toPositiveNumber(readValue(staff, "id", "Id"));
      const name = toText(readValue(staff, "fullName", "FullName"));
      if (!id) return;
      if (selectedLeadId && id === selectedLeadId) return;
      options.push({ value: String(id), label: name || `NhÃ¢n sá»± #${id}` });
    });
    return options;
  }, [supportResponseTechnicians, leaderSupportResponseLeadTechnicianId]);

  const managerPrimaryTechnicianOptions = useMemo(() => {
    const options = [{ value: "", label: "-- Chá»n thá»£ chÃ­nh --" }];
    managerTechnicianOptions.forEach((staff) => {
      const id = toPositiveNumber(readValue(staff, "id", "Id"));
      const name = toText(readValue(staff, "fullName", "FullName"));
      if (!id) return;
      options.push({ value: String(id), label: name || `NhÃ¢n sá»± #${id}` });
    });
    return options;
  }, [managerTechnicianOptions]);

  const managerAssistantTechnicianOptions = useMemo(() => {
    const selectedLeadId = toPositiveNumber(managerLeadTechnicianId);
    const options = [{ value: "", label: "-- KhÃ´ng chá»n --" }];
    managerTechnicianOptions.forEach((staff) => {
      const id = toPositiveNumber(readValue(staff, "id", "Id"));
      const name = toText(readValue(staff, "fullName", "FullName"));
      if (!id) return;
      if (selectedLeadId && id === selectedLeadId) return;
      options.push({ value: String(id), label: name || `NhÃ¢n sá»± #${id}` });
    });
    return options;
  }, [managerTechnicianOptions, managerLeadTechnicianId]);

  const buildTechnicianOptionsForTeam = useCallback((teamId, includeEmpty = false) => {
    const options = [];
    if (includeEmpty) {
      options.push({ value: "", label: "-- KhÃ´ng chá»n --" });
    }

    const teamTechnicians = getTechniciansByTeam(teamId);
    teamTechnicians.forEach((staff) => {
      const id = toPositiveNumber(readValue(staff, "id", "Id"));
      const name = toText(readValue(staff, "fullName", "FullName"));
      if (!id) return;
      options.push({ value: String(id), label: name || `NhÃ¢n sá»± #${id}` });
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
    const options = [{ value: "", label: "-- Chá»n khoang (báº¯t buá»™c) --" }];
    availableBays.forEach((bay) => {
      const id = toPositiveNumber(readValue(bay, "id", "Id"));
      const code = toText(readValue(bay, "code", "Code")) || toText(readValue(bay, "name", "Name")) || `Khoang #${id}`;
      const occupied = Boolean(readValue(bay, "currentOccupiedVehicleId", "CurrentOccupiedVehicleId"));
      const operational = isOperationalBayAvailable(readValue(bay, "operationalStatus", "OperationalStatus"));
      const disabled = occupied || !operational;
      if (!id) return;
      options.push({
        value: String(id),
        label: `${code} ${disabled ? "(Äang báº­n)" : "(Trá»‘ng)"}`,
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
    const options = [{ value: "", label: "Táº¥t cáº£ tá»•" }];
    activeTeams.forEach((team) => {
      const id = toPositiveNumber(readValue(team, "id", "Id"));
      const name = toText(readValue(team, "name", "Name"));
      options.push({
        value: id ? String(id) : "",
        label: name || `Tá»• #${id}`,
      });
    });
    return options;
  }, [activeTeams]);

  const dispatchTagFilterOptions = useMemo(
    () => [{ value: "", label: "Táº¥t cáº£ chuyÃªn mÃ´n" }, ...DISPATCH_TAG_OPTIONS.map((item) => ({ value: item.value, label: item.label }))],
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
      setError(resolveApiErrorMessage(err, "KhÃ´ng thá»ƒ táº£i báº£ng Ä‘iá»u phá»‘i."));
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
      { value: selectedValue, label: `${selectedBayLabel} (Äang gÃ¡n)`, disabled: true },
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
        toast({ title: "ChÆ°a chá»n lá»‡nh", status: "warning", duration: 2500, isClosable: true });
        return;
      }

      const teamId = targetMode === "team_leader" ? leaderTeamIdForAssignment : toPositiveNumber(managerTeamId);
      if (!teamId) {
        toast({ title: "Vui lÃ²ng chá»n tá»• trÆ°á»›c khi láº¥y gá»£i Ã½", status: "warning", duration: 2600, isClosable: true });
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

        toast({ title: "ÄÃ£ Ã¡p dá»¥ng gá»£i Ã½ phÃ¢n cÃ´ng", status: "success", duration: 2200, isClosable: true });
      } catch (err) {
        toast({ title: "KhÃ´ng thá»ƒ láº¥y gá»£i Ã½", description: resolveApiErrorMessage(err), status: "error", duration: 3500, isClosable: true });
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
        title: "Táº¥t cáº£ thá»£ Ä‘Ã£ nháº­n viá»‡c, khÃ´ng thá»ƒ giao láº¡i",
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
      toast({ title: "Vui lÃ²ng chá»n tá»• vÃ  thá»£ chÃ­nh", status: "warning", duration: 2600, isClosable: true });
      return;
    }

    if (assistantId && assistantId === leadId) {
      toast({ title: "Thá»£ phá»¥ khÃ´ng Ä‘Æ°á»£c trÃ¹ng thá»£ chÃ­nh", status: "warning", duration: 2600, isClosable: true });
      return;
    }

    setSaving(true);
    try {
      await serviceOrderApi.acceptDiagnosisRequest(selectedItem.serviceOrderId, leadId, {
        teamId,
        teamLeaderId: currentStaffId,
        assistantTechnicianId: assistantId || null,
      });

      toast({ title: "ÄÃ£ nháº­n xe vÃ  phÃ¢n cÃ´ng ca chuáº©n Ä‘oÃ¡n", status: "success", duration: 2500, isClosable: true });
      await fetchData();
      setSelectedServiceOrderId(selectedItem.serviceOrderId);
    } catch (err) {
      toast({ title: "KhÃ´ng thá»ƒ phÃ¢n cÃ´ng", description: resolveApiErrorMessage(err), status: "error", duration: 3500, isClosable: true });
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
        title: "Vui lÃ²ng chá»n Ã­t nháº¥t má»™t tá»• phá»¥ cáº§n há»— trá»£",
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

      toast({ title: "ÄÃ£ gá»­i yÃªu cáº§u há»— trá»£ cho tá»• phá»¥", status: "success", duration: 2500, isClosable: true });
      setLeaderSupportNote("");
      setLeaderSupportTeamIds([]);
      setLeaderSupportRequestMap({});
      setIsLeaderSupportSectionExpanded(false);
      await fetchData();
      setSelectedServiceOrderId(selectedItem.serviceOrderId);
    } catch (err) {
      toast({ title: "KhÃ´ng thá»ƒ gá»­i há»— trá»£", description: resolveApiErrorMessage(err), status: "error", duration: 3500, isClosable: true });
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
      toast({ title: "Vui lÃ²ng nháº­p lÃ½ do há»§y", status: "warning", duration: 2600, isClosable: true });
      return;
    }

    setSaving(true);
    try {
      await serviceOrderApi.requestDiagnosisCancel(selectedItem.serviceOrderId, {
        requestedByStaffId: currentStaffId,
        reason: payloadReason,
      });

      toast({ title: "ÄÃ£ gá»­i yÃªu cáº§u há»§y lá»‡nh", status: "success", duration: 2500, isClosable: true });
      setLeaderCancelNote("");
      await fetchData();
      setSelectedServiceOrderId(selectedItem.serviceOrderId);
    } catch (err) {
      toast({ title: "KhÃ´ng thá»ƒ gá»­i yÃªu cáº§u há»§y", description: resolveApiErrorMessage(err), status: "error", duration: 3500, isClosable: true });
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
      toast({ title: "Vui lÃ²ng chá»n tá»• trÆ°á»›c khi chá»n thá»£", status: "warning", duration: 2500, isClosable: true });
      return;
    }

    if (!leadId) {
      toast({ title: "Vui lÃ²ng chá»n thá»£ chÃ­nh", status: "warning", duration: 2500, isClosable: true });
      return;
    }

    if (supportingTeamIds.length > 0 && !teamId) {
      toast({ title: "Vui lÃ²ng chá»n tá»• chÃ­nh khi cÃ³ tá»• phá»‘i há»£p", status: "warning", duration: 2600, isClosable: true });
      return;
    }

    if (assistantId && assistantId === leadId) {
      toast({ title: "Thá»£ phá»¥ khÃ´ng Ä‘Æ°á»£c trÃ¹ng thá»£ chÃ­nh", status: "warning", duration: 2500, isClosable: true });
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
      const invalidTeamName = invalidTeam?.name || `Tá»• #${invalidSupportingAssignment.teamId}`;
      if (!invalidSupportingAssignment.technicianId) {
        toast({
          title: `Vui lÃ²ng chá»n thá»£ chÃ­nh cho ${invalidTeamName}`,
          status: "warning",
          duration: 3200,
          isClosable: true,
        });
      } else {
        toast({
          title: `Thá»£ phá»¥ cá»§a ${invalidTeamName} khÃ´ng Ä‘Æ°á»£c trÃ¹ng thá»£ chÃ­nh`,
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

      toast({ title: "ÄÃ£ override phÃ¢n cÃ´ng thÃ nh cÃ´ng", status: "success", duration: 2500, isClosable: true });
      await fetchData();
      setSelectedServiceOrderId(selectedItem.serviceOrderId);
    } catch (err) {
      toast({ title: "KhÃ´ng thá»ƒ override", description: resolveApiErrorMessage(err), status: "error", duration: 3500, isClosable: true });
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
        title: approve ? "ÄÃ£ duyá»‡t há»§y lá»‡nh" : "ÄÃ£ tá»« chá»‘i há»§y lá»‡nh",
        status: "success",
        duration: 2500,
        isClosable: true,
      });
      setManagerDecisionNote("");
      setManagerCancelDecisionModalOpen(false);
      await fetchData();
      setSelectedServiceOrderId(selectedItem.serviceOrderId);
    } catch (err) {
      toast({ title: "KhÃ´ng thá»ƒ xá»­ lÃ½ yÃªu cáº§u há»§y", description: resolveApiErrorMessage(err), status: "error", duration: 3500, isClosable: true });
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
        title: "KhÃ´ng tÃ¬m tháº¥y yÃªu cáº§u há»— trá»£ cho tá»• cá»§a báº¡n",
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
        title: "Vui lÃ²ng chá»n thá»£ chÃ­nh cá»§a tá»• phá»¥",
        status: "warning",
        duration: 2600,
        isClosable: true,
      });
      return;
    }

    if (assistantId && assistantId === leadId) {
      toast({
        title: "Thá»£ phá»¥ khÃ´ng Ä‘Æ°á»£c trÃ¹ng thá»£ chÃ­nh",
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
        title: "ÄÃ£ xÃ¡c nháº­n nhÃ¢n sá»± há»— trá»£ cho tá»• phá»¥",
        status: "success",
        duration: 2500,
        isClosable: true,
      });
      setLeaderSupportResponseNote("");
      await fetchData();
      setSelectedServiceOrderId(selectedItem.serviceOrderId);
    } catch (err) {
      toast({
        title: "KhÃ´ng thá»ƒ xÃ¡c nháº­n nhÃ¢n sá»± há»— trá»£",
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
      toast({ title: "KhÃ´ng cÃ³ yÃªu cáº§u há»§y chá» duyá»‡t", status: "info", duration: 2200, isClosable: true });
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
    "KhÃ´ng cÃ³ mÃ´ táº£.";
  const cancelRequestSenderText = (() => {
    const senderNameFromDetail = toText(cancelRequestDetail?.cancellationRequestedByName);
    if (senderNameFromDetail) return senderNameFromDetail;

    const senderId = toPositiveNumber(cancelRequestDetail?.cancellationRequestedBy);
    if (senderId) {
      const senderNameByStaffList = resolveStaffNameById(staffs, senderId);
      if (senderNameByStaffList) return senderNameByStaffList;
      return `NhÃ¢n sá»± #${senderId}`;
    }

    return "ChÆ°a xÃ¡c Ä‘á»‹nh";
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
              {isTeamLeaderMode ? "Äiá»u phá»‘i chuáº©n Ä‘oÃ¡n - Tá»• trÆ°á»Ÿng" : "Äiá»u phá»‘i chuáº©n Ä‘oÃ¡n - Quáº£n Ä‘á»‘c"}
            </Heading>
            <Text color="gray.600" mt={1}>
              {isAssignmentPage
                ? "Trang phÃ¢n cÃ´ng chi tiáº¿t cho tá»«ng lá»‡nh, táº­p trung thao tÃ¡c Ä‘iá»u phá»‘i vÃ  phÃª duyá»‡t."
                : "Theo dÃµi 4 giai Ä‘oáº¡n tá»« tiáº¿p nháº­n, phÃ¢n cÃ´ng Ä‘áº¿n phÃª duyá»‡t ká»¹ thuáº­t vÃ  bÃ¡o giÃ¡."}
            </Text>
            {isTeamLeaderMode && (
              <HStack mt={2} spacing={2} flexWrap="wrap">
                <Badge colorScheme="blue" variant="subtle" borderRadius="full" px={2.5} py={1}>
                  Tá»• phá»¥ trÃ¡ch: {toText(readValue(currentTeam, "name", "Name")) || "ChÆ°a xÃ¡c Ä‘á»‹nh"}
                </Badge>
                <Badge colorScheme="purple" variant="subtle" borderRadius="full" px={2.5} py={1}>
                  ChuyÃªn mÃ´n quáº£n lÃ½: {currentTeamSpecialtyLabel}
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
                Quay láº¡i
              </Button>
            )}
            {canViewSelectedQuote && (
              <Button
                colorScheme="blue"
                onClick={handleViewSelectedQuote}
              >
                Xem chuáº©n Ä‘oÃ¡n
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
            âŸ³ LÃ m má»›i
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
                  Tráº¡ng thÃ¡i Ä‘iá»u phá»‘i
                </Text>
                <Text fontSize="sm" color="gray.500">
                  Nháº¥n vÃ o má»™t tráº¡ng thÃ¡i Ä‘á»ƒ phÃ¢n loáº¡i danh sÃ¡ch lá»‡nh.
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
                Äang xem: {COLOR_META[activeColorTab]?.label || "--"} ({totalStats[activeColorTab] || 0})
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
                      Danh sÃ¡ch Ä‘iá»u phá»‘i
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
                      ThÃ´ng bÃ¡o há»— trá»£
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
                label="Lá»c theo tá»•"
                value={teamFilter}
                options={teamFilterOptions}
                onChange={setTeamFilter}
                minWidth={300}
                disabled={isTeamLeaderMode && !!currentStaffTeamId}
              />
              <FilterDropdown
                id="dispatch-tag-filter"
                label="Lá»c theo chuyÃªn mÃ´n"
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
                    <Text color="gray.500">Äang táº£i dá»¯ liá»‡u Ä‘iá»u phá»‘i...</Text>
                  </VStack>
                ) : activeBoardListItems.length === 0 ? (
                  <Box py={12} textAlign="center" color="gray.400">
                    {isTeamLeaderMode && teamLeaderBoardTab === "support_notifications"
                      ? "KhÃ´ng cÃ³ yÃªu cáº§u há»— trá»£ chá» tá»• cá»§a báº¡n xá»­ lÃ½"
                      : "KhÃ´ng cÃ³ lá»‡nh phÃ¹ há»£p bá»™ lá»c"}
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
                  KhÃ´ng tÃ¬m tháº¥y lá»‡nh cáº§n phÃ¢n cÃ´ng trong danh sÃ¡ch hiá»‡n táº¡i.
                </Box>
              ) : (
                <VStack align="stretch" spacing={4}>
                  <Box p={3} border="1px solid" borderColor="gray.100" borderRadius="xl" bg="gray.50">
                    <Text fontSize="xs" color="gray.500" mb={1}>Lá»†NH Sá»¬A CHá»®A</Text>
                    <Heading size="md" color="#1b2559">{selectedItem.serviceOrderCode}</Heading>
                    <HStack mt={2} spacing={2} flexWrap="wrap">
                      <Badge colorScheme={(selectedItem.queueColor === "RED" ? "red" : selectedItem.queueColor === "YELLOW" ? "yellow" : selectedItem.queueColor === "GREEN" ? "green" : "gray")} borderRadius="full">
                        {selectedItem.queueColor}
                      </Badge>
                      <Badge colorScheme="purple" borderRadius="full">ChuyÃªn mÃ´n: {selectedDispatchTagLabel}</Badge>
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
                        <strong>Dá»‹ch vá»¥ yÃªu cáº§u:</strong>{" "}
                        {selectedRequestedServicesLoading
                          ? "Äang táº£i..."
                          : (selectedRequestedServices || selectedItem.customerRequestedServices || "ChÆ°a ghi rÃµ")}
                      </Text>
                      <Text>
                        <strong>MÃ´ táº£ khÃ¡ch hÃ ng:</strong>{" "}
                        {selectedItem.customerMainNotes || "KhÃ´ng cÃ³"}
                      </Text>
                    </Stack>
                  </Box>

                  <Box p={3} border="1px solid" borderColor="blue.100" borderRadius="xl" bg="#f8fbff">
                    <Text fontSize="sm" color="gray.600">YÃªu cáº§u lÃºc: {formatDateTime(selectedItem.requestedAt)}</Text>
                    <Text fontSize="sm" color="gray.600">Nháº­n ca lÃºc: {formatDateTime(selectedItem.assignedAt)}</Text>
                    <Text fontSize="sm" color="gray.600">Tá»• chÃ­nh: {selectedItem.primaryTeamName || selectedItem.teamName || "ChÆ°a giao tá»•"}</Text>
                    {Array.isArray(selectedItem.supportingTeamNames) && selectedItem.supportingTeamNames.length > 0 && (
                      <Text fontSize="sm" color="gray.600">Tá»• phá»‘i há»£p: {selectedItem.supportingTeamNames.join(", ")}</Text>
                    )}
                    <Text fontSize="sm" color="gray.600">Chá» nháº­n: {formatMinutes(selectedItem.pendingMinutes)}</Text>
                    <Text fontSize="sm" color="gray.600">Äang xá»­ lÃ½: {formatMinutes(selectedItem.assignedMinutes)}</Text>
                    <Text fontSize="sm" color="gray.600">Duyá»‡t ká»¹ thuáº­t: {selectedItem.technicalApprovedAt ? formatDateTime(selectedItem.technicalApprovedAt) : "ChÆ°a duyá»‡t"}</Text>
                    <Text fontSize="sm" color="gray.600">Duyá»‡t cuá»‘i: {selectedItem.finalApprovedAt ? formatDateTime(selectedItem.finalApprovedAt) : "ChÆ°a duyá»‡t"}</Text>
                  </Box>

                  {extractAlertReasons(selectedItem).length > 0 && (
                    <Alert status="error" borderRadius="xl" flexDirection="column" alignItems="start" gap={1}>
                      <HStack>
                        <AlertIcon />
                        <Text fontWeight="700">Cáº£nh bÃ¡o cáº§n xá»­ lÃ½</Text>
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
                            Lá»‡nh nÃ y thuá»™c tá»• chÃ­nh{" "}
                            <strong>{selectedItem.primaryTeamName || selectedItem.teamName || "--"}</strong>. Tá»• cá»§a báº¡n chá»‰ xá»­ lÃ½ pháº§n xÃ¡c nháº­n nhÃ¢n sá»± há»— trá»£ náº¿u cÃ³ yÃªu cáº§u.
                          </Text>
                        </Alert>
                      )}

                      {canCurrentTeamLeadPrimaryFlow && (
                        <>
                          <Box border="1px solid" borderColor="gray.100" borderRadius="xl" p={3} bg="gray.50">
                            <Text fontWeight="700" color="gray.700" mb={2}>1) Nháº­n xe vÃ  phÃ¢n cÃ´ng cáº·p thá»£</Text>

                            <FormControl>
                              <FormLabel fontSize="sm" mb={1}>Tá»• thá»±c hiá»‡n</FormLabel>
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
                              <FormLabel fontSize="sm" mb={1}>Thá»£ chÃ­nh</FormLabel>
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
                              <FormLabel fontSize="sm" mb={1}>Thá»£ phá»¥</FormLabel>
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
                              <FormLabel fontSize="sm" mb={1}>Khoang (chá»‰ xem)</FormLabel>
                              <FilterDropdown
                                id="leader-bay-select"
                                value={leaderBayId}
                                options={leaderBaySelectionOptions}
                                onChange={setLeaderBayId}
                                minWidth="100%"
                                disabled
                              />
                              <Text mt={1} fontSize="xs" color="gray.500">
                                Tá»• trÆ°á»Ÿng khÃ´ng cÃ³ quyá»n sá»­a khoang. Chá»‰ quáº£n Ä‘á»‘c Ä‘Æ°á»£c Ä‘á»•i khoang á»Ÿ bÆ°á»›c override.
                              </Text>
                            </FormControl>

                            <Box mt={2} p={2.5} border="1px dashed" borderColor="blue.200" borderRadius="lg" bg="blue.50">
                              <Text fontSize="xs" color="blue.700" fontWeight="700" textTransform="uppercase">
                                LÆ°u Ã½ phÃ¢n quyá»n
                              </Text>
                              <Text mt={1} fontSize="sm" color="blue.700">
                                Tá»• trÆ°á»Ÿng tá»• chÃ­nh chá»‰ giao cáº·p thá»£ cho tá»• chÃ­nh. Nhu cáº§u tá»• phá»¥ sáº½ Ä‘Æ°á»£c gá»­i riÃªng á»Ÿ bÆ°á»›c há»— trá»£ bÃªn dÆ°á»›i.
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
                                Gá»£i Ã½ tá»± Ä‘á»™ng
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
                                Nháº­n xe vÃ  giao ca
                              </Button>
                            </HStack>
                            {isLeaderAssignmentLocked && (
                              <Text mt={2} fontSize="sm" color="gray.600">
                                Táº¥t cáº£ thá»£ Ä‘Ã£ nháº­n viá»‡c nÃªn cá»¥m phÃ¢n cÃ´ng vÃ  2 nÃºt Ä‘Ã£ bá»‹ khÃ³a.
                              </Text>
                            )}

                            <Box mt={3}>
                              <SuggestionPreview suggestion={suggestion} />
                            </Box>
                          </Box>

                          <Box border="1px solid" borderColor="gray.100" borderRadius="xl" p={3} bg="gray.50">
                            <Text fontWeight="700" color="gray.700" mb={2}>2) YÃªu cáº§u há»— trá»£ há»™i cháº©n</Text>
                            <HStack justify="space-between" align="center" mb={2}>
                              <Button
                                size="sm"
                                colorScheme="orange"
                                variant="outline"
                                onClick={() => setIsLeaderSupportSectionExpanded((prev) => !prev)}
                              >
                                {isLeaderSupportSectionExpanded ? "Thu gá»n há»— trá»£ tá»• phá»¥" : "Cáº§n tá»• phá»¥ há»— trá»£"}
                              </Button>
                              {leaderSupportTeamIds.length > 0 && (
                                <Badge colorScheme="orange" borderRadius="full" px={2.5}>
                                  {leaderSupportTeamIds.length} tá»• Ä‘Ã£ chá»n
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
                                  emptyText="KhÃ´ng cÃ³ tá»• phá»¥ phÃ¹ há»£p trong chi nhÃ¡nh hiá»‡n táº¡i."
                                  helperText="Chá»n tá»• phá»¥ vÃ  sá»‘ lÆ°á»£ng thá»£ cáº§n há»— trá»£. Tá»• trÆ°á»Ÿng tá»• phá»¥ sáº½ tá»± chá»n thá»£ cá»§a tá»• mÃ¬nh."
                                />
                                <FormControl mt={2}>
                                  <FormLabel fontSize="sm" mb={1}>MÃ´ táº£ váº¥n Ä‘á» cáº§n phá»‘i há»£p</FormLabel>
                                  <Textarea
                                    size="sm"
                                    value={leaderSupportNote}
                                    onChange={(event) => setLeaderSupportNote(event.target.value)}
                                    placeholder="VÃ­ dá»¥: cáº§n phá»‘i há»£p kiá»ƒm tra há»‡ thá»‘ng Ä‘iá»‡n khi cháº©n Ä‘oÃ¡n lá»—i mÃ¡y..."
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
                                  Gá»­i yÃªu cáº§u Ä‘áº¿n tá»• phá»¥
                                </Button>
                              </>
                            ) : (
                              <Text fontSize="sm" color="gray.500">
                                Nháº¥n "Cáº§n tá»• phá»¥ há»— trá»£" Ä‘á»ƒ má»Ÿ danh sÃ¡ch tá»• phá»¥ vÃ  táº¡o yÃªu cáº§u phá»‘i há»£p.
                              </Text>
                            )}
                          </Box>
                        </>
                      )}

                      {selectedIncomingSupportAssignment && (
                        <Box border="1px solid" borderColor="orange.200" borderRadius="xl" p={3} bg="#fffdf5">
                          <Text fontWeight="700" color="orange.700" mb={2}>
                            XÃ¡c nháº­n nhÃ¢n sá»± há»— trá»£ cá»§a tá»• phá»¥
                          </Text>

                          <SimpleGrid columns={{ base: 1, md: 2 }} spacing={2.5}>
                            <Box p={2.5} border="1px solid" borderColor="orange.100" borderRadius="lg" bg="white">
                              <Text fontSize="xs" color="orange.700" fontWeight="700" textTransform="uppercase">
                                Tá»• chÃ­nh yÃªu cáº§u
                              </Text>
                              <Text mt={1} fontSize="sm" color="gray.700" fontWeight="700">
                                {selectedItem.primaryTeamName || selectedItem.teamName || "ChÆ°a xÃ¡c Ä‘á»‹nh"}
                              </Text>
                              <Text mt={1} fontSize="sm" color="gray.600">
                                Cáº§n {selectedIncomingSupportAssignment.requestedTechnicianCount} thá»£ tá»« tá»• cá»§a báº¡n
                              </Text>
                            </Box>
                            <Box p={2.5} border="1px solid" borderColor="gray.200" borderRadius="lg" bg="white">
                              <Text fontSize="xs" color="gray.600" fontWeight="700" textTransform="uppercase">
                                Thá»i Ä‘iá»ƒm yÃªu cáº§u
                              </Text>
                              <Text mt={1} fontSize="sm" color="gray.700" fontWeight="700">
                                {formatDateTime(selectedItem.supportRequestedAt || selectedItem.requestedAt)}
                              </Text>
                              <Text mt={1} fontSize="xs" color="gray.500">
                                ChuyÃªn mÃ´n: {selectedDispatchTagLabel}
                              </Text>
                            </Box>
                          </SimpleGrid>

                          {!canLeaderSelectSupportTeam && (
                            <Alert mt={2.5} status="success" borderRadius="lg" bg="green.50" border="1px solid" borderColor="green.100">
                              <AlertIcon color="green.500" />
                              <Text fontSize="sm" color="green.700">
                                ÄÃ£ xÃ¡c nháº­n thá»£ há»— trá»£:{" "}
                                <strong>{selectedIncomingSupportAssignment.technicianName || "ÄÃ£ chá»n"}</strong>
                                {selectedIncomingSupportAssignment.assistantTechnicianName
                                  ? `, phá»¥: ${selectedIncomingSupportAssignment.assistantTechnicianName}`
                                  : ""}
                                .
                              </Text>
                            </Alert>
                          )}

                          <SimpleGrid mt={2.5} columns={{ base: 1, md: 2 }} spacing={2.5}>
                            <FormControl>
                              <FormLabel fontSize="sm" mb={1}>Thá»£ chÃ­nh tá»• phá»¥</FormLabel>
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
                              <FormLabel fontSize="sm" mb={1}>Thá»£ phá»¥ tá»• phá»¥</FormLabel>
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
                            <FormLabel fontSize="sm" mb={1}>Ghi chÃº pháº£n há»“i cá»§a tá»• phá»¥</FormLabel>
                            <Textarea
                              size="sm"
                              value={leaderSupportResponseNote}
                              onChange={(event) => setLeaderSupportResponseNote(event.target.value)}
                              placeholder="VÃ­ dá»¥: tá»• phá»¥ tiáº¿p nháº­n há»— trá»£ táº¡i khoang DN-001 trong 15 phÃºt ná»¯a..."
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
                            {canLeaderSelectSupportTeam ? "XÃ¡c nháº­n thá»£ há»— trá»£ cá»§a tá»• phá»¥" : "ÄÃ£ xÃ¡c nháº­n nhÃ¢n sá»± há»— trá»£"}
                          </Button>
                        </Box>
                      )}

                      {canCurrentTeamLeadPrimaryFlow && (
                        <>
                          <Box border="1px solid" borderColor="gray.100" borderRadius="xl" p={3} bg="gray.50">
                            <Text fontWeight="700" color="gray.700" mb={2}>3) Gá»­i há»§y lá»‡nh (chá» duyá»‡t quáº£n Ä‘á»‘c)</Text>
                            <FormControl>
                              <FormLabel fontSize="sm" mb={1}>LÃ½ do</FormLabel>
                              <FilterDropdown
                                id="leader-cancel-reason"
                                value={leaderCancelReason}
                                options={cancelReasonOptions}
                                onChange={setLeaderCancelReason}
                                minWidth="100%"
                              />
                            </FormControl>
                            <FormControl mt={2}>
                              <FormLabel fontSize="sm" mb={1}>Ghi chÃº bá»• sung</FormLabel>
                              <Textarea
                                size="sm"
                                value={leaderCancelNote}
                                onChange={(event) => setLeaderCancelNote(event.target.value)}
                                placeholder="ThÃªm chi tiáº¿t phá»¥c vá»¥ quyáº¿t Ä‘á»‹nh phÃ¢n láº¡i..."
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
                              Gá»­i yÃªu cáº§u há»§y
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
                        <Text fontWeight="700" color="gray.700" mb={2}>1) Nháº­n xe vÃ  override phÃ¢n cÃ´ng</Text>

                        <FormControl>
                          <FormLabel fontSize="sm" mb={1}>Tá»• thá»±c hiá»‡n</FormLabel>
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
                          <FormLabel fontSize="sm" mb={1}>Thá»£ chÃ­nh</FormLabel>
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
                          <FormLabel fontSize="sm" mb={1}>Thá»£ phá»¥</FormLabel>
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
                          <FormLabel fontSize="sm" mb={1}>Khoang (báº¯t buá»™c)</FormLabel>
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
                          <FormLabel fontSize="sm" mb={1}>LÃ½ do override</FormLabel>
                          <Input
                            size="sm"
                            borderRadius="xl"
                            value={managerOverrideReason}
                            onChange={(event) => setManagerOverrideReason(event.target.value)}
                            placeholder="VÃ­ dá»¥: xe chá» quÃ¡ 10 phÃºt, cáº§n Ä‘iá»u táº£i..."
                          />
                        </FormControl>

                        <Box mt={2} p={2.5} border="1px dashed" borderColor="blue.200" borderRadius="lg" bg="blue.50">
                          <Text fontSize="xs" color="blue.700" fontWeight="700" textTransform="uppercase">
                            LÆ°u Ã½ phÃ¢n quyá»n
                          </Text>
                          <Text mt={1} fontSize="sm" color="blue.700">
                            Quáº£n Ä‘á»‘c cÃ³ quyá»n override tá»•, cáº·p thá»£, khoang vÃ  chá»‰ Ä‘á»‹nh tá»• phá»‘i há»£p Ä‘á»ƒ cÃ¢n táº£i nhanh theo thá»±c táº¿ xÆ°á»Ÿng.
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
                            Gá»£i Ã½ tá»± Ä‘á»™ng
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
                            Giao láº¡i ngay
                          </Button>
                        </HStack>

                        <Box mt={3}>
                          <SuggestionPreview suggestion={suggestion} />
                        </Box>
                      </Box>

                      <Box border="1px solid" borderColor="gray.100" borderRadius="xl" p={3} bg="gray.50">
                        <Text fontWeight="700" color="gray.700" mb={2}>2) Chá»‰ Ä‘á»‹nh tá»• phá»‘i há»£p (tuá»³ chá»n)</Text>
                        <HStack justify="space-between" align="center" mb={2}>
                          <Button
                            size="sm"
                            colorScheme="blue"
                            variant="outline"
                            onClick={() => setIsManagerSupportSectionExpanded((prev) => !prev)}
                            isDisabled={!canManagerConfigureSupportingTeams}
                          >
                            {isManagerSupportSectionExpanded ? "Thu gá»n tá»• phá»‘i há»£p" : "ThÃªm tá»• phá»‘i há»£p"}
                          </Button>
                          {managerSupportingTeamIds.length > 0 && (
                            <Badge colorScheme="blue" borderRadius="full" px={2.5}>
                              {managerSupportingTeamIds.length} tá»• Ä‘Ã£ chá»n
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
                            emptyText="ChÆ°a cÃ³ tá»• phá»‘i há»£p phÃ¹ há»£p."
                            helperText="Tá»• chÃ­nh lÃ  tá»• Ä‘Æ°á»£c giao trÃ¡ch nhiá»‡m chÃ­nh thá»©c."
                          />
                        ) : (
                          <Text mt={1} fontSize="sm" color="gray.500">
                            {canManagerConfigureSupportingTeams
                              ? 'Nháº¥n "ThÃªm tá»• phá»‘i há»£p" Ä‘á»ƒ chá»n tá»• phá»¥ vÃ  chá»‰ Ä‘á»‹nh cáº·p thá»£ cho tá»«ng tá»•.'
                              : "Vui lÃ²ng chá»n tá»• thá»±c hiá»‡n trÆ°á»›c khi chá»‰ Ä‘á»‹nh tá»• phá»‘i há»£p."}
                          </Text>
                        )}
                      </Box>

                      <Box ref={cancelRequestSectionRef} border="1px solid" borderColor="gray.100" borderRadius="xl" p={3} bg="gray.50">
                        <Text fontWeight="700" color="gray.700" mb={2}>3) Duyá»‡t/Tá»« chá»‘i yÃªu cáº§u há»§y</Text>
                        {selectedItem.isCancelPendingApproval ? (
                          <VStack align="stretch" spacing={3}>
                            <Box p={3} border="1px solid" borderColor="orange.200" borderRadius="lg" bg="orange.50">
                              <Text fontSize="xs" color="orange.700" fontWeight="700" textTransform="uppercase" mb={1}>
                                Äang chá» quáº£n Ä‘á»‘c quyáº¿t Ä‘á»‹nh
                              </Text>
                              <VStack align="start" spacing={1}>
                                <Text fontSize="sm" color="gray.700">
                                  <strong>LÃ½ do:</strong> {cancelRequestReasonText}
                                </Text>
                                <Text fontSize="sm" color="gray.700">
                                  <strong>NgÆ°á»i gá»­i:</strong> {cancelRequestDetailLoading ? "Äang táº£i..." : cancelRequestSenderText}
                                </Text>
                                <Text fontSize="sm" color="gray.700">
                                  <strong>Thá»i Ä‘iá»ƒm gá»­i:</strong> {formatDateTime(cancelRequestDetail?.cancellationRequestedAt)}
                                </Text>
                              </VStack>
                            </Box>
                            <Button
                              size="sm"
                              colorScheme="orange"
                              variant="outline"
                              onClick={handleOpenManagerCancelDecisionModal}
                            >
                              Xem chi tiáº¿t vÃ  duyá»‡t/há»§y
                            </Button>
                          </VStack>
                        ) : (
                          <Text mt={1} fontSize="sm" color="gray.500">KhÃ´ng cÃ³ yÃªu cáº§u há»§y chá» duyá»‡t.</Text>
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
          <ModalHeader color="#1b2559" fontWeight="800">Duyá»‡t yÃªu cáº§u há»§y lá»‡nh</ModalHeader>
          <ModalCloseButton />
          <ModalBody>
            <VStack align="stretch" spacing={4}>
              <Box border="1px solid" borderColor="gray.100" borderRadius="xl" p={3} bg="gray.50">
                <Text fontSize="xs" color="gray.500" textTransform="uppercase" fontWeight="700">Lá»‡nh sá»­a chá»¯a</Text>
                <Text mt={1} fontWeight="800" color="gray.800">{selectedItem?.serviceOrderCode || "--"}</Text>
                <Text mt={1} fontSize="sm" color="gray.600">
                  {selectedItem?.customerName || "KhÃ¡ch hÃ ng chÆ°a rÃµ"}
                </Text>
                <HStack mt={1} spacing={1.5} flexWrap="wrap">
                  <Badge colorScheme="blue" variant="subtle" borderRadius="full" px={2.5}>
                    Biá»ƒn sá»‘: {getVehicleDisplayParts(selectedItem).plate}
                  </Badge>
                  <Badge colorScheme="gray" variant="subtle" borderRadius="full" px={2.5}>
                    DÃ²ng xe: {getVehicleDisplayParts(selectedItem).descriptor}
                  </Badge>
                </HStack>
                <Text mt={1} fontSize="sm" color="gray.700" fontWeight="600">
                  NÄƒm SX: {getVehicleDisplayParts(selectedItem).manufactureYear} | MÃ u: {getVehicleDisplayParts(selectedItem).color}
                </Text>
                {getVehicleDisplayParts(selectedItem).vin ? (
                  <Text mt={1} fontSize="sm" color="gray.700" fontWeight="600">
                    VIN: {getVehicleDisplayParts(selectedItem).vin}
                  </Text>
                ) : null}
              </Box>

              <Box border="1px solid" borderColor="orange.200" borderRadius="xl" p={3} bg="orange.50">
                <Text fontSize="xs" color="orange.700" textTransform="uppercase" fontWeight="700">LÃ½ do há»§y tá»« tá»• trÆ°á»Ÿng</Text>
                <VStack mt={1} align="start" spacing={1}>
                  <Text fontSize="sm" color="gray.700">{cancelRequestReasonText}</Text>
                  <Text fontSize="sm" color="gray.700">
                    <strong>NgÆ°á»i gá»­i:</strong> {cancelRequestDetailLoading ? "Äang táº£i..." : cancelRequestSenderText}
                  </Text>
                  <Text fontSize="sm" color="gray.700">
                    <strong>Thá»i Ä‘iá»ƒm gá»­i:</strong> {formatDateTime(cancelRequestDetail?.cancellationRequestedAt)}
                  </Text>
                </VStack>
              </Box>

              <FormControl>
                <FormLabel fontSize="sm" mb={1} fontWeight="700">Ghi chÃº quyáº¿t Ä‘á»‹nh cá»§a quáº£n Ä‘á»‘c</FormLabel>
                <Textarea
                  size="sm"
                  value={managerDecisionNote}
                  onChange={(event) => setManagerDecisionNote(event.target.value)}
                  placeholder="NÃªu rÃµ lÃ½ do Ä‘á»“ng Ã½/tá»« chá»‘i Ä‘á»ƒ lÆ°u lá»‹ch sá»­ xá»­ lÃ½..."
                />
              </FormControl>
            </VStack>
          </ModalBody>
          <ModalFooter gap={2}>
            <Button variant="ghost" onClick={handleCloseManagerCancelDecisionModal} isDisabled={saving}>
              ÄÃ³ng
            </Button>
            <Button
              colorScheme="red"
              variant="outline"
              onClick={() => handleManagerCancelDecision(false)}
              isLoading={saving}
              isDisabled={!canManagerCancelDecision}
            >
              Tá»« chá»‘i há»§y
            </Button>
            <Button
              colorScheme="green"
              onClick={() => handleManagerCancelDecision(true)}
              isLoading={saving}
              isDisabled={!canManagerCancelDecision}
            >
              Äá»“ng Ã½ há»§y
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
              {item.customerName || "KhÃ¡ch hÃ ng chÆ°a rÃµ"}
            </Text>
            <HStack mt={1} spacing={1.5} flexWrap="wrap">
              <Badge colorScheme="blue" variant="subtle" borderRadius="full" px={2.5}>
                Biá»ƒn sá»‘: {vehicle.plate}
              </Badge>
              <Badge colorScheme="gray" variant="subtle" borderRadius="full" px={2.5}>
                DÃ²ng xe: {vehicle.descriptor}
              </Badge>
            </HStack>
            <Text mt={1} fontSize="sm" color="gray.700" fontWeight="600" noOfLines={1}>
              NÄƒm SX: {vehicle.manufactureYear} | MÃ u: {vehicle.color}
            </Text>
            {vehicle.vin ? (
              <Text mt={1} fontSize="sm" color="gray.700" fontWeight="600" noOfLines={1}>
                VIN: {vehicle.vin}
              </Text>
            ) : null}
          </Box>
          <Badge colorScheme="orange" borderRadius="full" px={2.5}>
            Chá» tá»• phá»¥ chá»n thá»£
          </Badge>
        </HStack>

        <SimpleGrid columns={{ base: 1, md: 2 }} spacing={2}>
          <Box p={2.5} border="1px solid" borderColor="orange.100" borderRadius="lg" bg="orange.50">
            <Text fontSize="xs" color="orange.700" fontWeight="700" textTransform="uppercase">
              Tá»• yÃªu cáº§u há»— trá»£
            </Text>
            <Text mt={1} fontSize="sm" color="gray.700" fontWeight="700">
              {item.primaryTeamName || item.teamName || "ChÆ°a xÃ¡c Ä‘á»‹nh"}
            </Text>
            <Text mt={1} fontSize="sm" color="gray.600">
              Cáº§n {requestedCount} thá»£ tá»« tá»• cá»§a báº¡n
            </Text>
          </Box>
          <Box p={2.5} border="1px solid" borderColor="gray.100" borderRadius="lg" bg="gray.50">
            <Text fontSize="xs" color="gray.600" fontWeight="700" textTransform="uppercase">
              Thá»i Ä‘iá»ƒm yÃªu cáº§u
            </Text>
            <Text mt={1} fontSize="sm" color="gray.700" fontWeight="700">
              {formatDateTime(item.supportRequestedAt || item.requestedAt)}
            </Text>
            <Text mt={1} fontSize="xs" color="gray.500">
              ChuyÃªn mÃ´n: {dispatchTagLabel || getDispatchTagLabel(item.dispatchTag)}
            </Text>
          </Box>
        </SimpleGrid>

        <Box p={2.5} border="1px dashed" borderColor="orange.200" borderRadius="lg" bg="#fffbeb">
          <Text fontSize="xs" color="orange.700" fontWeight="700" textTransform="uppercase" mb={1}>
            Ghi chÃº tá»« tá»• chÃ­nh
          </Text>
          <Text fontSize="sm" color="gray.700">
            {toText(item.supportRequestNote) || "KhÃ´ng cÃ³ ghi chÃº bá»• sung."}
          </Text>
        </Box>

        <HStack justify="flex-end">
          <Button
            size="sm"
            colorScheme="orange"
            borderRadius="full"
            onClick={() => onSelect(item.serviceOrderId)}
          >
            VÃ o chá»n thá»£ há»— trá»£
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
    label: `${index + 1} thá»£`,
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
          {isRequestMode ? "Chá»n tá»• phá»¥ cáº§n há»— trá»£" : "Chá»n tá»• phá»‘i há»£p"}
        </Text>
        <Badge colorScheme={selectedValues.length > 0 ? (isRequestMode ? "orange" : "blue") : "gray"} borderRadius="full">
          {selectedValues.length} tá»•
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
                      {isSelected ? "âœ“" : ""}
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
                      ÄÃ£ chá»n
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
                          Tá»• trÆ°á»Ÿng tá»• nÃ y sáº½ tá»± chá»n thá»£ sau khi nháº­n yÃªu cáº§u.
                        </Text>
                      </Box>
                      <FormControl maxW="160px" justifySelf={{ base: "stretch", md: "end" }}>
                        <FormLabel fontSize="xs" mb={1} color="gray.600">
                          Sá»‘ lÆ°á»£ng thá»£ cáº§n há»— trá»£
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
                { value: "", label: "-- Chá»n thá»£ chÃ­nh --" },
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
                : [{ value: "", label: "-- KhÃ´ng chá»n --" }];
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
                        Thá»£ chÃ­nh
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
                        Thá»£ phá»¥
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

