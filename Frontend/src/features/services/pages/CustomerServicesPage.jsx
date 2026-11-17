import React from "react";
import { Link, useNavigate } from "react-router-dom";
import Header from "@shared/components/Header";
import Footer from "@shared/components/Footer";
import {
  Cpu,
  Disc,
  Sparkles,
  ShieldCheck,
  Calendar,
  CheckCircle2,
  ArrowRight,
  PhoneCall,
  Layers,
  Thermometer,
  Shield,
  Zap,
  Gauge,
  Sliders,
} from "lucide-react";
import "./CustomerServicesPage.css";

const TECH_PILLARS = [
  {
    id: "diagnostics",
    icon: Cpu,
    tag: "CHẨN ĐOÁN CHUYÊN HÃNG",
    title: "Trạm Chẩn Đoán Điện Tử & Lập Trình ECU",
    desc: "Trang bị đầy đủ thiết bị chuyên hãng OEM cho từng thương hiệu xe: Mercedes-Benz (Xentry Star), BMW (ISTA/D - ISTA/P), Porsche (PIWIS III), Audi/Volkswagen (ODIS) và máy quét toàn diện Autel Ultra.",
    highlights: [
      "Quét và phân tích mã lỗi chuyên sâu 120+ hộp ECU điều khiển",
      "Lập trình online, cập nhật phần mềm (software update) và mã hóa (coding)",
      "Đo sóng xung cảm biến oscilloscope, kiểm tra mạng truyền thông CAN/LIN",
      "Đồng bộ chìa khóa thông minh, hiệu chuẩn cảm biến hỗ trợ lái ADAS",
    ],
    badge: "OEM Diagnostics",
  },
  {
    id: "chassis-hunter",
    icon: Disc,
    tag: "CĂN CHỈNH 3D HUNTER",
    title: "Cân Chỉnh Góc Lái & Láng Đĩa Phanh 3D Laser",
    desc: "Sử dụng hệ thống cân chỉnh góc đặt bánh xe 3D Hunter HawkEye Elite (Hoa Kỳ) với 4 camera quang học độ phân giải cao. Khắc phục triệt để hiện tượng nhao lái, ăn mòn lốp không đều và lệch vô lăng.",
    highlights: [
      "Đo góc Camber, Caster, Toe-in và góc thoái lui trục với độ chuẩn xác 0.01 độ",
      "Máy ra vào lốp không chạm Hunter Revolution bảo vệ vành mâm siêu xe",
      "Cân bằng động bấm chì điện tử triệt tiêu rung giật tay lái ở tốc độ cao",
      "Máy láng đĩa phanh trực tiếp trên xe tái tạo bề mặt tiếp xúc phẳng tuyệt đối",
    ],
    badge: "Hunter USA Tech",
  },
  {
    id: "paint-booth",
    icon: Sparkles,
    tag: "ĐỒNG SƠN TIÊU CHUẨN ĐỨC",
    title: "Phòng Sơn Sấy Hấp & Pha Màu Vi Tính Hóa",
    desc: "Buồng sơn sấy hấp hồng ngoại áp suất dương với hệ thống lọc bụi 3 cấp độ, ngăn ngừa 100% bụi bẩn trong không khí bám vào bề mặt sơn. Sử dụng giàn pha màu vi tính Standox/Dupont chuẩn màu tuyệt đối.",
    highlights: [
      "Máy quang phổ quét và phân tích màu sơn gốc xe tự động đạt độ khớp màu 99.9%",
      "Sơn gốc nước cao cấp thân thiện môi trường, lớp bóng ceramic siêu kháng xước",
      "Đèn sấy hồng ngoại sóng ngắn giúp sấy khô nhanh từ trong ra ngoài",
      "Dàn kéo nắn khung gầm thủy lực đa năng phục hồi thân vỏ chuẩn kích thước ban đầu",
    ],
    badge: "Standox / Dupont",
  },
  {
    id: "storage-detailing",
    icon: Thermometer,
    tag: "LƯU KHO & BẢO QUẢN XE SANG",
    title: "Kho Bảo Quản & Chăm Sóc Xe Sang (Car Storage)",
    desc: "Khu vực lưu trữ xe chuyên biệt với hệ thống điều hòa kiểm soát nhiệt ẩm 24/7 (nhiệt độ 18°C - 22°C, độ ẩm < 55%). Bảo đảm xe luôn trong tình trạng vận hành hoàn hảo kể cả khi không sử dụng dài ngày.",
    highlights: [
      "Hệ thống sạc bù xung duy trì thông minh CTEK Thụy Điển bảo vệ bình ắc quy",
      "Quy trình kiểm tra tuần: nổ máy chạy rot-da, đảo lốp chống méo cao su",
      "Vệ sinh khoang máy bằng hơi nước nóng, phủ Ceramic 9H bảo vệ lớp sơn",
      "Bảo mật 24/7 với camera AI nhận diện khuôn mặt và hệ thống chữa cháy khí sạch",
    ],
    badge: "Climate Controlled",
  },
];

