import { useMemo, useState } from "react";
import { Navigate, useNavigate } from "react-router-dom";
import { getApiBaseUrl, googleLogin, login, register } from "../api/authApi";
import AuthResult from "../components/AuthResult";
import AuthTabs from "../components/AuthTabs";
import GoogleLoginButton from "../components/GoogleLoginButton";
import LoginForm from "../login/LoginForm";
import { isLoginFormValid } from "../login/loginValidation";
import RegisterForm from "../register/RegisterForm";
import { isRegisterFormValid } from "../register/registerValidation";

function AuthPage() {
  const navigate = useNavigate();
  const token = localStorage.getItem("mygarage_token");
  const [mode, setMode] = useState("login");
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

  const handleLogin = async (event) => {
    event.preventDefault();
    setError("");
    setResult(null);

    if (!isLoginValid) {
      setError("Thong tin dang nhap chua hop le.");
      return;
    }

    setLoading(true);
    try {
      const { ok, data } = await login({
        email: loginForm.email.trim(),
        password: loginForm.password
      });

      if (!ok) {
        setError(data?.message || "Dang nhap that bai.");
        return;
      }

      localStorage.setItem("mygarage_token", data.token);
      localStorage.setItem("mygarage_user", JSON.stringify(data));
      setResult(data);
      navigate("/home", { replace: true });
    } catch (apiError) {
      setError(`Khong ket noi duoc server. ${apiError?.message || ""}`.trim());
    } finally {
      setLoading(false);
    }
  };

  const handleRegister = async (event) => {
    event.preventDefault();
    setError("");
    setResult(null);

    if (!isRegisterValid) {
      setError("Thong tin dang ky chua hop le.");
      return;
    }

    setLoading(true);
    try {
      const { ok, data } = await register({
        fullName: registerForm.fullName.trim(),
        phone: registerForm.phone.trim(),
        email: registerForm.email.trim(),
        password: registerForm.password
      });

      if (!ok) {
        setError(data?.message || "Dang ky that bai.");
        return;
      }

      localStorage.setItem("mygarage_token", data.token);
      localStorage.setItem("mygarage_user", JSON.stringify(data));
      setResult(data);
      navigate("/home", { replace: true });
    } catch (apiError) {
      setError(`Khong ket noi duoc server. ${apiError?.message || ""}`.trim());
    } finally {
      setLoading(false);
    }
  };

  const handleLogout = () => {
    localStorage.removeItem("mygarage_token");
    localStorage.removeItem("mygarage_user");
    setResult(null);
  };

  const handleGoogleLogin = async (idToken) => {
    setError("");
    setResult(null);
    setLoading(true);

    try {
      const { ok, data } = await googleLogin(idToken);
      if (!ok) {
        setError(data?.message || "Dang nhap Google that bai.");
        return;
      }

      localStorage.setItem("mygarage_token", data.token);
      localStorage.setItem("mygarage_user", JSON.stringify(data));
      setResult(data);
      navigate("/home", { replace: true });
    } catch (apiError) {
      console.error("Google login error:", apiError);
      setError(
        `Dang nhap Google that bai. ${apiError?.message || ""} Kiem tra backend dang chay va thu lai.`.trim()
      );
    } finally {
      setLoading(false);
    }
  };

  if (token) {
    return <Navigate to="/home" replace />;
  }

  return (
    <main className="page">
      <section className="card">
        <h1>MyGarage Auth</h1>
        <p className="subtitle">He thong quan ly nhan su va chuoi dich vu gara</p>

        <AuthTabs
          mode={mode}
          onModeChange={(nextMode) => {
            setMode(nextMode);
            setError("");
          }}
        />

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

        <div className="divider">
          <span>Hoac tiep tuc voi</span>
        </div>
        <GoogleLoginButton
          onCredential={handleGoogleLogin}
          onError={(message) => setError(message)}
        />

        <AuthResult result={result} onLogout={handleLogout} />

        <p className="api-hint">
          API: {getApiBaseUrl()}/api/Auth/{mode === "login" ? "login" : "register"}
        </p>
      </section>
    </main>
  );
}

export default AuthPage;
