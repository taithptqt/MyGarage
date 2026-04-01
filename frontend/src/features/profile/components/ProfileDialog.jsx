import { useMemo, useState } from "react";
import { createPortal } from "react-dom";
import { useAuth } from "@shared/core/context/AuthContext";
import { getApiBaseUrl } from "@features/auth/api/authApi";
import { updateCustomer } from "@features/admin/api/customerApi";
import { updateStaff, uploadStaffAvatar } from "@features/admin/api/staffApi";
import ImageCropper from "@features/workshop/components/ImageCropper";
import "./ProfileDialog.css";

const STAFF_ROLES = ["ADMIN", "RECEPTIONIST", "TECH", "MANAGER", "QUALITY_CONTROL", "FOREMAN"];

function toDateInput(value) {
  if (!value) return "";
  if (typeof value === "string" && value.includes("T")) return value.split("T")[0];
  return String(value);
}

function resolveAvatarUrl(avatarUrl) {
  if (!avatarUrl) return "";
  const raw = String(avatarUrl).trim();
  if (!raw) return "";
  if (raw.startsWith("http://") || raw.startsWith("https://") || raw.startsWith("data:") || raw.startsWith("blob:")) {
    return raw;
  }
  const base = getApiBaseUrl();
  if (!base) return raw;
  return `${base}${raw.startsWith("/") ? "" : "/"}${raw}`;
}

