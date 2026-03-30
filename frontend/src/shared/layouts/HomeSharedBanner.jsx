import { useNavigate } from "react-router-dom";
import { useAuth } from "../../context/AuthContext";

function HomeSharedBanner({ overlay = false }) {
  const navigate = useNavigate();
  const { user } = useAuth();

  const roleCode = String(user?.roleCode || user?.RoleCode || "").toUpperCase();
  const canAccessWorkshop = ["RECEPTIONIST", "TECH", "MANAGER", "QUALITY_CONTROL", "FOREMAN", "ADMIN"].includes(roleCode);

  return (
    <div className={`home-shared-banner ${overlay ? "overlay" : ""}`}>
      <div className="home-banner-left">
        <button type="button" className="home-logo" onClick={() => navigate("/home")}>
          MyGarage
        </button>
        <div className="home-banner-links">
          <a href="#dich-vu-gia">Dịch vụ</a>
          <a href="#quy-trinh-xuong">Xưởng</a>
          <a href="#doi-tac">Đối tác</a>
          <a href="#lien-he">Liên hệ</a>
        </div>
      </div>

      <div className="home-banner-right">
        <button type="button" className="banner-btn" onClick={() => navigate("/booking")}>
          Đặt lịch
        </button>
        {canAccessWorkshop && (
          <button type="button" className="banner-btn ghost" onClick={() => navigate("/workshop") }>
            Vào xưởng
          </button>
        )}
        <span className="home-user-pill">{user?.fullName || "Khách"}</span>
      </div>
    </div>
  );
}

export default HomeSharedBanner;
