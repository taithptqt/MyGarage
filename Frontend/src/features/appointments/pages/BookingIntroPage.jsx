import { useEffect, useMemo, useState } from "react";
import { useNavigate } from "react-router-dom";
import { useAuth } from "@shared/core/context/AuthContext";
import { getAllServiceCategories, getAllStandardServices } from "@features/appointments/api/appointmentApi";
import Footer from "@shared/components/Footer";
import Header from "@shared/components/Header";
import "./BookingIntroPage.css";

const showcaseServices = [
  {
    title: "Bảo dưỡng định kỳ chuẩn hãng",
    desc: "Kiểm tra 82 hạng mục quan trọng, tối ưu vận hành và kéo dài tuổi thọ động cơ.",
    image: "/assets/booking/showcase-1.jpg",
  },
  {
    title: "Chọn do�n l?i bảng m�y chuy�n d?ng",
    desc: "Độc l?i di?n t? nhanh, minh b?ch b�o c�o tru?c khi th?c hi?n s?a ch?a.",
    image: "/assets/booking/showcase-2.jpg",
  },
  {
    title: "Chăm sóc xe cao cấp",
    desc: "Đánh b�ng, ph? b?o v? b? m?t v� cham s�c n?i th?t theo ti�u chu?n showroom.",
    image: "/assets/booking/hero.jpg",
  },
];

const highlights = [
  { number: "15+", label: "Năm kinh nghiệm vận hành chuỗi gara" },
  { number: "24/7", label: "Hệ thống nhắc lịch và theo dõi tiến độ" },
  { number: "10K+", label: "Lượt xe phục vụ mỗi năm" },
  { number: "98%", label: "Khách hàng h�i l�ng sau dịch vụ" },
];

const processSteps = [
  {
    title: "Tiếp nhận thông tin xe",
    desc: "Tu v?n vi�n ghi nh?n t�nh tr?ng v� nhu c?u chi ti?t c?a khách hàng.",
  },
  {
    title: "Xác nhận g�i dịch vụ",
    desc: "Độ xu?t h?ng m?c ph� h?p, b�o gi� r� r�ng tru?c khi x? l�.",
  },
  {
    title: "Thi c�ng v� ki?m so�t ch?t lu?ng",
    desc: "K? thu?t vi�n chuy�n tr�ch x? l� theo checklist ti�u chu?n.",
  },
  {
    title: "Bàn giao và nhắc lịch lần sau",
    desc: "Xe được kiểm tra lại, bàn giao minh bạch và lưu lịch sử dịch vụ.",
  },
];

const LOW_FEE_THRESHOLD = 100000;

const categoryPresets = [
  {
    keywords: ["dich vu nhanh", "nhanh", "quick", "fast"],
    desc: "G�i x? l� nhanh cho c�c h?ng m?c ph? bi?n, h?n ch? ch? l�u t?i xu?ng.",
    promo: "Uu ti�n thao t�c g?n, ki?m tra chu?n k? thu?t v� b�n giao ngay khi hoàn tất.",
    cta: "Đặt lịch nhanh",
  },
  {
    keywords: ["sua chua chung", "sua chua", "repair"],
    desc: "Gi?i ph�p s?a ch?a tổng qu�t cho d?ng co, di?n, g?m v� hệ thống v?n h�nh.",
    promo: "Khoanh v�ng l?i minh b?ch, b�o gi� tru?c khi l�m v� theo d�i ti?n d? r� r�ng.",
    cta: "Xem phuong �n s?a",
  },
  {
    keywords: ["bao duong", "maintenance"],
    desc: "B?o du?ng d?nh k? theo m?c km gi�p xe b?n m�y, ti?t ki?m nhi�n li?u hon.",
    promo: "Checklist chi tiết theo dòng xe, tối ưu tuổi thọ linh kiện và độ ổn định vận hành.",
    cta: "Chọn gói bảo dưỡng",
  },
  {
    keywords: ["bao hiem", "insurance"],
    desc: "Hỗ trợ quy trình sửa chữa bảo hiểm với hồ sơ rõ ràng và thời gian xử lý tối ưu.",
    promo: "Động h�nh t? bu?c ki?m tra t?n th?t d?n l�c b�n giao xe, gi?m �p l?c th? t?c.",
    cta: "Tư vấn bảo hiểm",
  },
  {
    keywords: ["rework", "sua chua lai"],
    desc: "G�i x? l� l?i c�c h?ng m?c chua d?t k? v?ng d? d?m b?o ch?t lu?ng cu?i c�ng.",
    promo: "Ki?m tra nguy�n nh�n g?c v� ho�n thi?n tri?t d? theo ti�u chu?n k? thu?t.",
    cta: "Đặt lịch kiểm tra lại",
  },
  {
    keywords: ["bao hanh", "warranty"],
    desc: "Dịch vụ b?o h�nh ch�nh s�ch r� r�ng, gi�p b?n an t�m sau m?i l?n s?a ch?a.",
    promo: "Ti?p nh?n nhanh, d?i so�t l?ch s? dịch vụ d?y d? v� x? l� d�ng cam k?t.",
    cta: "Kiểm tra bảo hành",
  },
];

