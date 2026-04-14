import React from "react";
import Header from "@shared/components/Header";
import Footer from "@shared/components/Footer";
import { Shield, Thermometer, UserCheck, Settings } from "lucide-react";

const StorageServicesPage = () => {
  return (
    <main className="bg-surface">
      <Header />
      
      {/* Hero Section */}
      <header className="relative w-full h-[716px] flex items-center justify-center overflow-hidden pt-[180px] bg-primary">
        <div className="absolute inset-0 bg-primary/60 z-10"></div>
        <img alt="Luxury Garage Hero" className="absolute inset-0 w-full h-full object-cover" src="https://lh3.googleusercontent.com/aida-public/AB6AXuAZO_wUvoCfqzL0eHg496AZJnIrDUKkMT7y9nckwjd4pj_C-RSTXJcS9PKEJBgsi7XOzp1yerfZ3GdRii-Y5jGeqVweeDkwZiHSxlOFItN647rHd5MtUlSkPzKIxnGtpMsjCB9lITnPASGXoS2cbCOrTFibybp-qxxrC4r_YppSb-WKWn2y-acDon6KXBnaf7RiIswVJi9U_arnQWf4g_cTsyn6ZuZpNFC4Kiy5w8j_gVjsnP-6eVJsEem7yyyhLXmlGVcTEgVHuMgV" />
        <div className="relative z-20 text-center px-6">
          <h1 className="font-serif text-5xl md:text-7xl font-light text-white uppercase tracking-wider mb-4">PRIVATE AUTOMOTIVE STORAGE</h1>
          <p className="font-sans text-surface text-sm md:text-base tracking-[0.2rem] uppercase font-bold">WHERE YOUR COLLECTION IS TREATED AS TREASURE</p>
        </div>
      </header>

      {/* Introduction Section */}
      <section className="max-w-screen-2xl mx-auto py-32 px-6 md:px-12 grid grid-cols-1 md:grid-cols-12 gap-16 items-start bg-surface">
        <div className="md:col-span-5">
          <h2 className="font-serif text-4xl md:text-6xl text-primary leading-tight uppercase">A HOME WORTHY OF YOUR COLLECTION</h2>
        </div>
        <div className="hidden md:flex md:col-span-1 justify-center h-full">
          <div className="w-[1px] h-full bg-secondary/20"></div>
        </div>
        <div className="md:col-span-6 space-y-8">
          <p className="text-on-surface-variant font-sans text-lg leading-relaxed opacity-80">
            Beyond mere preservation, our storage facility is a sanctuary for the mechanical elite. Designed with architectural precision, every square inch of our space is curated to provide a neutral, temperature-stabilized environment that respects the engineering of your investment.
          </p>
          <p className="text-on-surface-variant font-sans text-lg leading-relaxed opacity-80">
            We believe that rare automobiles shouldn't just be parked—they should be curated. Our facility offers an atmosphere of silence, security, and absolute discretion for motorconnaisseurs who demand nothing less than perfection.
          </p>
        </div>
      </section>

      {/* Storage Tiers */}
      <section className="bg-surface-container-low py-32">
        <div className="max-w-screen-2xl mx-auto px-6 md:px-12">
          <div className="text-center mb-24">
            <h2 className="font-sans text-sm tracking-[0.3rem] uppercase text-secondary mb-2 font-bold">THE OPTIONS</h2>
            <h3 className="font-serif text-4xl text-primary uppercase">STORAGE TIERS</h3>
          </div>
          
          <div className="grid grid-cols-1 lg:grid-cols-3 gap-12">
            {/* Standard */}
            <div className="bg-surface p-0 flex flex-col group transition-all duration-500">
              <div className="h-64 overflow-hidden">
                <img alt="Standard Storage" className="w-full h-full object-cover grayscale group-hover:grayscale-0 transition-all duration-700" src="https://lh3.googleusercontent.com/aida-public/AB6AXuBZQJYfPZ2clZQI-LEqc7h93BB7kaTOFL6NcOnrfZJ6UwUdUBANjazvgNu4OoieYv53RwDuLZYFZwPY7a8nDzyAYo5dy_sZLNnxUVIjRwPCVxYwDCpB9wrm0wWqhYCS0HNp60jgH06MZzWw79q8R90CFAlksfdSUBCgdabdU7BQDggN7Vxt1j_nVboMKNdXumRjv3e3C_qYFaBpX1Msk0X1u70zQdJAtixUuuEoAeCepxwi6Np3MOtijhFavr14N7ZM08hyySJmXe-h" />
              </div>
              <div className="p-10 flex flex-col items-center text-center flex-grow border-b border-transparent group-hover:border-secondary/30 transition-colors">
                <span className="font-sans text-xs tracking-widest uppercase text-on-surface-variant mb-4 font-bold">STANDARD</span>
                <div className="font-serif text-3xl text-secondary mb-6">€500<span className="text-sm">/mo</span></div>
                <p className="font-sans text-sm text-on-surface-variant mb-10 leading-loose uppercase tracking-wider opacity-70">
                  Climate Control<br/>
                  —<br/>
                  Security 24/7
                </p>
                <button className="mt-auto border border-secondary text-secondary font-serif uppercase tracking-[0.1rem] text-[0.875rem] px-8 py-3 hover:bg-secondary hover:text-primary transition-all duration-300">INQUIRE</button>
              </div>
            </div>

            {/* Premium */}
            <div className="bg-surface p-0 flex flex-col group transition-all duration-500 relative">
              <div className="h-64 overflow-hidden">
                <img alt="Premium Storage" className="w-full h-full object-cover grayscale group-hover:grayscale-0 transition-all duration-700" src="https://lh3.googleusercontent.com/aida-public/AB6AXuD4wLnfAb4HngEEDa_tylvyvI5VsHh-l8V7Ekqce25J7pTEOUmxXTtJdUpvUHWjNg-HLK_QqSSsKbgNgESyGbRTvqieoWqeV2KTvEz0Wq8afSfIY0HC-3tyFCuIC2FN8QFVS2Nme8J8VN9IxCVZoULuM8pN-u8JIZDn4UV7Y3KoL0L-z-TYPfG0HgolutsblsB7CSL7W4NohkDHYbx5CTES0Z9bWBCFCe1MtnheybqjWuity9jflJR_wJZ9MfgfWKki0SDRV_a51LUk" />
              </div>
              <div className="p-10 flex flex-col items-center text-center flex-grow border-b border-transparent group-hover:border-secondary/30 transition-colors bg-secondary/5">
                <span className="font-sans text-xs tracking-widest uppercase text-on-surface-variant mb-4 font-bold">PREMIUM</span>
                <div className="font-serif text-3xl text-secondary mb-6">€850<span className="text-sm">/mo</span></div>
                <p className="font-sans text-sm text-on-surface-variant mb-10 leading-loose uppercase tracking-wider opacity-70 font-bold">
                  Climate Control<br/>
                  —<br/>
                  Security 24/7<br/>
                  —<br/>
                  Concierge Service
                </p>
                <button className="mt-auto bg-secondary text-primary px-8 py-3 font-serif uppercase tracking-[0.1rem] text-[0.875rem] hover:bg-secondary/90 transition-all duration-300">INQUIRE</button>
              </div>
            </div>

            {/* Connoisseur */}
            <div className="bg-surface p-0 flex flex-col group transition-all duration-500">
              <div className="h-64 overflow-hidden">
                <img alt="Connoisseur Storage" className="w-full h-full object-cover grayscale group-hover:grayscale-0 transition-all duration-700" src="https://lh3.googleusercontent.com/aida-public/AB6AXuAFtQp_6O4xxjEzt8-M3ZXGvJZP1tZErMvyH-ItS_VhYoCt8kahtJHMKIOtc6o1pDXMpuRWceXX5ms7AikaxE0zGZLuo_zH_EHkLHgKRnmLKRAB0x9uxXEyc1uU6St3fuFE7FZWPRr1CTc2XYGJDuGLCiWqdMvNSVBvBzcqnVvTHqI8oPePD7Hj4FcOMm8nHbNKZTYEILMDsyIyXDCSsNKl95R-4KyeWcUUBsfntbIPsfLkoSic1dAbP_mZ6HI_2H9KRZhTfH0edOkc" />
              </div>
              <div className="p-10 flex flex-col items-center text-center flex-grow border-b border-transparent group-hover:border-secondary/30 transition-colors">
                <span className="font-sans text-xs tracking-widest uppercase text-on-surface-variant mb-4 font-bold">CONNOISSEUR</span>
                <div className="font-serif text-3xl text-secondary mb-6">€1.200<span className="text-sm">/mo</span></div>
                <p className="font-sans text-sm text-on-surface-variant mb-10 leading-loose uppercase tracking-wider opacity-70">
                  Climate Control<br/>
                  —<br/>
                  Security 24/7<br/>
                  —<br/>
                  Concierge Service<br/>
                  —<br/>
                  Weekly Maintenance
                </p>
                <button className="mt-auto border border-secondary text-secondary font-serif uppercase tracking-[0.1rem] text-[0.875rem] px-8 py-3 hover:bg-secondary hover:text-primary transition-all duration-300">INQUIRE</button>
              </div>
            </div>
          </div>
        </div>
      </section>

      {/* Features Spec Grid */}
      <section className="bg-surface py-32 overflow-hidden">
        <div className="max-w-screen-2xl mx-auto px-6 md:px-12">
          <div className="mb-20">
            <h2 className="font-serif text-4xl text-primary uppercase tracking-tight">THE SANCTUARY STANDARD</h2>
            <div className="w-24 h-[1px] bg-secondary mt-6"></div>
          </div>
          <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-x-12 gap-y-20">
            <div className="space-y-6">
              <Shield className="w-12 h-12 text-secondary" />
              <div>
                <div className="font-serif text-3xl mb-1 text-primary">99.9% Up-time</div>
                <div className="font-sans text-[0.7rem] uppercase tracking-[0.2rem] text-on-surface-variant font-bold">24/7 SECURITY MONITORING</div>
              </div>
            </div>
            <div className="space-y-6">
              <Thermometer className="w-12 h-12 text-secondary" />
              <div>
                <div className="font-serif text-3xl mb-1 text-primary">18°C Stable</div>
                <div className="font-sans text-[0.7rem] uppercase tracking-[0.2rem] text-on-surface-variant font-bold">CLIMATE CONTROL PRECISION</div>
              </div>
            </div>
            <div className="space-y-6">
              <UserCheck className="w-12 h-12 text-secondary" />
              <div>
                <div className="font-serif text-3xl mb-1 text-primary">24/7 Access</div>
                <div className="font-sans text-[0.7rem] uppercase tracking-[0.2rem] text-on-surface-variant font-bold">PERSONAL CONCIERGE</div>
              </div>
            </div>
            <div className="space-y-6">
              <Settings className="w-12 h-12 text-secondary" />
              <div>
                <div className="font-serif text-3xl mb-1 text-primary">Weekly Detail</div>
                <div className="font-sans text-[0.7rem] uppercase tracking-[0.2rem] text-on-surface-variant font-bold">PREVENTATIVE MAINTENANCE</div>
              </div>
            </div>
          </div>
        </div>
      </section>

      {/* Testimonial Section */}
      <section className="bg-surface-container py-32 relative overflow-hidden">
        <div className="max-w-4xl mx-auto px-6 md:px-12 text-center relative z-10">
          <blockquote className="font-serif italic text-2xl md:text-4xl leading-snug text-primary mb-12">
            "A sanctuary where my collection breathes in silent elegance. The attention to detail is unparalleled, from the micro-climate control to the concierge's discretion."
          </blockquote>
          <div className="space-y-2">
            <div className="font-sans text-sm uppercase tracking-[0.25rem] font-bold text-secondary">ALEXANDER VON GLERION</div>
            <div className="font-sans text-[0.65rem] uppercase tracking-[0.15rem] text-on-surface-variant opacity-70 font-bold">1962 FERRARI 250 GTO</div>
          </div>
        </div>
      </section>

      <Footer />
    </main>
  );
};

export default StorageServicesPage;
