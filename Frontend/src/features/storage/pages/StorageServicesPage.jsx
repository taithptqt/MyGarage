import React from "react";
import Header from "@shared/components/Header";
import Footer from "@shared/components/Footer";
import { Shield, Thermometer, UserCheck, Settings } from "lucide-react";
import "./StorageServicesPage.css";

const tiers = [
  {
    key: "standard",
    title: "STANDARD",
    price: "500",
    features: ["Climate Control", "Security 24/7"],
    image:
      "https://lh3.googleusercontent.com/aida-public/AB6AXuBZQJYfPZ2clZQI-LEqc7h93BB7kaTOFL6NcOnrfZJ6UwUdUBANjazvgNu4OoieYv53RwDuLZYFZwPY7a8nDzyAYo5dy_sZLNnxUVIjRwPCVxYwDCpB9wrm0wWqhYCS0HNp60jgH06MZzWw79q8R90CFAlksfdSUBCgdabdU7BQDggN7Vxt1j_nVboMKNdXumRjv3e3C_qYFaBpX1Msk0X1u70zQdJAtixUuuEoAeCepxwi6Np3MOtijhFavr14N7ZM08hyySJmXe-h"
  },
  {
    key: "premium",
    title: "PREMIUM",
    price: "850",
    features: ["Climate Control", "Security 24/7", "Concierge Service"],
    image:
      "https://lh3.googleusercontent.com/aida-public/AB6AXuD4wLnfAb4HngEEDa_tylvyvI5VsHh-l8V7Ekqce25J7pTEOUmxXTtJdUpvUHWjNg-HLK_QqSSsKbgNgESyGbRTvqieoWqeV2KTvEz0Wq8afSfIY0HC-3tyFCuIC2FN8QFVS2Nme8J8VN9IxCVZoULuM8pN-u8JIZDn4UV7Y3KoL0L-z-TYPfG0HgolutsblsB7CSL7W4NohkDHYbx5CTES0Z9bWBCFCe1MtnheybqjWuity9jflJR_wJZ9MfgfWKki0SDRV_a51LUk"
  },
  {
    key: "connoisseur",
    title: "CONNOISSEUR",
    price: "1.200",
    features: ["Climate Control", "Security 24/7", "Concierge Service", "Weekly Maintenance"],
    image:
      "https://lh3.googleusercontent.com/aida-public/AB6AXuAFtQp_6O4xxjEzt8-M3ZXGvJZP1tZErMvyH-ItS_VhYoCt8kahtJHMKIOtc6o1pDXMpuRWceXX5ms7AikaxE0zGZLuo_zH_EHkLHgKRnmLKRAB0x9uxXEyc1uU6St3fuFE7FZWPRr1CTc2XYGJDuGLCiWqdMvNSVBvBzcqnVvTHqI8oPePD7Hj4FcOMm8nHbNKZTYEILMDsyIyXDCSsNKl95R-4KyeWcUUBsfntbIPsfLkoSic1dAbP_mZ6HI_2H9KRZhTfH0edOkc"
  }
];

const standards = [
  { icon: Shield, value: "99.9% Up-time", label: "24/7 Security Monitoring" },
  { icon: Thermometer, value: "18 C Stable", label: "Climate Control Precision" },
  { icon: UserCheck, value: "24/7 Access", label: "Personal Concierge" },
  { icon: Settings, value: "Weekly Detail", label: "Preventative Maintenance" }
];

