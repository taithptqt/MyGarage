import { useState } from "react";

function RegisterForm({ form, onChange, onSubmit, isValid, loading }) {
  const [showPassword, setShowPassword] = useState(false);

  return (
    <form onSubmit={onSubmit} className="form">
      <div className="form-field">
        <label htmlFor="register-name">Họ và tên</label>
        <input
          id="register-name"
          type="text"
          value={form.fullName}
          onChange={(e) => onChange("fullName", e.target.value)}
          placeholder="Họ và tên"
          required
        />
      </div>

      <div className="form-field">
        <label htmlFor="register-phone">Số điện thoại</label>
        <input
          id="register-phone"
          type="tel"
          value={form.phone}
          onChange={(e) => onChange("phone", e.target.value)}
          placeholder="Số điện thoại"
          required
        />
      </div>

      <div className="form-field">
        <label htmlFor="register-email">Email</label>
        <input
          id="register-email"
          type="email"
          value={form.email}
          onChange={(e) => onChange("email", e.target.value)}
          placeholder="Địa chỉ Email"
          autoComplete="email"
          required
        />
      </div>

      <div className="form-field">
        <label htmlFor="register-password">Mật khẩu</label>
        <div className="password-input-wrapper">
          <input
            id="register-password"
            type={showPassword ? "text" : "password"}
            value={form.password}
            onChange={(e) => onChange("password", e.target.value)}
            placeholder="Mật khẩu (ít nhất 6 ký tự)"
            autoComplete="new-password"
            required
            minLength={6}
          />
          <button 
            type="button" 
            className="password-toggle"
            onClick={() => setShowPassword(!showPassword)}
            aria-label={showPassword ? "Ẩn mật khẩu" : "Hiện mật khẩu"}
          >
            {showPassword ? (
              <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><path d="M17.94 17.94A10.07 10.07 0 0 1 12 19c-7 0-11-8-11-8a18.45 18.45 0 0 1 5.06-5.94M9.9 4.24A9.12 9.12 0 0 1 12 4c7 0 11 8 11 8a18.5 18.5 0 0 1-2.16 3.19m-6.72-1.07a3 3 0 1 1-4.24-4.24"></path><line x1="1" y1="1" x2="23" y2="23"></line></svg>
            ) : (
              <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><path d="M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z"></path><circle cx="12" cy="12" r="3"></circle></svg>
            )}
          </button>
        </div>
      </div>

      <button type="submit" disabled={loading || !isValid}>
        {loading ? "Đang xử lý..." : "Đăng ký"}
      </button>
    </form>
  );
}

export default RegisterForm;
