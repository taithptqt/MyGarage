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
    title: "Chẩn đoán lỗi bằng máy chuyên dụng",
    desc: "Đọc lỗi điện tử nhanh, minh bạch báo cáo trước khi thực hiện sửa chữa.",
    image: "/assets/booking/showcase-2.jpg",
  },
  {
    title: "Chăm sóc xe cao cấp",
    desc: "Đánh bóng, phủ bảo vệ bề mặt và chăm sóc nội thất theo tiêu chuẩn showroom.",
    image: "/assets/booking/hero.jpg",
  },
];

const highlights = [
  { number: "15+", label: "Năm kinh nghiệm vận hành chuỗi gara" },
  { number: "24/7", label: "Hệ thống nhắc lịch và theo dõi tiến độ" },
  { number: "10K+", label: "Lượt xe phục vụ mỗi năm" },
  { number: "98%", label: "Khách hàng hài lòng sau dịch vụ" },
];

const processSteps = [
  {
    title: "Tiếp nhận thông tin xe",
    desc: "Tư vấn viên ghi nhận tình trạng và nhu cầu chi tiết của khách hàng.",
  },
  {
    title: "Xác nhận gói dịch vụ",
    desc: "Đề xuất hạng mục phù hợp, báo giá rõ ràng trước khi xử lý.",
  },
  {
    title: "Thi công và kiểm soát chất lượng",
    desc: "Kỹ thuật viên chuyên trách xử lý theo checklist tiêu chuẩn.",
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
    desc: "Gói xử lý nhanh cho các hạng mục phổ biến, hạn chế chờ lâu tại xưởng.",
    promo: "Ưu tiên thao tác gọn, kiểm tra chuẩn kỹ thuật và bàn giao ngay khi hoàn tất.",
    cta: "Đặt lịch nhanh",
  },
  {
    keywords: ["sua chua chung", "sua chua", "repair"],
    desc: "Giải pháp sửa chữa tổng quát cho động cơ, điện, gầm và hệ thống vận hành.",
    promo: "Khoanh vùng lỗi minh bạch, báo giá trước khi làm và theo dõi tiến độ rõ ràng.",
    cta: "Xem phương án sửa",
  },
  {
    keywords: ["bao duong", "maintenance"],
    desc: "Bảo dưỡng định kỳ theo mốc km giúp xe bền máy, tiết kiệm nhiên liệu hơn.",
    promo: "Checklist chi tiết theo dòng xe, tối ưu tuổi thọ linh kiện và độ ổn định vận hành.",
    cta: "Chọn gói bảo dưỡng",
  },
  {
    keywords: ["bao hiem", "insurance"],
    desc: "Hỗ trợ quy trình sửa chữa bảo hiểm với hồ sơ rõ ràng và thời gian xử lý tối ưu.",
    promo: "Đồng hành từ bước kiểm tra tổn thất đến lúc bàn giao xe, giảm áp lực thủ tục.",
    cta: "Tư vấn bảo hiểm",
  },
  {
    keywords: ["rework", "sua chua lai"],
    desc: "Gói xử lý lại các hạng mục chưa đạt kỳ vọng để đảm bảo chất lượng cuối cùng.",
    promo: "Kiểm tra nguyên nhân gốc và hoàn thiện triệt để theo tiêu chuẩn kỹ thuật.",
    cta: "Đặt lịch kiểm tra lại",
  },
  {
    keywords: ["bao hanh", "warranty"],
    desc: "Dịch vụ bảo hành chính sách rõ ràng, giúp bạn an tâm sau mỗi lần sửa chữa.",
    promo: "Tiếp nhận nhanh, đối soát lịch sử dịch vụ đầy đủ và xử lý đúng cam kết.",
    cta: "Kiểm tra bảo hành",
  },
];

const genericPresets = [
  {
    desc: "Danh mục dịch vụ chuyên biệt theo từng nhu cầu bảo dưỡng và sửa chữa xe.",
    promo: "Quy trình minh bạch, kỹ thuật viên theo dõi sát để đảm bảo chất lượng đầu ra.",
    cta: "Khám phá gói",
  },
  {
    desc: "Giải pháp chăm xe được tối ưu theo tình trạng thực tế và mục tiêu vận hành.",
    promo: "Tư vấn rõ ràng từng hạng mục để bạn chủ động thời gian và ngân sách.",
    cta: "Xem chi tiết",
  },
  {
    desc: "Danh mục linh hoạt từ chăm sóc cơ bản đến xử lý chuyên sâu cho mọi dòng xe.",
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
            Từ bảo dưỡng định kỳ đến xử lý lỗi chuyên sâu, đội ngũ kỹ thuật viên của
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
          <h2>Giải pháp toàn diện cho xe của bạn</h2>
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
          <p className="booking-intro-eyebrow">Các loại dịch vụ</p>
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
        <h2>Cuộn đến đây là động lực để chăm sóc xe của bạn</h2>
        <p>
          Đặt lịch ngay để chọn khung giờ phù hợp, tránh chờ đợi và nhận tư vấn
          chính xác theo tình trạng xe.
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







