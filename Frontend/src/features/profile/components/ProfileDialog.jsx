import { useEffect, useMemo, useState } from "react";
import { createPortal } from "react-dom";
import { useAuth } from "@shared/core/context/AuthContext";
import { changePassword, getApiBaseUrl } from "@features/auth/api/authApi";
import { getCustomerById, updateCustomer } from "@features/admin/api/customerApi";
import { getStaffById, updateStaff, uploadStaffAvatar } from "@features/admin/api/staffApi";
import { VIETNAM_PHONE_PATTERN, isValidEmail, isValidVietnamesePhone } from "@shared/utils/contactValidation";
import ImageCropper from "@features/workshop/components/ImageCropper";
import "./ProfileDialog.css";

const STAFF_ROLES = ["ADMIN", "RECEPTIONIST", "TECH", "MANAGER", "QUALITY_CONTROL", "FOREMAN", "TEAM_LEADER", "STORE_KEEPER"];

function toDateInput(value) {
  if (!value) return "";
  if (typeof value === "string" && value.includes("T")) return value.split("T")[0];
  return String(value);
}

function pickText(source, keys) {
  for (const key of keys) {
    const value = source?.[key];
    if (value !== undefined && value !== null) return String(value);
  }
  return "";
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

function buildInitialFormData(source) {
  return {
    fullName: pickText(source, ["fullName", "FullName"]),
    phone: pickText(source, ["phone", "Phone", "phoneNumber", "PhoneNumber", "mobile", "Mobile"]),
    email: pickText(source, ["email", "Email", "emailAddress", "EmailAddress"]),
    citizenId: pickText(source, ["citizenId", "CitizenId", "cccd", "CCCD"]),
    dateOfBirth: toDateInput(source?.dateOfBirth || source?.DateOfBirth || source?.dob || source?.DOB),
    avatarUrl: pickText(source, ["avatarUrl", "AvatarUrl"]),
  };
}

export default function ProfileDialog({ onClose }) {
  const { user, setUser, logout } = useAuth();
  const userId = user?.id || user?.Id;
  const roleCode = String(user?.roleCode || user?.RoleCode || "").toUpperCase();
  const isStaffRole = STAFF_ROLES.includes(roleCode);

  const [formData, setFormData] = useState(() => buildInitialFormData(user));
  const [saving, setSaving] = useState(false);
  const [uploadingAvatar, setUploadingAvatar] = useState(false);
  const [loadingProfile, setLoadingProfile] = useState(false);
  const [error, setError] = useState("");
  const [success, setSuccess] = useState("");
  const [changingPassword, setChangingPassword] = useState(false);
  const [passwordError, setPasswordError] = useState("");
  const [passwordSuccess, setPasswordSuccess] = useState("");
  const [passwordForm, setPasswordForm] = useState({
    oldPassword: "",
    newPassword: "",
    confirmPassword: "",
  });
  const [selectedImage, setSelectedImage] = useState("");
  const [showCropper, setShowCropper] = useState(false);

  useEffect(() => {
    if (!userId) return undefined;
    let ignore = false;

    const loadProfileDetail = async () => {
      setLoadingProfile(true);
      setError("");
      try {
        if (isStaffRole) {
          const staff = await getStaffById(userId);
          if (ignore) return;
          setFormData((prev) => ({
            ...prev,
            ...buildInitialFormData(staff),
            citizenId: pickText(staff, ["citizenId", "CitizenId"]),
            dateOfBirth: toDateInput(staff?.dateOfBirth || staff?.DateOfBirth),
          }));
          return;
        }

        const customer = await getCustomerById(userId);
        if (ignore) return;
        setFormData((prev) => ({
          ...prev,
          ...buildInitialFormData(customer),
          citizenId: "",
          dateOfBirth: "",
        }));
      } catch {
        if (!ignore) {
          setFormData((prev) => ({ ...prev, ...buildInitialFormData(user) }));
        }
      } finally {
        if (!ignore) setLoadingProfile(false);
      }
    };

    loadProfileDetail();
    return () => {
      ignore = true;
    };
  }, [userId, isStaffRole, user]);

  const displayName = useMemo(() => {
    const name = String(formData.fullName || user?.fullName || user?.FullName || "Ngu?i dùng").trim();
    return name || "Ngu?i dùng";
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

  const onPasswordChange = (event) => {
    const { name, value } = event.target;
    setPasswordForm((prev) => ({ ...prev, [name]: value }));
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
      if (!nextAvatar) throw new Error("Không nh?n du?c du?ng d?n ?nh m?i.");

      const nextUser = { ...user, avatarUrl: nextAvatar, AvatarUrl: nextAvatar };
      setFormData((prev) => ({ ...prev, avatarUrl: nextAvatar }));
      setUser(nextUser);
      localStorage.setItem("mygarage_user", JSON.stringify(nextUser));
      setSuccess("C?p nh?t ?nh d?i di?n thành công.");
    } catch (e) {
      setError(e?.message || "Không th? c?p nh?t ?nh d?i di?n.");
    } finally {
      URL.revokeObjectURL(localPreviewUrl);
      setUploadingAvatar(false);
    }
  };

  const onSubmit = async (event) => {
    event.preventDefault();
    if (!userId) {
      setError("Không tìm th?y thông tin ngu?i dùng.");
      return;
    }

    const phone = String(formData.phone || "").trim();
    const email = String(formData.email || "").trim();
    const citizenId = String(formData.citizenId || "").trim();

    if (!isValidVietnamesePhone(phone)) {
      setError("S? di?n tho?i không dúng d?nh d?ng.");
      return;
    }
    if (!isValidEmail(email)) {
      setError("Email không dúng d?nh d?ng.");
      return;
    }
    if (isStaffRole && citizenId && !/^\d{12}$/.test(citizenId)) {
      setError("S? CCCD ph?i g?m dúng 12 ch? s?.");
      return;
    }

    setSaving(true);
    setError("");
    setSuccess("");

    const basePayload = {
      fullName: String(formData.fullName || "").trim(),
      phone,
      email,
    };

    const payload = isStaffRole
      ? {
          ...basePayload,
          citizenId: citizenId || null,
          dateOfBirth: formData.dateOfBirth || null,
          avatarUrl: formData.avatarUrl || null,
        }
      : basePayload;

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
      setSuccess("Luu h? so thành công.");
    } catch (e) {
      setError(e?.message || "Không th? luu h? so.");
    } finally {
      setSaving(false);
    }
  };

  const onPasswordSubmit = async (event) => {
    event.preventDefault();

    const oldPassword = String(passwordForm.oldPassword || "");
    const newPassword = String(passwordForm.newPassword || "");
    const confirmPassword = String(passwordForm.confirmPassword || "");

    if (!oldPassword) {
      setPasswordError("Vui lòng nh?p m?t kh?u hi?n t?i.");
      setPasswordSuccess("");
      return;
    }

    if (newPassword.length < 6) {
      setPasswordError("M?t kh?u m?i ph?i có ít nh?t 6 ký t?.");
      setPasswordSuccess("");
      return;
    }

    if (newPassword !== confirmPassword) {
      setPasswordError("M?t kh?u x?c nh?n chua kh?p.");
      setPasswordSuccess("");
      return;
    }

    if (oldPassword === newPassword) {
      setPasswordError("M?t kh?u m?i c?n kh?c m?t kh?u hi?n t?i.");
      setPasswordSuccess("");
      return;
    }

    setChangingPassword(true);
    setPasswordError("");
    setPasswordSuccess("");

    const { ok, data } = await changePassword({
      oldPassword,
      newPassword,
    });

    if (!ok) {
      setPasswordError(data?.message || "Không th? d?i m?t kh?u.");
      setChangingPassword(false);
      return;
    }

    setPasswordForm({
      oldPassword: "",
      newPassword: "",
      confirmPassword: "",
    });
    setPasswordSuccess("Ð?i m?t kh?u thành công. Vui lòng dang nh?p l?i.");
    setChangingPassword(false);

    window.setTimeout(() => {
      onClose();
      logout();
    }, 1200);
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
            <button type="button" className="profile-dialog-close" onClick={onClose} aria-label="Ðóng">
              x
            </button>
          </header>

          {error ? <div className="profile-dialog-alert error">{error}</div> : null}
          {success ? <div className="profile-dialog-alert success">{success}</div> : null}

          <section className="profile-dialog-section">
            <div className="profile-dialog-section-title">
              <h3>Thông tin h? so</h3>
              <p>C?p nh?t thông tin c? nh?n dang d?ng trong h? th?ng.</p>
            </div>

            <form className="profile-dialog-form" onSubmit={onSubmit}>
              <label>
                H? và tên
                <input name="fullName" value={formData.fullName} onChange={onChange} required />
              </label>
              <label>
                S? di?n tho?i
                <input
                  type="tel"
                  inputMode="tel"
                  name="phone"
                  value={formData.phone}
                  onChange={onChange}
                  pattern={VIETNAM_PHONE_PATTERN}
                  title="S? di?n tho?i h?p l?: 0xxxxxxxxx ho?c +84xxxxxxxxx"
                  required
                />
              </label>
              <label>
                Email
                <input type="email" name="email" value={formData.email} onChange={onChange} required />
              </label>

              {isStaffRole ? (
                <>
                  <label>
                    S? CCCD
                    <input name="citizenId" value={formData.citizenId} onChange={onChange} placeholder="Chua c?p nh?t" />
                  </label>
                  <label>
                    Ngày sinh
                    <input type="date" name="dateOfBirth" value={formData.dateOfBirth} onChange={onChange} />
                  </label>
                </>
              ) : null}

              <label>
                {isStaffRole ? "Chi nh?nh" : "Chi nh?nh uu ti?n"}
                <input value={user?.branchName || "Chua x?c d?nh"} disabled />
              </label>

              <div className="profile-dialog-actions">
                <button type="button" className="ghost" onClick={onClose} disabled={saving || uploadingAvatar}>
                  H?y
                </button>
                <button type="submit" disabled={saving || uploadingAvatar || loadingProfile}>
                  {loadingProfile ? "Ðang t?i..." : saving ? "Ðang luu..." : "Luu h? so"}
                </button>
              </div>
            </form>
          </section>

          <section className="profile-dialog-section profile-dialog-section-password">
            <div className="profile-dialog-section-title">
              <h3>Ð?i m?t kh?u</h3>
              <p>Sau khi d?i m?t kh?u, phiên dang nh?p hi?n t?i s? du?c dang xu?t d? b?o m?t.</p>
            </div>

            {passwordError ? <div className="profile-dialog-alert error">{passwordError}</div> : null}
            {passwordSuccess ? <div className="profile-dialog-alert success">{passwordSuccess}</div> : null}

            <form className="profile-dialog-form" onSubmit={onPasswordSubmit}>
              <label>
                M?t kh?u hi?n t?i
                <input
                  type="password"
                  name="oldPassword"
                  value={passwordForm.oldPassword}
                  onChange={onPasswordChange}
                  autoComplete="current-password"
                  required
                />
              </label>
              <label>
                M?t kh?u m?i
                <input
                  type="password"
                  name="newPassword"
                  value={passwordForm.newPassword}
                  onChange={onPasswordChange}
                  autoComplete="new-password"
                  minLength={6}
                  required
                />
              </label>
              <label className="profile-dialog-field-full">
                Xác nh?n m?t kh?u m?i
                <input
                  type="password"
                  name="confirmPassword"
                  value={passwordForm.confirmPassword}
                  onChange={onPasswordChange}
                  autoComplete="new-password"
                  minLength={6}
                  required
                />
              </label>

              <div className="profile-dialog-actions">
                <button
                  type="button"
                  className="ghost"
                  onClick={() =>
                    setPasswordForm({
                      oldPassword: "",
                      newPassword: "",
                      confirmPassword: "",
                    })
                  }
                  disabled={changingPassword}
                >
                  Xóa nhanh
                </button>
                <button type="submit" disabled={changingPassword}>
                  {changingPassword ? "Ðang d?i..." : "Ð?i m?t kh?u"}
                </button>
              </div>
            </form>
          </section>
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

