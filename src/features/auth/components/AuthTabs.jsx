function AuthTabs({ mode, onModeChange }) {
  return (
    <div className="tabs">
      <button
        type="button"
        className={`tab ${mode === "login" ? "active" : ""}`}
        onClick={() => onModeChange("login")}
      >
        Đăng nhập
      </button>
      <button
        type="button"
        className={`tab ${mode === "register" ? "active" : ""}`}
        onClick={() => onModeChange("register")}
      >
        Đăng ký
      </button>
    </div>
  );
}

export default AuthTabs;
