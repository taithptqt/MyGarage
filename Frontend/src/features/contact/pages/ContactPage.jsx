import React, { useState } from "react";
import { useNavigate } from "react-router-dom";
import Header from "@shared/components/Header";
import Footer from "@shared/components/Footer";
import {
  MapPin,
  Phone,
  Mail,
  Clock,
  Wrench,
  Calendar,
  AlertTriangle,
  ExternalLink,
  Building2,
  PhoneCall,
  Navigation,
} from "lucide-react";
import "./ContactPage.css";

const BRANCHES = [
  {
    id: 1,
    code: "HN",
    region: "Miền Bắc",
    tag: "TRỤ SỞ & TRUNG TÂM KỸ THUẬT",
    name: "MyGarage Hà Nội",
    address: "Số 68 Cầu Giấy, Phường Quan Hoa, Quận Cầu Giấy, Hà Nội",
    lat: 21.0352,
    lng: 105.7985,
    hotline: "1900 0001",
    tel: "tel:19000001",
    email: "MyGarageHN_No1@gmail.com",
    hours: "08:00 - 18:00 (Thứ Hai - Chủ Nhật)",
    capacity: "20 Khoang sửa chữa tiêu chuẩn Đức & Phòng chờ VIP 5 sao",
    mapEmbedUrl:
      "https://maps.google.com/maps?q=21.0352,105.7985&t=m&z=16&output=embed",
    mapExternalUrl:
      "https://www.google.com/maps/search/?api=1&query=21.0352,105.7985",
  },
  {
    id: 2,
    code: "DN",
    region: "Miền Trung",
    tag: "TRUNG TÂM DỊCH VỤ & BẢO HÀNH",
    name: "MyGarage Đà Nẵng",
    address: "Số 126 Nguyễn Văn Linh, Phường Nam Dương, Quận Hải Châu, TP. Đà Nẵng",
    lat: 16.0610,
    lng: 108.2165,
    hotline: "0394 178 125",
    tel: "tel:0394178125",
    email: "MyGarageFuda_No2@gmail.com",
    hours: "08:00 - 18:00 (Thứ Hai - Thứ Bảy)",
    capacity: "15 Khoang sửa chữa, Căn chỉnh góc lái 3D Laser & Đồng sơn nhanh",
    mapEmbedUrl:
      "https://maps.google.com/maps?q=16.0610,108.2165&t=m&z=16&output=embed",
    mapExternalUrl:
      "https://www.google.com/maps/search/?api=1&query=16.0610,108.2165",
  },
  {
    id: 3,
    code: "HCM",
    region: "Miền Nam",
    tag: "FLAGSHIP GARAGE & SHOWROOM",
    name: "MyGarage TP. Hồ Chí Minh",
    address: "Số 202 Nguyễn Thị Thập, Phường Tân Phú, Quận 7, TP. Hồ Chí Minh",
    lat: 10.7356,
    lng: 106.7208,
    hotline: "1900 0003",
    tel: "tel:19000003",
    email: "MyGarageXavalo_No3@gmail.com",
    hours: "08:00 - 18:30 (Thứ Hai - Chủ Nhật)",
    capacity: "20 Khoang công nghệ cao, Trung tâm đại tu máy gầm & Lưu kho xe sang",
    mapEmbedUrl:
      "https://maps.google.com/maps?q=10.7356,106.7208&t=m&z=16&output=embed",
    mapExternalUrl:
      "https://www.google.com/maps/search/?api=1&query=10.7356,106.7208",
  },
];

