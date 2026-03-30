function HomeFooter() {
  return (
    <footer className="home-footer">
      <div className="footer-columns">
        <div>
          <h3>MY GARAGE</h3>
          <p>Điểm đến hàng đầu cho đam mê ô tô và văn hóa sưu tầm xe.</p>
          <div className="footer-icons">
            <span>↗</span>
            <span>👍</span>
            <span>✉</span>
          </div>
        </div>
        <div>
          <h4>NHẬN TIN MỚI</h4>
          <div className="footer-input">
            <input type="email" placeholder="EMAIL CỦA BẠN" />
            <button type="button">→</button>
          </div>
          <h4 className="hours-title">GIỜ MỞ CỬA</h4>
          <p>THỨ HAI - THỨ BẢY: 08:00 - 20:00</p>
          <p>CHỦ NHẬT: NGHỈ</p>
        </div>
      </div>
      <div className="footer-bottom">
        <span>© MY GARAGE. BẢO LƯU MỌI QUYỀN.</span>
        <div>
          <a href="#">CHÍNH SÁCH BẢO MẬT</a>
          <a href="#">ĐIỀU KHOẢN &amp; ĐIỀU KIỆN</a>
          <a href="#">COOKIE</a>
        </div>
      </div>
    </footer>
  );
}

export default HomeFooter;

