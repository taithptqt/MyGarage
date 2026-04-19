import React, { useState, useRef } from "react";
import { Modal } from "@features/admin/components/AdminModals";
import { updateStaff, uploadStaffAvatar } from "@features/admin/api/staffApi";
import { useAuth } from "@shared/core/context/AuthContext";
import { VIETNAM_PHONE_PATTERN, isOptionalEmailValid, isValidVietnamesePhone } from "@shared/utils/contactValidation";
import { API_BASE_URL } from "@app/apiClient";
import ImageCropper from "@features/workshop/components/ImageCropper";

export default function ProfileModal({ user, onClose }) {
  const { setUser } = useAuth();
  const fileInputRef = useRef(null);
  const [formData, setFormData] = useState({
    fullName: user?.fullName || "",
    phone: user?.phone || "",
    email: user?.email || "",
    citizenId: user?.citizenId || "",
    dateOfBirth: user?.dateOfBirth ? user.dateOfBirth.split('T')[0] : "",
    avatarUrl: user?.avatarUrl || ""
  });
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");
  const [fieldErrors, setFieldErrors] = useState({});
  const [success, setSuccess] = useState("");
  
  // Image Crop states
  const [selectedImage, setSelectedImage] = useState(null);
  const [showCropper, setShowCropper] = useState(false);

  const handleChange = (e) => {
    const { name, value } = e.target;
    setFormData(prev => ({ ...prev, [name]: value }));
    // Clear field error when user starts typing
    if (fieldErrors[name]) {
      setFieldErrors(prev => ({ ...prev, [name]: "" }));
    }
  };

  const handleFileChange = (e) => {
    const file = e.target.files[0];
    if (file) {
      const reader = new FileReader();
      reader.onload = () => {
        setSelectedImage(reader.result);
        setShowCropper(true);
      };
      reader.readAsDataURL(file);
    }
  };

  const onCropComplete = async (croppedBlob) => {
    setShowCropper(false);
    setLoading(true);
    setError("");
    
    // Create local preview immediately
    const localPreviewUrl = URL.createObjectURL(croppedBlob);
    setFormData(prev => ({ ...prev, avatarUrl: localPreviewUrl }));
    
    const userId = user?.id || user?.Id;
    if (!userId) {
      setError("Không tìm thấy ID người dùng. Vui lòng tải lại trang.");
      setLoading(false);
      return;
    }
    try {
      // Upload the cropped blob to backend
      const res = await uploadStaffAvatar(userId, croppedBlob);
      if (res && res.avatarUrl) {
        // Use the relative path from server to leverage Vite proxy and avoid certificate issues
        const serverUrl = res.avatarUrl.includes('?') 
          ? `${res.avatarUrl}&t=${new Date().getTime()}` 
          : `${res.avatarUrl}?t=${new Date().getTime()}`;
          
        setFormData(prev => ({ ...prev, avatarUrl: serverUrl }));
        // Sync context
        const newUser = { ...user, avatarUrl: serverUrl };
        setUser(newUser);
        localStorage.setItem('mygarage_user', JSON.stringify(newUser));
        setSuccess("Cập nhật ảnh đại diện thành công!");
      }
    } catch (err) {
      setError("Không thể tải ảnh lên. Lỗi: " + err.message);
    } finally {
      setLoading(false);
    }
  };

  const handleSubmit = async (e) => {
    e.preventDefault();
    setLoading(true);
    setError("");
    setFieldErrors({});
    setSuccess("");

    // Validation
    const errors = {};
    if (!isValidVietnamesePhone(formData.phone)) {
      errors.phone = "Số điện thoại phải bao gồm đúng 10 chữ số.";
    }

    if (!isOptionalEmailValid(formData.email)) {
      errors.email = "Email không đúng định dạng.";
    }

    if (formData.citizenId && !/^\d{12}$/.test(formData.citizenId)) {
      errors.citizenId = "Số CCCD phải bao gồm đúng 12 chữ số.";
    }

    if (Object.keys(errors).length > 0) {
      setFieldErrors(errors);
      setLoading(false);
      return;
    }

    try {
      const userId = user?.id || user?.Id;
      if (!userId) {
        setError("Không tìm thấy ID người dùng.");
        setLoading(false);
        return;
      }

      // Convert empty strings to null for the backend
      const payload = { 
        ...formData,
        dateOfBirth: formData.dateOfBirth || null,
        citizenId: formData.citizenId || null
      };
      
      const updated = await updateStaff(userId, payload);
      
      const newUser = { ...user, ...updated };
      setUser(newUser);
      localStorage.setItem('mygarage_user', JSON.stringify(newUser));
      
      setSuccess("Cập nhật hồ sơ thành công!");
      setTimeout(() => {
        onClose();
      }, 1500);
    } catch (err) {
      setError(err.message || "Không thể cập nhật hồ sơ.");
    } finally {
      setLoading(false);
    }
  };

  return (
    <>
      <Modal title="Hồ sơ nhân viên" onClose={onClose}>
        <form className="admin-form profile-form full-profile" onSubmit={handleSubmit}>
          <div className="profile-top-section" style={{ display: 'flex', alignItems: 'center', gap: '24px', marginBottom: '32px' }}>
            <div className="avatar-preview-container" style={{ position: 'relative' }}>
              <div className="user-avatar" style={{ 
                width: '100px', 
                height: '100px', 
                fontSize: '40px',
                backgroundImage: formData.avatarUrl 
                  ? `url("${formData.avatarUrl.startsWith('http') || formData.avatarUrl.startsWith('data:') || formData.avatarUrl.startsWith('blob:') || formData.avatarUrl.startsWith('/')
                      ? formData.avatarUrl 
                      : `${API_BASE_URL}${formData.avatarUrl.startsWith('/') ? '' : '/'}${formData.avatarUrl}`}")` 
                  : 'none',
                backgroundSize: 'cover',
                backgroundPosition: 'center',
                border: '4px solid #f4f7fe',
                backgroundColor: '#11047a',
                color: '#ffffff',
                boxShadow: '0 4px 12px rgba(17, 4, 122, 0.2)'
              }}>
                {!formData.avatarUrl && (user?.fullName?.split(' ').pop().substring(0,2).toUpperCase())}
              </div>
              <button 
                type="button" 
                className="btn-primary-sm" 
                onClick={() => fileInputRef.current.click()}
                style={{ 
                  position: 'absolute', 
                  bottom: '-10px', 
                  right: '-10px', 
                  borderRadius: '50%', 
                  width: '32px', 
                  height: '32px', 
                  padding: 0, 
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'center',
                  fontSize: '16px'
                }}
                title="Thay đổi ảnh"
              >
                {"\u{1F4F7}"}
              </button>
              <input 
                type="file" 
                ref={fileInputRef} 
                onChange={handleFileChange} 
                accept="image/*" 
                style={{ display: 'none' }} 
              />
            </div>
            <div className="profile-identity">
              <h3 style={{ margin: 0, color: '#2b3674', fontSize: '22px', fontWeight: 800 }}>{formData.fullName || "Tên tài khoản"}</h3>
              <p style={{ margin: '4px 0 0', color: '#a3aed0', fontSize: '14px', fontWeight: 500 }}>{user?.role || user?.roleCode}</p>
            </div>
          </div>

          {success && <div className="success-msg" style={{ borderLeft: '4px solid #2ecc71', background: '#eefcf5', color: '#1e7e4a', padding: '12px 20px', marginBottom: '20px' }}>{success}</div>}
          {error && !Object.keys(fieldErrors).length && <div className="error-msg" style={{ borderLeft: '4px solid #e74c3c', marginBottom: '20px' }}>{error}</div>}

          <div className="form-grid" style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '20px' }}>
            <div className="form-group">
              <label>Họ và tên *</label>
              <input type="text" name="fullName" value={formData.fullName} onChange={handleChange} required />
              {fieldErrors.fullName && <p className="field-error-msg" style={{ color: '#e74c3c', fontSize: '12px', marginTop: '4px', marginBottom: 0 }}>{fieldErrors.fullName}</p>}
            </div>

            <div className="form-group">
              <label>Số điện thoại *</label>
              <input type="tel" inputMode="tel" pattern={VIETNAM_PHONE_PATTERN} title="Số điện thoại hợp lệ: 0xxxxxxxxx hoặc +84xxxxxxxxx" name="phone" value={formData.phone} onChange={handleChange} required />
              {fieldErrors.phone && <p className="field-error-msg" style={{ color: '#e74c3c', fontSize: '12px', marginTop: '4px', marginBottom: 0 }}>{fieldErrors.phone}</p>}
            </div>

            <div className="form-group">
              <label>Email *</label>
              <input type="email" name="email" value={formData.email} onChange={handleChange} required />
              {fieldErrors.email && <p className="field-error-msg" style={{ color: '#e74c3c', fontSize: '12px', marginTop: '4px', marginBottom: 0 }}>{fieldErrors.email}</p>}
            </div>

            <div className="form-group">
              <label>Số CCCD</label>
              <input type="text" name="citizenId" value={formData.citizenId} onChange={handleChange} placeholder="Chưa cập nhật" />
              {fieldErrors.citizenId && <p className="field-error-msg" style={{ color: '#e74c3c', fontSize: '12px', marginTop: '4px', marginBottom: 0 }}>{fieldErrors.citizenId}</p>}
            </div>

            <div className="form-group">
              <label>Ngày sinh</label>
              <input type="date" name="dateOfBirth" value={formData.dateOfBirth} onChange={handleChange} />
              {fieldErrors.dateOfBirth && <p className="field-error-msg" style={{ color: '#e74c3c', fontSize: '12px', marginTop: '4px', marginBottom: 0 }}>{fieldErrors.dateOfBirth}</p>}
            </div>
            
          </div>

          {user?.roleCode !== 'CUSTOMER' && (
            <div className="form-group" style={{ marginTop: '10px', opacity: 0.8 }}>
              <label>Chi nhánh</label>
              <input type="text" value={user?.branchName || "Chưa xác định"} disabled />
            </div>
          )}

          <div className="form-actions" style={{ marginTop: '32px', borderTop: '1px solid #f4f7fe', paddingTop: '24px' }}>
            <button type="button" className="btn-ghost" onClick={onClose} disabled={loading}>Hủy</button>
            <button type="submit" className="btn-primary" style={{ padding: '0 32px' }} disabled={loading}>
              {loading ? "Đang lưu..." : "Lưu hồ sơ"}
            </button>
          </div>
        </form>
      </Modal>

      {showCropper && (
        <ImageCropper 
          image={selectedImage} 
          onCropComplete={onCropComplete} 
          onCancel={() => setShowCropper(false)} 
        />
      )}
    </>
  );
}








