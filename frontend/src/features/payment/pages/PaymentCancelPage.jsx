import { Link } from "react-router-dom";

export default function PaymentCancelPage() {
  return (
    <div>
      <h1 style={{ fontSize: 24, fontWeight: 700, color: "#1a202c", marginBottom: 12 }}>
        Giao dich da huy
      </h1>
      <div className="booking-card" style={{ maxWidth: 720 }}>
        <p style={{ margin: 0, color: "#2d3748" }}>
          Ban da huy giao dich tren PAYOS. Neu can, ban co the tao lai link thanh toan.
        </p>
        <div style={{ marginTop: 14 }}>
          <Link to="/payment" className="btn-book" style={{ textDecoration: "none", display: "inline-block", marginTop: 0 }}>
            Thu lai thanh toan
          </Link>
        </div>
      </div>
    </div>
  );
}

