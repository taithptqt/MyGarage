import { Navigate } from "react-router-dom";
import { isAuthenticated } from "../../../app/authState";
import Header from "../../../shared/components/Header";
import Footer from "../../../shared/components/Footer";
import "./HomePage.css";

const brandLogos = [
  "https://lh3.googleusercontent.com/aida-public/AB6AXuBVUOXWTL5y4oH3N1D4bUOzYAlqCIy5_VwnqRzshber8H9tQcD8UqNRjCj9eGCz8sJJz0dhotTYGphnCcs3-gvvc_I7uzKn6_JUBLR1TCORskXRuqLuEX7iVK5hSMt0WbXaJmhHoFO0VDKFEVpPTHbizVaTBj9WH5mIfycLJe1zIxDvzjinyiyXnCd0bXWCf1A7UWtm64cI97CwRejnppABHQRLIhAA0CMfx_UV6WU7fHpCvJ9G_JG7ejvGmTDDctyBNVBKth-CsLFw",
  "https://lh3.googleusercontent.com/aida-public/AB6AXuBiqzOk6BJCrODNuZb0gPx-pOtnPI9vO2EJ21QZzY3OBMRUQ9XU8w4uPxunL3uH57iGseTGBEITelyypa0CwZYFIwRJZz1OHRp13GxtRXiL1blxAWcToeKNR8xa37U_5yGEarg3sNFNuN7jNSQCsimSxrEKUB_TuLD8QmxAVC90c9rQkd4LpZopc1WKjnQxZA9I4m_jZf0XgEkfGR_5Nag3PQWc09cWVDseZxByVcaU2ur64x8isbm9ahvVbip9FScOIdUscdWOu5OW",
  "https://lh3.googleusercontent.com/aida-public/AB6AXuAZrZmgtcDuDwECNGGvzo7fsFTL46ST-F2BJLeCpzWHTwsLLiWS25R8PtsDsh56FulWtyDu3KDqX0d8cHfoDOR4I-hvKMjrKEqz6rdE8H9gM2JQyvtcEiV0zMLP2l1O-Oau700R_pR6ypt05oWw-6cGBBO028RD9JeT7rJoIkpKFOLH_XepKaFdcgU3CQXeZ6yIXVMhoRyxEzw232LNkLgMGbYO1wa2-KaSRQam-Ajg3Op2ASBTTlQb-kYhFX1ND1LFrUf65zbMY3NK",
  "https://lh3.googleusercontent.com/aida-public/AB6AXuCO0IBXCS0y5MR3h9JyCzRlhP9eG4AgEi4LWDXxK9yr_D3IwULK--fNrp0bxcqxTJLAL9naDDJ9tjj0ncJzooZ5vqYEP1BQ_zPnTYd0zgkzPZDw66b4fpkA0TmLUiJDGJ3k_QGqdBKz0h2hcSkhMFvfo3KxYeNpVZPXPXEOCXtAAv_6d3vGJQPoDITLUjLqRFE40zJd-b7jRFuBZV3uOYRPSV2dET5L7tTaatmweOr9oqYq1rO6QrbOkF4UbxN7qz8GY8rs89FeFIF2",
  "https://lh3.googleusercontent.com/aida-public/AB6AXuB7eUl0IRxZH_hXT6Drf851hMDyhcqQXlA25OB-WWmBIQGKHSP2LhRTZr3E09lOvjtrgARYHPxdlXc3t85nKoVc_OD1m87ZAeZ5KK77d_zVKchmPWhUuoOqOt0cFjJdxP3xAi0I6RFa6ua5BB5QmcEFbAJCFSuYMCDD68xTt5UvyUoIt9qBsufTrb5pbOGsEYb0yC2cQLiEvFtuUUaQbrLGloHQtDHZSgcc1mFVoKkEuRDDzAsYq1K4vrUweHfO_ODSyBnaO2g7kbLh",
  "https://lh3.googleusercontent.com/aida-public/AB6AXuAn_W0bkDFzRgZ3id6M-b0M-Od5jfAq1InCjaGhBNJzEp9ufPYncTCfXKoOs_uvnTkRLKmFX-L6MqGILxi5MiOZb2LpPzcSgIvlPWJYxNb6COMi8gmPwV6EuUhLTjUAxXQmpnD1qFVaucV2H3dyIfvsdXXLpdsXjqn-BafDUp_q8Jyoof16Pg8HqHu_Jp3tkQsNT4zDUz2VTC160LVdCgIxiNg4Q7ETomfNifJwK2M5gS-CwxgRzpF0QcrwwPdAFYvF9DhhQSspN2pf",
];

