import { useEffect, useMemo, useState } from "react";
import { Link, useNavigate, useSearchParams } from "react-router-dom";
import * as authApi from "@features/auth/api/authApi";
import "./AuthRecoveryPage.css";

function ResetPasswordPage() {
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const token = searchParams.get("token") || "";
  const [newPassword, setNewPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");
  const [success, setSuccess] = useState("");

  const isValid = useMemo(() => {
    return token.trim().length > 0 && newPassword.length >= 6 && newPassword === confirmPassword;
  }, [token, newPassword, confirmPassword]);

  useEffect(() => {
    document.title = "Đặt lại mật khẩu | MyGarage";
  }, []);

  const handleSubmit = async (event) => {
    event.preventDefault();
    if (!token.trim()) {
      setError("Liên kết đặt lại mật khẩu không hợp lệ hoặc thiếu mã xác thực.");
      setSuccess("");
      return;
    }

    if (newPassword.length < 6) {
      setError("Mật khẩu mới phải có ít nhất 6 ký tự.");
      setSuccess("");
      return;
    }

    if (newPassword !== confirmPassword) {
      setError("Mật khẩu xác nhận chưa khớp.");
      setSuccess("");
      return;
    }

    setLoading(true);
    setError("");
    setSuccess("");

    const { ok, data } = await authApi.resetPassword({
      token,
      newPassword,
    });

    if (ok) {
      setSuccess(data?.Message || data?.message || "Mật khẩu đã được đặt lại thành công.");
      setNewPassword("");
      setConfirmPassword("");
      navigate("/login", {
        replace: true,
        state: { flashMessage: "Đổi mật khẩu thành công." },
      });
      return;
    } else {
      setError(data?.message || "Không thể đặt lại mật khẩu với liên kết này.");
    }

    setLoading(false);
  };

  return (
    <main className="page auth-recovery-page">
      <div className="auth-scene" aria-hidden="true">
        <div className="auth-floating-tag auth-floating-tag--left">
          <span>15 phút</span>
          <p>Liên kết đặt lại mật khẩu chỉ có hiệu lực trong khoảng thời gian ngắn.</p>
        </div>
        <div className="auth-floating-tag auth-floating-tag--right">
          <span>Chỉ lưu mã băm</span>
          <p>Token được lưu dưới dạng hash và không bị ghi log trong quá trình xử lý.</p>
        </div>
        <div className="auth-floating-tag auth-floating-tag--bottom">
          <span>Thu hồi</span>
          <p>Đặt lại thành công sẽ vô hiệu hóa token cũ và các phiên đang mở.</p>
        </div>
      </div>

      <div className="auth-shell auth-recovery-shell">
        <section className="card auth-recovery-card">
          <p className="auth-recovery-kicker">Đặt lại an toàn</p>
          <h1>Đặt lại mật khẩu</h1>
          <p className="subtitle">
            Nhập mật khẩu mới cho tài khoản của bạn. Liên kết này chỉ dùng được một lần và sẽ hết
            hạn sau một khoảng thời gian ngắn.
          </p>

          {!token ? (
            <div className="auth-recovery-note">
              Liên kết đặt lại mật khẩu không hợp lệ. Hãy quay lại bước quên mật khẩu để yêu cầu
              một liên kết mới.
            </div>
          ) : null}

          <form className="form auth-recovery-form" onSubmit={handleSubmit}>
            <label htmlFor="reset-password">Mật khẩu mới</label>
            <input
              id="reset-password"
              type="password"
              value={newPassword}
              onChange={(event) => setNewPassword(event.target.value)}
              placeholder="Tối thiểu 6 ký tự"
              autoComplete="new-password"
              required
            />

            <label htmlFor="reset-password-confirm">Xác nhận mật khẩu mới</label>
            <input
              id="reset-password-confirm"
              type="password"
              value={confirmPassword}
              onChange={(event) => setConfirmPassword(event.target.value)}
              placeholder="Nhập lại mật khẩu mới"
              autoComplete="new-password"
              required
            />

            {error ? <div className="error">{error}</div> : null}
            {success ? <div className="auth-recovery-success">{success}</div> : null}

            <button type="submit" disabled={loading || !isValid}>
              {loading ? "Đang cập nhật..." : "Cập nhật mật khẩu"}
            </button>
          </form>

          <div className="auth-recovery-actions">
            <Link to="/forgot-password" className="auth-recovery-link">
              Yêu cầu liên kết mới
            </Link>
            <Link to="/login" className="auth-recovery-link">
              Quay lại đăng nhập
            </Link>
          </div>
        </section>
      </div>
    </main>
  );
}

export default ResetPasswordPage;
