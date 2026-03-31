function RegisterForm({ form, onChange, onSubmit, isValid, loading }) {
  return (
    <form onSubmit={onSubmit} className="form">
      <label htmlFor="register-name">Họ tên</label>
      <input
        id="register-name"
        type="text"
        value={form.fullName}
        onChange={(e) => onChange("fullName", e.target.value)}
        placeholder="Nguyễn Văn A"
        required
      />

      <label htmlFor="register-phone">Số điện thoại *</label>
      <input
        id="register-phone"
        type="tel"
        value={form.phone}
        onChange={(e) => onChange("phone", e.target.value)}
        placeholder="0900000000"
        pattern="^(0[0-9]{9,10}|\+84[0-9]{9,10})$"
        title="Số điện thoại hợp lệ: 0xxxxxxxxx hoặc +84xxxxxxxxx"
        required
      />

      <label htmlFor="register-email">Email *</label>
      <input
        id="register-email"
        type="email"
        value={form.email}
        onChange={(e) => onChange("email", e.target.value)}
        placeholder="user@example.com"
        autoComplete="email"
        required
      />

      <label htmlFor="register-password">Mật khẩu</label>
      <input
        id="register-password"
        type="password"
        value={form.password}
        onChange={(e) => onChange("password", e.target.value)}
        placeholder="Tối thiểu 6 ký tự"
        autoComplete="new-password"
        required
        minLength={6}
      />

      <button type="submit" disabled={loading || !isValid}>
        {loading ? "Đang xử lý..." : "Đăng ký"}
      </button>
    </form>
  );
}

export default RegisterForm;

