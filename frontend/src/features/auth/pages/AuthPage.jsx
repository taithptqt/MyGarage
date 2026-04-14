import { useEffect, useMemo, useState } from "react";
import { Navigate, useLocation, useNavigate } from "react-router-dom";
import { getApiBaseUrl } from "@features/auth/api/authApi";
import AuthResult from "@features/auth/components/AuthResult";
import GoogleLoginButton from "@features/auth/components/GoogleLoginButton";
import LoginForm from "@features/auth/login/LoginForm";
import { isLoginFormValid } from "@features/auth/login/loginValidation";
import RegisterForm from "@features/auth/register/RegisterForm";
import { isRegisterFormValid } from "@features/auth/register/registerValidation";
import { useAuth } from "@shared/core/context/AuthContext";
import "./AuthPage.css";

const AUTH_HIGHLIGHTS = [
  {
    title: "Đặt lịch nhanh chóng",
    description: "Chủ động chọn dịch vụ, chi nhánh và khung giờ phù hợp chỉ trong vài phút.",
  },
  {
    title: "Theo dõi minh bạch",
    description: "Cập nhật tiến độ xử lý xe theo từng công đoạn ngay trên hệ thống.",
  },
  {
    title: "Dữ liệu tập trung",
    description: "Lưu trữ lịch sử sửa chữa và thông tin xe để tư vấn chính xác hơn mỗi lần quay lại.",
  },
];