const ContactPage = () => {
  const navigate = useNavigate();
  const [selectedBranchId, setSelectedBranchId] = useState(1);

  const activeBranch =
    BRANCHES.find((b) => b.id === selectedBranchId) || BRANCHES[0];

  const handleBranchSelect = (id) => {
    setSelectedBranchId(id);
  };

  const handleBookingAtBranch = (branch) => {
    try {
      localStorage.setItem("mygarage_selected_branch_id", String(branch.id));
      localStorage.setItem("mygarage_selected_branch_name", branch.name);
    } catch {
      // Storage unavailable fallback
    }
    navigate("/booking");
  };

  return (
    <main className="contact-redesign-page">
      <Header overlay />

      {/* DISTINCT HERO: NETWORK COMMAND BANNER */}
      <section className="contact-network-hero">
        <img
          className="contact-network-hero-img"
          src="https://images.unsplash.com/photo-1758025541713-a389a32d6c9e?auto=format&fit=crop&w=2000&q=85"
          alt="Trung tâm dịch vụ và văn phòng chi nhánh MyGarage"
          loading="eager"
        />
        <div className="contact-network-hero-overlay" />
        <div className="contact-network-hero-fade" />
        <div className="contact-network-hero-container">
          <div className="contact-network-hero-top">
            <h1 className="contact-network-title">
              Hệ Thống Chi Nhánh & Kênh Kết Nối <span>MyGarage</span>
            </h1>
            <p className="contact-network-desc">
              Sở hữu 3 trung tâm kỹ thuật quy mô lớn tại Hà Nội, Đà Nẵng và TP. Hồ Chí Minh.
              Mọi quy trình từ tiếp nhận xe, bảo dưỡng định kỳ đến đại tu máy gầm đều được quản lý tập trung và đồng bộ tiêu chuẩn OEM.
            </p>
          </div>

          {/* Quick Metrics Bar inside Hero */}
          <div className="contact-network-metrics-bar">
            <div className="network-metric-item">
              <div className="metric-icon-wrap">
                <Building2 size={20} />
              </div>
              <div className="metric-info">
                <strong>03 Chi Nhánh</strong>
                <span>Bắc - Trung - Nam</span>
              </div>
            </div>

            <div className="network-metric-item">
              <div className="metric-icon-wrap">
                <Wrench size={20} />
              </div>
              <div className="metric-info">
                <strong>55+ Khoang Nâng</strong>
                <span>Chuẩn tải châu Âu</span>
              </div>
            </div>

            <div className="network-metric-item">
              <div className="metric-icon-wrap">
                <PhoneCall size={20} />
              </div>
              <div className="metric-info">
                <strong>1900 0000</strong>
                <span>Hotline tổng 24/7</span>
              </div>
            </div>

            <div className="network-metric-item">
              <div className="metric-icon-wrap">
                <Clock size={20} />
              </div>
              <div className="metric-info">
                <strong>08:00 - 18:30</strong>
                <span>Làm việc cả tuần</span>
              </div>
            </div>
          </div>
        </div>
      </section>

      {/* MAIN BRANCHES & REAL WORKFLOW SECTION */}
      <section className="contact-branches-showcase">
        <div className="contact-container">
          {/* Branch Selector Tabs */}
          <div className="contact-branch-tabs-head">
            <div className="section-head-left">
              <span className="contact-sub-badge">ĐỊNH VỊ XƯỞNG DỊCH VỤ</span>
              <h2 className="contact-branches-h2">Chọn Chi Nhánh Gần Bạn</h2>
            </div>
            <div className="contact-branch-tabs" role="tablist">
              {BRANCHES.map((branch) => {
                const isActive = branch.id === selectedBranchId;
                return (
                  <button
                    key={branch.id}
                    type="button"
                    role="tab"
                    aria-selected={isActive}
                    className={`contact-branch-tab-btn ${isActive ? "active" : ""}`}
                    onClick={() => handleBranchSelect(branch.id)}
                  >
                    <span className="tab-region-tag">{branch.region}</span>
                    <strong className="tab-branch-name">{branch.name}</strong>
                    <span className="tab-branch-address-preview">{branch.address.split(",")[0]}</span>
                  </button>
                );
              })}
            </div>
          </div>

          {/* Active Branch Grid: Details & Map */}
          <div className="contact-showcase-grid">
            {/* Left Card: Active Branch Meta */}
            <div className="contact-branch-card">
              <div className="contact-branch-card-header">
                <span className="branch-status-pill">
                  <span className="status-dot-pulse" /> Đang Hoạt Động
                </span>
                <span className="branch-tag-pill">{activeBranch.tag}</span>
              </div>

              <h3 className="branch-display-title">{activeBranch.name}</h3>

              <div className="branch-meta-items">
                <div className="branch-meta-row">
                  <div className="meta-icon-box">
                    <MapPin size={20} />
                  </div>
                  <div>
                    <span className="meta-label">Địa Chỉ Xưởng</span>
                    <strong className="meta-value address">{activeBranch.address}</strong>
                  </div>
                </div>

                <div className="branch-meta-row">
                  <div className="meta-icon-box">
                    <Phone size={20} />
                  </div>
                  <div>
                    <span className="meta-label">Hotline Kỹ Thuật Chi Nhánh</span>
                    <a href={activeBranch.tel} className="meta-value hotline-link">
                      {activeBranch.hotline}
                    </a>
                  </div>
                </div>

                <div className="branch-meta-row">
                  <div className="meta-icon-box">
                    <Mail size={20} />
                  </div>
                  <div>
                    <span className="meta-label">Email Tiếp Nhận Hồ Sơ</span>
                    <a href={`mailto:${activeBranch.email}`} className="meta-value email-link">
                      {activeBranch.email}
                    </a>
                  </div>
                </div>

                <div className="branch-meta-row">
                  <div className="meta-icon-box">
                    <Clock size={20} />
                  </div>
                  <div>
                    <span className="meta-label">Khung Giờ Phục Vụ</span>
                    <strong className="meta-value">{activeBranch.hours}</strong>
                  </div>
                </div>

                <div className="branch-meta-row">
                  <div className="meta-icon-box">
                    <Wrench size={20} />
                  </div>
                  <div>
                    <span className="meta-label">Quy Mô Tiếp Nhận</span>
                    <strong className="meta-value">{activeBranch.capacity}</strong>
                  </div>
                </div>
              </div>

              {/* Branch Direct Actions */}
              <div className="branch-card-actions">
                <button
                  type="button"
                  className="btn-branch-book"
                  onClick={() => handleBookingAtBranch(activeBranch)}
                >
                  <Calendar size={18} />
                  Đặt Lịch Tại Chi Nhánh Này
                </button>
                <a href={activeBranch.tel} className="btn-branch-call">
                  <Phone size={18} />
                  Gọi Ngay
                </a>
              </div>
            </div>

            {/* Right Card: Interactive Map Display (Google Maps) */}
            <div className="contact-map-card">
              <div className="contact-map-header">
                <div>
                  <span className="map-kicker">GOOGLE MAPS • TỌA ĐỘ GPS CỐ ĐỊNH</span>
                  <h4 className="map-title">{activeBranch.name}</h4>
                </div>
                <a
                  href={activeBranch.mapExternalUrl}
                  target="_blank"
                  rel="noreferrer"
                  className="map-ext-link"
                >
                  Mở Google Maps <ExternalLink size={15} />
                </a>
              </div>

              <div className="contact-map-frame-wrap">
                <iframe
                  title={`Google Maps ${activeBranch.name}`}
                  src={activeBranch.mapEmbedUrl}
                  className="contact-map-iframe"
                  loading="lazy"
                  referrerPolicy="no-referrer-when-downgrade"
                />
                <div className="contact-map-overlay-badge">
                  <Navigation size={18} className="map-badge-icon" />
                  <div>
                    <strong>{activeBranch.name}</strong>
                    <p>{activeBranch.address}</p>
                    <span className="map-coords-badge">GPS: {activeBranch.lat}, {activeBranch.lng}</span>
                  </div>
                </div>
              </div>
            </div>
          </div>

          {/* Emergency Rescue Banner */}
          <div className="contact-emergency-banner">
            <div className="emergency-banner-content">
              <div className="emergency-icon-glow">
                <AlertTriangle size={32} />
              </div>
              <div>
                <span className="emergency-tag">ĐỘI CỨU HỘ KHẨN CẤP 24/7</span>
                <h3 className="emergency-title">Xe Của Bạn Gặp Sự Cố Trên Đường?</h3>
                <p className="emergency-desc">
                  Đội cứu hộ sàn trượt chuyên dụng MyGarage sẵn sàng điều phối hỗ trợ kích bình ắc quy,
                  thay lốp dự phòng hoặc cẩu kéo an toàn về xưởng gần nhất 24/7 trên toàn quốc.
                </p>
              </div>
            </div>
            <div className="emergency-banner-actions">
              <a href="tel:19000000" className="btn-emergency-hotline">
                <Phone size={20} />
                Hotline Cứu Hộ: 1900 0000
              </a>
            </div>
          </div>
        </div>
      </section>

      <Footer />
    </main>
  );
};

export default ContactPage;