const featuredCars = [
  {
    title: "PORSCHE 911 (992) GT3",
    tag: "SẴN SÀNG GIAO NGAY",
    year: "2023",
    mileage: "1,250 KM",
    image:
      "https://lh3.googleusercontent.com/aida-public/AB6AXuBt7JDMNFEDF5iRjfXCPAusiogk9iunAh9RYgBC3I3okl_xoznueSXaOKJGswioEk8D9xGb7R81Opp_fhU-F8ArT53LWfyiUaR5pnccbZW4c1YwIyI-Mtxlm5U9AUksVrEZUfudQJG7pVFiDk_19vd1RtQKb25gydOb5hQYyjUAsk4pIevEbX7CEGhS5YjXtjoAlBakIWMgn5k7Fla01ljQ9QcC_ap9FLvEfmUKgMiI-7pSXYvSQoRyzFYlu7shO2urGwiCSZhnxrxf",
  },
  {
    title: "FERRARI F8 TRIBUTO",
    tag: "BỘ SƯU TẬP RIÊNG",
    year: "2021",
    mileage: "3,400 KM",
    image:
      "https://lh3.googleusercontent.com/aida-public/AB6AXuDfpqF1kvCrN9Oaxo6Yqg7IBm2dsPuLry0JiyriOSY3C7sLgDe1-65KDMRxbtIjGLl7-7ZuSOjbFfhiHuofR4PoI94JvDfqf67R4N6eRx6PRq9uoE9hutVIxqliJwZvsyvN5zGMqI69-rop3D6juzphI_kWj48OgShrqEaBmFGbCa1XkE8fi-BXmcemb6JP8EN7leTM3fX_XXLSdGGVxj2ldYoFVkCcZRsF89pFYzi6uZ5RwTl8Ux49HmNLCs2bjhyikYj9wKnzdku3",
  },
  {
    title: "LAMBORGHINI SVJ ROADSTER",
    tag: "PHIÊN BẢN SƯU TẦM",
    year: "2022",
    mileage: "850 KM",
    image:
      "https://lh3.googleusercontent.com/aida-public/AB6AXuCE6YbYm9CR7y5pZNva-pIcybRljJdDQqJjCDWNJcUtUjpfmNUoqOWLnhQslY8HuJSNHdSHa0BEv-A69rBtHOZQwyskGFYBGCQDg7kX-GWOkLBk1WECRdqUh1r9ahKXYkSZC-SkodfkKx-17ef8SdNHPvflSpWikis_7sBYAk_Dil931npbmQPBHaNaHcpgzpwakwlqltkl0qy5Lfr4idQ2DPVZ6B5z-F29nLlNETVVAVMNaZUuiFIp46IqEblV2nG4HDLBCPT5SMwf",
  },
];

