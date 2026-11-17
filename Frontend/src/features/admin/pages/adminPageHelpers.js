export const EMPTY_STAFF_FORM = {
  fullName: "",
  phone: "",
  email: "",
  password: "",
  roleId: "",
  status: "active"
};

export const EMPTY_CUSTOMER_FORM = {
  fullName: "",
  phone: "",
  email: "",
  password: "",
  tierId: "",
  brandId: "",
  modelId: "",
  licensePlate: "",
  color: "",
  manufactureYear: "",
  note: "",
  isActive: true
};

export const EMPTY_BRAND_FORM = {
  code: "",
  name: "",
  countryOfOrigin: "",
  logoUrl: "",
  isActive: true
};

export const EMPTY_MODEL_FORM = {
  brandId: "",
  modelName: "",
  segment: "",
  fuelType: "",
  cylinderCapacity: ""
};

export const EMPTY_SERVICE_FORM = {
  code: "",
  name: "",
  categoryId: "",
  basePrice: "",
  standardMinutes: "",
  description: "",
  isActive: true
};

export const EMPTY_BRANCH_FORM = {
  name: "",
  address: "",
  hotline: "",
  email: "",
  status: "active",
  maxWorkSlots: 20,
  description: ""
};

export const SECTION_META = {
  staff: {
    title: "Quản lý nhân sự",
    subtitle: "Quản trị tài khoản nhân sự nội bộ và vai trò làm việc",
    addLabel: "+ Thêm nhân sự",
    searchPlaceholder: "Tìm theo tên, email, số điện thoại hoặc vai trò..."
  },
  branches: {
    title: "Quản lý chi nhánh",
    subtitle: "Cấu hình thông tin chi nhánh và vận hành",
    addLabel: "+ Thêm chi nhánh",
    searchPlaceholder: "Tìm chi nhánh theo tên, địa chỉ, hotline hoặc email..."
  },
  customers: {
    title: "Quản lý khách hàng",
    subtitle: "Quản trị tài khoản khách hàng",
    addLabel: "+ Thêm khách hàng",
    searchPlaceholder: "Tìm theo tên, số điện thoại, hạng, biển số, hãng hoặc dòng xe..."
  },
  brands: {
    title: "Quản lý hãng xe",
    subtitle: "Quản trị danh sách hãng xe dùng trong hệ thống",
    addLabel: "+ Thêm hãng xe",
    searchPlaceholder: "Tìm theo mã, tên hãng hoặc quốc gia..."
  },
  models: {
    title: "Quản lý dòng xe",
    subtitle: "Quản trị dòng xe theo từng hãng",
    addLabel: "+ Thêm dòng xe",
    searchPlaceholder: "Tìm theo dòng xe, phân khúc hoặc nhiên liệu..."
  },
  services: {
    title: "Quản lý dịch vụ",
    subtitle: "Quản trị danh mục dịch vụ sửa chữa và bảo dưỡng",
    addLabel: "+ Thêm dịch vụ",
    searchPlaceholder: "Tìm theo mã, tên dịch vụ, nhóm dịch vụ hoặc trạng thái..."
  }
};

export function normalizeText(value) {
  return String(value || "")
    .normalize("NFD")
    .replace(/[\u0300-\u036f]/g, "")
    .toLowerCase();
}

export function isTargetBusinessRole(role) {
  const text = normalizeText(`${role.roleName || ""} ${role.roleCode || ""}`);
  return (
    text.includes("admin") ||
    text.includes("le tan") ||
    text.includes("reception") ||
    text.includes("nhan vien") ||
    text.includes("staff") ||
    text.includes("tech") ||
    text.includes("technician") ||
    text.includes("ky thuat") ||
    text.includes("advisor") ||
    text.includes("service advisor") ||
    text.includes("co van") ||
    text.includes("ke toan") ||
    text.includes("account")
  );
}

export function getStatusBadge(status) {
  const normalized = String(status || "").toLowerCase();
  if (normalized === "active" || normalized === "true") return "badge-active";
  if (normalized === "suspended") return "badge-suspended";
  if (normalized === "deleted") return "badge-deleted";
  return "badge-inactive";
}

export function getStatusLabel(status) {
  const normalized = String(status || "").toLowerCase();
  if (normalized === "active" || normalized === "true") return "Đang hoạt động";
  if (normalized === "inactive" || normalized === "false") return "Ngừng hoạt động";
  if (normalized === "suspended") return "Tạm khóa";
  if (normalized === "deleted") return "Đã xóa";
  return status || "-";
}

export function fileToDataUrl(file) {
  return new Promise((resolve, reject) => {
    const reader = new FileReader();
    reader.onload = () => resolve(String(reader.result || ""));
    reader.onerror = () => reject(new Error("Không thể đọc file ảnh."));
    reader.readAsDataURL(file);
  });
}

export function blobToDataUrl(blob) {
  return new Promise((resolve, reject) => {
    const reader = new FileReader();
    reader.onload = () => resolve(String(reader.result || ""));
    reader.onerror = () => reject(new Error("Không thể xử lý ảnh đã cắt."));
    reader.readAsDataURL(blob);
  });
}