function AuthPage() {
  const phoneRegex = /^(0\d{9,10}|\+84\d{9,10})$/;
  const navigate = useNavigate();
  const location = useLocation();
  const {
    login: contextLogin,
    register: contextRegister,
    googleLogin: contextGoogleLogin,
    logout: contextLogout,
  } = useAuth();

  const token = localStorage.getItem("mygarage_token");
  const [mode, setMode] = useState("login");
  const [loginForm, setLoginForm] = useState({ identifier: "", password: "" });
  const [registerForm, setRegisterForm] = useState({
    fullName: "",
    phone: "",
    email: "",
    password: "",
  });
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");
  const [result, setResult] = useState(null);
  const [pendingGoogleToken, setPendingGoogleToken] = useState("");
  const [needGooglePhone, setNeedGooglePhone] = useState(false);
  const [googlePhone, setGooglePhone] = useState("");
  const [locationDenied, setLocationDenied] = useState(false);
  const requestedPath = location?.state?.from?.pathname || "";

  useEffect(() => {
    document.title = mode === "login" ? "Đăng nhập | MyGarage" : "Đăng ký | MyGarage";
  }, [mode]);

  useEffect(() => {
    if (navigator?.permissions) {
      navigator.permissions
        .query({ name: "geolocation" })
        .then((permissionResult) => {
          setLocationDenied(permissionResult.state === "denied");
          permissionResult.onchange = () => setLocationDenied(permissionResult.state === "denied");
        })
        .catch(() => {
          // ignore
        });
    }
  }, []);

  const isLoginValid = useMemo(() => isLoginFormValid(loginForm), [loginForm]);
  const isRegisterValid = useMemo(() => isRegisterFormValid(registerForm), [registerForm]);

  const getLocationPayload = async () => {
    if (!navigator?.geolocation) return {};
    return new Promise((resolve) => {
      navigator.geolocation.getCurrentPosition(
        (position) =>
          resolve({
            lat: position.coords.latitude,
            lng: position.coords.longitude,
          }),
        () => resolve({}),
        { enableHighAccuracy: true, timeout: 5000, maximumAge: 60000 }
      );
    });
  };

  const updateLogin = (key, value) => setLoginForm((prev) => ({ ...prev, [key]: value }));
  const updateRegister = (key, value) => setRegisterForm((prev) => ({ ...prev, [key]: value }));

  const redirectAfterAuth = (data) => {
    const code = String(data?.roleCode || "").toUpperCase();
    const roleName = String(data?.role || "").toLowerCase();
    let destination = "/home";

    if (requestedPath.startsWith("/booking/form") && code === "CUSTOMER") {
      destination = "/booking/form";
    } else if (code === "ADMIN" || roleName === "admin") {
      destination = "/admin";
    } else if (code === "ACCOUNTANT") {
      destination = "/accounting/dashboard";
    } else if (["RECEPTIONIST", "TECH", "MANAGER", "QUALITY_CONTROL", "FOREMAN"].includes(code)) {
      destination = "/workshop";
    }

    navigate(destination, { replace: true });
  };

  const handleLogin = async (event) => {
    if (event) event.preventDefault();
    setError("");
    setResult(null);

    if (!isLoginValid) {
      setError("Thông tin đăng nhập chưa hợp lệ.");
      return;
    }

    setLoading(true);
    try {
      const location = await getLocationPayload();
      const data = await contextLogin({
        identifier: loginForm.identifier.trim(),
        password: loginForm.password,
        ...location,
      });
      setResult(data);
      redirectAfterAuth(data);
    } catch (apiError) {
      setError(apiError?.message || "Đăng nhập thất bại.");
    } finally {
      setLoading(false);
    }
  };

  const handleRegister = async (event) => {
    if (event) event.preventDefault();
    setError("");
    setResult(null);

    if (!isRegisterValid) {
      setError("Thông tin đăng ký chưa hợp lệ.");
      return;
    }

    setLoading(true);
    try {
      const location = await getLocationPayload();
      const data = await contextRegister({
        fullName: registerForm.fullName.trim(),
        phone: registerForm.phone.trim(),
        email: registerForm.email.trim() || null,
        password: registerForm.password,
        ...location,
      });
      setResult(data);
      redirectAfterAuth(data);
    } catch (apiError) {
      setError(apiError?.message || "Đăng ký thất bại.");
    } finally {
      setLoading(false);
    }
  };

  const handleLogout = () => {
    contextLogout();
    setResult(null);
  };

  const handleGoogleLogin = async (idToken) => {
    setError("");
    setResult(null);
    setLoading(true);

    try {
      const location = await getLocationPayload();
      const data = await contextGoogleLogin({ idToken, ...location });
      setNeedGooglePhone(false);
      setPendingGoogleToken("");
      setGooglePhone("");
      setResult(data);
      redirectAfterAuth(data);
    } catch (apiError) {
      const msg = apiError?.message || "Đăng nhập Google thất bại.";
      if (String(msg).includes("PHONE_REQUIRED")) {
        setNeedGooglePhone(true);
        setPendingGoogleToken(idToken);
        setError("Chào mừng! Vui lòng nhập số điện thoại để chúng tôi tìm lịch sử sửa xe của bạn.");
      } else {
        setNeedGooglePhone(false);
        setError(msg);
      }
    } finally {
      setLoading(false);
    }
  };

  const handleGooglePhoneConfirm = async (event) => {
    if (event) event.preventDefault();
    if (!googlePhone.trim() || !pendingGoogleToken) return;
    if (!phoneRegex.test(googlePhone.trim())) {
      setError("Số điện thoại không đúng định dạng (0xxxxxxxxx hoặc +84xxxxxxxxx).");
      return;
    }

    setLoading(true);
    setError("");
    try {
      const location = await getLocationPayload();
      const data = await contextGoogleLogin({
        idToken: pendingGoogleToken,
        phone: googlePhone.trim(),
        ...location,
      });
      setNeedGooglePhone(false);
      setPendingGoogleToken("");
      setGooglePhone("");
      setResult(data);
      redirectAfterAuth(data);
    } catch (apiError) {
      setError(apiError?.message || "Liên kết số điện thoại thất bại.");
    } finally {
      setLoading(false);
    }
  };

  const closeGooglePhoneModal = () => {
    if (loading) return;
    setNeedGooglePhone(false);
    setPendingGoogleToken("");
    setGooglePhone("");
    setError("");
  };

  if (token) {
    const savedUser = JSON.parse(localStorage.getItem("mygarage_user") || "{}");
    const code = String(savedUser?.roleCode || "").toUpperCase();
    const roleName = String(savedUser?.role || "").toLowerCase();
    let dest = "/home";

    if (requestedPath.startsWith("/booking/form") && code === "CUSTOMER") {
      dest = "/booking/form";
    } else if (code === "ADMIN" || roleName === "admin") {
      dest = "/admin";
    } else if (code === "ACCOUNTANT") {
      dest = "/accounting/dashboard";
    } else if (["RECEPTIONIST", "TECH", "MANAGER", "QUALITY_CONTROL", "FOREMAN"].includes(code)) {
      dest = "/workshop";
    }

    return <Navigate to={dest} replace />;
  }

  return (
    <main className="page">
      <div className="auth-scene" aria-hidden="true">
        <div className="auth-floating-tag auth-floating-tag--left">
          <span>15.000+</span>
          <p>Lượt xe được phục vụ mỗi năm</p>
        </div>
        <div className="auth-floating-tag auth-floating-tag--right">
          <span>4.9 / 5</span>
          <p>Đánh giá trung bình từ khách hàng</p>
        </div>
        <div className="auth-floating-tag auth-floating-tag--bottom">
          <span>30+</span>
          <p>Kỹ thuật viên nhiều kinh nghiệm</p>
        </div>
      </div>

      <div className="auth-shell">
        <section className="auth-showcase">
          <div className="auth-showcase-content">
            <p className="auth-showcase-kicker">MYGARAGE PREMIUM CARE</p>
            <h2>
              Nền tảng đặt lịch và theo dõi dịch vụ xe
              theo chuẩn gara chuyên nghiệp
            </h2>
            <p>
              Đăng nhập để quản lý lịch hẹn, lịch sử sửa chữa và nhận
              gợi ý dịch vụ phù hợp theo tình trạng xe của bạn.
            </p>

            <div className="auth-showcase-features">
              {AUTH_HIGHLIGHTS.map((item, index) => (
                <article key={item.title} className="auth-feature-card">
                  <span className="auth-feature-index">{String(index + 1).padStart(2, "0")}</span>
                  <h3>{item.title}</h3>
                  <p>{item.description}</p>
                </article>
              ))}
            </div>
          </div>
        </section>

        <section className="card">
          <h1>{mode === "login" ? "Đăng nhập" : "Đăng ký"}</h1>
          <p className="subtitle">Một tài khoản sử dụng tất cả dịch vụ của MyGarage</p>

          {mode === "login" ? (
            <LoginForm
              form={loginForm}
              onChange={updateLogin}
              onSubmit={handleLogin}
              isValid={isLoginValid}
              loading={loading}
            />
          ) : (
            <RegisterForm
              form={registerForm}
              onChange={updateRegister}
              onSubmit={handleRegister}
              isValid={isRegisterValid}
              loading={loading}
            />
          )}

          {error && <div className="error">{error}</div>}

          {locationDenied && !error && (
            <div className="location-hint">
              Mẹo: Bật vị trí để hệ thống ưu tiên gợi ý chi nhánh gần nhất.
            </div>
          )}

          <div className="footer-switcher">
            {mode === "login" ? (
              <>
                Bạn chưa có tài khoản,
                <span className="switcher-link" onClick={() => { setMode("register"); setError(""); }}>
                  Đăng ký ngay
                </span>
              </>
            ) : (
              <>
                Bạn đã có tài khoản?
                <span className="switcher-link" onClick={() => { setMode("login"); setError(""); }}>
                  Đăng nhập ngay
                </span>
              </>
            )}
          </div>

          <div className="divider">
            <span>Hoặc đăng nhập với</span>
          </div>

          <div className="google-login-btn-container">
            <GoogleLoginButton onCredential={handleGoogleLogin} onError={(message) => setError(message)} />
          </div>

          <AuthResult result={result} onLogout={handleLogout} />
        </section>
      </div>

      {needGooglePhone && (
        <div className="phone-modal-backdrop" onClick={closeGooglePhoneModal}>
          <div className="phone-modal" onClick={(e) => e.stopPropagation()}>
            <h3>Liên kết số điện thoại</h3>
            <p>Chào mừng! Vui lòng nhập số điện thoại để chúng tôi tìm lịch sử sửa xe của bạn.</p>
            <form onSubmit={handleGooglePhoneConfirm} className="form">
              <label htmlFor="google-phone">Số điện thoại</label>
              <input
                id="google-phone"
                type="tel"
                value={googlePhone}
                onChange={(e) => setGooglePhone(e.target.value)}
                placeholder="0900000000"
                pattern="^(0[0-9]{9,10}|\+84[0-9]{9,10})$"
                title="Số điện thoại hợp lệ: 0xxxxxxxxx hoặc +84xxxxxxxxx"
                required
                autoFocus
              />
              <div className="phone-modal-actions">
                <button type="button" className="btn-cancel" onClick={closeGooglePhoneModal} disabled={loading}>
                  Hủy
                </button>
                <button type="submit" disabled={loading || !googlePhone.trim()}>
                  {loading ? "Đang xử lý..." : "Xác nhận"}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      <p className="api-hint">API: {getApiBaseUrl()}</p>
    </main>
  );
}

export default AuthPage;

