import { Link, useNavigate } from "react-router-dom";
import { useAuth } from "../../../context/AuthContext";

function HomePage() {
  const navigate = useNavigate();
  const { user, logout } = useAuth();

  return (
    <main className="page" style={{ padding: 40, fontFamily: "sans-serif", textAlign: "center", minHeight: "80vh", display: "flex", alignItems: "center", justifyContent: "center" }}>
      <section className="card" style={{ background: "white", padding: 40, borderRadius: 16, boxShadow: "0 4px 20px rgba(0,0,0,0.08)", maxWidth: 500, width: "100%" }}>
        <h1 style={{ color: "#422afb", marginBottom: 16, fontSize: 32 }}>MyGarage</h1>
        <p className="subtitle" style={{ fontSize: 18, color: "#4a5568", marginBottom: 24 }}>
          Bạn đã đăng nhập thành công vào MyGarage.
        </p>

        <div className="success" style={{ marginBottom: 32, padding: 20, background: "#f8faff", borderRadius: 12 }}>
          <p style={{ margin: "4px 0" }}>Xin chào, <strong style={{ color: "#422afb" }}>{user?.fullName || "Khách"}</strong></p>
          <p style={{ margin: "4px 0", fontSize: 13, color: "#718096" }}>Email: {user?.email || "-"}</p>
          <p style={{ margin: "4px 0", fontSize: 13, color: "#718096" }}>Vai trò: {user?.roleCode || user?.role || "-"}</p>
        </div>

        <div style={{ display: "flex", flexDirection: "column", gap: 12 }}>
          <Link 
            to="/booking" 
            style={{ 
              padding: "14px 24px", 
              background: "#422afb", 
              color: "white", 
              textDecoration: "none", 
              borderRadius: 10,
              fontWeight: 600,
              transition: "transform 0.1s"
            }}
          >
            Đặt lịch bảo dưỡng ngay
          </Link>
          <Link 
            to="/my-appointments" 
            style={{ 
              padding: "14px 24px", 
              border: "2px solid #422afb", 
              color: "#422afb", 
              textDecoration: "none", 
              borderRadius: 10,
              fontWeight: 600
            }}
          >
            Lịch hẹn của tôi
          </Link>
        </div>

        <button
          type="button"
          onClick={() => {
            logout();
            navigate("/login", { replace: true });
          }}
          style={{
            marginTop: 32,
            padding: "8px 16px",
            background: "transparent",
            border: "none",
            color: "#718096",
            cursor: "pointer",
            fontSize: 14,
            textDecoration: "underline"
          }}
        >
          Đăng xuất
        </button>
      </section>
    </main>
  );
}

export default HomePage;
