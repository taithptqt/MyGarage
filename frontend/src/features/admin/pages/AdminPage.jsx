import { Navigate, useNavigate } from "react-router-dom";
import { useAuth } from "@shared/core/context/AuthContext";
import {
  createStaff,
  deleteStaff,
  getAllRoles,
  getAllStaff,
  updateStaff
} from "@features/admin/api/staffApi";
import {
  createCustomerVehicle,
  createCustomer,
  createVehicle,
  deleteCustomerVehicle,
  getAllCustomerTiers,
  getAllCustomerVehicles,
  deleteCustomer,
  getAllCustomers,
  getAllVehicles,
  updateVehicle,
  updateCustomerVehicle,
  updateCustomer
} from "@features/admin/api/customerApi";
import {
  createVehicleBrand,
  createVehicleModel,
  deleteVehicleBrand,
  deleteVehicleModel,
  getAllVehicleBrands,
  getAllVehicleModels,
  updateVehicleBrand,
  updateVehicleModel
} from "@features/admin/api/vehicleCatalogApi";
import {
  createStandardService,
  deleteStandardService,
  getAllServiceCategories,
  getAllStandardServices,
  updateStandardService
} from "@features/admin/api/serviceApi";
import {
  createBranch,
  deleteBranch,
  getAllBranches,
  updateBranch
} from "@features/admin/api/branchApi";
import { useCallback, useEffect, useMemo, useState } from "react";
import { useSearch } from "@shared/core/context/SearchContext";
import { useBranch } from "@shared/core/context/BranchContext";
import { VIETNAM_PHONE_PATTERN, isOptionalEmailValid, isValidVietnamesePhone } from "@shared/utils/contactValidation";
import { VIETNAM_LICENSE_PLATE_PATTERN, VIETNAM_LICENSE_PLATE_TITLE, isValidVietnameseLicensePlate } from "@shared/utils/licensePlateValidation";
import { formatLicensePlateByFieldName } from "@shared/utils/licensePlateInputFormatter";
import {
  filterPackageManagedStandardServices,
  getPackageManagedStandardServiceMessage,
  isPackageManagedStandardServiceCode,
  normalizeStandardServiceCode,
} from "@shared/utils/standardServiceCodeRules";
import ImageCropper from "@features/workshop/components/ImageCropper";
import TablePagination from "@shared/components/TablePagination";
import "./AdminPage.css";

const EMPTY_STAFF_FORM = {
  fullName: "",
  phone: "",
  email: "",
  password: "",
  roleId: "",
  status: "active"
};

