function AuthTabs({ mode, onModeChange }) {
  return (
    <div className="tabs">
      <button
        type="button"
        className={`tab ${mode === "login" ? "active" : ""}`}
        onClick={() => onModeChange("login")}
      >
        Dang nhap
      </button>
      <button
        type="button"
        className={`tab ${mode === "register" ? "active" : ""}`}
        onClick={() => onModeChange("register")}
      >
        Dang ky
      </button>
    </div>
  );
}

export default AuthTabs;
