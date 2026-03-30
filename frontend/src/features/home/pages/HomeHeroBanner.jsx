import { useEffect, useMemo, useRef, useState } from "react";
import { useLocation, useNavigate } from "react-router-dom";
import { useAuth } from "../../../context/AuthContext";
import ProfileDialog from "../../profile/components/ProfileDialog";

function HomeHeroBanner() {
  const { isAuthenticated, user, logout } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const [menuOpen, setMenuOpen] = useState(false);
  const [showProfileDialog, setShowProfileDialog] = useState(false);
  const menuRef = useRef(null);
  const displayName = String(user?.fullName || user?.name || "Bạn").trim();
  const avatarUrl = String(user?.avatarUrl || user?.AvatarUrl || "").trim();
  const avatarInitial = displayName ? displayName.charAt(0).toUpperCase() : "B";
  const roleCode = String(user?.roleCode || user?.RoleCode || "").toUpperCase();
  const isStaffOrAdmin = ["ADMIN", "RECEPTIONIST", "TECH", "MANAGER", "QUALITY_CONTROL", "FOREMAN"].includes(roleCode);

  const dashboardPath = useMemo(() => {
    switch (roleCode) {
      case "ADMIN":
        return "/admin/default";
      case "RECEPTIONIST":
        return "/workshop/reception";
      case "TECH":
        return "/workshop/technician";
      case "MANAGER":
        return "/workshop/manager";
      case "QUALITY_CONTROL":
        return "/workshop/qc";
      case "FOREMAN":
        return "/workshop/foreman";
      default:
        return "";
    }
  }, [roleCode]);

  useEffect(() => {
    setMenuOpen(false);
  }, [location.pathname]);

  useEffect(() => {
    if (!menuOpen) return undefined;
    const onDocClick = (event) => {
      if (menuRef.current && !menuRef.current.contains(event.target)) {
        setMenuOpen(false);
      }
    };
    document.addEventListener("mousedown", onDocClick);
    return () => document.removeEventListener("mousedown", onDocClick);
  }, [menuOpen]);

  const handleNavigate = (path) => {
    setMenuOpen(false);
    navigate(path);
  };

  const handleLogout = () => {
    setMenuOpen(false);
    setShowProfileDialog(false);
    logout();
  };

  return (
    <nav className="hero-nav-wrap hero-nav-banner">
      <div className="main-nav">
        <span className="utility-call banner-hotline">HOTLINE +45 69 133 911</span>
        <div className="main-nav-side">
          <a href="/home" onClick={(event) => { event.preventDefault(); navigate("/home"); }}>TRANG CHỦ</a>
          <a href="/booking" onClick={(event) => { event.preventDefault(); navigate("/booking"); }}>ĐẶT LỊCH</a>
          <a href="/my-appointments" onClick={(event) => { event.preventDefault(); navigate("/my-appointments"); }}>LỊCH HẸN CỦA TÔI</a>
        </div>
        <div className="logo-center" role="img" aria-label="MY GARAGE">
          <span>MY</span>
          <small>GARAGE</small>
        </div>
        <div className="main-nav-side right">
          <a href="#">HỘP & SỰ KIỆN</a>
          <a href="#">ĐỐI TÁC</a>
          <a href="#">TIN TỨC</a>
          <a href="#">LIÊN HỆ</a>
        </div>
        {isAuthenticated ? (
          <div className="banner-user" ref={menuRef}>
            <button
              type="button"
              className="banner-user-trigger"
              onClick={() => setMenuOpen((prev) => !prev)}
            >
              <span className="banner-user-name">Xin chào, {displayName}</span>
              <span className="banner-user-avatar" aria-hidden="true">
                {avatarUrl ? <img src={avatarUrl} alt="" /> : avatarInitial}
              </span>
            </button>
            {menuOpen && (
              <div className="banner-user-menu">
                <div className="banner-user-menu-head">Xin chào, {displayName}</div>
                <button
                  type="button"
                  className="banner-user-menu-item"
                  onClick={() => {
                    setMenuOpen(false);
                    setShowProfileDialog(true);
                  }}
                >
                  Hồ sơ của tôi
                </button>
                {isStaffOrAdmin && dashboardPath ? (
                  <button
                    type="button"
                    className="banner-user-menu-item"
                    onClick={() => handleNavigate(dashboardPath)}
                  >
                    Dashboard
                  </button>
                ) : null}
                <button
                  type="button"
                  className="banner-user-menu-item logout"
                  onClick={handleLogout}
                >
                  Đăng xuất
                </button>
              </div>
            )}
          </div>
        ) : (
          <a className="banner-login-btn" href="/login">ĐĂNG NHẬP</a>
        )}
      </div>
      {showProfileDialog ? <ProfileDialog onClose={() => setShowProfileDialog(false)} /> : null}
    </nav>
  );
}

export default HomeHeroBanner;