const partnerLogos = [
  "https://lh3.googleusercontent.com/aida-public/AB6AXuBEjuu_07IOgfu-BDu_twW8utReU0q2CCh_pVvUW4f_wiPww7qo9SeJ21niv5QcmYKG2bWjMDC47THqDxcn9BTkO2fVdV0ynEQqK0yTxDAEzgY50cqv4ZEN7Cx4oRwfY6a-J3YTpzs7JEX3V0f7-bhmPgxpPpsIa3R_QTARW7TkX1fDIr_Ozffo9Nm3ib1kwxJo4_62tzhmKZAm4BGqDdEY8-lIpkalS3y0T6DrJKdqlMI8e-t9y5wb9YtksvbIS0DRImQHve96Zz64",
  "https://lh3.googleusercontent.com/aida-public/AB6AXuC8aLKmTdlfJnVDUPYE_bxzVOa58jHY4XC-uSEDvkQXpjX0VHVAjOG5XnD7uyNnnw9tXeQM1-m1cXm8i4_I_e9qKROlkdOtaUa8Te1t20hf9qVjmsFEDlBOUyefmx6kOyqjntqeleg8UywHyRE23kHdzt4Pupo3uw85GxCPpa_kUAcftwQpVPoJdsRKV8OP92JlaWusY8vMyPI5XJ2Acef2Rf2kgSf4_vCPSDEiI5qsJgEh7f4h87qnkLN1tuQl2g4vSrKPKP0PnsQv",
  "https://lh3.googleusercontent.com/aida-public/AB6AXuDGnrFY16sd6uWUU6DBg3QETd39Aho4epYNFvxjl-TdT_H3ZpIYTr9KkT4zUieM2oB_fapnBUsevu51Gh5xDkjRfVZi90Beba9MB70xES6-8ybkbKqbXdTA_APlh8-y8-G2st57hfKaW--Ml2nyQ38HMiD63yTyOM1iqbZFHfDPoRXTa2QqvJYKLPiy1XS0gblC7wREuwX5Tsq9a1CLXEYdO3C4WqRwSMAMi6sPtnGwWmrvj0EwRZIaIhUpjITkUd_GrhRBPBzaw1Zy",
  "https://lh3.googleusercontent.com/aida-public/AB6AXuD4QzUKZKC3YZLrWSKc1pKCNkGErwxDYG5PMRy2YgBjEYHBzmXfZgkcC-kJklxhPfTCkozKWW6vFrV0mJSKLj7EVMoksvrESTH2UL8R3sUX96M8vxEsoUE1vhN3aMpI7rkyr_DWYX3NHNE5Ztcv3jTujdj2v-4LI6WF9D5oiKpvbGiTSdtveMv9Mnt2R1G2vYdU5KYF7G8X4sw25trzB7W6sHCZ_ctAKi8wcaiC_M3R92azaA4sKB3ZqrP9WNchjC29pj5zMkS_KZp0",
  "https://lh3.googleusercontent.com/aida-public/AB6AXuD9TKS3mRQKJg8WTgLavjNdobNnW3iMSTeWEiwXIZ5ofLnQFr_24QoHPHfQXxnXSWAMVgyKIBmfdsW52Z-Xmkb8kAw_gEmKnWDBmwXO3IkVT79sMzvEsRDi3dbxcIXwEjCAc0N87F8emX8StihjAGuR-kCypbOoVTrYAzaHEeCDH0LTH1Ca3sMtbxhI2ejnUmzwEuHYrqER3OxGWKQnVA8XG64iiSyvYImBxvz5Oa1pnZmzBks-IcQF471Kg4SNLaWh8frmVe8umYTc",
  "https://lh3.googleusercontent.com/aida-public/AB6AXuD6xBhbxqN1-eoHbDn7Rt6Y2vUVunoC7EzDiNk4K10P4vDpRgwBg50m7yysxVxuVeiUNxtu-E48e16XCOpyWgX5rAzoG6wdS_flYmVStmXnmbfadqYWnVjimD608DRhPhklsx7YsxH7_E-Sm5tqkS-9vmvGpGxjh1lmrgLSzFVnveP_xWd36JuV-c1vPo8lq1U3SfiOP8FBWUlwX80xo0avJ8xlysqcsINToc2tc_Wgn0-wd7KqZ8ww9QH8LuVcyhJ0evTOCap5y9NX",
  "https://lh3.googleusercontent.com/aida-public/AB6AXuA9n06I_bZfG0cwZ5VpVTLmYypwHQAn5_-3Q4LhgOSqKAGq7id4goNPnAYFggP-LzSfW8Nlc7DivAmkaZFPVR8dm5qb58qazKZKf7zBdbnmHrtQfhS8-wTqMN0-G5S2cIp61Y0J2XSXJxgJ6hmgaX9kuLp-lt0qiCThZGosYcTc3xsx914lBOga3RNPbJ5B6f1aioxtBQN-kQqrlpOcI8ifb2nWv_-_oNTT-VLr30sv5AS3Oa_UHmLcfNqK-mubI9MWI6_cdhIYvlLJ",
  "https://lh3.googleusercontent.com/aida-public/AB6AXuAcpeCUF05ckMGbF_06wkEr8bhPypCeBfC2G5b5Kgbf_pi9aOq5lezQrGgZl09_ct51F-EHZ2SAn4IN1l_luI1PkShVRv9RkvI1Fpj542IdQUdnTrGaAAQ3ppWO6rJmih1Z_Fr_TLfF8Q0puz20achVCEpFuRbuehH7GBYv_YAHlJ-beqZG62pPPy2gx4-C4x30DJjkdvpzJ8UTlqfi9eEd5vC-SvLdn9CHHt0ckWgoKtvc_pG-s2tCISnfdCIOS4BPf05bGSvWE4KU",
];

