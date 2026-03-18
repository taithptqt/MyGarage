import { useMemo } from "react";
import { Navigate, useNavigate } from "react-router-dom";

function HomePage() {
  const navigate = useNavigate();
  const token = localStorage.getItem("mygarage_token");
  const user = useMemo(() => {
    const raw = localStorage.getItem("mygarage_user");
    if (!raw) return null;
    try {
      return JSON.parse(raw);
    } catch {
      return null;
    }
  }, []);

  if (!token) {
    return <Navigate to="/" replace />;
  }

  return (
    <main className="page">
      <section className="card">
        <h1>Home</h1>
        <p className="subtitle">Ban da dang nhap thanh cong vao MyGarage.</p>

        <div className="success">
          <p>Xin chao, {user?.fullName || "User"}.</p>
          <p>Email: {user?.email || "-"}</p>
          <p>Role: {user?.role || "-"}</p>
        </div>

        <button
          type="button"
          onClick={() => {
            localStorage.removeItem("mygarage_token");
            localStorage.removeItem("mygarage_user");
            navigate("/", { replace: true });
          }}
        >
          Dang xuat
        </button>
      </section>
    </main>
  );
}

export default HomePage;
