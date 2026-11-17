import { useMemo, useState } from "react";
import { VIETNAM_PHONE_PATTERN } from "@shared/utils/contactValidation";
import { VIETNAM_LICENSE_PLATE_PATTERN, VIETNAM_LICENSE_PLATE_TITLE } from "@shared/utils/licensePlateValidation";
import { formatLicensePlateByFieldName } from "@shared/utils/licensePlateInputFormatter";
import {
  getPackageManagedStandardServiceMessage,
  isPackageManagedStandardServiceCode,
  normalizeStandardServiceCode,
} from "@shared/utils/standardServiceCodeRules";
import ImageCropper from "@features/workshop/components/ImageCropper";
import { blobToDataUrl, fileToDataUrl } from "./adminPageHelpers";

export function Modal({ title, onClose, children }) {
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

export function ConfirmDialog({ message, onConfirm, onCancel, loading }) {
  return (
    <div className="confirm-msg">
      <p>{message}</p>
      <div className="form-actions">
        <button className="btn-ghost" onClick={onCancel} type="button" disabled={loading}>
          Hủy
        </button>
        <button className="btn-danger" onClick={onConfirm} type="button" disabled={loading}>
          {loading ? "Đang xóa..." : "Xóa"}
        </button>
      </div>
    </div>
  );
}

export function StaffForm({ initial, roles, onSubmit, onCancel, loading }) {
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
        <label>Họ và tên *</label>
        <input name="fullName" value={form.fullName || ""} onChange={handleChange} required />
      </div>
      <div className="form-group">
        <label>Số điện thoại *</label>
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
        <label>Vai trò *</label>
        <select name="roleId" value={form.roleId} onChange={handleChange} required>
          <option value="">Chọn vai trò</option>
          {roles.map((role) => (
            <option key={role.id} value={role.id}>
              {role.roleName || role.roleCode || `Vai trò #${role.id}`}
            </option>
          ))}
        </select>
      </div>
      <div className="form-group">
        <label>{initial.id ? "Mật khẩu mới (không bắt buộc)" : "Mật khẩu *"}</label>
        <input
          name="password"
          type="password"
          value={form.password || ""}
          onChange={handleChange}
          required={!initial.id}
        />
      </div>
      <div className="form-group">
        <label>Trạng thái</label>
        <select name="status" value={form.status || "active"} onChange={handleChange}>
          <option value="active">Đang hoạt động</option>
          <option value="inactive">Ngừng hoạt động</option>
          <option value="suspended">Tạm khóa</option>
        </select>
      </div>

      <div className="form-actions">
        <button className="btn-ghost" type="button" onClick={onCancel}>
          Hủy
        </button>
        <button className="btn-primary" type="submit" disabled={loading}>
          {loading ? "Đang lưu..." : "Lưu"}
        </button>
      </div>
    </form>
  );
}

export function CustomerForm({ initial, tiers, brands, models, onSubmit, onCancel, loading }) {
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
        <label>Họ và tên *</label>
        <input name="fullName" value={form.fullName || ""} onChange={handleChange} required />
      </div>
      <div className="form-group">
        <label>Số điện thoại *</label>
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
        <label>{initial.id ? "Mật khẩu mới (không bắt buộc)" : "Mật khẩu *"}</label>
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
              {brand.name || brand.code || `Hãng #${brand.id}`}
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
        <label>Trạng thái</label>
        <select
          name="isActive"
          value={String(form.isActive ?? true)}
          onChange={handleChange}
        >
          <option value="true">Đang hoạt động</option>
          <option value="false">Ngừng hoạt động</option>
        </select>
      </div>

      <div className="form-actions">
        <button className="btn-ghost" type="button" onClick={onCancel}>
          Hủy
        </button>
        <button className="btn-primary" type="submit" disabled={loading}>
          {loading ? "Đang lưu..." : "Lưu"}
        </button>
      </div>
    </form>
  );
}

