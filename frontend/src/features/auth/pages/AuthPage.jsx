import { useEffect, useMemo, useState } from "react";
import { Navigate, useNavigate } from "react-router-dom";
import { getApiBaseUrl } from "../api/authApi";
import AuthResult from "../components/AuthResult";
import GoogleLoginButton from "../components/GoogleLoginButton";
import LoginForm from "../login/LoginForm";
import { isLoginFormValid } from "../login/loginValidation";
import RegisterForm from "../register/RegisterForm";
import { isRegisterFormValid } from "../register/registerValidation";
import { useAuth } from "../../../context/AuthContext";
import MyGarageLogo from "../components/MyGarageLogo";
import "./AuthPage.css";

function AuthPage() {
  const navigate = useNavigate();
  const { login: contextLogin, register: contextRegister, googleLogin: contextGoogleLogin, logout: contextLogout } = useAuth();
  const token = localStorage.getItem("mygarage_token");
  const [mode, setMode] = useState("login");

  useEffect(() => {
    document.title = mode === "login" ? "Dang nhap | MyGarage" : "Dang ky | MyGarage";
  }, [mode]);

  const [loginForm, setLoginForm] = useState({ email: "", password: "" });
  const [registerForm, setRegisterForm] = useState({
    fullName: "",
    phone: "",
    city: "",
    email: "",
    password: ""
  });
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");
  const [result, setResult] = useState(null);

  const isLoginValid = useMemo(() => isLoginFormValid(loginForm), [loginForm]);
  const isRegisterValid = useMemo(() => isRegisterFormValid(registerForm), [registerForm]);

  const getLocationPayload = async () => {
    if (!navigator?.geolocation) return {};
    return new Promise((resolve) => {
      navigator.geolocation.getCurrentPosition(
        (position) =>
          resolve({
            lat: position.coords.latitude,
            lng: position.coords.longitude
          }),
        () => resolve({}),
        { enableHighAccuracy: true, timeout: 3500, maximumAge: 60000 }
      );
    });
  };

  const updateLogin = (key, value) => {
    setLoginForm((prev) => ({ ...prev, [key]: value }));
  };

  const updateRegister = (key, value) => {
    setRegisterForm((prev) => ({ ...prev, [key]: value }));
  };

  const redirectAfterAuth = (data) => {
    const destination = data?.role === "Staff" ? "/admin" : "/home";
    navigate(destination, { replace: true });
  };

  const handleLogin = async (event) => {
    if (event) event.preventDefault();
    setError("");
    setResult(null);

    if (!isLoginValid) {
      setError("Thong tin dang nhap chua hop le.");
      return;
    }

    setLoading(true);
    try {
      const location = await getLocationPayload();
      const data = await contextLogin({
        email: loginForm.email.trim(),
        password: loginForm.password,
        ...location
      });

      setResult(data);
      redirectAfterAuth(data);
    } catch (apiError) {
      setError(apiError?.message || "Dang nhap that bai.");
    } finally {
      setLoading(false);
    }
  };

  const handleRegister = async (event) => {
    if (event) event.preventDefault();
    setError("");
    setResult(null);

    if (!isRegisterValid) {
      setError("Thong tin dang ky chua hop le.");
      return;
    }

    setLoading(true);
    try {
      const location = await getLocationPayload();
      const data = await contextRegister({
        fullName: registerForm.fullName.trim(),
        phone: registerForm.phone.trim(),
        city: registerForm.city.trim(),
        email: registerForm.email.trim(),
        password: registerForm.password,
        ...location
      });

      setResult(data);
      redirectAfterAuth(data);
    } catch (apiError) {
      setError(apiError?.message || "Dang ky that bai.");
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
      const data = await contextGoogleLogin({
        idToken,
        ...location
      });
      setResult(data);
      redirectAfterAuth(data);
    } catch (apiError) {
      console.error("Google login error:", apiError);
      setError(apiError?.message || "Dang nhap Google that bai.");
    } finally {
      setLoading(false);
    }
  };

  if (token) {
    const savedUser = JSON.parse(localStorage.getItem("mygarage_user") || "{}");
    const dest = savedUser?.role === "Staff" ? "/admin" : "/home";
    return <Navigate to={dest} replace />;
  }

  return (
    <main className="page">
      <section className="card">
        <MyGarageLogo />
        
        <h1>{mode === "login" ? "Dang nhap" : "Tao tai khoan"}</h1>
        <p className="subtitle">
          Mot tai khoan su dung tat ca dich vu cua MyGarage
        </p>

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

        <div className="footer-switcher">
          {mode === "login" ? (
            <>
              Ban chua co tai khoan, 
              <span 
                className="switcher-link" 
                onClick={() => { setMode("register"); setError(""); }}
              >
                Dang ky ngay
              </span>
            </>
          ) : (
            <>
              Ban da co tai khoan?
              <span 
                className="switcher-link" 
                onClick={() => { setMode("login"); setError(""); }}
              >
                Dang nhap ngay
              </span>
            </>
          )}
        </div>

        <div className="divider">
          <span>Hoac dang nhap voi</span>
        </div>
        
        <div className="google-login-btn-container">
          <GoogleLoginButton
            onCredential={handleGoogleLogin}
            onError={(message) => setError(message)}
          />
        </div>

        <AuthResult result={result} onLogout={handleLogout} />

      </section>
      
      <p className="api-hint">
        API: {getApiBaseUrl()}
      </p>
    </main>
  );
}

export default AuthPage;
