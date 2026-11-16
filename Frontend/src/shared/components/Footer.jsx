import "./Footer.css";

import { Link, useNavigate } from "react-router-dom";

function Footer() {
  const navigate = useNavigate();
  const contactForUpdates = event => {
    event.preventDefault();
    navigate("/contact", { state: { email: String(new FormData(event.currentTarget).get("email") || ""), topic: "Newsletter" } });
  };
  return (
    <footer className="mg-footer">
      <div className="mg-footer-columns">
        <div>
          <h3>MY GARAGE</h3>
          <p>Điểm đến hàng đầu cho đam mê ô tô và văn hóa sưu tầm xe.</p>
        </div>
        <nav className="brand-footer-nav" aria-label="MY GARAGE">
          <h4>MY GARAGE</h4>
          <Link to="/events">Hội họp &amp; sự kiện</Link>
          <Link to="/partners">Đối tác</Link>
          <Link to="/contact">Liên hệ</Link>
          <Link to="/booking">Đặt lịch</Link>
        </nav>
        <div>
          <h4>NHẬN TIN MỚI</h4>
          <form className="mg-footer-input" onSubmit={contactForUpdates}>
            <input type="email" name="email" autoComplete="email" required aria-label="EMAIL CỦA BẠN" placeholder="EMAIL CỦA BẠN" />
            <button type="submit" aria-label="Liên hệ nhận tin">→</button>
          </form>
          <h4 className="mg-footer-hours-title">GIỜ MỞ CỬA</h4>
          <p>THỨ HAI - THỨ BẢY: 08:00 - 20:00</p>
          <p>CHỦ NHẬT: NGHỈ</p>
        </div>
      </div>
      <div className="mg-footer-bottom">
        <span>© MY GARAGE. BẢO LƯU MỌI QUYỀN.</span>
        <div>
          <a href="#">CHÍNH SÁCH BẢO MẬT</a>
          <a href="#">ĐIỀU KHOẢN & ĐIỀU KIỆN</a>
          <a href="#">COOKIE</a>
        </div>
      </div>
    </footer>
  );
}

export default Footer;



