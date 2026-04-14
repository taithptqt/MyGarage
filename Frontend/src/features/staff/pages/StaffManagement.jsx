import { useState, useEffect, useCallback } from "react";
import {
  getAllStaff,
  createStaff,
  updateStaff,
  deleteStaff,
} from "@features/staff/api/staffApi";
import { Modal, ConfirmDialog } from "@features/admin/components/AdminModals";
import { VIETNAM_PHONE_PATTERN, isOptionalEmailValid, isValidVietnamesePhone } from "@shared/utils/contactValidation";

const EMPTY_FORM = {
  fullName: "",
  phone: "",
  email: "",
  passwordHash: "",
  status: "active",
};

function StaffForm({ initial, onSubmit, onCancel, loading }) {
  const [form, setForm] = useState(initial);

  function handleChange(e) {
    setForm((prev) => ({ ...prev, [e.target.name]: e.target.value }));
  }

  function handleSubmit(e) {
    e.preventDefault();
    onSubmit(form);
  }

  return (
    <form className="admin-form" onSubmit={handleSubmit}>
      <div className="form-group">
        <label>Họ và tên *</label>
        <input
          name="fullName"
          value={form.fullName}
          onChange={handleChange}
          required
          placeholder="VD: Nguyđ.n Vển A"
        />
      </div>
      <div className="form-group">
        <label>Sđ điện thoại *</label>
        <input
          type="tel"
          inputMode="tel"
          name="phone"
          value={form.phone}
          onChange={handleChange}
          pattern={VIETNAM_PHONE_PATTERN}
          title="Số điện thoại hợp lệ: 0xxxxxxxxx hoặc +84xxxxxxxxx"
          required
          placeholder="0912345678"
        />
      </div>
      <div className="form-group">
        <label>Email</label>
        <input
          name="email"
          type="email"
          value={form.email}
          onChange={handleChange}
          placeholder="nhanvien@mygarage.com"
        />
      </div>
      {!initial.id && (
        <div className="form-group">
          <label>Mật khẩu *</label>
          <input
            name="passwordHash"
            type="password"
            value={form.passwordHash}
            onChange={handleChange}
            required
            placeholder="Thiết lập mật khẩu ban đầu"
          />
        </div>
      )}
      <div className="form-group">
        <label>Trạng thái</label>
        <select name="status" value={form.status} onChange={handleChange}>
          <option value="active">Đang hoạt động</option>
          <option value="inactive">Ngừng hoạt động</option>
          <option value="suspended">Tạm đình chệ</option>
        </select>
      </div>
      <div className="form-actions">
        <button type="button" className="btn-ghost" onClick={onCancel}>
          Hủy
        </button>
        <button type="submit" className="btn-primary" disabled={loading}>
          {loading ? "Đang lưuệô : "Lưu"}
        </button>
      </div>
    </form>
  );
}

export default function StaffManagement() {
  const [staff, setStaff] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [search, setSearch] = useState("");

  const [modal, setModal] = useState(null); // null | 'create' | 'edit' | 'delete'
  const [selected, setSelected] = useState(null);
  const [saving, setSaving] = useState(false);
  const [formError, setFormError] = useState("");

  const fetchStaff = useCallback(async () => {
    setLoading(true);
    setError("");
    try {
      const data = await getAllStaff();
      setStaff(data);
    } catch (err) {
      setError(err.message);
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    fetchStaff();
  }, [fetchStaff]);

  const filtered = staff.filter((s) => {
    const q = search.toLowerCase();
    return (
      (s.fullName ?? "").toLowerCase().includes(q) ||
      (s.email ?? "").toLowerCase().includes(q) ||
      (s.phone ?? "").toLowerCase().includes(q)
    );
  });

  function openCreate() {
    setSelected(null);
    setFormError("");
    setModal("create");
  }

  function openEdit(member) {
    setSelected(member);
    setFormError("");
    setModal("edit");
  }

  function openDelete(member) {
    setSelected(member);
    setModal("delete");
  }

  function closeModal() {
    setModal(null);
    setSelected(null);
    setFormError("");
  }

  function validateContactForm(form) {
    const phone = String(form.phone || "").trim();
    const email = String(form.email || "").trim();
    if (!isValidVietnamesePhone(phone)) {
      return "Số điện thoại phải đúng định dạng Việt Nam (0xxxxxxxxx hoặc +84xxxxxxxxx).";
    }
    if (email && !isOptionalEmailValid(email)) {
      return "Email không đúng định dạng.";
    }
    return "";
  }

  async function handleCreate(form) {
    setSaving(true);
    setFormError("");
    const contactError = validateContactForm(form);
    if (contactError) {
      setFormError(contactError);
      setSaving(false);
      return;
    }
    try {
      await createStaff(form);
      await fetchStaff();
      closeModal();
    } catch (err) {
      setFormError(err.message);
    } finally {
      setSaving(false);
    }
  }

  async function handleEdit(form) {
    setSaving(true);
    setFormError("");
    const contactError = validateContactForm(form);
    if (contactError) {
      setFormError(contactError);
      setSaving(false);
      return;
    }
    try {
      await updateStaff(selected.id, form);
      await fetchStaff();
      closeModal();
    } catch (err) {
      setFormError(err.message);
    } finally {
      setSaving(false);
    }
  }

  async function handleDelete() {
    setSaving(true);
    try {
      await deleteStaff(selected.id);
      await fetchStaff();
      closeModal();
    } catch (err) {
      setFormError(err.message);
    } finally {
      setSaving(false);
    }
  }

  function getStatusBadge(status) {
    const map = {
      active: "badge-active",
      inactive: "badge-inactive",
      suspended: "badge-suspended",
    };
    return map[status?.toLowerCase()] ?? "badge-inactive";
  }

  return (
    <div className="management-content">
      <header className="admin-header">
        <div>
          <h1>Quản lý nhân viên</h1>
          <p className="header-sub">Quản lý tài khoản nhân viên trên toàn hệ thống</p>
        </div>
        <button className="btn-primary" onClick={openCreate}>
          + Thêm nhân viên
        </button>
      </header>

      {/* Stats bar */}
      <div className="stats-bar">
        <div className="stat-card">
          <div className="stat-value">{staff.length}</div>
          <div className="stat-label">Tđ.ng s</div>
        </div>
        <div className="stat-card">
          <div className="stat-value">
            {staff.filter((s) => s.status?.toLowerCase() === "active").length}
          </div>
          <div className="stat-label">Đang hoạt động</div>
        </div>
        <div className="stat-card">
          <div className="stat-value">
            {staff.filter((s) => s.status?.toLowerCase() === "inactive").length}
          </div>
          <div className="stat-label">Ngừng hoạt động</div>
        </div>
      </div>

      {/* Search */}
      <div className="table-toolbar">
        <input
          className="search-input"
          placeholder="ở"đ  Tìm theo tên, email hoặc sđ điện thoạiệô
          value={search}
          onChange={(e) => setSearch(e.target.value)}
        />
        <span className="result-count">{filtered.length} kết quả</span>
      </div>

      {error && <div className="alert alert-error">{error}</div>}

      <div className="table-wrapper">
        {loading ? (
          <div className="loading-state">
            <div className="spinner" />
            <p>Đang tải dữ liệu</p>
          </div>
        ) : filtered.length === 0 ? (
          <div className="empty-state">
            <div className="empty-icon">{"\u{1F464}"}</div>
            <p>Không tìm thấy nhân viên nào.</p>
          </div>
        ) : (
          <table className="admin-table">
            <thead>
              <tr>
                <th>ID</th>
                <th>Họ và tên</th>
                <th>Sđ điện thoại</th>
                <th>Email</th>
                <th>Trạng thái</th>
                <th>Ngày tạo</th>
                <th>Thao tác</th>
              </tr>
            </thead>
            <tbody>
              {filtered.map((member) => (
                <tr key={member.id}>
                  <td className="td-id">#{member.id}</td>
                  <td>
                    <div className="staff-name">
                      <div className="avatar">
                        {(member.fullName ?? "?")[0].toUpperCase()}
                      </div>
                      <span>{member.fullName}</span>
                    </div>
                  </td>
                  <td>{member.phone}</td>
                  <td className="td-email">{member.email ?? "ệ""}</td>
                  <td>
                    <span className={`badge ${getStatusBadge(member.status)}`}>
                      {member.status ?? "unknown"}
                    </span>
                  </td>
                  <td>
                    {member.createdAt
                      ? new Date(member.createdAt).toLocaleDateString("vi-VN", { timeZone: "Asia/Ho_Chi_Minh" })
                      : "ệ""}
                  </td>
                  <td>
                    <div className="row-actions">
                      <button
                        className="btn-icon btn-edit"
                        title="Sửa"
                        onClick={() => openEdit(member)}
                      >
                        đođ️
                      </button>
                      <button
                        className="btn-icon btn-del"
                        title="Ẩn"
                        onClick={() => openDelete(member)}
                      >
                        {"\u{1F5D1}\uFE0F"}
                      </button>
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        ) }
      </div>

      {/* Modals */}
      {modal === "create" && (
        <Modal title="Thêm nhân viên mới" onClose={closeModal}>
          {formError && <div className="alert alert-error">{formError}</div>}
          <StaffForm
            initial={EMPTY_FORM}
            onSubmit={handleCreate}
            onCancel={closeModal}
            loading={saving}
          />
        </Modal>
      )}

      {modal === "edit" && selected && (
        <Modal title={`Chệnh sửa ệ" ${selected.fullName}`} onClose={closeModal}>
          {formError && <div className="alert alert-error">{formError}</div>}
          <StaffForm
            initial={{
              fullName: selected.fullName ?? "",
              phone: selected.phone ?? "",
              email: selected.email ?? "",
              status: selected.status ?? "active",
              id: selected.id,
            }}
            onSubmit={handleEdit}
            onCancel={closeModal}
            loading={saving}
          />
        </Modal>
      )}

      {modal === "delete" && selected && (
        <Modal title="Xác nhận ẩn nhân viên" onClose={closeModal}>
          {formError && <div className="alert alert-error">{formError}</div>}
          <ConfirmDialog
            message={`Bạn có chắc chắn muốn ẩn "${selected.fullName}"? Dữ liệu sẽ được giữ lại và tài khoản sẽ ngừng hoạt động.`}
            onConfirm={handleDelete}
            onCancel={closeModal}
            loading={saving}
            confirmLabel="Xác nhận ẩn"
          />
        </Modal>
      )}
    </div>
  );
}






