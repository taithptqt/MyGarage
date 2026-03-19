import { useEffect } from "react";
import { useAuth } from "../../../context/AuthContext";

function HomePage() {
  const { user, logout } = useAuth();

  useEffect(() => {
    document.title = "Trang chủ | MyGarage";
  }, []);

  return (
    <main className="page" style={{ 
      display: 'flex', 
      alignItems: 'center', 
      justifyContent: 'center', 
      minHeight: '80vh',
      background: 'linear-gradient(135deg, #f5f7fa 0%, #c3cfe2 100%)' 
    }}>
      <section className="card" style={{ 
        textAlign: 'center', 
        padding: '40px', 
        borderRadius: '16px',
        boxShadow: '0 10px 25px rgba(0,0,0,0.1)',
        backgroundColor: '#fff',
        maxWidth: '400px',
        width: '100%'
      }}>
        <h1 style={{ color: '#2d3748', marginBottom: '10px' }}>Chào mừng trở lại!</h1>
        <p className="subtitle" style={{ color: '#718096', marginBottom: '30px' }}>
          Bạn đã đăng nhập thành công vào MyGarage.
        </p>

        <div className="user-info" style={{ 
          backgroundColor: '#edf2f7', 
          padding: '20px', 
          borderRadius: '12px', 
          marginBottom: '30px',
          textAlign: 'left'
        }}>
          <p style={{ margin: '5px 0' }}><strong>Họ tên:</strong> {user?.fullName || "Khách hàng"}</p>
          <p style={{ margin: '5px 0' }}><strong>Email:</strong> {user?.email || "-"}</p>
          <p style={{ margin: '5px 0' }}><strong>Vai trò:</strong> {user?.role || "Customer"}</p>
        </div>

        <button
          type="button"
          className="btn-primary"
          onClick={logout}
          style={{ 
            width: '100%', 
            padding: '12px', 
            borderRadius: '8px', 
            fontWeight: 'bold',
            cursor: 'pointer'
          }}
        >
          Đăng xuất
        </button>
      </section>
    </main>
  );
}

export default HomePage;
