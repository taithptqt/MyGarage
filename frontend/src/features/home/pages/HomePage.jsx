import { Navigate, useNavigate } from "react-router-dom";
import { isAuthenticated, clearAuth, getUser } from "../../../app/authState";

function HomePage() {
  const navigate = useNavigate();

  // Auth guard: chua dang nhap → ve trang login
  if (!isAuthenticated()) {
    return <Navigate to="/" replace />;
  }

  const user = getUser();

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
            clearAuth();
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
