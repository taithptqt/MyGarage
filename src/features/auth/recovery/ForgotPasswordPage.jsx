import { useEffect, useMemo, useState } from "react";
import { Link } from "react-router-dom";
import * as authApi from "@features/auth/api/authApi";
import "./AuthRecoveryPage.css";

function ForgotPasswordPage() {
  const [email, setEmail] = useState("");
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");
  const [success, setSuccess] = useState("");

  const isValid = useMemo(() => /\S+@\S+\.\S+/.test(email.trim()), [email]);

  useEffect(() => {
    document.title = "Quên mật khẩu | MyGarage";
  }, []);

  const handleSubmit = async (event) => {
    event.preventDefault();
    if (!isValid) {
      setError("Vui lòng nhập email hợp lệ.");
      setSuccess("");
      return;
    }

    setLoading(true);
    setError("");
    setSuccess("");

    const { ok, data } = await authApi.forgotPassword({ email: email.trim() });
    if (ok) {
      setSuccess(data?.Message || data?.message || "Đã gửi liên kết thành công");
    } else {
      setError(data?.message || "Không thể xử lý yêu cầu lúc này.");
    }

    setLoading(false);
  };

  return (
    <main className="page auth-recovery-page">
      <div className="auth-scene" aria-hidden="true">
        <div className="auth-floating-tag auth-floating-tag--left">
          <span>Bảo mật</span>
          <p>Mỗi liên kết đặt lại mật khẩu chỉ có hiệu lực trong thời gian ngắn.</p>
        </div>
        <div className="auth-floating-tag auth-floating-tag--right">
          <span>1 lần</span>
          <p>Token chỉ được dùng một lần và sẽ bị vô hiệu hóa sau khi đặt lại.</p>
        </div>
        <div className="auth-floating-tag auth-floating-tag--bottom">
          <span>An toàn</span>
          <p>Thông tin trả về luôn được giữ trung tính để bảo vệ tài khoản.</p>
        </div>
      </div>

      <div className="auth-shell auth-recovery-shell">
        <section className="card auth-recovery-card auth-recovery-card--forgot">
          <h1>Quên mật khẩu</h1>

          <form className="form auth-recovery-form" onSubmit={handleSubmit}>
            <label htmlFor="forgot-email">Email</label>
            <input
              id="forgot-email"
              type="email"
              value={email}
              onChange={(event) => setEmail(event.target.value)}
              placeholder="user@example.com"
              autoComplete="email"
              required
            />

            {error ? <div className="error">{error}</div> : null}
            {success ? <div className="auth-recovery-success">{success}</div> : null}

            <button type="submit" disabled={loading || !isValid}>
              {loading ? "Đang gửi..." : "Gửi liên kết đặt lại"}
            </button>
          </form>

          <div className="auth-recovery-actions auth-recovery-actions--compact">
            <Link to="/login" className="auth-recovery-link">
            Quay lại
            </Link>
          </div>
        </section>
      </div>
    </main>
  );
}

export default ForgotPasswordPage;
