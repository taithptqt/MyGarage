import { useEffect, useMemo, useState } from "react";
import { Navigate, useLocation, useNavigate } from "react-router-dom";
import { getApiBaseUrl } from "@features/auth/api/authApi";
import AuthResult from "@features/auth/components/AuthResult";
import GoogleLoginButton from "@features/auth/components/GoogleLoginButton";
import LoginForm from "@features/auth/login/LoginForm";
import { isLoginFormValid } from "@features/auth/login/loginValidation";
import RegisterForm from "@features/auth/register/RegisterForm";
import { isRegisterFormValid } from "@features/auth/register/registerValidation";
import { clearAuthStorage, readAuthToken } from "@features/auth/utils/authStorage";
import { useAuth } from "@shared/core/context/AuthContext";
import { isValidVietnamesePhone } from "@shared/utils/contactValidation";
import "./AuthPage.css";

const AUTH_HIGHLIGHTS = [
  {
    title: "Ð?t l?ch nhanh chóng",
    description: "Ch? d?ng ch?n d?ch v?, chi nh?nh v? khung gi? ph? h?p ch? trong v?i ph?t.",
  },
  {
    title: "Theo dõi minh b?ch",
    description: "C?p nh?t ti?n d? x? lý xe theo t?ng công do?n ngay trên h? th?ng.",
  },
  {
    title: "D? li?u t?p trung",
    description: "Luu tr? l?ch s? s?a ch?a v? thông tin xe d? tu v?n ch?nh x?c hon m?i l?n quay l?i.",
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
    document.title = mode === "login" ? "Ðang nh?p | MyGarage" : "Ðang ký | MyGarage";
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
    if (!authLoading && !isAuthenticated && readAuthToken()) {
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
      setError("Thông tin dang nh?p chua h?p l?.");
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
      setError(apiError?.message || "Ðang nh?p th?t b?i.");
    } finally {
      setLoading(false);
    }
  };

  const handleRegister = async (event) => {
    if (event) event.preventDefault();
    setError("");
    setResult(null);

    if (!isRegisterValid) {
      setError("Thông tin dang ký chua h?p l?.");
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
      setError(apiError?.message || "Ðang ký th?t b?i.");
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
      const msg = apiError?.message || "Ðang nh?p Google th?t b?i.";
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
      setError("S? di?n tho?i không dúng d?nh d?ng (0xxxxxxxxx ho?c +84xxxxxxxxx).");
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
      const msg = apiError?.message || "Liên k?t s? di?n tho?i th?t b?i.";
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
            <h1>Ðang ki?m tra phiên dang nh?p...</h1>
            <p className="subtitle">MyGarage dang d?ng b? tr?ng thái t?i kho?n c?a b?n.</p>
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
          <p>Lu?t xe du?c ph?c v? m?i nam</p>
        </div>
        <div className="auth-floating-tag auth-floating-tag--right">
          <span>4.9 / 5</span>
          <p>Ðánh gi? trung b?nh t? khách hàng</p>
        </div>
        <div className="auth-floating-tag auth-floating-tag--bottom">
          <span>30+</span>
          <p>K? thu?t viên nhi?u kinh nghi?m</p>
        </div>
      </div>

      <div className="auth-shell">
        <section className="auth-showcase">
          <div className="auth-showcase-content">
            <p className="auth-showcase-kicker">MYGARAGE PREMIUM CARE</p>
            <h2>
              N?n t?ng d?t l?ch và theo dõi d?ch v? xe
              theo chu?n gara chuyên nghi?p
            </h2>
            <p>
              Ðang nh?p d? qu?n lý l?ch h?n, l?ch s? s?a ch?a và nh?n
              g?i ý d?ch v? phù h?p theo tình tr?ng xe c?a b?n.
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
          <h1>{mode === "login" ? "Ðang nh?p" : "Ðang ký"}</h1>
          <p className="subtitle">M?t tài kho?n s? d?ng t?t c? d?ch v? c?a MyGarage</p>

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
              M?o: B?t v? trí d? h? th?ng uu ti?n g?i ? chi nh?nh g?n nh?t.
            </div>
          )}

          <div className="footer-switcher">
            {mode === "login" ? (
              <>
                B?n chua có tài kho?n,
                <span className="switcher-link" onClick={() => { setMode("register"); setError(""); }}>
                  Ðang ký ngay
                </span>
              </>
            ) : (
              <>
                B?n dã có tài kho?n?
                <span className="switcher-link" onClick={() => { setMode("login"); setError(""); }}>
                  Ðang nh?p ngay
                </span>
              </>
            )}
          </div>

          <div className="divider">
            <span>Ho?c dang nh?p v?i</span>
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
            <h3>{isStaffPhoneRequest ? "C?p nh?t s? di?n tho?i nhân viên" : "Li?n k?t s? di?n tho?i"}</h3>
            <p>
              {isStaffPhoneRequest
                ? "T?i kho?n nhân viên c?a b?n chua c? s? di?n tho?i. Vui l?ng nh?p d? hoàn t?t dang nh?p Google."
                : "Chào m?ng! Vui lòng nh?p s? di?n tho?i d? chúng tôi tìm l?ch s? s?a xe c?a b?n."}
            </p>
            <form onSubmit={handleGooglePhoneConfirm} className="form">
              <label htmlFor="google-phone">
                {isStaffPhoneRequest ? "S? di?n tho?i nhân viên" : "S? di?n tho?i"}
              </label>
              <input
                id="google-phone"
                type="tel"
                value={googlePhone}
                onChange={(e) => setGooglePhone(e.target.value)}
                placeholder="0900000000"
                pattern="^(0[0-9]{9}|\+84[0-9]{9})$"
                title="S? di?n tho?i h?p l?: 0xxxxxxxxx ho?c +84xxxxxxxxx"
                required
                autoFocus
              />
              <div className="phone-modal-actions">
                <button type="button" className="btn-cancel" onClick={closeGooglePhoneModal} disabled={loading}>
                  H?y
                </button>
                <button type="submit" disabled={loading || !googlePhone.trim()}>
                  {loading ? "Ðang x? l?..." : isStaffPhoneRequest ? "C?p nh?t" : "Xác nh?n"}
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