const genericPresets = [
  {
    desc: "Danh mục dịch vụ chuyên biệt theo từng nhu cầu bảo dưỡng và sửa chữa xe.",
    promo: "Quy tr�nh minh b?ch, k? thu?t vi�n theo d�i s�t d? d?m b?o ch?t lu?ng d?u ra.",
    cta: "Kh�m ph� g�i",
  },
  {
    desc: "Gi?i ph�p cham xe du?c t?i uu theo t�nh tr?ng th?c t? v� m?c ti�u v?n h�nh.",
    promo: "Tu v?n r� r�ng tổng h?ng m?c d? b?n ch? d?ng thời gian v� ng�n s�ch.",
    cta: "Xem chi tiết",
  },
  {
    desc: "Danh m?c linh ho?t t? cham s�c co b?n d?n x? l� chuy�n s�u cho m?i d�ng xe.",
    promo: "Cam kết minh bạch chi phí và kết quả kỹ thuật trong từng bước thực hiện.",
    cta: "Đặt lịch tư vấn",
  },
];

function normalizeText(value) {
  return String(value || "")
    .toLowerCase()
    .normalize("NFD")
    .replace(/[\u0300-\u036f]/g, "")
    .trim();
}

function extractServicePrice(service) {
  const candidate =
    service?.finalPrice ??
    service?.unitPrice ??
    service?.price ??
    service?.basePrice ??
    service?.servicePrice ??
    service?.standardPrice ??
    service?.estimatedPrice;

  const parsed = Number(candidate);
  return Number.isFinite(parsed) && parsed > 0 ? parsed : null;
}

function formatVnd(value) {
  return `${new Intl.NumberFormat("vi-VN").format(Math.round(value))}đ`;
}

function pickCategoryPreset(name, index) {
  const normalized = normalizeText(name);
  const matched = categoryPresets.find((item) =>
    item.keywords.some((keyword) => normalized.includes(keyword))
  );
  if (matched) return matched;
  return genericPresets[index % genericPresets.length];
}

