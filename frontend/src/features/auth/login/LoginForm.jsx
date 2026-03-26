function LoginForm({ form, onChange, onSubmit, isValid, loading }) {
  return (
    <form onSubmit={onSubmit} className="form">
      <label htmlFor="login-identifier">Email hoặc số điện thoại</label>
      <input
        id="login-identifier"
        type="text"
        value={form.identifier}
        onChange={(e) => onChange("identifier", e.target.value)}
        placeholder="user@example.com hoặc 0900000000"
        autoComplete="username"
        required
      />

      <label htmlFor="login-password">Mật khẩu</label>
      <input
        id="login-password"
        type="password"
        value={form.password}
        onChange={(e) => onChange("password", e.target.value)}
        placeholder="Tối thiểu 6 ký tự"
        autoComplete="current-password"
        required
        minLength={6}
      />

      <button type="submit" disabled={loading || !isValid}>
        {loading ? "Đang xử lý..." : "Đăng nhập"}
      </button>
    </form>
  );
}

export default LoginForm;
