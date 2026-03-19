function RegisterForm({ form, onChange, onSubmit, isValid, loading }) {
  return (
    <form onSubmit={onSubmit} className="form">
      <label htmlFor="register-name">Ho ten</label>
      <input
        id="register-name"
        type="text"
        value={form.fullName}
        onChange={(e) => onChange("fullName", e.target.value)}
        placeholder="Nguyen Van A"
        required
      />

      <label htmlFor="register-phone">So dien thoai</label>
      <input
        id="register-phone"
        type="tel"
        value={form.phone}
        onChange={(e) => onChange("phone", e.target.value)}
        placeholder="0900000000"
        required
      />

      <label htmlFor="register-email">Email</label>
      <input
        id="register-email"
        type="email"
        value={form.email}
        onChange={(e) => onChange("email", e.target.value)}
        placeholder="user@example.com"
        autoComplete="email"
        required
      />

      <label htmlFor="register-password">Mat khau</label>
      <input
        id="register-password"
        type="password"
        value={form.password}
        onChange={(e) => onChange("password", e.target.value)}
        placeholder="Toi thieu 6 ky tu"
        autoComplete="new-password"
        required
        minLength={6}
      />

      <button type="submit" disabled={loading || !isValid}>
        {loading ? "Dang xu ly..." : "Dang ky"}
      </button>
    </form>
  );
}

export default RegisterForm;
