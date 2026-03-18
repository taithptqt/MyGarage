function LoginForm({ form, onChange, onSubmit, isValid, loading }) {
  return (
    <form onSubmit={onSubmit} className="form">
      <label htmlFor="login-email">Email</label>
      <input
        id="login-email"
        type="email"
        value={form.email}
        onChange={(e) => onChange("email", e.target.value)}
        placeholder="user@example.com"
        autoComplete="email"
        required
      />

      <label htmlFor="login-password">Mat khau</label>
      <input
        id="login-password"
        type="password"
        value={form.password}
        onChange={(e) => onChange("password", e.target.value)}
        placeholder="Toi thieu 6 ky tu"
        autoComplete="current-password"
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