const EMPTY_CUSTOMER_FORM = {
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

const EMPTY_BRAND_FORM = {
  code: "",
  name: "",
  countryOfOrigin: "",
  logoUrl: "",
  isActive: true
};

const EMPTY_MODEL_FORM = {
  brandId: "",
  modelName: "",
  segment: "",
  fuelType: "",
  cylinderCapacity: ""
};

const EMPTY_SERVICE_FORM = {
  code: "",
  name: "",
  categoryId: "",
  basePrice: "",
  standardMinutes: "",
  description: "",
  isActive: true
};

const EMPTY_BRANCH_FORM = {
  name: "",
  address: "",
  hotline: "",
  email: "",
  status: "active",
  maxWorkSlots: 20,
  description: ""
};

const SECTION_META = {
  staff: {
    title: "Staff Management",
    subtitle: "Manage internal staff accounts and role assignment",
    addLabel: "+ Add Staff",
    searchPlaceholder: "Search staff by name, email, phone or role..."
  },
  branches: {
    title: "Branch Management",
    subtitle: "Configure branch information and operating setup",
    addLabel: "+ Add Branch",
    searchPlaceholder: "Search branch by name, address, hotline or email..."
  },
  customers: {
    title: "Users (Customers)",
    subtitle: "Manage customer accounts only",
    addLabel: "+ Add Customer",
    searchPlaceholder: "Search by name, phone, tier, plate, brand or model..."
  },
  brands: {
    title: "Vehicle Brands",
    subtitle: "Manage car brands for service catalog",
    addLabel: "+ Add Brand",
    searchPlaceholder: "Search by code, brand name or country..."
  },
  models: {
    title: "Vehicle Models",
    subtitle: "Manage car models for each brand",
    addLabel: "+ Add Model",
    searchPlaceholder: "Search by model, segment or fuel type..."
  },
  services: {
    title: "Service Management",
    subtitle: "Manage repair and maintenance service catalog",
    addLabel: "+ Add Service",
    searchPlaceholder: "Search by code, service name, category or status..."
  }
};

function normalizeText(value) {
  return String(value || "")
    .normalize("NFD")
    .replace(/[\u0300-\u036f]/g, "")
    .toLowerCase();
}

function isTargetBusinessRole(role) {
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

function Modal({ title, onClose, children }) {
  return (
    <div className="modal-backdrop" onClick={onClose}>
      <div className="modal-box" onClick={(event) => event.stopPropagation()}>
        <div className="modal-header">
          <h2>{title}</h2>
          <button className="modal-close" onClick={onClose} type="button">
            x
          </button>
        </div>
        {children}
      </div>
    </div>
  );
}

function ConfirmDialog({ message, onConfirm, onCancel, loading }) {
  return (
    <div className="confirm-msg">
      <p>{message}</p>
      <div className="form-actions">
        <button className="btn-ghost" onClick={onCancel} type="button" disabled={loading}>
          Cancel
        </button>
        <button className="btn-danger" onClick={onConfirm} type="button" disabled={loading}>
          {loading ? "Deleting..." : "Delete"}
        </button>
      </div>
    </div>
  );
}

function StaffForm({ initial, roles, onSubmit, onCancel, loading }) {
  const [form, setForm] = useState({
    ...initial,
    roleId: initial.roleId ? String(initial.roleId) : ""
  });

  const handleChange = (event) => {
    const { name, value } = event.target;
    setForm((prev) => ({ ...prev, [name]: value }));
  };

  const handleSubmit = (event) => {
    event.preventDefault();
    onSubmit({
      ...form,
      roleId: form.roleId ? Number(form.roleId) : null
    });
  };

  return (
    <form className="admin-form" onSubmit={handleSubmit}>
      <div className="form-group">
        <label>Full Name *</label>
        <input name="fullName" value={form.fullName || ""} onChange={handleChange} required />
      </div>
      <div className="form-group">
        <label>Phone *</label>
        <input
          name="phone"
          type="tel"
          inputMode="tel"
          pattern={VIETNAM_PHONE_PATTERN}
          title="Số điện thoại hợp lệ: 0xxxxxxxxx hoặc +84xxxxxxxxx"
          value={form.phone || ""}
          onChange={handleChange}
          required
        />
      </div>
      <div className="form-group">
        <label>Email</label>
        <input name="email" type="email" value={form.email || ""} onChange={handleChange} />
      </div>
      <div className="form-group">
        <label>Role *</label>
        <select name="roleId" value={form.roleId} onChange={handleChange} required>
          <option value="">Select role</option>
          {roles.map((role) => (
            <option key={role.id} value={role.id}>
              {role.roleName || role.roleCode || `Role #${role.id}`}
            </option>
          ))}
        </select>
      </div>
      <div className="form-group">
        <label>{initial.id ? "New Password (optional)" : "Password *"}</label>
        <input
          name="password"
          type="password"
          value={form.password || ""}
          onChange={handleChange}
          required={!initial.id}
        />
      </div>
      <div className="form-group">
        <label>Status</label>
        <select name="status" value={form.status || "active"} onChange={handleChange}>
          <option value="active">Active</option>
          <option value="inactive">Inactive</option>
          <option value="suspended">Suspended</option>
        </select>
      </div>

      <div className="form-actions">
        <button className="btn-ghost" type="button" onClick={onCancel}>
          Cancel
        </button>
        <button className="btn-primary" type="submit" disabled={loading}>
          {loading ? "Saving..." : "Save"}
        </button>
      </div>
    </form>
  );
}

function CustomerForm({ initial, tiers, brands, models, onSubmit, onCancel, loading }) {
  const [form, setForm] = useState({
    ...initial,
    isActive: initial.isActive ?? true,
    tierId: initial.tierId ? String(initial.tierId) : "",
    brandId: initial.brandId ? String(initial.brandId) : "",
    modelId: initial.modelId ? String(initial.modelId) : ""
  });

  const modelOptions = useMemo(() => {
    if (!form.brandId) return models;
    return models.filter((item) => String(item.brandId) === String(form.brandId));
  }, [form.brandId, models]);

  const handleChange = (event) => {
    const { name, value } = event.target;
    setForm((prev) => {
      if (name === "brandId") {
        return { ...prev, brandId: value, modelId: "" };
      }
      if (name === "licensePlate") {
        return { ...prev, licensePlate: formatLicensePlateByFieldName(name, value) };
      }
      return { ...prev, [name]: value };
    });
  };

  const handleSubmit = (event) => {
    event.preventDefault();
    onSubmit({
      ...form,
      tierId: form.tierId ? Number(form.tierId) : null,
      brandId: form.brandId ? Number(form.brandId) : null,
      modelId: form.modelId ? Number(form.modelId) : null,
      manufactureYear: form.manufactureYear ? Number(form.manufactureYear) : null,
      isActive: form.isActive === true || form.isActive === "true"
    });
  };

  return (
    <form className="admin-form" onSubmit={handleSubmit}>
      <div className="form-group">
        <label>Full Name *</label>
        <input name="fullName" value={form.fullName || ""} onChange={handleChange} required />
      </div>
      <div className="form-group">
        <label>Phone *</label>
        <input
          name="phone"
          type="tel"
          inputMode="tel"
          pattern={VIETNAM_PHONE_PATTERN}
          title="Số điện thoại hợp lệ: 0xxxxxxxxx hoặc +84xxxxxxxxx"
          value={form.phone || ""}
          onChange={handleChange}
          required
        />
      </div>
      <div className="form-group">
        <label>Email</label>
        <input name="email" type="email" value={form.email || ""} onChange={handleChange} />
      </div>
      <div className="form-group">
        <label>Hạng khách</label>
        <select name="tierId" value={form.tierId} onChange={handleChange}>
          <option value="">Chọn hạng</option>
          {tiers.map((tier) => (
            <option key={tier.id} value={tier.id}>
              {tier.tierName || `Tier #${tier.id}`}
            </option>
          ))}
        </select>
      </div>
      <div className="form-group">
        <label>{initial.id ? "New Password (optional)" : "Password *"}</label>
        <input
          name="password"
          type="password"
          value={form.password || ""}
          onChange={handleChange}
          required={!initial.id}
        />
      </div>
      <div className="form-group">
        <label>Biển số xe</label>
        <input
          name="licensePlate"
          value={form.licensePlate || ""}
          onChange={handleChange}
          pattern={VIETNAM_LICENSE_PLATE_PATTERN}
          title={VIETNAM_LICENSE_PLATE_TITLE}
          style={{ textTransform: "uppercase" }}
        />
      </div>
      <div className="form-group">
        <label>Hãng xe</label>
        <select name="brandId" value={form.brandId} onChange={handleChange}>
          <option value="">Chọn hãng xe</option>
          {brands.map((brand) => (
            <option key={brand.id} value={brand.id}>
              {brand.name || brand.code || `Brand #${brand.id}`}
            </option>
          ))}
        </select>
      </div>
      <div className="form-group">
        <label>Dòng xe</label>
        <select name="modelId" value={form.modelId} onChange={handleChange}>
          <option value="">Chọn dòng xe</option>
          {modelOptions.map((model) => (
            <option key={model.id} value={model.id}>
              {model.modelName || `Model #${model.id}`}
            </option>
          ))}
        </select>
      </div>
      <div className="form-group">
        <label>Màu xe</label>
        <input name="color" value={form.color || ""} onChange={handleChange} />
      </div>
      <div className="form-group">
        <label>Nam SX</label>
        <input
          name="manufactureYear"
          type="number"
          min="1950"
          max="2100"
          value={form.manufactureYear || ""}
          onChange={handleChange}
        />
      </div>
      <div className="form-group">
        <label>Note</label>
        <input name="note" value={form.note || ""} onChange={handleChange} />
      </div>
      <div className="form-group">
        <label>Status</label>
        <select
          name="isActive"
          value={String(form.isActive ?? true)}
          onChange={handleChange}
        >
          <option value="true">Active</option>
          <option value="false">Inactive</option>
        </select>
      </div>

      <div className="form-actions">
        <button className="btn-ghost" type="button" onClick={onCancel}>
          Cancel
        </button>
        <button className="btn-primary" type="submit" disabled={loading}>
          {loading ? "Saving..." : "Save"}
        </button>
      </div>
    </form>
  );
}

function VehicleBrandForm({ initial, onSubmit, onCancel, loading }) {
  const [form, setForm] = useState({
    ...initial,
    isActive: initial.isActive ?? true
  });
  const [uploadingImage, setUploadingImage] = useState(false);
  const [cropSource, setCropSource] = useState("");

  const handleChange = (event) => {
    const { name, value } = event.target;
    setForm((prev) => ({ ...prev, [name]: value }));
  };

  const handleSubmit = (event) => {
    event.preventDefault();
    onSubmit({
      ...form,
      isActive: form.isActive === true || form.isActive === "true"
    });
  };

  const handleImageUpload = async (event) => {
    const file = event.target.files?.[0];
    event.target.value = "";
    if (!file) return;
    if (!file.type.startsWith("image/")) return;

    setUploadingImage(true);
    try {
      const dataUrl = await fileToDataUrl(file);
      setCropSource(dataUrl);
    } finally {
      setUploadingImage(false);
    }
  };

  const handleCropComplete = async (blob) => {
    if (!blob) {
      setCropSource("");
      return;
    }
    const dataUrl = await blobToDataUrl(blob);
    setForm((prev) => ({ ...prev, logoUrl: dataUrl }));
    setCropSource("");
  };

  return (
    <form className="admin-form" onSubmit={handleSubmit}>
      <div className="form-group">
        <label>Code *</label>
        <input name="code" value={form.code || ""} onChange={handleChange} required />
      </div>
      <div className="form-group">
        <label>Brand Name *</label>
        <input name="name" value={form.name || ""} onChange={handleChange} required />
      </div>
      <div className="form-group">
        <label>Country Of Origin</label>
        <input
          name="countryOfOrigin"
          value={form.countryOfOrigin || ""}
          onChange={handleChange}
        />
      </div>
      <div className="form-group">
        <label>Logo URL</label>
        <input name="logoUrl" value={form.logoUrl || ""} onChange={handleChange} />
      </div>
      <div className="form-group">
        <label>Upload Logo</label>
        <input type="file" accept="image/*" onChange={handleImageUpload} />
        {uploadingImage ? <small>Đang tải ảnh...</small> : null}
        {cropSource ? <small>Vui lòng cắt ảnh trước khi lưu.</small> : null}
        {form.logoUrl ? (
          <img
            src={form.logoUrl}
            alt="Logo preview"
            style={{ width: 80, height: 80, objectFit: "cover", borderRadius: 8, marginTop: 8 }}
          />
        ) : null}
      </div>
      <div className="form-group">
        <label>Status</label>
        <select
          name="isActive"
          value={String(form.isActive ?? true)}
          onChange={handleChange}
        >
          <option value="true">Active</option>
          <option value="false">Inactive</option>
        </select>
      </div>

      <div className="form-actions">
        <button className="btn-ghost" type="button" onClick={onCancel}>
          Cancel
        </button>
        <button className="btn-primary" type="submit" disabled={loading}>
          {loading ? "Saving..." : "Save"}
        </button>
      </div>
      {cropSource ? (
        <ImageCropper
          image={cropSource}
          onCropComplete={handleCropComplete}
          onCancel={() => setCropSource("")}
        />
      ) : null}
    </form>
  );
}

function VehicleModelForm({ initial, brands, onSubmit, onCancel, loading }) {
  const [form, setForm] = useState({
    ...initial,
    brandId: initial.brandId ? String(initial.brandId) : ""
  });

  const handleChange = (event) => {
    const { name, value } = event.target;
    setForm((prev) => ({ ...prev, [name]: value }));
  };

  const handleSubmit = (event) => {
    event.preventDefault();
    onSubmit({
      ...form,
      brandId: Number(form.brandId)
    });
  };

  return (
    <form className="admin-form" onSubmit={handleSubmit}>
      <div className="form-group">
        <label>Brand *</label>
        <select name="brandId" value={form.brandId} onChange={handleChange} required>
          <option value="">Select a brand</option>
          {brands.map((brand) => (
            <option key={brand.id} value={brand.id}>
              {brand.name || brand.code || `Brand #${brand.id}`}
            </option>
          ))}
        </select>
      </div>
      <div className="form-group">
        <label>Model Name *</label>
        <input name="modelName" value={form.modelName || ""} onChange={handleChange} required />
      </div>
      <div className="form-group">
        <label>Segment</label>
        <input name="segment" value={form.segment || ""} onChange={handleChange} />
      </div>
      <div className="form-group">
        <label>Fuel Type</label>
        <input name="fuelType" value={form.fuelType || ""} onChange={handleChange} />
      </div>
      <div className="form-group">
        <label>Cylinder Capacity</label>
        <input
          name="cylinderCapacity"
          value={form.cylinderCapacity || ""}
          onChange={handleChange}
        />
      </div>

      <div className="form-actions">
        <button className="btn-ghost" type="button" onClick={onCancel}>
          Cancel
        </button>
        <button className="btn-primary" type="submit" disabled={loading}>
          {loading ? "Saving..." : "Save"}
        </button>
      </div>
    </form>
  );
}

function ServiceForm({ initial, categories, onSubmit, onCancel, loading }) {
  const [form, setForm] = useState({
    ...initial,
    categoryId: initial.categoryId ? String(initial.categoryId) : "",
    isActive: initial.isActive ?? true
  });
  const [codeError, setCodeError] = useState("");

  const handleChange = (event) => {
    const { name, value } = event.target;
    const nextValue = name === "code" ? normalizeStandardServiceCode(value) : value;
    setForm((prev) => ({ ...prev, [name]: nextValue }));

    if (name === "code") {
      setCodeError(isPackageManagedStandardServiceCode(value) ? getPackageManagedStandardServiceMessage() : "");
    }
  };

  const handleSubmit = (event) => {
    event.preventDefault();
    const normalizedCode = normalizeStandardServiceCode(form.code);
    if (isPackageManagedStandardServiceCode(normalizedCode)) {
      setCodeError(getPackageManagedStandardServiceMessage());
      return;
    }

    onSubmit({
      ...form,
      code: normalizedCode,
      categoryId: form.categoryId ? Number(form.categoryId) : null,
      basePrice: form.basePrice === "" ? null : Number(form.basePrice),
      standardMinutes: form.standardMinutes === "" ? null : Number(form.standardMinutes),
      isActive: form.isActive === true || form.isActive === "true"
    });
  };

  return (
    <form className="admin-form" onSubmit={handleSubmit}>
      <div className="form-group">
        <label>Service Code *</label>
        <input name="code" value={form.code || ""} onChange={handleChange} required />
        <small className="form-helper">Codes starting with PKG- belong to Packages and should not be created here.</small>
        {codeError ? <small className="form-error-text">{codeError}</small> : null}
      </div>
      <div className="form-group">
        <label>Service Name *</label>
        <input name="name" value={form.name || ""} onChange={handleChange} required />
      </div>
      <div className="form-group">
        <label>Category *</label>
        <select name="categoryId" value={form.categoryId} onChange={handleChange} required>
          <option value="">Select category</option>
          {categories.map((category) => (
            <option key={category.id} value={category.id}>
              {category.name || category.code || `Category #${category.id}`}
            </option>
          ))}
        </select>
      </div>
      <div className="form-group">
        <label>Base Price</label>
        <input
          name="basePrice"
          type="number"
          min="0"
          step="1000"
          value={form.basePrice ?? ""}
          onChange={handleChange}
        />
      </div>
      <div className="form-group">
        <label>Estimated Minutes *</label>
        <input
          name="standardMinutes"
          type="number"
          min="1"
          value={form.standardMinutes ?? ""}
          onChange={handleChange}
          required
        />
      </div>
      <div className="form-group">
        <label>Description</label>
        <input name="description" value={form.description || ""} onChange={handleChange} />
      </div>
      <div className="form-group">
        <label>Status</label>
        <select
          name="isActive"
          value={String(form.isActive ?? true)}
          onChange={handleChange}
        >
          <option value="true">Active</option>
          <option value="false">Inactive</option>
        </select>
      </div>

      <div className="form-actions">
        <button className="btn-ghost" type="button" onClick={onCancel}>
          Cancel
        </button>
        <button className="btn-primary" type="submit" disabled={loading || Boolean(codeError)}>
          {loading ? "Saving..." : "Save"}
        </button>
      </div>
    </form>
  );
}

function BranchForm({ initial, onSubmit, onCancel, loading }) {
  const [form, setForm] = useState({
    ...initial,
    maxWorkSlots:
      initial.maxWorkSlots == null || Number.isNaN(Number(initial.maxWorkSlots))
        ? ""
        : String(initial.maxWorkSlots)
  });

  const handleChange = (event) => {
    const { name, value } = event.target;
    setForm((prev) => ({ ...prev, [name]: value }));
  };

  const handleSubmit = (event) => {
    event.preventDefault();
    onSubmit({
      ...form,
      maxWorkSlots: form.maxWorkSlots === "" ? null : Number(form.maxWorkSlots)
    });
  };

  return (
    <form className="admin-form" onSubmit={handleSubmit}>
      <div className="form-group">
        <label>Branch Name *</label>
        <input name="name" value={form.name || ""} onChange={handleChange} required />
      </div>
      <div className="form-group">
        <label>Address</label>
        <input name="address" value={form.address || ""} onChange={handleChange} />
      </div>
      <div className="form-group">
        <label>Hotline</label>
        <input
          name="hotline"
          type="tel"
          inputMode="tel"
          pattern={VIETNAM_PHONE_PATTERN}
          title="Số điện thoại hợp lệ: 0xxxxxxxxx hoặc +84xxxxxxxxx"
          value={form.hotline || ""}
          onChange={handleChange}
        />
      </div>
      <div className="form-group">
        <label>Email</label>
        <input name="email" type="email" value={form.email || ""} onChange={handleChange} />
      </div>
      <div className="form-group">
        <label>Max Work Slots</label>
        <input
          name="maxWorkSlots"
          type="number"
          min="1"
          value={form.maxWorkSlots}
          onChange={handleChange}
        />
      </div>
      <div className="form-group">
        <label>Status</label>
        <select name="status" value={form.status || "active"} onChange={handleChange}>
          <option value="active">Active</option>
          <option value="inactive">Inactive</option>
        </select>
      </div>
      <div className="form-group">
        <label>Description</label>
        <input name="description" value={form.description || ""} onChange={handleChange} />
      </div>

      <div className="form-actions">
        <button className="btn-ghost" type="button" onClick={onCancel}>
          Cancel
        </button>
        <button className="btn-primary" type="submit" disabled={loading}>
          {loading ? "Saving..." : "Save"}
        </button>
      </div>
    </form>
  );
}

function getStatusBadge(status) {
  const normalized = String(status || "").toLowerCase();
  if (normalized === "active" || normalized === "true") return "badge-active";
  if (normalized === "suspended") return "badge-suspended";
  if (normalized === "deleted") return "badge-deleted";
  return "badge-inactive";
}

function fileToDataUrl(file) {
  return new Promise((resolve, reject) => {
    const reader = new FileReader();
    reader.onload = () => resolve(String(reader.result || ""));
    reader.onerror = () => reject(new Error("Không thể đọc file ảnh."));
    reader.readAsDataURL(file);
  });
}

function blobToDataUrl(blob) {
  return new Promise((resolve, reject) => {
    const reader = new FileReader();
    reader.onload = () => resolve(String(reader.result || ""));
    reader.onerror = () => reject(new Error("Không thể xử lý ảnh đã cắt."));
    reader.readAsDataURL(blob);
  });
}

export default function AdminPage({ embedded = false, initialSection = "staff" }) {
  const { user, isAuthenticated, loading: authLoading } = useAuth();
  const {
    selectedBranchId,
    selectedBranch,
    branches: globalBranches,
    refreshBranches: refreshGlobalBranches
  } = useBranch();
  const [section, setSection] = useState(initialSection);
  const [branches, setBranches] = useState([]);
  const [staff, setStaff] = useState([]);
  const [customers, setCustomers] = useState([]);
  const [customerTiers, setCustomerTiers] = useState([]);
  const [customerVehicles, setCustomerVehicles] = useState([]);
  const [vehicles, setVehicles] = useState([]);
  const [roles, setRoles] = useState([]);
  const [brands, setBrands] = useState([]);
  const [models, setModels] = useState([]);
  const [serviceCategories, setServiceCategories] = useState([]);
  const [services, setServices] = useState([]);

  const [loading, setLoading] = useState(false);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState("");
  const [formError, setFormError] = useState("");

  const { searchQuery } = useSearch();
  const [modal, setModal] = useState(null);
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(10);

  useEffect(() => {
    setSection(initialSection || "staff");
    setModal(null);
    setFormError("");
  }, [initialSection]);

  useEffect(() => {
    if (!globalBranches?.length) return;
    setBranches(globalBranches);
  }, [globalBranches]);

  const fetchBranches = useCallback(async () => setBranches((await getAllBranches()) || []), []);
  const fetchStaff = useCallback(
    async () => setStaff((await getAllStaff(selectedBranchId)) || []),
    [selectedBranchId]
  );
  const fetchCustomers = useCallback(
    async () => setCustomers((await getAllCustomers(selectedBranchId)) || []),
    [selectedBranchId]
  );
  const fetchCustomerTiers = useCallback(async () => setCustomerTiers((await getAllCustomerTiers()) || []), []);
  const fetchCustomerVehicles = useCallback(async () => setCustomerVehicles((await getAllCustomerVehicles()) || []), []);
  const fetchVehicles = useCallback(async () => setVehicles((await getAllVehicles()) || []), []);
  const fetchRoles = useCallback(async () => setRoles((await getAllRoles()) || []), []);
  const fetchBrands = useCallback(async () => setBrands((await getAllVehicleBrands()) || []), []);
  const fetchModels = useCallback(async () => setModels((await getAllVehicleModels()) || []), []);
  const fetchServiceCategories = useCallback(
    async () => setServiceCategories((await getAllServiceCategories()) || []),
    []
  );
  const fetchServices = useCallback(
    async () => setServices(filterPackageManagedStandardServices((await getAllStandardServices(selectedBranchId)) || [])),
    [selectedBranchId]
  );

  const fetchAll = useCallback(async () => {
    setLoading(true);
    setError("");
    try {
      await Promise.all([
        fetchBranches(),
        fetchStaff(),
        fetchCustomers(),
        fetchCustomerTiers(),
        fetchCustomerVehicles(),
        fetchVehicles(),
        fetchRoles(),
        fetchBrands(),
        fetchModels(),
        fetchServiceCategories(),
        fetchServices()
      ]);
    } catch (apiError) {
      setError(apiError.message || "Failed to load admin data.");
    } finally {
      setLoading(false);
    }
  }, [
    fetchBranches,
    fetchBrands,
    fetchCustomerTiers,
    fetchCustomerVehicles,
    fetchCustomers,
    fetchModels,
    fetchServiceCategories,
    fetchRoles,
    fetchStaff,
    fetchVehicles,
    fetchServices
  ]);

  useEffect(() => {
    if (isAuthenticated) fetchAll();
  }, [fetchAll, isAuthenticated]);

  // NOTE: early returns will come AFTER all hooks below

  const roleNameMap = useMemo(() => {
    const map = new Map();
    (roles || []).forEach((role) => map.set(role.id, role.roleName || role.roleCode || `Role #${role.id}`));
    return map;
  }, [roles]);

  const tierNameMap = useMemo(() => {
    const map = new Map();
    (customerTiers || []).forEach((tier) => map.set(tier.id, tier.tierName || `Tier #${tier.id}`));
    return map;
  }, [customerTiers]);

  const customerTierOptions = useMemo(() => {
    return (customerTiers || []).filter((tier) => tier?.isDeleted !== true);
  }, [customerTiers]);

  const serviceCategoryOptions = useMemo(() => {
    return (serviceCategories || []).filter((item) => item?.isDeleted !== true && item?.isActive !== false);
  }, [serviceCategories]);

  const staffRoleOptions = useMemo(() => {
    const activeRoles = (roles || []).filter((role) => role?.isDeleted !== true && role?.isActive !== false);
    const selectedRoles = activeRoles.filter(isTargetBusinessRole);
    return selectedRoles.length > 0 ? selectedRoles : activeRoles;
  }, [roles]);

  const brandNameMap = useMemo(() => {
    const map = new Map();
    (brands || []).forEach((brand) => map.set(brand.id, brand.name || brand.code || `Brand #${brand.id}`));
    return map;
  }, [brands]);

  const serviceCategoryNameMap = useMemo(() => {
    const map = new Map();
    (serviceCategories || []).forEach((item) => {
      map.set(item.id, item.name || item.code || `Category #${item.id}`);
    });
    return map;
  }, [serviceCategories]);

  const modelMap = useMemo(() => {
    const map = new Map();
    (models || []).forEach((model) => map.set(model.id, model));
    return map;
  }, [models]);

  const vehicleMap = useMemo(() => {
    const map = new Map();
    (vehicles || []).forEach((vehicle) => map.set(vehicle.id, vehicle));
    return map;
  }, [vehicles]);

  const primaryVehicleByCustomerId = useMemo(() => {
    const map = new Map();
    (customerVehicles || [])
      .filter((item) => item?.isDeleted !== true)
      .forEach((link) => {
        const current = map.get(link.customerId);
        if (!current || link.isCurrentOwner === true) {
          map.set(link.customerId, link);
        }
      });
    return map;
  }, [customerVehicles]);

  const enrichedCustomers = useMemo(() => {
    return (customers || []).map((customer) => {
      const customerVehicle = primaryVehicleByCustomerId.get(customer.id);
      const vehicleId = customerVehicle?.vehicleId;
      const vehicle = vehicleId ? vehicleMap.get(vehicleId) : null;
      const modelId = vehicle?.modelId;
      const model = modelId ? modelMap.get(modelId) : null;
      const brandId = model?.brandId;
      return {
        ...customer,
        tierName: (tierNameMap && tierNameMap.get ? tierNameMap.get(customer.tierId) : null) || "-",
        brandId: brandId || null,
        brandName: (brandId && brandNameMap && brandNameMap.get ? brandNameMap.get(brandId) : null) || "-",
        modelId: model?.id || null,
        modelName: model?.modelName || "-",
        licensePlate: vehicle?.licensePlate || "-",
        color: vehicle?.color || "",
        manufactureYear: vehicle?.manufactureYear || "",
        customerVehicleId: customerVehicle?.id || null,
        vehicleId: vehicle?.id || null
      };
    });
  }, [brandNameMap, customers, modelMap, primaryVehicleByCustomerId, tierNameMap, vehicleMap]);

  const filteredStaff = useMemo(() => {
    const query = String(searchQuery || "").trim().toLowerCase();
    const data = staff || [];
    if (!query) return data;
    return data.filter((item) => {
      const roleName = roleNameMap.get(item.roleId) || "";
      return (
        (item.fullName || "").toLowerCase().includes(query) ||
        (item.email || "").toLowerCase().includes(query) ||
        (item.phone || "").toLowerCase().includes(query) ||
        roleName.toLowerCase().includes(query)
      );
    });
  }, [roleNameMap, searchQuery, staff]);

  const filteredCustomers = useMemo(() => {
    const query = String(searchQuery || "").trim().toLowerCase();
    const data = enrichedCustomers || [];
    if (!query) return data;
    return data.filter((item) => {
      return (
        (item.fullName || "").toLowerCase().includes(query) ||
        (item.email || "").toLowerCase().includes(query) ||
        (item.phone || "").toLowerCase().includes(query) ||
        (item.tierName || "").toLowerCase().includes(query) ||
        (item.licensePlate || "").toLowerCase().includes(query) ||
        (item.brandName || "").toLowerCase().includes(query) ||
        (item.modelName || "").toLowerCase().includes(query)
      );
    });
  }, [enrichedCustomers, searchQuery]);

  const filteredBrands = useMemo(() => {
    const query = String(searchQuery || "").trim().toLowerCase();
    const data = brands || [];
    if (!query) return data;
    return data.filter((item) => {
      return (
        (item.code || "").toLowerCase().includes(query) ||
        (item.name || "").toLowerCase().includes(query) ||
        (item.countryOfOrigin || "").toLowerCase().includes(query)
      );
    });
  }, [brands, searchQuery]);

  const filteredModels = useMemo(() => {
    const query = String(searchQuery || "").trim().toLowerCase();
    const data = models || [];
    if (!query) return data;
    return data.filter((item) => {
      const brandName = brandNameMap.get(item.brandId) || "";
      return (
        (item.modelName || "").toLowerCase().includes(query) ||
        (item.segment || "").toLowerCase().includes(query) ||
        (item.fuelType || "").toLowerCase().includes(query) ||
        brandName.toLowerCase().includes(query)
      );
    });
  }, [brandNameMap, models, searchQuery]);

  const filteredServices = useMemo(() => {
    const query = String(searchQuery || "").trim().toLowerCase();
    const data = services || [];
    if (!query) return data;
    return data.filter((item) => {
      const categoryName = serviceCategoryNameMap.get(item.categoryId) || "";
      const statusText = item.isActive === true ? "active" : "inactive";
      return (
        (item.code || "").toLowerCase().includes(query) ||
        (item.name || "").toLowerCase().includes(query) ||
        categoryName.toLowerCase().includes(query) ||
        statusText.includes(query)
      );
    });
  }, [searchQuery, serviceCategoryNameMap, services]);

  const filteredBranches = useMemo(() => {
    const query = String(searchQuery || "").trim().toLowerCase();
    const data = branches || [];
    if (!query) return data;
    return data.filter((item) => {
      return (
        (item.name || "").toLowerCase().includes(query) ||
        (item.address || "").toLowerCase().includes(query) ||
        (item.hotline || "").toLowerCase().includes(query) ||
        (item.email || "").toLowerCase().includes(query)
      );
    });
  }, [branches, searchQuery]);

  // Early returns AFTER all hooks (required by React Rules of Hooks)
  if (authLoading) return <div className="loading-state"><div className="spinner" /><p>Đang xác thực...</p></div>;
  if (!isAuthenticated) return <Navigate to="/login" replace />;

  const visibleRows =
    section === "branches"
      ? filteredBranches
      : section === "staff"
      ? filteredStaff
      : section === "customers"
        ? filteredCustomers
        : section === "brands"
          ? filteredBrands
          : section === "models"
            ? filteredModels
            : filteredServices;

  const totalPages = Math.max(1, Math.ceil(visibleRows.length / pageSize));
  const pagedRows = useMemo(() => {
    const start = (page - 1) * pageSize;
    return visibleRows.slice(start, start + pageSize);
  }, [visibleRows, page, pageSize]);

  useEffect(() => {
    setPage(1);
  }, [section, searchQuery, pageSize]);

  useEffect(() => {
    if (page > totalPages) {
      setPage(totalPages);
    }
  }, [page, totalPages]);

  const openModal = (entity, mode, item = null) => {
    setFormError("");
    setModal({ entity, mode, item });
  };

  const closeModal = () => {
    setFormError("");
    setModal(null);
  };

  const handleLogout = () => {
    clearAuth();
    navigate("/login", { replace: true });
  };

  const handleCreateClick = () => {
    if (section === "branches") return openModal("branch", "create");
    if (section === "staff") return openModal("staff", "create");
    if (section === "customers") return openModal("customer", "create");
    if (section === "brands") return openModal("brand", "create");
    if (section === "models") return openModal("model", "create");
    return openModal("service", "create");
  };

  const reloadSectionData = async (entity) => {
    if (entity === "branch") {
      await Promise.all([fetchBranches(), refreshGlobalBranches()]);
      return;
    }
    if (entity === "staff") return Promise.all([fetchStaff(), fetchRoles()]);
    if (entity === "customer") {
      return Promise.all([
        fetchCustomers(),
        fetchCustomerTiers(),
        fetchCustomerVehicles(),
        fetchVehicles()
      ]);
    }
    if (entity === "brand") return Promise.all([fetchBrands(), fetchModels()]);
    if (entity === "model") return fetchModels();
    if (entity === "service") return Promise.all([fetchServices(), fetchServiceCategories()]);
    return fetchModels();
  };

  const handleSave = async (formData) => {
    if (!modal) return;
    setSaving(true);
    setFormError("");

    try {
      if (modal.entity === "branch") {
        const hotline = String(formData.hotline || "").trim();
        const email = String(formData.email || "").trim();
        if (hotline && !isValidVietnamesePhone(hotline)) {
          throw new Error("Hotline chi nhánh phải đúng định dạng Việt Nam (0xxxxxxxxx hoặc +84xxxxxxxxx).");
        }
        if (email && !isOptionalEmailValid(email)) {
          throw new Error("Email chi nhánh không đúng định dạng.");
        }

        const payload = {
          name: formData.name,
          address: formData.address || null,
          hotline: hotline || null,
          email: email || null,
          status: formData.status || "active",
          maxWorkSlots: formData.maxWorkSlots ?? null,
          description: formData.description || null
        };

        if (modal.mode === "create") await createBranch(payload);
        else await updateBranch(modal.item.id, payload);
      } else if (modal.entity === "staff") {
        if (!selectedBranchId) {
          throw new Error("Please select a branch before creating/updating staff.");
        }
        const phone = String(formData.phone || "").trim();
        const email = String(formData.email || "").trim();
        if (!isValidVietnamesePhone(phone)) {
          throw new Error("Số điện thoại nhân viên phải đúng định dạng Việt Nam (0xxxxxxxxx hoặc +84xxxxxxxxx).");
        }
        if (email && !isOptionalEmailValid(email)) {
          throw new Error("Email nhân viên không đúng định dạng.");
        }

        const payload = {
          fullName: formData.fullName,
          phone,
          email: email || null,
          roleId: formData.roleId || null,
          status: formData.status || "active",
          branchId: selectedBranchId || null
        };
        if (formData.password?.trim()) payload.password = formData.password.trim();

        if (modal.mode === "create") await createStaff(payload);
        else await updateStaff(modal.item.id, payload);
      } else if (modal.entity === "customer") {
        const phone = String(formData.phone || "").trim();
        const email = String(formData.email || "").trim();
        if (!isValidVietnamesePhone(phone)) {
          throw new Error("Số điện thoại khách hàng phải đúng định dạng Việt Nam (0xxxxxxxxx hoặc +84xxxxxxxxx).");
        }
        if (email && !isOptionalEmailValid(email)) {
          throw new Error("Email khách hàng không đúng định dạng.");
        }

        const payload = {
          fullName: formData.fullName,
          phone,
          email: email || null,
          tierId: formData.tierId || null,
          note: formData.note,
          isActive: formData.isActive === true || formData.isActive === "true"
        };
        if (formData.password?.trim()) payload.password = formData.password.trim();

        const formattedLicensePlate = formatLicensePlateByFieldName("licensePlate", formData.licensePlate || "");
        if (formattedLicensePlate && !isValidVietnameseLicensePlate(formattedLicensePlate)) {
          throw new Error("Biển số xe không đúng định dạng Việt Nam.");
        }

        const hasVehicleInput = Boolean(formattedLicensePlate || Number(formData.modelId));
        if (hasVehicleInput && !formattedLicensePlate) {
          throw new Error("Vui lòng nhập biển số xe.");
        }
        if (hasVehicleInput && !formData.modelId) {
          throw new Error("Vui lòng chọn dòng xe khi nhập thông tin xe.");
        }

        let savedCustomer = null;
        if (modal.mode === "create") savedCustomer = await createCustomer(payload);
        else savedCustomer = await updateCustomer(modal.item.id, payload);

        if (hasVehicleInput) {
          const vehiclePayload = {
            licensePlate: formattedLicensePlate,
            modelId: Number(formData.modelId),
            color: String(formData.color || "").trim() || null,
            manufactureYear: formData.manufactureYear || null
          };

          const customerId = modal.mode === "create" ? savedCustomer?.id : modal.item.id;
          const existingLink =
            modal.mode === "edit" ? primaryVehicleByCustomerId.get(modal.item.id) : null;

          if (existingLink) {
            await updateVehicle(existingLink.vehicleId, vehiclePayload);
            await updateCustomerVehicle(existingLink.id, {
              customerId: modal.item.id,
              vehicleId: existingLink.vehicleId,
              isCurrentOwner: true,
              note: formData.note || null
            });
          } else if (customerId) {
            const createdVehicle = await createVehicle(vehiclePayload);
            await createCustomerVehicle({
              customerId,
              vehicleId: createdVehicle.id,
              isCurrentOwner: true,
              note: formData.note || null
            });
          }
        }
      } else if (modal.entity === "brand") {
        if (modal.mode === "create") await createVehicleBrand(formData);
        else await updateVehicleBrand(modal.item.id, formData);
      } else if (modal.entity === "model") {
        if (!formData.brandId || Number.isNaN(formData.brandId)) throw new Error("Brand is required.");

        if (modal.mode === "create") await createVehicleModel(formData);
        else await updateVehicleModel(modal.item.id, formData);
      } else {
        if (!formData.categoryId || Number.isNaN(formData.categoryId)) {
          throw new Error("Service category is required.");
        }
        if (!formData.standardMinutes || Number(formData.standardMinutes) <= 0) {
          throw new Error("Estimated minutes must be greater than 0.");
        }

        const payload = {
          code: formData.code,
          name: formData.name,
          categoryId: Number(formData.categoryId),
          basePrice: formData.basePrice ?? null,
          standardMinutes: Number(formData.standardMinutes),
          description: formData.description || null,
          isActive: formData.isActive === true || formData.isActive === "true"
        };

        if (modal.mode === "create") await createStandardService(payload);
        else await updateStandardService(modal.item.id, payload);
      }

      await reloadSectionData(modal.entity);
      closeModal();
    } catch (apiError) {
      setFormError(apiError.message || "Save failed.");
    } finally {
      setSaving(false);
    }
  };

  const handleDelete = async () => {
    if (!modal?.item) return;
    setSaving(true);
    setFormError("");

    try {
      if (modal.entity === "branch") await deleteBranch(modal.item.id);
      else if (modal.entity === "staff") await deleteStaff(modal.item.id);
      else if (modal.entity === "customer") {
        const links = customerVehicles.filter((link) => link.customerId === modal.item.id);
        for (const link of links) {
          await deleteCustomerVehicle(link.id);
        }
        await deleteCustomer(modal.item.id);
      }
      else if (modal.entity === "brand") await deleteVehicleBrand(modal.item.id);
      else if (modal.entity === "model") await deleteVehicleModel(modal.item.id);
      else await deleteStandardService(modal.item.id);

      await reloadSectionData(modal.entity);
      closeModal();
    } catch (apiError) {
      setFormError(apiError.message || "Delete failed.");
    } finally {
      setSaving(false);
    }
  };

  const sectionMeta = SECTION_META[section];

  return (
    <div className={`admin-page ${embedded ? "embedded" : ""}`}>
      {!embedded && (
        <aside className="admin-sidebar">
            <div className="sidebar-brand">
              <span className="brand-name">MyGarage</span>
            </div>

            <nav className="sidebar-nav">
              <button className={`nav-item ${section === "branches" ? "active" : ""}`} type="button" onClick={() => { setSection("branches"); }}>
                Branches
              </button>
              <button className={`nav-item ${section === "staff" ? "active" : ""}`} type="button" onClick={() => { setSection("staff"); }}>
                Staff
              </button>
              <button className={`nav-item ${section === "customers" ? "active" : ""}`} type="button" onClick={() => { setSection("customers"); }}>
                Users
              </button>
              <button className={`nav-item ${section === "brands" ? "active" : ""}`} type="button" onClick={() => { setSection("brands"); }}>
                Vehicle Brands
              </button>
              <button className={`nav-item ${section === "models" ? "active" : ""}`} type="button" onClick={() => { setSection("models"); }}>
                Vehicle Models
              </button>
              <button className={`nav-item ${section === "services" ? "active" : ""}`} type="button" onClick={() => { setSection("services"); }}>
                Services
              </button>
              <button className="nav-item" type="button" onClick={handleLogout}>
                Logout
              </button>
            </nav>
        </aside>
      )}

      <main className="admin-main">
        <header className="admin-header">
          <div>
            <h1>{sectionMeta.title}</h1>
            <p className="header-sub">{sectionMeta.subtitle}</p>
            {selectedBranch?.name && section !== "branches" && (
              <p className="header-sub">Current branch: {selectedBranch.name}</p>
            )}
          </div>
          <button className="btn-primary" onClick={handleCreateClick} type="button">
            {sectionMeta.addLabel}
          </button>
        </header>

        <div className="stats-bar">
          {section === "branches" && (
            <>
              <div className="stat-card"><div className="stat-value">{branches.length}</div><div className="stat-label">Total Branches</div></div>
              <div className="stat-card"><div className="stat-value">{branches.filter((item) => String(item.status || "").toLowerCase() === "active").length}</div><div className="stat-label">Active</div></div>
              <div className="stat-card"><div className="stat-value">{selectedBranch?.name || "-"}</div><div className="stat-label">Selected Branch</div></div>
            </>
          )}

          {section === "staff" && (
            <>
              <div className="stat-card"><div className="stat-value">{staff.length}</div><div className="stat-label">Total Staff</div></div>
              <div className="stat-card"><div className="stat-value">{staff.filter((item) => String(item.status || "").toLowerCase() === "active").length}</div><div className="stat-label">Active</div></div>
              <div className="stat-card"><div className="stat-value">{new Set(staff.filter((item) => item.roleId).map((item) => item.roleId)).size}</div><div className="stat-label">Roles Used</div></div>
            </>
          )}

          {section === "customers" && (
            <>
              <div className="stat-card"><div className="stat-value">{customers.length}</div><div className="stat-label">Total Customers</div></div>
              <div className="stat-card"><div className="stat-value">{customers.filter((item) => item.isActive === true).length}</div><div className="stat-label">Active</div></div>
              <div className="stat-card"><div className="stat-value">{customerTierOptions.length}</div><div className="stat-label">Tier Levels</div></div>
            </>
          )}

          {section === "brands" && (
            <>
              <div className="stat-card"><div className="stat-value">{brands.length}</div><div className="stat-label">Total Brands</div></div>
              <div className="stat-card"><div className="stat-value">{brands.filter((item) => item.isActive === true).length}</div><div className="stat-label">Active</div></div>
              <div className="stat-card"><div className="stat-value">{brands.filter((item) => item.isActive === false).length}</div><div className="stat-label">Inactive</div></div>
            </>
          )}

          {section === "models" && (
            <>
              <div className="stat-card"><div className="stat-value">{models.length}</div><div className="stat-label">Total Models</div></div>
              <div className="stat-card"><div className="stat-value">{new Set(models.map((item) => item.brandId)).size}</div><div className="stat-label">Brand Coverage</div></div>
              <div className="stat-card"><div className="stat-value">{models.filter((item) => !item.isDeleted).length}</div><div className="stat-label">Active Models</div></div>
            </>
          )}

          {section === "services" && (
            <>
              <div className="stat-card"><div className="stat-value">{services.length}</div><div className="stat-label">Total Services</div></div>
              <div className="stat-card"><div className="stat-value">{services.filter((item) => item.isActive === true).length}</div><div className="stat-label">Active</div></div>
              <div className="stat-card"><div className="stat-value">{serviceCategoryOptions.length}</div><div className="stat-label">Categories</div></div>
            </>
          )}
        </div>

        <div className="table-toolbar">
          <span className="result-count">{visibleRows.length} result(s)</span>
        </div>

        {error && <div className="alert alert-error">{error}</div>}

        <div className="table-wrapper">
          {loading ? (
            <div className="loading-state"><div className="spinner" /><p>Loading data...</p></div>
          ) : visibleRows.length === 0 ? (
            <div className="empty-state"><p>No records found.</p></div>
          ) : (
            <table className="admin-table">
              {section === "branches" && (
                <>
                  <thead>
                    <tr>
                      <th>ID</th>
                      <th>Name</th>
                      <th>Address</th>
                      <th>Hotline</th>
                      <th>Email</th>
                      <th>Status</th>
                      <th>Max Slots</th>
                      <th className="th-center">Actions</th>
                    </tr>
                  </thead>
                  <tbody>
                    {pagedRows.map((item) => (
                      <tr key={item.id}>
                        <td className="td-id">#{item.id}</td>
                        <td>{item.name || "-"}</td>
                        <td>{item.address || "-"}</td>
                        <td>{item.hotline || "-"}</td>
                        <td className="td-email">{item.email || "-"}</td>
                        <td><span className={`badge ${getStatusBadge(item.status)}`}>{item.status || "-"}</span></td>
                        <td>{item.maxWorkSlots ?? "-"}</td>
                        <td className="td-center">
                          <div className="row-actions">
                            <button className="btn-icon btn-edit" type="button" onClick={() => openModal("branch", "edit", item)}>Edit</button>
                            <button className="btn-icon btn-del" type="button" onClick={() => openModal("branch", "delete", item)}>Del</button>
                          </div>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </>
              )}

              {section === "staff" && (
                <>
                  <thead><tr><th>ID</th><th>Full Name</th><th>Phone</th><th>Email</th><th>Role</th><th>Status</th><th>Created</th><th className="th-center">Actions</th></tr></thead>
                  <tbody>
                    {pagedRows.map((item) => (
                      <tr key={item.id}>
                        <td className="td-id">#{item.id}</td><td>{item.fullName || "-"}</td><td>{item.phone || "-"}</td><td className="td-email">{item.email || "-"}</td>
                        <td>{roleNameMap.get(item.roleId) || "-"}</td>
                        <td><span className={`badge ${getStatusBadge(item.status)}`}>{item.status || "-"}</span></td>
                        <td>{item.createdAt ? new Date(item.createdAt).toLocaleDateString() : "-"}</td>
                        <td className="td-center"><div className="row-actions"><button className="btn-icon btn-edit" type="button" onClick={() => openModal("staff", "edit", item)}>Sửa</button><button className="btn-icon btn-del" type="button" onClick={() => openModal("staff", "delete", item)}>Xóa</button></div></td>
                      </tr>
                    ))}
                  </tbody>
                </>
              )}

              {section === "customers" && (
                <>
                  <thead>
                    <tr>
                      <th>ID</th>
                      <th>Full Name</th>
                      <th>Phone</th>
                      <th>Email</th>
                      <th>Tier</th>
                      <th>Plate</th>
                      <th>Brand</th>
                      <th>Model</th>
                      <th>Status</th>
                      <th>Created</th>
                      <th className="th-center">Actions</th>
                    </tr>
                  </thead>
                  <tbody>
                    {pagedRows.map((item) => (
                      <tr key={item.id}>
                        <td className="td-id">#{item.id}</td><td>{item.fullName || "-"}</td><td>{item.phone || "-"}</td><td className="td-email">{item.email || "-"}</td>
                        <td>{item.tierName || "-"}</td>
                        <td>{item.licensePlate || "-"}</td>
                        <td>{item.brandName || "-"}</td>
                        <td>{item.modelName || "-"}</td>
                        <td><span className={`badge ${getStatusBadge(item.isActive)}`}>{item.isActive ? "active" : "inactive"}</span></td>
                        <td>{item.createdAt ? new Date(item.createdAt).toLocaleDateString() : "-"}</td>
                        <td className="td-center"><div className="row-actions"><button className="btn-icon btn-edit" type="button" onClick={() => openModal("customer", "edit", item)}>Sửa</button><button className="btn-icon btn-del" type="button" onClick={() => openModal("customer", "delete", item)}>Xóa</button></div></td>
                      </tr>
                    ))}
                  </tbody>
                </>
              )}

              {section === "brands" && (
                <>
                  <thead><tr><th>ID</th><th>Code</th><th>Name</th><th>Country</th><th>Status</th><th>Updated</th><th className="th-center">Actions</th></tr></thead>
                  <tbody>
                    {pagedRows.map((item) => (
                      <tr key={item.id}>
                        <td className="td-id">#{item.id}</td><td>{item.code || "-"}</td><td>{item.name || "-"}</td><td>{item.countryOfOrigin || "-"}</td>
                        <td><span className={`badge ${getStatusBadge(item.isActive)}`}>{item.isActive ? "active" : "inactive"}</span></td>
                        <td>{item.updatedAt ? new Date(item.updatedAt).toLocaleDateString() : "-"}</td>
                        <td className="td-center"><div className="row-actions"><button className="btn-icon btn-edit" type="button" onClick={() => openModal("brand", "edit", item)}>Sửa</button><button className="btn-icon btn-del" type="button" onClick={() => openModal("brand", "delete", item)}>Xóa</button></div></td>
                      </tr>
                    ))}
                  </tbody>
                </>
              )}

              {section === "models" && (
                <>
                  <thead><tr><th>ID</th><th>Brand</th><th>Model</th><th>Segment</th><th>Fuel</th><th>Cylinder</th><th className="th-center">Actions</th></tr></thead>
                  <tbody>
                    {pagedRows.map((item) => (
                      <tr key={item.id}>
                        <td className="td-id">#{item.id}</td><td>{brandNameMap.get(item.brandId) || `Brand #${item.brandId}`}</td><td>{item.modelName || "-"}</td><td>{item.segment || "-"}</td><td>{item.fuelType || "-"}</td><td>{item.cylinderCapacity || "-"}</td>
                        <td className="td-center"><div className="row-actions"><button className="btn-icon btn-edit" type="button" onClick={() => openModal("model", "edit", item)}>Sửa</button><button className="btn-icon btn-del" type="button" onClick={() => openModal("model", "delete", item)}>Xóa</button></div></td>
                      </tr>
                    ))}
                  </tbody>
                </>
              )}

              {section === "services" && (
                <>
                  <thead>
                    <tr>
                      <th>ID</th>
                      <th>Code</th>
                      <th>Service Name</th>
                      <th>Category</th>
                      <th>Price</th>
                      <th>Est. Time</th>
                      <th>Status</th>
                      <th className="th-center">Actions</th>
                    </tr>
                  </thead>
                  <tbody>
                    {(pagedRows || []).map((item) => (
                      <tr key={item.id}>
                        <td className="td-id">#{item.id}</td>
                        <td>{item.code || "-"}</td>
                        <td>{item.name || "-"}</td>
                        <td>{(serviceCategoryNameMap && serviceCategoryNameMap.get ? serviceCategoryNameMap.get(item.categoryId) : null) || `Category #${item.categoryId}`}</td>
                        <td>{item.basePrice != null ? Number(item.basePrice).toLocaleString() : "-"}</td>
                        <td>{item.standardMinutes ? `${item.standardMinutes} min` : "-"}</td>
                        <td><span className={`badge ${getStatusBadge(item.isActive)}`}>{item.isActive ? "active" : "inactive"}</span></td>
                        <td className="td-center"><div className="row-actions"><button className="btn-icon btn-edit" type="button" onClick={() => openModal("service", "edit", item)}>Sửa</button><button className="btn-icon btn-del" type="button" onClick={() => openModal("service", "delete", item)}>Xóa</button></div></td>
                      </tr>
                    ))}
                  </tbody>
                </>
              )}
            </table>
          )}
        </div>

        <TablePagination
          totalItems={visibleRows.length}
          currentPage={page}
          pageSize={pageSize}
          onPageChange={setPage}
          onPageSizeChange={(size) => {
            setPageSize(size);
            setPage(1);
          }}
        />
      </main>

      {modal?.mode === "create" && (
        <Modal title={modal.entity === "branch" ? "Add Branch" : modal.entity === "staff" ? "Add Staff" : modal.entity === "customer" ? "Add Customer" : modal.entity === "brand" ? "Add Vehicle Brand" : modal.entity === "model" ? "Add Vehicle Model" : "Add Service"} onClose={closeModal}>
          {formError && <div className="alert alert-error">{formError}</div>}

          {modal.entity === "branch" && <BranchForm initial={EMPTY_BRANCH_FORM} onSubmit={handleSave} onCancel={closeModal} loading={saving} />}
          {modal.entity === "staff" && <StaffForm initial={EMPTY_STAFF_FORM} roles={staffRoleOptions} onSubmit={handleSave} onCancel={closeModal} loading={saving} />}
          {modal.entity === "customer" && (
            <CustomerForm
              initial={EMPTY_CUSTOMER_FORM}
              tiers={customerTierOptions}
              brands={brands}
              models={models}
              onSubmit={handleSave}
              onCancel={closeModal}
              loading={saving}
            />
          )}
          {modal.entity === "brand" && <VehicleBrandForm initial={EMPTY_BRAND_FORM} onSubmit={handleSave} onCancel={closeModal} loading={saving} />}
          {modal.entity === "model" && <VehicleModelForm initial={EMPTY_MODEL_FORM} brands={brands} onSubmit={handleSave} onCancel={closeModal} loading={saving} />}
          {modal.entity === "service" && <ServiceForm initial={EMPTY_SERVICE_FORM} categories={serviceCategoryOptions} onSubmit={handleSave} onCancel={closeModal} loading={saving} />}
        </Modal>
      )}

      {modal?.mode === "edit" && modal.item && (
        <Modal title={modal.entity === "branch" ? `Edit Branch #${modal.item.id}` : modal.entity === "staff" ? `Edit Staff #${modal.item.id}` : modal.entity === "customer" ? `Edit Customer #${modal.item.id}` : modal.entity === "brand" ? `Edit Brand #${modal.item.id}` : modal.entity === "model" ? `Edit Model #${modal.item.id}` : `Edit Service #${modal.item.id}`} onClose={closeModal}>
          {formError && <div className="alert alert-error">{formError}</div>}

          {modal.entity === "branch" && (
            <BranchForm
              initial={{
                id: modal.item.id,
                name: modal.item.name || "",
                address: modal.item.address || "",
                hotline: modal.item.hotline || "",
                email: modal.item.email || "",
                status: modal.item.status || "active",
                maxWorkSlots: modal.item.maxWorkSlots ?? "",
                description: modal.item.description || ""
              }}
              onSubmit={handleSave}
              onCancel={closeModal}
              loading={saving}
            />
          )}

          {modal.entity === "staff" && (
            <StaffForm
              initial={{ id: modal.item.id, fullName: modal.item.fullName || "", phone: modal.item.phone || "", email: modal.item.email || "", password: "", roleId: modal.item.roleId ? String(modal.item.roleId) : "", status: modal.item.status || "active" }}
              roles={staffRoleOptions}
              onSubmit={handleSave}
              onCancel={closeModal}
              loading={saving}
            />
          )}

          {modal.entity === "customer" && (
            <CustomerForm
              initial={{
                id: modal.item.id,
                fullName: modal.item.fullName || "",
                phone: modal.item.phone || "",
                email: modal.item.email || "",
                password: "",
                tierId: modal.item.tierId ? String(modal.item.tierId) : "",
                brandId: modal.item.brandId ? String(modal.item.brandId) : "",
                modelId: modal.item.modelId ? String(modal.item.modelId) : "",
                licensePlate: modal.item.licensePlate && modal.item.licensePlate !== "-" ? modal.item.licensePlate : "",
                color: modal.item.color || "",
                manufactureYear: modal.item.manufactureYear || "",
                note: modal.item.note || "",
                isActive: modal.item.isActive ?? true
              }}
              tiers={customerTierOptions}
              brands={brands}
              models={models}
              onSubmit={handleSave}
              onCancel={closeModal}
              loading={saving}
            />
          )}

          {modal.entity === "brand" && (
            <VehicleBrandForm
              initial={{ id: modal.item.id, code: modal.item.code || "", name: modal.item.name || "", countryOfOrigin: modal.item.countryOfOrigin || "", logoUrl: modal.item.logoUrl || "", isActive: modal.item.isActive ?? true }}
              onSubmit={handleSave}
              onCancel={closeModal}
              loading={saving}
            />
          )}

          {modal.entity === "model" && (
            <VehicleModelForm
              initial={{ id: modal.item.id, brandId: modal.item.brandId, modelName: modal.item.modelName || "", segment: modal.item.segment || "", fuelType: modal.item.fuelType || "", cylinderCapacity: modal.item.cylinderCapacity || "" }}
              brands={brands}
              onSubmit={handleSave}
              onCancel={closeModal}
              loading={saving}
            />
          )}

          {modal.entity === "service" && (
            <ServiceForm
              initial={{
                id: modal.item.id,
                code: modal.item.code || "",
                name: modal.item.name || "",
                categoryId: modal.item.categoryId ? String(modal.item.categoryId) : "",
                basePrice: modal.item.basePrice ?? "",
                standardMinutes: modal.item.standardMinutes ?? "",
                description: modal.item.description || "",
                isActive: modal.item.isActive ?? true
              }}
              categories={serviceCategoryOptions}
              onSubmit={handleSave}
              onCancel={closeModal}
              loading={saving}
            />
          )}
        </Modal>
      )}

      {modal?.mode === "delete" && modal.item && (
        <Modal title="Confirm Delete" onClose={closeModal}>
          {formError && <div className="alert alert-error">{formError}</div>}
          <ConfirmDialog message={`Delete this ${modal.entity} record (#${modal.item.id})?`} onConfirm={handleDelete} onCancel={closeModal} loading={saving} />
        </Modal>
      )}
    </div>
  );
}
