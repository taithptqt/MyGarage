import Header from "@shared/components/Header";
import Footer from "@shared/components/Footer";
import "./HomePage.css";

const brandLogos = [
  "https://mygarage.skaevt.com/wp-content/uploads/2025/05/Corvette-logo-guld-uden-tekst.png",
  "https://mygarage.skaevt.com/wp-content/uploads/2025/12/logo.png",
  "https://mygarage.skaevt.com/wp-content/uploads/2025/05/Corvette-logo-guld-uden-tekst.png",
  "https://mygarage.skaevt.com/wp-content/uploads/2020/12/LOTUS_Roundel_Primary_CMYK-GRAa-NY-2.png",
  "https://mygarage.skaevt.com/wp-content/uploads/2021/02/KALMAR_logo_KOKS.nobcg_.png",
];

const featuredCars = [
  {
    title: "ASTON MARTIN VANTAGE",
    tag: "",
    subtitle: "COUPÉ F1 EDITION AUT.",
    year: "2022",
    mileage: "4.301",
    image:
      "https://billeder.bilinfo.net/bilinfo/a20840f9-6e7f-4929-9225-49d46cac91a8.jpeg?class=S1600X1600",
  },
  {
    title: "LAUGE JENSEN GREAT DANE",
    tag: "",
    subtitle: "",
    year: "2016",
    mileage: "50",
    image:
      "https://billeder.bilinfo.net/bilinfo/472a467d-c6ca-4ac3-8318-0143cb923963.jpeg?class=S1600X1600",
  },
  {
    title: "AUDI Q8 E-TRON",
    tag: "",
    subtitle: "S-LINE QUATTRO",
    year: "2024",
    mileage: "10.000",
    image:
      "https://billeder.bilinfo.net/bilinfo/f846f4a2-29ff-4ec7-b5f4-6a3a0c3e6521.jpeg?class=S1600X1600",
  },
  {
    title: "MERCEDES S580 E",
    tag: "",
    subtitle: "AUT. LANG",
    year: "2022",
    mileage: "62.000",
    image:
      "https://billeder.bilinfo.net/bilinfo/ccbe5177-e693-42a5-b160-99a65f2cbd89.jpeg?class=S1600X1600",
  },
];

const partnerLogos = [
  "https://lh3.googleusercontent.com/aida-public/AB6AXuBEjuu_07IOgfu-BDu_twW8utReU0q2CCh_pVvUW4f_wiPww7qo9SeJ21niv5QcmYKG2bWjMDC47THqDxcn9BTkO2fVdV0ynEQqK0yTxDAEzgY50cqv4ZEN7Cx4oRwfY6a-J3YTpzs7JEX3V0f7-bhmPgxpPpsIa3R_QTARW7TkX1fDIr_Ozffo9Nm3ib1kwxJo4_62tzhmKZAm4BGqDdEY8-lIpkalS3y0T6DrJKdqlMI8e-t9y5wb9YtksvbIS0DRImQHve96Zz64",
  "https://lh3.googleusercontent.com/aida-public/AB6AXuC8aLKmTdlfJnVDUPYE_bxzVOa58jHY4XC-uSEDvkQXpjX0VHVAjOG5XnD7uyNnnw9tXeQM1-m1cXm8i4_I_e9qKROlkdOtaUa8Te1t20hf9qVjmsFEDlBOUyefmx6kOyqjntqeleg8UywHyRE23kHdzt4Pupo3uw85GxCPpa_kUAcftwQpVPoJdsRKV8OP92JlaWusY8vMyPI5XJ2Acef2Rf2kgSf4_vCPSDEiI5qsJgEh7f4h87qnkLN1tuQl2g4vSrKPKP0PnsQv",
  "https://lh3.googleusercontent.com/aida-public/AB6AXuDGnrFY16sd6uWUU6DBg3QETd39Aho4epYNFvxjl-TdT_H3ZpIYTr9KkT4zUieM2oB_fapnBUsevu51Gh5xDkjRfVZi90Beba9MB70xES6-8ybkbKqbXdTA_APlh8-y8-G2st57hfKaW--Ml2nyQ38HMiD63yTyOM1iqbZFHfDPoRXTa2QqvJYKLPiy1XS0gblC7wREuwX5Tsq9a1CLXEYdO3C4WqRwSMAMi6sPtnGwWmrvj0EwRZIaIhUpjITkUd_GrhRBPBzaw1Zy",
  "https://lh3.googleusercontent.com/aida-public/AB6AXuD4QzUKZKC3YZLrWSKc1pKCNkGErwxDYG5PMRy2YgBjEYHBzmXfZgkcC-kJklxhPfTCkozKWW6vFrV0mJSKLj7EVMoksvrESTH2UL8R3sUX96M8vxEsoUE1vhN3aMpI7rkyr_DWYX3NHNE5Ztcv3jTujdj2v-4LI6WF9D5oiKpvbGiTSdtveMv9Mnt2R1G2vYdU5KYF7G8X4sw25trzB7W6sHCZ_ctAKi8wcaiC_M3R92azaA4sKB3ZqrP9WNchjC29pj5zMkS_KZp0",
  "https://lh3.googleusercontent.com/aida-public/AB6AXuD9TKS3mRQKJg8WTgLavjNdobNnW3iMSTeWEiwXIZ5ofLnQFr_24QoHPHfQXxnXSWAMVgyKIBmfdsW52Z-Xmkb8kAw_gEmKnWDBmwXO3IkVT79sMzvEsRDi3dbxcIXwEjCAc0N87F8emX8StihjAGuR-kCypbOoVTrYAzaHEeCDH0LTH1Ca3sMtbxhI2ejnUmzwEuHYrqER3OxGWKQnVA8XG64iiSyvYImBxvz5Oa1pnZmzBks-IcQF471Kg4SNLaWh8frmVe8umYTc",
  "https://lh3.googleusercontent.com/aida-public/AB6AXuD6xBhbxqN1-eoHbDn7Rt6Y2vUVunoC7EzDiNk4K10P4vDpRgwBg50m7yysxVxuVeiUNxtu-E48e16XCOpyWgX5rAzoG6wdS_flYmVStmXnmbfadqYWnVjimD608DRhPhklsx7YsxH7_E-Sm5tqkS-9vmvGpGxjh1lmrgLSzFVnveP_xWd36JuV-c1vPo8lq1U3SfiOP8FBWUlwX80xo0avJ8xlysqcsINToc2tc_Wgn0-wd7KqZ8ww9QH8LuVcyhJ0evTOCap5y9NX",
];