const CustomerServicesPage = () => {
  const navigate = useNavigate();

  return (
    <div className="customer-services-page">
      <Header overlay />

      {/* DISTINCT HERO: ASYMMETRIC ENGINEERING SPLIT */}
      <section className="services-tech-hero">
        <img
          className="services-tech-hero-img"
          src="https://images.unsplash.com/photo-1652852592938-15bf8c50ab6d?auto=format&fit=crop&w=2000&q=85"
          alt="Hạ tầng xưởng kỹ thuật tiêu chuẩn châu Âu và khoang nâng công nghệ cao MyGarage"
          loading="eager"
        />
        <div className="services-tech-hero-overlay" />
        <div className="services-tech-hero-fade" />
        <div className="services-tech-hero-container">
          {/* Left Column: Mission & Metrics */}
          <div className="tech-hero-copy">
            <h1 className="tech-hero-title">
              Năng Lực Kỹ Thuật & <span>Hạ Tầng Công Nghệ</span> MyGarage
            </h1>
            <p className="tech-hero-desc">
              MyGarage được xây dựng như một trung tâm kỹ thuật chuyên sâu cho các dòng xe châu Âu và xe sang.
              Chúng tôi đầu tư mạnh mẽ vào máy móc chẩn đoán chuyên hãng, phòng sơn hấp hiện đại và quy chuẩn phụ tùng chính ngạch.
            </p>

            <div className="tech-hero-stats">
              <div className="hero-stat-box">
                <span className="stat-number">3</span>
                <span className="stat-label">Đại xưởng Bắc - Trung - Nam</span>
              </div>
              <div className="hero-stat-box">
                <span className="stat-number">55+</span>
                <span className="stat-label">Khoang nâng chuẩn tải</span>
              </div>
              <div className="hero-stat-box">
                <span className="stat-number">100%</span>
                <span className="stat-label">Thiết bị chẩn đoán chuyên hãng</span>
              </div>
            </div>

            <div className="tech-hero-actions">
              <Link to="/storage/garage-3d" className="services-btn-secondary">Khám phá xe 3D</Link>
              <a href="#tech-pillars" className="services-btn-primary">
                Xem 4 Cụm Công Nghệ Xưởng
              </a>
              <button
                type="button"
                className="services-btn-secondary"
                onClick={() => navigate("/booking")}
              >
                <Calendar size={18} /> Đặt Lịch Xưởng
              </button>
            </div>
          </div>

          {/* Right Column: Interactive Engineering Showcase Card */}
          <div className="tech-hero-showcase">
            <div className="tech-showcase-glass-card">
              <div className="showcase-card-header">
                <div className="showcase-status-indicator">
                  <span className="status-live-pulse" />
                  <span>XƯỞNG ĐANG VẬN HÀNH</span>
                </div>
                <span className="showcase-tag">ISO 9001:2015</span>
              </div>

              <div className="showcase-card-content">
                <div className="showcase-metric-highlight">
                  <Gauge size={28} className="text-gold" />
                  <div>
                    <strong>Chẩn Đoán Chuẩn Tuyệt Đối</strong>
                    <p>Phần mềm hãng kết nối trực tiếp cổng OBD-II quét toàn diện</p>
                  </div>
                </div>

                <div className="showcase-metric-highlight">
                  <Sliders size={28} className="text-gold" />
                  <div>
                    <strong>Căn Chỉnh 3D Laser Hunter</strong>
                    <p>Đo đạc 4 bánh với độ chính xác đến từng phần trăm milimet</p>
                  </div>
                </div>

                <div className="showcase-metric-highlight">
                  <Thermometer size={28} className="text-gold" />
                  <div>
                    <strong>Kho Bảo Quản Xe Nhiệt Ẩm Ổn Định</strong>
                    <p>Duy trì 20°C liên tục, sạc xung ắc quy thông minh</p>
                  </div>
                </div>
              </div>

              <div className="showcase-card-footer">
                <div className="showcase-guarantee-pill">
                  <ShieldCheck size={16} /> Bảo Hành Phụ Tùng & Kỹ Thuật Lên Đến 24 Tháng
                </div>
              </div>
            </div>
          </div>
        </div>
      </section>

      {/* 4 CORE TECH PILLARS */}
      <section id="tech-pillars" className="tech-pillars-section">
        <div className="services-container">
          <div className="tech-section-header">
            <span className="contact-sub-badge">HẠ TẦNG TRANG THIẾT BỊ</span>
            <h2 className="tech-section-title">4 Cụm Công Nghệ Kỹ Thuật Độc Quyền</h2>
            <p className="tech-section-subtitle">
              Được trang bị đồng bộ tại cả 3 chi nhánh Hà Nội, Đà Nẵng và TP. Hồ Chí Minh.
            </p>
          </div>

          <div className="tech-pillars-grid">
            {TECH_PILLARS.map((item) => {
              const Icon = item.icon;
              return (
                <article key={item.id} className="tech-pillar-card">
                  <div className="pillar-card-top">
                    <div className="pillar-icon-box">
                      <Icon size={26} />
                    </div>
                    <span className="pillar-tech-badge">{item.badge}</span>
                  </div>

                  <span className="pillar-category-tag">{item.tag}</span>
                  <h3 className="pillar-title">{item.title}</h3>
                  <p className="pillar-desc">{item.desc}</p>

                  <div className="pillar-highlights-list">
                    {item.highlights.map((h, i) => (
                      <div key={i} className="pillar-highlight-row">
                        <CheckCircle2 size={16} className="text-gold" />
                        <span>{h}</span>
                      </div>
                    ))}
                  </div>

                  <div className="pillar-card-action">
                    <button
                      type="button"
                      className="btn-pillar-cta"
                      onClick={() => navigate("/booking")}
                    >
                      Đặt hẹn kiểm tra hạng mục này <ArrowRight size={16} />
                    </button>
                  </div>
                </article>
              );
            })}
          </div>

          {/* Genuine Parts Standard Commitment */}
          <div className="tech-commitment-banner">
            <div className="commitment-left">
              <Shield size={36} className="text-gold" />
              <div>
                <span className="commitment-kicker">CAM KẾT NGUỒN GỐC</span>
                <h3 className="commitment-title">100% Phụ Tùng & Dầu Nhớt Nhập Khẩu Chính Ngạch</h3>
                <p className="commitment-desc">
                  Mọi phụ tùng thay thế tại xưởng đều xuất xứ từ các thương hiệu hàng đầu thế giới
                  (Bosch, Brembo, Bilstein, Mobil 1, Motul, ZF) với tem chứng nhận và mã QR truy xuất nguồn gốc minh bạch.
                </p>
              </div>
            </div>
            <div className="commitment-right">
              <button
                type="button"
                className="btn-commitment-action"
                onClick={() => navigate("/parts-shop")}
              >
                Khám Phá Part Shop
              </button>
            </div>
          </div>
        </div>
      </section>

      <Footer />
    </div>
  );
};

export default CustomerServicesPage;
