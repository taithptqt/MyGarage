import { useEffect, useMemo, useState } from "react";
import { Navigate, useLocation, useNavigate } from "react-router-dom";
import { getApiBaseUrl } from "@features/auth/api/authApi";
import AuthResult from "@features/auth/components/AuthResult";
import GoogleLoginButton from "@features/auth/components/GoogleLoginButton";
import LoginForm from "@features/auth/login/LoginForm";
import { isLoginFormValid } from "@features/auth/login/loginValidation";
import RegisterForm from "@features/auth/register/RegisterForm";
import { isRegisterFormValid } from "@features/auth/register/registerValidation";
import { clearAuthStorage, readAuthUser } from "@features/auth/utils/authStorage";
import { useAuth } from "@shared/core/context/AuthContext";
import { isValidVietnamesePhone } from "@shared/utils/contactValidation";
import "./AuthPage.css";

const AUTH_HIGHLIGHTS = [
  {
    title: "Đặt lịch nhanh chóng",
    description: "Chỉ cần chọn dịch vụ, chi nhánh và khung giờ phù hợp chỉ trong vài phút.",
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

const PHONE_REQUEST_CODE = "PHONE_REQUEST";
const PHONE_REQUEST_STAFF_CODE = `${PHONE_REQUEST_CODE}:STAFF`;
const GOOGLE_PHONE_ROLE = {
  CUSTOMER: "CUSTOMER",
  STAFF: "STAFF",
};

function shouldReturnToRequestedCustomerPath(roleCode, requestedPath = "") {
  const normalizedRoleCode = String(roleCode || "").trim().toUpperCase();
  const normalizedPath = String(requestedPath || "").trim();
  if (normalizedRoleCode !== "CUSTOMER") {
    return false;
  }
  if (!normalizedPath || normalizedPath === "/login") {
    return false;
  }
  return normalizedPath.startsWith("/");
}

function resolvePostAuthDestination(data, requestedPath) {
  const code = String(data?.roleCode || "").toUpperCase();
  const roleName = String(data?.role || "").toLowerCase();
  if (shouldReturnToRequestedCustomerPath(code, requestedPath)) {
    return requestedPath;
  }
  if (code === "ADMIN" || roleName === "admin") {
    return "/admin";
  }
  if (code === "GATE_SECURITY") return "/workshop/gate";
  if (code === "ACCOUNTANT") {
    return "/accounting/dashboard";
  }
  if (["RECEPTIONIST", "TECH", "MANAGER", "QUALITY_CONTROL", "FOREMAN", "TEAM_LEADER", "STORE_KEEPER"].includes(code)) {
    return "/workshop";
  }
  return "/home";
}

function AuthPage() {
  const navigate = useNavigate();
  const location = useLocation();
  const {
    login: contextLogin,
    register: contextRegister,
    googleLogin: contextGoogleLogin,
    logout: contextLogout,
    user,
    isAuthenticated,
    loading: authLoading,
  } = useAuth();

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
  const [flashMessage, setFlashMessage] = useState("");
  const [result, setResult] = useState(null);
  const [pendingGoogleToken, setPendingGoogleToken] = useState("");
  const [needGooglePhone, setNeedGooglePhone] = useState(false);
  const [googlePhone, setGooglePhone] = useState("");
  const [pendingGoogleRole, setPendingGoogleRole] = useState(GOOGLE_PHONE_ROLE.CUSTOMER);
  const [locationDenied, setLocationDenied] = useState(false);
  const requestedPath = location?.state?.from?.pathname || "";
  const isStaffPhoneRequest = pendingGoogleRole === GOOGLE_PHONE_ROLE.STAFF;

  useEffect(() => {
    document.title = mode === "login" ? "Đăng nhập | MyGarage" : "Đăng ký | MyGarage";
  }, [mode]);

  useEffect(() => {
    const incomingFlash = location?.state?.flashMessage;
    if (!incomingFlash) return;

    setFlashMessage(String(incomingFlash));
    const timer = setTimeout(() => setFlashMessage(""), 3000);
    navigate(location.pathname, { replace: true, state: null });

    return () => clearTimeout(timer);
  }, [location, navigate]);

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

  useEffect(() => {
    if (!authLoading && !isAuthenticated && readAuthUser()) {
      clearAuthStorage();
    }
  }, [authLoading, isAuthenticated]);

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
    navigate(resolvePostAuthDestination(data, requestedPath), { replace: true });
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
      setPendingGoogleRole(GOOGLE_PHONE_ROLE.CUSTOMER);
      setResult(data);
      redirectAfterAuth(data);
    } catch (apiError) {
      const msg = apiError?.message || "Đăng nhập Google thất bại.";
      const normalizedMessage = String(msg).toUpperCase();
      if (normalizedMessage.includes(PHONE_REQUEST_CODE)) {
        setNeedGooglePhone(true);
        setPendingGoogleToken(idToken);
        setPendingGoogleRole(
          normalizedMessage.includes(PHONE_REQUEST_STAFF_CODE)
            ? GOOGLE_PHONE_ROLE.STAFF
            : GOOGLE_PHONE_ROLE.CUSTOMER
        );
        setError("");
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
    if (!isValidVietnamesePhone(googlePhone.trim())) {
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
      setPendingGoogleRole(GOOGLE_PHONE_ROLE.CUSTOMER);
      setResult(data);
      redirectAfterAuth(data);
    } catch (apiError) {
      const msg = apiError?.message || "Liên kết số điện thoại thất bại.";
      const normalizedMessage = String(msg).toUpperCase();
      if (normalizedMessage.includes(PHONE_REQUEST_CODE)) {
        setNeedGooglePhone(true);
        setPendingGoogleRole(
          normalizedMessage.includes(PHONE_REQUEST_STAFF_CODE)
            ? GOOGLE_PHONE_ROLE.STAFF
            : GOOGLE_PHONE_ROLE.CUSTOMER
        );
        setError("");
      } else {
        setNeedGooglePhone(false);
        setError(msg);
      }
    } finally {
      setLoading(false);
    }
  };

  const closeGooglePhoneModal = () => {
    if (loading) return;
    setNeedGooglePhone(false);
    setPendingGoogleToken("");
    setGooglePhone("");
    setPendingGoogleRole(GOOGLE_PHONE_ROLE.CUSTOMER);
    setError("");
  };

  if (authLoading) {
    return (
      <main className="page">
        <div className="auth-shell">
          <section className="card">
            <h1>Đang kiểm tra phiên đăng nhập...</h1>
            <p className="subtitle">MyGarage đang đồng bộ trạng thái tài khoản của bạn.</p>
          </section>
        </div>
      </main>
    );
  }

  if (isAuthenticated && user) {
    return <Navigate to={resolvePostAuthDestination(user, requestedPath)} replace />;
  }

  return (
    <main className="page">
      {flashMessage ? <div className="auth-toast-success">{flashMessage}</div> : null}
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
                  đăng ký ngay
                </span>
              </>
            ) : (
              <>
                Bạn đã có tài khoản?
                <span className="switcher-link" onClick={() => { setMode("login"); setError(""); }}>
                  đăng nhập ngay
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
            <h3>{isStaffPhoneRequest ? "Cập nhật số điện thoại nhân viên" : "Liên kết số điện thoại"}</h3>
            <p>
              {isStaffPhoneRequest
                ? "Tài khoản nhân viên của bạn chưa có số điện thoại. Vui lòng nhập để hoàn tất đăng nhập Google."
                : "Chào mừng! Vui lòng nhập số điện thoại để chúng tôi tìm lịch sử sửa xe của bạn."}
            </p>
            <form onSubmit={handleGooglePhoneConfirm} className="form">
              <label htmlFor="google-phone">
                {isStaffPhoneRequest ? "Số điện thoại nhân viên" : "Số điện thoại"}
              </label>
              <input
                id="google-phone"
                type="tel"
                value={googlePhone}
                onChange={(e) => setGooglePhone(e.target.value)}
                placeholder="0900000000"
                pattern="^(0[0-9]{9}|\+84[0-9]{9})$"
                title="Số điện thoại hợp lệ: 0xxxxxxxxx hoặc +84xxxxxxxxx"
                required
                autoFocus
              />
              <div className="phone-modal-actions">
                <button type="button" className="btn-cancel" onClick={closeGooglePhoneModal} disabled={loading}>
                  Hủy
                </button>
                <button type="submit" disabled={loading || !googlePhone.trim()}>
                  {loading ? "Đang xử lý..." : isStaffPhoneRequest ? "Cập nhật" : "Xác nhận"}
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