function HomePage() {

  if (!isAuthenticated()) {
    return <Navigate to="/" replace />;
  }

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
        {brandLogos.map((logo) => (
          <img key={logo} src={logo} alt="Brand" />
        ))}
      </section>

      <section className="cars-section">
        <div className="cars-head">
          <h2>XE NỔI BẬT TỪ KHU MUA BÁN</h2>
          <button type="button" className="outline-btn">
            XEM TOÀN BỘ XE
          </button>
        </div>
        <div className="cars-grid">
          {featuredCars.map((car) => (
            <article key={car.title} className="car-card">
              <div className="car-image">
                <img src={car.image} alt={car.title} />
              </div>
              <span className="gold-label">{car.tag}</span>
              <h3>{car.title}</h3>
              <div className="car-meta">
                <span>NĂM SX: {car.year}</span>
                <span>SỐ KM: {car.mileage}</span>
              </div>
            </article>
          ))}
        </div>
      </section>

      <section className="split split-dark">
        <div className="split-image">
          <img
            src="https://lh3.googleusercontent.com/aida-public/AB6AXuBhLfEiN-KXl4kR-gNwuYZ_n3bvwEkWpNWeRkHqNXUqj8_7atxNjdM-cJJ0lBVo6e83tfsNcdkzK157t1_pqwB6H6HIbMr6PukFCu3HhCzp7G4jaD0U59eKFRlQccvl7OOd1yJ_wxpKi36zRAkGWCyn8C-VCxzyCZ6mYB7_vjJbCffzv81Cr2vcaPZ-FGXmyPI0BDmO31DXzPd3o0rPJwb2gUzTPFzwbacB86Nz4T5Dm7OYy_lZdx9UJJ-PlVvBZF01hCdCzsbzpRF9"
            alt="Lưu trữ"
          />
        </div>
        <div className="split-content light">
          <span className="gold-label">AN NINH TUYỆT ĐỐI</span>
          <h2>LƯU TRỮ</h2>
          <p>
            Khu lưu trữ kiểm soát nhiệt độ của chúng tôi mang đến không gian an toàn
            tối ưu cho tài sản xe của bạn. Với hệ thống giám sát 24/7 và chương trình
            bảo dưỡng chuyên biệt, chiếc xe luôn giữ được tình trạng như mới.
          </p>
          <button type="button" className="outline-btn">
            XEM THÊM
          </button>
        </div>
      </section>

      <section className="split split-light reverse">
        <div className="split-image">
          <img
            src="https://lh3.googleusercontent.com/aida-public/AB6AXuBuCEIOKmm8Y5igFx9DGrl31tieRKTGeNHB8lMZCsTnlwMnmXqWKxXMjz2ZpI_vbcaGnYK_3c28RmGK5-rW36kwpjfoCnoWx4NGEhrwU1YhtAfBcbQPbDtBndtisIu2R3HHAT50as9UBfsFAJqDaZMTVhtEg0cl0EJy4KPC0NcKN8MW6Z1Iapv3UkXgdDBcme5_zmb6kUovizpLs-kkqdtEc5N2qCAUnsV7lnIr3IScUuHHdlS7e_nSSGbb_l1r_o3W7dOUCCYf_Sis"
            alt="Họp và sự kiện"
          />
        </div>
        <div className="split-content muted">
          <span className="gold-label">KHÔNG GIAN ĐẲNG CẤP</span>
          <h2>
            HỌP
            <br />
            &amp; SỰ KIỆN
          </h2>
          <p>
            Tổ chức cuộc họp quan trọng hoặc bữa tiệc riêng giữa những mẫu xe đẹp bậc
            nhất thế giới. Không gian được thiết kế riêng cho những ai trân trọng sự
            giao thoa giữa kỹ thuật và tinh tế.
          </p>
          <button type="button" className="outline-btn">
            KHÁM PHÁ KHÔNG GIAN
          </button>
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

      <section className="contact-section-home">
        <div className="contact-left">
          <h2>LIÊN HỆ</h2>
          <div className="contact-block">
            <p>
              MY GARAGE AS
              <br />
              Vejlevej 167
              <br />
              7100 Vejle, Denmark
            </p>
          </div>
          <div className="contact-block">
            <p>
              +45 70 70 20 00
              <br />
              info@mygarage.dk
            </p>
          </div>
          <div className="contact-socials">
            <span>$</span>
            <span>G</span>
            <span>@</span>
          </div>
        </div>
        <form className="contact-form-home" onSubmit={(event) => event.preventDefault()}>
          <label>
            HỌ VÀ TÊN
            <input type="text" placeholder="Nhập tại đây..." />
          </label>
          <label>
            ĐỊA CHỈ EMAIL
            <input type="email" placeholder="your@email.com" />
          </label>
          <label>
            TIN NHẮN
            <textarea rows={4} placeholder="Hãy cho chúng tôi biết nhu cầu của bạn..." />
          </label>
          <button type="submit" className="filled-btn">
            GỬI YÊU CẦU
          </button>
        </form>
      </section>

      <Footer />
    </main>
  );
}

export default HomePage;