export default function ProfileDialog({ onClose }) {
  const { user, setUser } = useAuth();
  const userId = user?.id || user?.Id;
  const roleCode = String(user?.roleCode || user?.RoleCode || "").toUpperCase();
  const isStaffRole = STAFF_ROLES.includes(roleCode);

  const [formData, setFormData] = useState({
    fullName: user?.fullName || user?.FullName || "",
    phone: user?.phone || user?.Phone || user?.phoneNumber || user?.PhoneNumber || user?.mobile || user?.Mobile || "",
    email: user?.email || user?.Email || user?.emailAddress || user?.EmailAddress || "",
    citizenId: user?.citizenId || user?.CitizenId || user?.cccd || user?.CCCD || "",
    dateOfBirth: toDateInput(user?.dateOfBirth || user?.DateOfBirth || user?.dob || user?.DOB),
    avatarUrl: user?.avatarUrl || user?.AvatarUrl || ""
  });
  const [saving, setSaving] = useState(false);
  const [uploadingAvatar, setUploadingAvatar] = useState(false);
  const [error, setError] = useState("");
  const [success, setSuccess] = useState("");
  const [selectedImage, setSelectedImage] = useState("");
  const [showCropper, setShowCropper] = useState(false);

  const displayName = useMemo(() => {
    const name = String(formData.fullName || user?.fullName || user?.FullName || "Người dùng").trim();
    return name || "Người dùng";
  }, [formData.fullName, user?.fullName, user?.FullName]);

  const avatarText = useMemo(() => {
    return displayName
      .split(" ")
      .filter(Boolean)
      .slice(-2)
      .map((part) => part.charAt(0))
      .join("")
      .toUpperCase();
  }, [displayName]);

  const avatarSrc = resolveAvatarUrl(formData.avatarUrl);

  const onChange = (event) => {
    const { name, value } = event.target;
    setFormData((prev) => ({ ...prev, [name]: value }));
  };

  const onAvatarChange = (event) => {
    const file = event.target.files?.[0];
    event.target.value = "";
    if (!file || !isStaffRole || !userId) return;

    const reader = new FileReader();
    reader.onload = () => {
      setSelectedImage(String(reader.result || ""));
      setShowCropper(true);
    };
    reader.readAsDataURL(file);
  };

  const onCropComplete = async (croppedBlob) => {
    if (!isStaffRole || !userId || !croppedBlob) {
      setShowCropper(false);
      setSelectedImage("");
      return;
    }

    setShowCropper(false);
    setSelectedImage("");
    setUploadingAvatar(true);
    setError("");
    setSuccess("");

    const localPreviewUrl = URL.createObjectURL(croppedBlob);
    setFormData((prev) => ({ ...prev, avatarUrl: localPreviewUrl }));

    try {
      const res = await uploadStaffAvatar(userId, croppedBlob);
      const nextAvatar = res?.avatarUrl || "";
      if (!nextAvatar) throw new Error("Không nhận được đường dẫn ảnh mơi.");

      const nextUser = { ...user, avatarUrl: nextAvatar, AvatarUrl: nextAvatar };
      setFormData((prev) => ({ ...prev, avatarUrl: nextAvatar }));
      setUser(nextUser);
      localStorage.setItem("mygarage_user", JSON.stringify(nextUser));
      setSuccess("Cập nhật ảnh đại diện thành công.");
    } catch (e) {
      setError(e?.message || "Không thể cập nhật ảnh đại diện.");
    } finally {
      URL.revokeObjectURL(localPreviewUrl);
      setUploadingAvatar(false);
    }
  };

  const onSubmit = async (event) => {
    event.preventDefault();
    if (!userId) {
      setError("Không tìm thấy thông tin người dùng.");
      return;
    }

    const phone = String(formData.phone || "").trim();
    const email = String(formData.email || "").trim();
    const citizenId = String(formData.citizenId || "").trim();

    const phoneRegex = /^(0\d{9,10}|\+84\d{9,10})$/;
    const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

    if (!phoneRegex.test(phone)) {
      setError("Sđ điện thoại không đúng định dạng.");
      return;
    }
    if (!emailRegex.test(email)) {
      setError("Email không đúng định dạng.");
      return;
    }
    if (citizenId && !/^\d{12}$/.test(citizenId)) {
      setError("Sđ CCCD phải gôm đúng 12 chữ sđ.");
      return;
    }

    setSaving(true);
    setError("");
    setSuccess("");

    const payload = {
      fullName: String(formData.fullName || "").trim(),
      phone,
      email,
      citizenId: citizenId || null,
      dateOfBirth: formData.dateOfBirth || null
    };

    try {
      const updated = isStaffRole
        ? await updateStaff(userId, payload)
        : await updateCustomer(userId, payload);

      const nextUser = { ...user, ...payload, ...updated };
      setUser(nextUser);
      localStorage.setItem("mygarage_user", JSON.stringify(nextUser));
      setTimeout(() => {
        onClose();
      }, 2000);
      setSuccess("Lưu hô sơ thành công.");
    } catch (e) {
      setError(e?.message || "Không thể lưu hô sơ.");
    } finally {
      setSaving(false);
    }
  };

  const content = (
    <>
      <div className="profile-dialog-overlay" onClick={onClose}>
        <section className="profile-dialog-card" onClick={(e) => e.stopPropagation()}>
          <header className="profile-dialog-header">
            <div className="profile-dialog-header-left">
              <div className="profile-dialog-avatar-wrap">
                <div className="profile-dialog-avatar">
                  {avatarSrc ? <img src={avatarSrc} alt="Avatar" /> : avatarText}
                </div>
                {isStaffRole ? (
                  <label className="profile-dialog-avatar-btn">
                    {uploadingAvatar ? "..." : "\u{1F4F7}"}
                    <input type="file" accept="image/*" onChange={onAvatarChange} disabled={uploadingAvatar} />
                  </label>
                ) : null}
              </div>
              <div className="profile-dialog-title-wrap">
                <h2>{displayName}</h2>
                <p>{user?.role || roleCode || "Khách hàng"}</p>
              </div>
            </div>
            <button type="button" className="profile-dialog-close" onClick={onClose} aria-label="Đóng">
              đ-
            </button>
          </header>

          {error ? <div className="profile-dialog-alert error">{error}</div> : null}
          {success ? <div className="profile-dialog-alert success">{success}</div> : null}

          <form className="profile-dialog-form" onSubmit={onSubmit}>
            <label>
              Họ và tên
              <input name="fullName" value={formData.fullName} onChange={onChange} required />
            </label>
            <label>
              Sđ điện thoại
              <input name="phone" value={formData.phone} onChange={onChange} required />
            </label>
            <label>
              Email
              <input name="email" value={formData.email} onChange={onChange} required />
            </label>
            <label>
              Sđ CCCD
              <input name="citizenId" value={formData.citizenId} onChange={onChange} placeholder="Chưa cập nhật" />
            </label>
            <label>
              Ngày sinh
              <input type="date" name="dateOfBirth" value={formData.dateOfBirth} onChange={onChange} />
            </label>
            <label>
              Chi nhánh
              <input value={user?.branchName || "Chưa xác định"} disabled />
            </label>

            <div className="profile-dialog-actions">
              <button type="button" className="ghost" onClick={onClose} disabled={saving || uploadingAvatar}>
                Hủy
              </button>
              <button type="submit" disabled={saving || uploadingAvatar}>
                {saving ? "Đang lưu..." : "Lưu hô sơ"}
              </button>
            </div>
          </form>
        </section>
      </div>

      {showCropper ? (
        <ImageCropper
          image={selectedImage}
          onCropComplete={onCropComplete}
          onCancel={() => {
            setShowCropper(false);
            setSelectedImage("");
          }}
        />
      ) : null}
    </>
  );

  if (typeof document === "undefined") return content;
  return createPortal(content, document.body);
}