const StorageServicesPage = () => {
  return (
    <main className="storage-page">
      <Header overlay />

      <header className="storage-hero">
        <div className="storage-hero__overlay" />
        <img
          alt="Luxury Garage Hero"
          className="storage-hero__image"
          src="https://lh3.googleusercontent.com/aida-public/AB6AXuAZO_wUvoCfqzL0eHg496AZJnIrDUKkMT7y9nckwjd4pj_C-RSTXJcS9PKEJBgsi7XOzp1yerfZ3GdRii-Y5jGeqVweeDkwZiHSxlOFItN647rHd5MtUlSkPzKIxnGtpMsjCB9lITnPASGXoS2cbCOrTFibybp-qxxrC4r_YppSb-WKWn2y-acDon6KXBnaf7RiIswVJi9U_arnQWf4g_cTsyn6ZuZpNFC4Kiy5w8j_gVjsnP-6eVJsEem7yyyhLXmlGVcTEgVHuMgV"
        />
        <div className="storage-hero__content">
          <h1 className="storage-hero__title mg-scene-fade-up mg-scene-delay-1">PRIVATE AUTOMOTIVE STORAGE</h1>
          <p className="storage-hero__subtitle mg-scene-fade-up mg-scene-delay-2">WHERE YOUR COLLECTION IS TREATED AS TREASURE</p>
        </div>
      </header>

      <section className="storage-intro container-xl mg-scene-fade-up mg-scene-delay-3">
        <div className="storage-intro__title-wrap">
          <h2 className="storage-intro__title">A HOME WORTHY OF YOUR COLLECTION</h2>
        </div>
        <div className="storage-intro__divider" />
        <div className="storage-intro__text-wrap">
          <p>
            Beyond mere preservation, our storage facility is a sanctuary for the mechanical elite. Designed with architectural precision,
            every square inch of our space is curated to provide a neutral, temperature-stabilized environment that respects the
            engineering of your investment.
          </p>
          <p>
            We believe that rare automobiles should not just be parked - they should be curated. Our facility offers an atmosphere of
            silence, security, and absolute discretion for motor connoisseurs who demand nothing less than perfection.
          </p>
        </div>
      </section>

      <section className="storage-tiers">
        <div className="container-xl">
          <div className="storage-section-head">
            <p className="storage-section-kicker">THE OPTIONS</p>
            <h3 className="storage-section-title">STORAGE TIERS</h3>
          </div>

          <div className="storage-tiers__grid">
            {tiers.map((tier) => (
              <article key={tier.key} className={`storage-tier storage-tier--${tier.key}`}>
                <div className="storage-tier__image-wrap">
                  <img src={tier.image} alt={`${tier.title} Storage`} className="storage-tier__image" />
                </div>
                <div className="storage-tier__body">
                  <p className="storage-tier__name">{tier.title}</p>
                  <p className="storage-tier__price">
                    EUR {tier.price}
                    <span>/mo</span>
                  </p>
                  <ul className="storage-tier__features">
                    {tier.features.map((feature) => (
                      <li key={feature}>{feature}</li>
                    ))}
                  </ul>
                  <button className="storage-tier__button" type="button">
                    INQUIRE
                  </button>
                </div>
              </article>
            ))}
          </div>
        </div>
      </section>

      <section className="storage-standards">
        <div className="container-xl">
          <div className="storage-standards__head">
            <h3>THE SANCTUARY STANDARD</h3>
            <div className="storage-standards__line" />
          </div>
          <div className="storage-standards__grid">
            {standards.map(({ icon: Icon, value, label }) => (
              <article key={value} className="storage-standard-card">
                <Icon className="storage-standard-card__icon" />
                <p className="storage-standard-card__value">{value}</p>
                <p className="storage-standard-card__label">{label}</p>
              </article>
            ))}
          </div>
        </div>
      </section>

      <section className="storage-testimonial">
        <div className="storage-testimonial__inner">
          <blockquote>
            "A sanctuary where my collection breathes in silent elegance. The attention to detail is unparalleled, from the micro-climate
            control to the concierge&apos;s discretion."
          </blockquote>
          <p className="storage-testimonial__author">ALEXANDER VON GLERION</p>
          <p className="storage-testimonial__car">1962 FERRARI 250 GTO</p>
        </div>
      </section>

      <Footer />
    </main>
  );
};

export default StorageServicesPage;
