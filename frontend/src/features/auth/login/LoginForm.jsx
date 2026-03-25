function LoginForm({ form, onChange, onSubmit, isValid, loading }) {
  return (
    <form onSubmit={onSubmit} className="form" autoComplete="off">
      <label htmlFor="login-identifier">Email hoac so dien thoai</label>
      <input
        id="login-identifier"
        name="login_identifier"
        type="text"
        value={form.identifier}
        onChange={(e) => onChange("identifier", e.target.value)}
        placeholder="user@gmail.com hoac 09xxxxxxxx"
        autoComplete="off"
        data-lpignore="true"
        required
      />

      <label htmlFor="login-password">Mat khau</label>
      <input
        id="login-password"
        name="login_password"
        type="password"
        value={form.password}
        onChange={(e) => onChange("password", e.target.value)}
        placeholder="Toi thieu 6 ky tu"
        autoComplete="off"
        data-lpignore="true"
        required
        minLength={6}
      />

      <button type="submit" disabled={loading || !isValid}>
        {loading ? "Dang xu ly..." : "Dang nhap"}
      </button>
    </form>
  );
}

export default LoginForm;