function HomePage() {
  return (
    <main className="stitch-home">
      <header className="home-hero">
        <div className="hero-top-overlay" />
        <Header overlay />

        <div className="hero-media" aria-hidden="true">
          <video
            className="hero-video"
            autoPlay
            muted
            loop
            playsInline
            preload="auto"
          >
            <source src="/hero.mp4" type="video/mp4" />
          </video>
          <div className="hero-dim" />
        </div>

        <div className="hero-scroll">
          <span>CUỘN</span>
          <div className="scroll-line" />
          <div className="scroll-dot" />
        </div>
      </header>

      <section className="brand-strip">
        {brandLogos.map((logo, index) => (
          <div key={`${logo}-${index}`} className="brand-tile">
            <img src={logo} alt="Brand" />
            <span className="brand-line" />
          </div>
        ))}
      </section>

      <section className="cars-section">
        <div className="cars-head">
          <h2>XE CHỌN LỌC TỪ SALES STUDIO CỦA CHÚNG TÔI</h2>
        </div>
        <div className="cars-grid">
          {featuredCars.map((car, index) => (
            <article key={`${car.title}-${index}`} className="car-card">
              <div className="car-image">
                <img src={car.image} alt={car.title} />
              </div>
              <div className="car-content">
                <span className="gold-label">{car.tag}</span>
                <h3>{car.title}</h3>
                {car.subtitle ? <p className="car-subtitle">{car.subtitle}</p> : null}
                <p className="car-facts">
                  NĂM: {car.year} | KM: {car.mileage}
                </p>
              </div>
            </article>
          ))}
        </div>
      </section>

      <section className="experience-section">
        <div className="experience-row">
          <div
            className="experience-media"
            style={{
              backgroundImage:
                'url("https://mygarage.skaevt.com/wp-content/uploads/2024/11/Gentlemans_Garage_MG_2024-9.jpg")',
            }}
            role="img"
            aria-label="Lưu trữ xe"
          />
          <div className="experience-content">
            <h2>LƯU TRỮ</h2>
            <p>
              Hãy để chiếc xe đặc biệt của bạn được bảo quản trong hệ thống kho kính
              chuyên dụng của MY GARAGE. Chúng tôi đảm bảo điều kiện bảo quản ổn định,
              an ninh và hỗ trợ dịch vụ bảo dưỡng định kỳ.
            </p>
            <button type="button" className="experience-btn">
              XEM THÊM
            </button>
          </div>
        </div>

        <div className="experience-row reverse">
          <div className="experience-content">
            <h2>HỘI HỌP &amp; SỰ KIỆN</h2>
            <p>
              Không gian họp và sự kiện cao cấp được đặt ngay trong khu showroom trung
              tâm, bao quanh bởi những mẫu xe độc bản. Khu vực được trang bị đầy đủ và
              sẵn sàng cho các sự kiện quy mô lên đến 300 khách.
            </p>
            <button type="button" className="experience-btn">
              XEM THÊM
            </button>
          </div>
          <div
            className="experience-media"
            style={{
              backgroundImage:
                'url("https://mygarage.skaevt.com/wp-content/uploads/2024/11/Meeting_Garage_MG_2024-8-e1732700312672.jpg")',
            }}
            role="img"
            aria-label="Møder og events"
          />
        </div>
      </section>

      <section className="partners-section">
        <div className="partners-head">
          <h2>GẶP GỠ ĐỐI TÁC CỦA CHÚNG TÔI</h2>
          <p>
            Hệ sinh thái được tuyển chọn gồm các chuyên gia ô tô, từ dịch vụ chăm sóc
            xe cao cấp đến kỹ thuật viên hàng đầu và cố vấn tài chính đẳng cấp quốc tế.
          </p>
        </div>
        <div className="partners-grid">
          {partnerLogos.map((logo) => (
            <img key={logo} src={logo} alt="Partner" />
          ))}
        </div>
      </section>

      <Footer />
    </main>
  );
}

export default HomePage;










