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
    document.title = mode === "login" ? "Đăng nhập | MyGarage" : "Đăng ký | MyGarage";
  }, [mode]);

  const [loginForm, setLoginForm] = useState({ email: "", password: "" });
  const [registerForm, setRegisterForm] = useState({
    fullName: "",
    phone: "",
    email: "",
    password: ""
  });
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");
  const [result, setResult] = useState(null);

  const isLoginValid = useMemo(() => isLoginFormValid(loginForm), [loginForm]);
  const isRegisterValid = useMemo(() => isRegisterFormValid(registerForm), [registerForm]);

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
      setError("Thông tin đăng nhập chưa hợp lệ.");
      return;
    }

    setLoading(true);
    try {
      const data = await contextLogin({
        email: loginForm.email.trim(),
        password: loginForm.password
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
      const data = await contextRegister({
        fullName: registerForm.fullName.trim(),
        phone: registerForm.phone.trim(),
        email: registerForm.email.trim(),
        password: registerForm.password
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
      const data = await contextGoogleLogin(idToken);
      setResult(data);
      redirectAfterAuth(data);
    } catch (apiError) {
      console.error("Google login error:", apiError);
      setError(apiError?.message || "Đăng nhập Google thất bại.");
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
        
        <h1>{mode === "login" ? "Đăng nhập" : "Tạo tài khoản"}</h1>
        <p className="subtitle">
          Một tài khoản sử dụng tất cả dịch vụ của MyGarage
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
              Bạn chưa có tài khoản, 
              <span 
                className="switcher-link" 
                onClick={() => { setMode("register"); setError(""); }}
              >
                Đăng ký ngay
              </span>
            </>
          ) : (
            <>
              Bạn đã có tài khoản?
              <span 
                className="switcher-link" 
                onClick={() => { setMode("login"); setError(""); }}
              >
                Đăng nhập ngay
              </span>
            </>
          )}
        </div>

        <div className="divider">
          <span>Hoặc đăng nhập với</span>
        </div>
        
        <div className="google-login-btn-container">
          <GoogleLoginButton
            mode={mode === "login" ? "signin" : "signup"}
            onCredential={handleGoogleLogin}
            onError={(message) => setError(message)}
          />
        </div>

        <AuthResult result={result} onLogout={handleLogout} />

      </section>
      
      <p className="api-hint">
        Build Version: 1.0.4 | API: {getApiBaseUrl()}
      </p>
    </main>
  );
}

export default AuthPage;
