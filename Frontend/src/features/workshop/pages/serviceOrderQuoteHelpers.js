import { firstNonEmptyText, readField, toPositiveNumber } from "./quotationFlowUtils";

export const SERVICE_ROW_TOOLTIP_EXPAND = "Nhấn để xem phụ tùng thuộc dịch vụ này.";
export const SERVICE_ROW_TOOLTIP_COLLAPSE = "Nhấn để thu gọn phụ tùng thuộc dịch vụ này.";
export const SERVICE_ROW_TOOLTIP_STATIC = "Dòng dịch vụ chính trong báo giá.";

export const DISCOUNT_MODE_AMOUNT = "AMOUNT";
export const DISCOUNT_MODE_PERCENT = "PERCENT";
export const SERVICE_QTY_WARNING_MESSAGE = "Số lượng dịch vụ vượt 5. Vui lòng kiểm tra lại.";
export const PART_QTY_WARNING_MESSAGE = "Số lượng phụ tùng vượt 20. Vui lòng kiểm tra lại.";

export function getQuantityWarningMessage(quantityValue, isServiceRow) {
  const quantity = Number(quantityValue);
  if (!Number.isFinite(quantity) || quantity <= 0) return "";
  if (isServiceRow && quantity > 5) return SERVICE_QTY_WARNING_MESSAGE;
  if (!isServiceRow && quantity > 20) return PART_QTY_WARNING_MESSAGE;
  return "";
}

export const EXCEL_UI_TEXT = {
  exportButton: "Xuất Excel",
  importButton: "Nhập Excel",
  exportTitle: "Xuất file Excel báo giá theo trạng thái hiện tại",
  importTitle: "Nhập lại file Excel báo giá để cập nhật cột bảo hiểm",
  noQuotationImport: "Chưa có báo giá để nhập Excel.",
  noQuotationExport: "Chưa có báo giá để xuất Excel.",
  processing: "Hệ thống đang xử lý, vui lòng chờ.",
  noStaff: "Không xác định được nhân viên thao tác.",
  orderCanceledImport: "Lệnh sửa chữa đã hủy, không thể nhập Excel.",
  orderCanceledExport: "Lệnh sửa chữa đã hủy, không thể xuất Excel.",
  quotationCanceledImport: "Báo giá đã hủy, không thể nhập Excel.",
  quotationCanceledExport: "Báo giá đã hủy, không thể xuất Excel.",
  splitLocked: "Báo giá đã khóa tách bill theo trạng thái hiện tại.",
};


export function extractServiceId(service) {
  return toPositiveNumber(readField(service, "id", "Id"));
}

export function extractServiceName(service) {
  return String(readField(service, "name", "Name", "serviceName", "ServiceName") || "").trim();
}

export function extractServicePrice(service) {
  const val = Number(readField(service, "basePrice", "BasePrice", "unitPrice", "UnitPrice", "price", "Price") || 0);
  return Number.isFinite(val) && val > 0 ? val : 0;
}

export function extractServiceTax(service) {
  const val = Number(readField(service, "taxRate", "TaxRate") || 0);
  return Number.isFinite(val) && val >= 0 ? val : 0;
}

export function extractProductId(product) {
  return toPositiveNumber(readField(product, "id", "Id", "productId", "ProductId"));
}

export function extractProductName(product) {
  return String(readField(product, "name", "Name", "productName", "ProductName") || "").trim();
}

export function extractProductPrice(product) {
  const val = Number(
    readField(product, "salePrice", "SalePrice", "retailPrice", "RetailPrice", "unitPrice", "UnitPrice", "sellingPrice", "SellingPrice") || 0,
  );
  return Number.isFinite(val) && val > 0 ? val : 0;
}

export function extractProductTax(product) {
  const val = Number(readField(product, "taxRate", "TaxRate") || 0);
  return Number.isFinite(val) && val >= 0 ? val : 0;
}

export function normalizeOrderDetailType(rawType) {
  const normalized = String(rawType || "").trim().toUpperCase();
  if (["LABOR", "SERVICE", "SERVICES", "WORK"].includes(normalized)) return "SERVICE";
  if (["PART", "PARTS", "PRODUCT", "MATERIAL", "INVENTORY"].includes(normalized)) return "PART";
  return normalized;
}

export function isValidHttpUrl(value) {
  const raw = String(value || "").trim();
  if (!raw) return false;
  try {
    const parsed = new URL(raw);
    return parsed.protocol === "http:" || parsed.protocol === "https:";
  } catch {
    return false;
  }
}

