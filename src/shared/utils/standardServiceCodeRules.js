export const PACKAGE_MANAGED_CODE_PREFIX = "PKG-";

export function normalizeStandardServiceCode(value) {
  return String(value || "").trim().toUpperCase();
}

export function isPackageManagedStandardServiceCode(value) {
  return normalizeStandardServiceCode(value).startsWith(PACKAGE_MANAGED_CODE_PREFIX);
}

export function getPackageManagedStandardServiceMessage() {
  return "Mã bắt đầu bằng PKG- thuộc gói dịch vụ và phải được quản lý trong Packages, không tạo ở Standard Services.";
}

export function filterPackageManagedStandardServices(services) {
  return (Array.isArray(services) ? services : []).filter(
    (service) => !isPackageManagedStandardServiceCode(service?.code ?? service?.Code)
  );
}