export default function BookingIntroPage() {
  const navigate = useNavigate();
  const { isAuthenticated } = useAuth();
  const [services, setServices] = useState([]);
  const [categories, setCategories] = useState([]);

  function handleStartBooking() {
    if (isAuthenticated) {
      navigate("/booking/form");
      return;
    }
    navigate("/login", {
      state: { from: { pathname: "/booking/form" } },
    });
  }

  useEffect(() => {
    let ignore = false;

    async function loadServiceData() {
      try {
        const [serviceData, categoryData] = await Promise.all([
          getAllStandardServices(),
          getAllServiceCategories(),
        ]);

        if (!ignore) {
          const serviceList = Array.isArray(serviceData)
            ? serviceData.filter((item) => item?.isActive !== false)
            : [];
          const categoryList = Array.isArray(categoryData)
            ? categoryData.filter((item) => item?.isDeleted !== true)
            : [];

          setServices(serviceList);
          setCategories(categoryList);
        }
      } catch {
        if (!ignore) {
          setServices([]);
          setCategories([]);
        }
      }
    }

    loadServiceData();

    return () => {
      ignore = true;
    };
  }, []);

  const serviceTypeCards = useMemo(() => {
    const countByCategoryId = new Map();
    const fallbackByName = new Map();
    const priceByCategoryId = new Map();
    const priceByName = new Map();

    function addPriceStat(map, key, price) {
      if (key === null || key === undefined || price === null) return;
      const current = map.get(key) || { sum: 0, pricedCount: 0 };
      current.sum += price;
      current.pricedCount += 1;
      map.set(key, current);
    }

    services.forEach((service, index) => {
      const categoryId = service?.categoryId ?? service?.serviceCategoryId;
      const categoryName = service?.serviceCategoryName ||
        service?.categoryName ||
        service?.serviceCategory?.categoryName ||
        service?.serviceCategory?.name ||
        null;
      const normalizedCategoryName = normalizeText(categoryName);
      const servicePrice = extractServicePrice(service);

      if (categoryId !== null && categoryId !== undefined) {
        countByCategoryId.set(categoryId, (countByCategoryId.get(categoryId) || 0) + 1);
        addPriceStat(priceByCategoryId, categoryId, servicePrice);
      } else if (categoryName) {
        fallbackByName.set(categoryName, (fallbackByName.get(categoryName) || 0) + 1);
        addPriceStat(priceByName, normalizedCategoryName, servicePrice);
      } else {
        const fallbackName = `Loại dịch vụ ${index + 1}`;
        fallbackByName.set(fallbackName, (fallbackByName.get(fallbackName) || 0) + 1);
      }
    });

    const categoryCards = (categories || [])
      .map((category, index) => {
        const name = category?.categoryName || category?.name || `Loại dịch vụ ${index + 1}`;
        const normalizedName = normalizeText(name);
        const count = countByCategoryId.get(category?.id) || 0;
        const preset = pickCategoryPreset(name, index);
        const priceStat =
          priceByCategoryId.get(category?.id) ||
          priceByName.get(normalizedName) ||
          null;
        const averagePrice = priceStat?.pricedCount
          ? priceStat.sum / priceStat.pricedCount
          : null;
        const showAveragePrice =
          averagePrice !== null && averagePrice >= LOW_FEE_THRESHOLD;

        return {
          key: `category-${category?.id ?? index}`,
          title: name,
          desc: preset.desc,
          marketingLine: preset.promo,
          badge: count > 0 ? "Đang phục vụ" : "Sắp cập nhật",
          showAveragePrice,
          averagePriceLabel: showAveragePrice ? formatVnd(averagePrice) : "",
          ctaLabel: preset.cta,
          rank: count,
          accent: index % 3,
        };
      })
      .sort((a, b) => b.rank - a.rank);

    const fallbackCards = [...fallbackByName.entries()].map(([name, count], index) => {
      const preset = pickCategoryPreset(name, categoryCards.length + index);
      const priceStat = priceByName.get(normalizeText(name)) || null;
      const averagePrice = priceStat?.pricedCount
        ? priceStat.sum / priceStat.pricedCount
        : null;
      const showAveragePrice =
        averagePrice !== null && averagePrice >= LOW_FEE_THRESHOLD;

      return {
        key: `fallback-${name}-${index}`,
        title: name,
        desc: preset.desc,
        marketingLine: preset.promo,
        badge: count > 0 ? "Đang phục vụ" : "Sắp cập nhật",
        showAveragePrice,
        averagePriceLabel: showAveragePrice ? formatVnd(averagePrice) : "",
        ctaLabel: preset.cta,
        rank: count,
        accent: (categoryCards.length + index) % 3,
      };
    });

    const merged = [...categoryCards, ...fallbackCards]
      .filter((item, index, arr) => arr.findIndex((x) => x.title === item.title) === index)
      .slice(0, 6);

    while (merged.length < 6) {
      const index = merged.length + 1;
      merged.push({
        key: `placeholder-${index}`,
        title: `Loại dịch vụ ${index}`,
        desc: genericPresets[index % genericPresets.length].desc,
        marketingLine: genericPresets[index % genericPresets.length].promo,
        badge: "Sắp cập nhật",
        showAveragePrice: false,
        averagePriceLabel: "",
        ctaLabel: genericPresets[index % genericPresets.length].cta,
        rank: 0,
        accent: index % 3,
      });
    }

    return merged;
  }, [categories, services]);

  return (
    <div className="booking-intro">
      <section className="booking-intro-hero">
        <div className="booking-intro-hero-overlay" />
        <Header overlay />
        <img
          className="booking-intro-hero-image"
          src="/assets/booking/hero.jpg"
          alt="Không gian kỹ thuật gara chuyên nghiệp"
        />
        <div className="booking-intro-hero-content">
          <p className="booking-intro-eyebrow">MY GARAGE PREMIUM CARE</p>
          <h1>Dịch vụ gara chuyên nghiệp cho mọi hành trình của bạn</h1>
          <p>
            T? b?o du?ng d?nh k? d?n x? l� l?i chuy�n s�u, d?i ngu k? thu?t vi�n c?a
            MyGarage mang lại quy trình rõ ràng, chất lượng ổn định và trải nghiệm
            dịch vụ đẳng cấp.
          </p>
        </div>
      </section>

      <section className="booking-intro-highlights">
        {highlights.map((item) => (
          <article key={item.label} className="booking-intro-highlight-card">
            <h3>{item.number}</h3>
            <p>{item.label}</p>
          </article>
        ))}
      </section>

      <section className="booking-intro-services">
        <header>
          <p className="booking-intro-eyebrow">Dịch vụ nổi bật</p>
          <h2>Gi?i ph�p to�n di?n cho xe c?a b?n</h2>
        </header>
        <div className="booking-intro-service-grid">
          {showcaseServices.map((service) => (
            <article key={service.title} className="booking-intro-service-card">
              <img src={service.image} alt={service.title} />
              <div className="booking-intro-service-body">
                <h3>{service.title}</h3>
                <p>{service.desc}</p>
              </div>
            </article>
          ))}
        </div>
      </section>

      <section className="booking-intro-types">
        <header>
          <p className="booking-intro-eyebrow">C�c lo?i dịch vụ</p>
          <h2>Danh mục dịch vụ tại gara</h2>
          <p className="booking-intro-types-subtitle">
            Chọn đúng loại dịch vụ theo tình trạng xe để chủ động thời gian,
            chi phí và chất lượng xử lý.
          </p>
        </header>
        <div className="booking-intro-type-grid booking-intro-type-grid-fixed">
          {serviceTypeCards.map((group) => (
            <article
              key={group.key}
              className={`booking-intro-type-card accent-${group.accent}`}
            >
              <div className="booking-intro-type-head">
                <div>
                  <h3>{group.title}</h3>
                  <p>{group.desc}</p>
                </div>
                <span>{group.badge}</span>
              </div>
              <p className="booking-intro-type-promo">{group.marketingLine}</p>
              <div className="booking-intro-type-meta">
                {group.showAveragePrice ? (
                  <div className="booking-intro-type-pricing">
                    <small>Chỉ từ</small>
                    <strong>{group.averagePriceLabel}</strong>
                  </div>
                ) : (
                  <span className="booking-intro-type-pricing-empty" />
                )}
                <button
                  type="button"
                  className="booking-intro-type-link"
                  onClick={handleStartBooking}
                >
                  {group.ctaLabel}
                </button>
              </div>
            </article>
          ))}
        </div>
      </section>

      <section className="booking-intro-process">
        <div className="booking-intro-process-content">
          <p className="booking-intro-eyebrow">Quy trình minh bạch</p>
          <h2>4 bước phục vụ tại MyGarage</h2>
          <div className="booking-intro-step-list">
            {processSteps.map((step, idx) => (
              <article key={step.title} className="booking-intro-step-card">
                <span>{`0${idx + 1}`}</span>
                <div>
                  <h3>{step.title}</h3>
                  <p>{step.desc}</p>
                </div>
              </article>
            ))}
          </div>
        </div>
        <div className="booking-intro-process-media">
          <img
            src="/assets/booking/showcase-2.jpg"
            alt="Kỹ thuật viên đang kiểm tra động cơ xe"
          />
        </div>
      </section>

      <section className="booking-intro-cta">
        <p className="booking-intro-eyebrow">Sẵn sàng đặt lịch?</p>
        <h2>Cu?n d?n d�y l� d?ng l?c d? cham s�c xe c?a b?n</h2>
        <p>
          Đặt l?ch ngay d? chọn khung gi? ph� h?p, tr�nh ch? d?i v� nh?n tu v?n
          ch�nh x�c theo t�nh tr?ng xe.
        </p>
        <button
          type="button"
          className="booking-intro-primary booking-intro-primary-large"
          onClick={handleStartBooking}
        >
          Đặt lịch ngay
        </button>
      </section>

      <Footer />
    </div>
  );
}