export function VehicleBrandForm({ initial, onSubmit, onCancel, loading }) {
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
        <label>Tên hãng *</label>
        <input name="name" value={form.name || ""} onChange={handleChange} required />
      </div>
      <div className="form-group">
        <label>Quốc gia</label>
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
        <label>Tải logo</label>
        <input type="file" accept="image/*" onChange={handleImageUpload} />
        {uploadingImage ? <small>Đang tải ảnh...</small> : null}
        {cropSource ? <small>Vui lòng cắt ảnh trước khi lưu.</small> : null}
        {form.logoUrl ? (
          <img
            src={form.logoUrl}
            alt="Xem trước logo"
            style={{ width: 80, height: 80, objectFit: "cover", borderRadius: 8, marginTop: 8 }}
          />
        ) : null}
      </div>
      <div className="form-group">
        <label>Trạng thái</label>
        <select
          name="isActive"
          value={String(form.isActive ?? true)}
          onChange={handleChange}
        >
          <option value="true">Đang hoạt động</option>
          <option value="false">Ngừng hoạt động</option>
        </select>
      </div>

      <div className="form-actions">
        <button className="btn-ghost" type="button" onClick={onCancel}>
          Hủy
        </button>
        <button className="btn-primary" type="submit" disabled={loading}>
          {loading ? "Đang lưu..." : "Lưu"}
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

export function VehicleModelForm({ initial, brands, onSubmit, onCancel, loading }) {
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
        <label>Hãng xe *</label>
        <select name="brandId" value={form.brandId} onChange={handleChange} required>
          <option value="">Chọn hãng xe</option>
          {brands.map((brand) => (
            <option key={brand.id} value={brand.id}>
              {brand.name || brand.code || `Hãng #${brand.id}`}
            </option>
          ))}
        </select>
      </div>
      <div className="form-group">
        <label>Tên dòng xe *</label>
        <input name="modelName" value={form.modelName || ""} onChange={handleChange} required />
      </div>
      <div className="form-group">
        <label>Phân khúc</label>
        <input name="segment" value={form.segment || ""} onChange={handleChange} />
      </div>
      <div className="form-group">
        <label>Loại nhiên liệu</label>
        <input name="fuelType" value={form.fuelType || ""} onChange={handleChange} />
      </div>
      <div className="form-group">
        <label>Dung tích xi-lanh</label>
        <input
          name="cylinderCapacity"
          value={form.cylinderCapacity || ""}
          onChange={handleChange}
        />
      </div>

      <div className="form-actions">
        <button className="btn-ghost" type="button" onClick={onCancel}>
          Hủy
        </button>
        <button className="btn-primary" type="submit" disabled={loading}>
          {loading ? "Đang lưu..." : "Lưu"}
        </button>
      </div>
    </form>
  );
}

export function ServiceForm({ initial, categories, onSubmit, onCancel, loading }) {
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
        <label>Mã dịch vụ *</label>
        <input name="code" value={form.code || ""} onChange={handleChange} required />
        <small className="form-helper">Mã bắt đầu bằng PKG- thuộc gói dịch vụ, không tạo mới tại đây.</small>
        {codeError ? <small className="form-error-text">{codeError}</small> : null}
      </div>
      <div className="form-group">
        <label>Tên dịch vụ *</label>
        <input name="name" value={form.name || ""} onChange={handleChange} required />
      </div>
      <div className="form-group">
        <label>Nhóm dịch vụ *</label>
        <select name="categoryId" value={form.categoryId} onChange={handleChange} required>
          <option value="">Chọn nhóm dịch vụ</option>
          {categories.map((category) => (
            <option key={category.id} value={category.id}>
              {category.name || category.code || `Nhóm #${category.id}`}
            </option>
          ))}
        </select>
      </div>
      <div className="form-group">
        <label>Giá cơ bản</label>
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
        <label>Thời lượng dự kiến (phút) *</label>
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
        <label>Mô tả</label>
        <input name="description" value={form.description || ""} onChange={handleChange} />
      </div>
      <div className="form-group">
        <label>Trạng thái</label>
        <select
          name="isActive"
          value={String(form.isActive ?? true)}
          onChange={handleChange}
        >
          <option value="true">Đang hoạt động</option>
          <option value="false">Ngừng hoạt động</option>
        </select>
      </div>

      <div className="form-actions">
        <button className="btn-ghost" type="button" onClick={onCancel}>
          Hủy
        </button>
        <button className="btn-primary" type="submit" disabled={loading || Boolean(codeError)}>
          {loading ? "Đang lưu..." : "Lưu"}
        </button>
      </div>
    </form>
  );
}

export function BranchForm({ initial, onSubmit, onCancel, loading }) {
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
        <label>Tên chi nhánh *</label>
        <input name="name" value={form.name || ""} onChange={handleChange} required />
      </div>
      <div className="form-group">
        <label>Địa chỉ</label>
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
        <label>Số ô làm việc tối đa</label>
        <input
          name="maxWorkSlots"
          type="number"
          min="1"
          value={form.maxWorkSlots}
          onChange={handleChange}
        />
      </div>
      <div className="form-group">
        <label>Trạng thái</label>
        <select name="status" value={form.status || "active"} onChange={handleChange}>
          <option value="active">Đang hoạt động</option>
          <option value="inactive">Ngừng hoạt động</option>
        </select>
      </div>
      <div className="form-group">
        <label>Mô tả</label>
        <input name="description" value={form.description || ""} onChange={handleChange} />
      </div>

      <div className="form-actions">
        <button className="btn-ghost" type="button" onClick={onCancel}>
          Hủy
        </button>
        <button className="btn-primary" type="submit" disabled={loading}>
          {loading ? "Đang lưu..." : "Lưu"}
        </button>
      </div>
    </form>
  );
}
