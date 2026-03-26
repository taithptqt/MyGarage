import React, { useMemo, useState } from "react";

export function StaffForm({ initial, roles, branches, onSubmit, onCancel, loading }) {
  const [form, setForm] = useState({
    ...initial,
    status: initial.status || "ACTIVE",
    roleId: initial.roleId ? String(initial.roleId) : "",
    branchId: initial.branchId ? String(initial.branchId) : ""
  });

  const handleChange = (event) => {
    const { name, value } = event.target;
    setForm((prev) => ({ ...prev, [name]: value }));
  };

  const handleSubmit = (event) => {
    event.preventDefault();
    onSubmit({
      ...form,
      roleId: form.roleId ? Number(form.roleId) : null,
      branchId: form.branchId ? Number(form.branchId) : null
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
        <input name="phone" value={form.phone || ""} onChange={handleChange} pattern="^(0[0-9]{9,10}|\+84[0-9]{9,10})$" title="Số điện thoại hợp lệ: 0xxxxxxxxx hoặc +84xxxxxxxxx" required />
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
        <label>Chi nhánh *</label>
        <select name="branchId" value={form.branchId || ""} onChange={handleChange} required>
          <option value="">Select branch</option>
          {branches.map((b) => (
            <option key={b.id} value={b.id}>
              {b.name || `Branch #${b.id}`}
            </option>
          ))}
        </select>
      </div>
      <div className="form-group">
        <label>{initial.id ? "New Password (optional)" : "Password *"}</label>
        <input
          name="password"
          type="password"
          autoComplete="new-password"
          value={form.password || ""}
          onChange={handleChange}
          required={!initial.id}
        />
      </div>
      <div className="form-group">
        <label>Status</label>
        <select name="status" value={form.status || "ACTIVE"} onChange={handleChange}>
          <option value="ACTIVE">Active</option>
          <option value="ON_LEAVE">On Leave</option>
          <option value="SUSPENDED">Suspended</option>
          <option value="TERMINATED">Terminated</option>
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

export function CustomerForm({ initial, tiers, brands, models, onSubmit, onCancel, loading }) {
  const [form, setForm] = useState({
    ...initial,
    status: initial.status || "ACTIVE",
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
        <input name="phone" value={form.phone || ""} onChange={handleChange} pattern="^(0[0-9]{9,10}|\+84[0-9]{9,10})$" title="Số điện thoại hợp lệ: 0xxxxxxxxx hoặc +84xxxxxxxxx" required />
      </div>
      <div className="form-group">
        <label>Email</label>
        <input name="email" type="email" value={form.email || ""} onChange={handleChange} />
      </div>
      <div className="form-group">
        <label>Google ID</label>
        <input name="googleId" value={form.googleId || ""} onChange={handleChange} />
      </div>
      <div className="form-group">
        <label>Type</label>
        <select name="type" value={form.type || "GUEST"} onChange={handleChange}>
          <option value="GUEST">GUEST</option>
          <option value="MEMBER">MEMBER</option>
        </select>
      </div>
      <div className="form-group">
        <label>Hạng Khách</label>
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
          autoComplete="new-password"
          value={form.password || ""}
          onChange={handleChange}
          required={!initial.id}
        />
      </div>
      <div className="form-group">
        <label>Biển Số Xe</label>
        <input name="licensePlate" value={form.licensePlate || ""} onChange={handleChange} />
      </div>
      <div className="form-group">
        <label>Hãng Xe</label>
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
        <label>Dòng Xe</label>
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
        <label>Màu Xe</label>
        <input name="color" value={form.color || ""} onChange={handleChange} />
      </div>
      <div className="form-group">
        <label>Năm SX</label>
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

export function VehicleBrandForm({ initial, onSubmit, onCancel, loading }) {
  const [form, setForm] = useState({
    ...initial,
    isActive: initial.isActive ?? true
  });

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

export function ServiceForm({ initial, categories, onSubmit, onCancel, loading }) {
  const [form, setForm] = useState({
    ...initial,
    categoryId: initial.categoryId ? String(initial.categoryId) : "",
    isActive: initial.isActive ?? true
  });

  const handleChange = (event) => {
    const { name, value } = event.target;
    setForm((prev) => ({ ...prev, [name]: value }));
  };

  const handleSubmit = (event) => {
    event.preventDefault();
    onSubmit({
      ...form,
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
        <button className="btn-primary" type="submit" disabled={loading}>
          {loading ? "Saving..." : "Save"}
        </button>
      </div>
    </form>
  );
}