export function normalizeBooleanLike(value) {
  const normalized = String(value ?? "").trim().toLowerCase();
  return normalized === "true" || normalized === "1" || normalized === "yes";
}

export function pickPreferredActiveQuotationId(quotationRows) {
  const rows = Array.isArray(quotationRows) ? quotationRows : [];
  if (rows.length === 0) return null;
  // Rows are already sorted by VersionNo desc, so pick latest version directly.
  return Number(readField(rows[0], "id", "Id")) || null;
}

export function toCsvCell(value) {
  const normalized = String(value ?? "").replace(/"/g, "\"\"");
  return `"${normalized}"`;
}

export function resolveCustomerDisplay(order, customerById) {
  const directText = firstNonEmptyText(
    readField(order, "customerName", "CustomerName", "customerFullName", "CustomerFullName", "guestName", "GuestName"),
    order?.customer?.fullName,
    order?.customer?.FullName,
    order?.customer?.name,
    order?.customer?.Name,
  );
  if (directText) return directText;

  const customerId = toPositiveNumber(readField(order, "customerId", "CustomerId"));
  const customer = customerId ? customerById.get(customerId) : null;
  return firstNonEmptyText(customer?.fullName, customer?.FullName, customer?.name, customer?.Name, "-");
}

export function resolveVehiclePlateDisplay(order, vehicleById) {
  const directText = firstNonEmptyText(
    readField(
      order,
      "vehicleLicensePlate",
      "VehicleLicensePlate",
      "vehiclePlate",
      "VehiclePlate",
      "licensePlate",
      "LicensePlate",
      "guestVehicleLicensePlate",
      "GuestVehicleLicensePlate",
    ),
    order?.vehicle?.licensePlate,
    order?.vehicle?.LicensePlate,
    order?.customerVehicle?.licensePlate,
    order?.customerVehicle?.LicensePlate,
  );
  if (directText) return directText;

  const vehicleId = toPositiveNumber(readField(order, "vehicleId", "VehicleId"));
  const vehicle = vehicleId ? vehicleById.get(vehicleId) : null;
  return firstNonEmptyText(vehicle?.licensePlate, vehicle?.LicensePlate, vehicle?.plateNumber, vehicle?.PlateNumber, "-");
}

export function resolveVehicleBrandModelDisplay(order, vehicleById, brandById, modelById) {
  const directBrand = firstNonEmptyText(
    readField(order, "vehicleBrandName", "VehicleBrandName", "brandName", "BrandName"),
    order?.vehicle?.brandName,
    order?.vehicle?.BrandName,
    order?.customerVehicle?.brandName,
    order?.customerVehicle?.BrandName,
  );
  const directModel = firstNonEmptyText(
    readField(order, "vehicleModelName", "VehicleModelName", "modelName", "ModelName"),
    order?.vehicle?.modelName,
    order?.vehicle?.ModelName,
    order?.customerVehicle?.modelName,
    order?.customerVehicle?.ModelName,
  );
  if (directBrand || directModel) {
    return firstNonEmptyText(`${directBrand} ${directModel}`.trim(), directBrand, directModel, "-");
  }

  const vehicleId = toPositiveNumber(readField(order, "vehicleId", "VehicleId"));
  const vehicle = vehicleId ? vehicleById.get(vehicleId) : null;
  const brandId = toPositiveNumber(
    readField(order, "vehicleBrandId", "VehicleBrandId", "brandId", "BrandId"),
    readField(vehicle, "vehicleBrandId", "VehicleBrandId", "brandId", "BrandId"),
  );
  const modelId = toPositiveNumber(
    readField(order, "vehicleModelId", "VehicleModelId", "modelId", "ModelId"),
    readField(vehicle, "vehicleModelId", "VehicleModelId", "modelId", "ModelId"),
  );

  const brandFromCatalog = brandId ? brandById.get(brandId) : null;
  const modelFromCatalog = modelId ? modelById.get(modelId) : null;

  const fallbackBrand = firstNonEmptyText(
    vehicle?.brandName,
    vehicle?.BrandName,
    vehicle?.vehicleBrandName,
    vehicle?.VehicleBrandName,
    readField(brandFromCatalog, "name", "Name", "code", "Code"),
  );
  const fallbackModel = firstNonEmptyText(
    vehicle?.modelName,
    vehicle?.ModelName,
    vehicle?.vehicleModelName,
    vehicle?.VehicleModelName,
    readField(modelFromCatalog, "modelName", "ModelName", "name", "Name"),
  );
  return firstNonEmptyText(`${fallbackBrand} ${fallbackModel}`.trim(), fallbackBrand, fallbackModel, "-");
}

