import React from "react";
import Header from "@shared/components/Header";
import Footer from "@shared/components/Footer";
import { ArrowRight, ChevronDown } from "lucide-react";
import "./HomePage.css";

const brandLogos = [
  { id: 1, src: "https://mygarage.skaevt.com/wp-content/uploads/2025/05/Corvette-logo-guld-uden-tekst.png" },
  { id: 2, src: "https://mygarage.skaevt.com/wp-content/uploads/2020/02/RR-logo-graa-1.png" },
  { id: 3, src: "https://mygarage.skaevt.com/wp-content/uploads/2025/12/logo.png" },
  { id: 4, src: "https://mygarage.skaevt.com/wp-content/uploads/2021/02/KALMAR_logo_KOKS.nobcg_.png" },
  { id: 5, src: "https://mygarage.skaevt.com/wp-content/uploads/2021/08/Dallara-logo-graa-1.png" },
  { id: 6, src: "https://mygarage.skaevt.com/wp-content/uploads/2021/05/Morgan-Vejle-Brand-Mark_Colour-GRAa-1.png" },
];

const featuredCars = [
  {
    title: "LAMBORGHINI SVJ ROADSTER",
    tag: "COLLECTOR'S EDITION",
    year: "2022",
    mileage: "850 KM",
    price: "DKK 5,249,000",
    image: "https://lh3.googleusercontent.com/aida-public/AB6AXuCE6YbYm9CR7y5pZNva-pIcybRljJdDQqJjCDWNJcUtUjpfmNUoqOWLnhQslY8HuJSNHdSHa0BEv-A69rBtHOZQwyskGFYBGCQDg7kX-GWOkLBk1WECRdqUh1r9ahKXYkSZC-SkodfkKx-17ef8SdNHPvflSpWikis_7sBYAk_Dil931npbmQPBHaNaHcpgzpwakwlqltkl0qy5Lfr4idQ2DPVZ6B5z-F29nLlNETVVAVMNaZUuiFIp46IqEblV2nG4HDLBCPT5SMwf"
  },
  {
    title: "PORSCHE 911 (992) GT3",
    tag: "AVAILABLE NOW",
    year: "2023",
    mileage: "1,250 KM",
    price: "DKK 4,749,000",
    image: "https://lh3.googleusercontent.com/aida-public/AB6AXuBt7JDMNFEDF5iRjfXCPAusiogk9iunAh9RYgBC3I3okl_xoznueSXaOKJGswioEk8D9xGb7R81Opp_fhU-F8ArT53LWfyiUaR5pnccbZW4c1YwIyI-Mtxlm5U9AUksVrEZUfudQJG7pVFiDk_19vd1RtQKb25gydOb5hQYyjUAsk4pIevEbX7CEGhS5YjXtjoAlBakIWMgn5k7Fla01ljQ9QcC_ap9FLvEfmUKgMiI-7pSXYvSQoRyzFYlu7shO2urGwiCSZhnxrxf"
  },
  {
    title: "FERRARI F8 TRIBUTO",
    tag: "PRIVATE COLLECTION",
    year: "2021",
    mileage: "3,400 KM",
    price: "DKK 4,249,000",
    image: "https://lh3.googleusercontent.com/aida-public/AB6AXuDfpqF1kvCrN9Oaxo6Yqg7IBm2dsPuLry0JiyriOSY3C7sLgDe1-65KDMRxbtIjGLl7-7ZuSOjbFfhiHuofR4PoI94JvDfqf67R4F6eRx6PRq9uoE9hutVIxqliJwZvsyvN5zGMqI69-rop3D6juzphI_kWj48OgShrqEaBmFGbCa1XkE8fi-BXmcemb6JP8EN7leTM3fX_XXLSdGGVxj2ldYoFVkCcZRsF89pFYzi6uZ5RwTl8Ux49HmNLCs2bjhyikYj9wKnzdku3"
  },
];

const partnerLogos = [
  "/assets/partners/morgan.png",
  "/assets/partners/encor.png",
  "/assets/partners/corvette.png",
  "/assets/partners/kalmar.png",
  "/assets/partners/dallara.png",
  "/assets/partners/lotus.png",
  "/assets/partners/rolls-royce.png",
];

function HomePage() {
  return (
    <main className="stitch-home">
      <Header overlay />

      {/* Hero Section */}
      <section className="relative h-screen bg-black overflow-hidden flex flex-col justify-end">
        <video
          className="absolute inset-0 w-full h-full object-cover opacity-60"
          autoPlay
          muted
          loop
          playsInline
        >
          <source src="/hero.mp4" type="video/mp4" />
        </video>
        <div className="absolute inset-0 bg-gradient-to-t from-black/80 via-transparent to-black/40" />
      </section>

      {/* Brand Strip */}
      <section className="bg-garage-cream/30 py-12 px-8">
        <div className="max-w-[1440px] mx-auto grid grid-cols-2 md:grid-cols-6 gap-12 items-center justify-items-center opacity-40">
          {brandLogos.map((brand) => (
            <div key={brand.id} className="w-full h-8 flex flex-col items-center justify-center group pointer-events-none">
              <img src={brand.src} alt="Brand" className="max-h-full max-w-[120px] object-contain grayscale brightness-0" />
              <div className="w-0 group-hover:w-full h-[1px] bg-garage-gold mt-4 transition-all duration-300" />
            </div>
          ))}
        </div>
      </section>

      {/* Sales Studio Section */}
      <section className="bg-garage-cream py-32 px-8">
        <div className="max-w-[1440px] mx-auto">
          <div className="flex items-center justify-between mb-20">
            <h2 className="font-serif text-3xl md:text-5xl lg:text-6xl text-garage-green uppercase tracking-tight flex items-center gap-6">
              SELECTED CARS FROM OUR SALES STUDIO
              <ArrowRight className="text-garage-gold w-10 h-10 md:w-16 md:h-16 stroke-1" />
            </h2>
          </div>
          <div className="grid grid-cols-1 md:grid-cols-3 gap-1 px-1">
            {featuredCars.map((car) => (
              <div key={car.title} className="bg-white p-1 shadow-sm hover:shadow-2xl transition-all duration-700 group cursor-pointer border border-transparent hover:border-garage-gold/20">
                <div className="aspect-[4/3] overflow-hidden bg-garage-green/5 relative">
                  <img src={car.image} alt={car.title} className="w-full h-full object-cover transition-transform duration-1000 group-hover:scale-110" />
                  <div className="absolute inset-0 bg-black/0 group-hover:bg-black/10 transition-colors duration-500" />
                </div>
                <div className="p-8 space-y-4">
                  <span className="text-garage-gold text-[10px] font-bold tracking-[0.4em] uppercase block">{car.tag}</span>
                  <h3 className="font-serif text-xl md:text-2xl text-garage-green leading-tight">{car.title}</h3>
                  <div className="flex gap-4 text-[9px] font-bold text-garage-green/40 tracking-[0.2em] pt-4 border-t border-garage-green/5">
                    <span>{car.year}</span>
                    <span>{car.mileage}</span>
                  </div>
                  <p className="text-garage-gold font-serif text-2xl pt-2">{car.price}</p>
                </div>
              </div>
            ))}
          </div>
          <div className="mt-20">
            <button className="text-[11px] font-bold tracking-[0.3em] text-garage-green hover:text-garage-gold border-b-2 border-garage-gold pb-2 transition-all">
              VIEW ALL CARS FOR SALE
            </button>
          </div>
        </div>
      </section>

      {/* Storage & Events Sections with Botanical Layout */}
      <div className="botanical-container">
        {/* Storage Section */}
        <section className="flex flex-col md:flex-row bg-garage-green overflow-hidden relative">
          <div className="absolute inset-0 opacity-10 pointer-events-none botanical-pattern" />
          <div className="w-full md:w-1/2 h-[600px] md:h-[800px] overflow-hidden">
            <img src="/assets/storage-bg.png" alt="Storage" className="w-full h-full object-cover scale-110" />
          </div>
          <div className="w-full md:w-1/2 flex flex-col justify-center p-12 md:p-32 z-10">
            <span className="text-garage-gold text-[10px] font-bold tracking-[0.5em] uppercase mb-8">SECURE FACILITY</span>
            <h2 className="font-serif text-5xl md:text-7xl lg:text-8xl text-garage-cream mb-12 uppercase leading-[0.85]">STORAGE</h2>
            <p className="text-garage-cream/60 max-w-lg mb-12 text-sm md:text-base leading-relaxed">
              Our specially designed storage facilities ensure the optimal setting for your vehicle collection.
              With precise climate control and 24/7 security monitoring, your investment is in the safest hands.
            </p>
            <button className="w-fit px-12 py-5 border border-garage-gold text-garage-gold text-[11px] font-bold tracking-[0.3em] hover:bg-garage-gold hover:text-garage-green transition-all uppercase">
              READ MORE
            </button>
          </div>
        </section>

        {/* Events Section */}
        <section className="flex flex-col md:flex-row-reverse bg-garage-green overflow-hidden relative border-t border-white/5">
          <div className="absolute inset-0 opacity-10 pointer-events-none botanical-pattern-alt" />
          <div className="w-full md:w-1/2 h-[600px] md:h-[800px] overflow-hidden">
            <img src="/assets/lounge-bg.png" alt="Events" className="w-full h-full object-cover" />
          </div>
          <div className="w-full md:w-1/2 flex flex-col justify-center p-12 md:p-32 z-10">
            <span className="text-garage-gold text-[10px] font-bold tracking-[0.5em] uppercase mb-8">EXCLUSIVE SPACES</span>
            <h2 className="font-serif text-5xl md:text-7xl lg:text-8xl text-garage-cream mb-12 uppercase leading-[0.85]">
              MEETINGS & <br /> EVENTS
            </h2>
            <p className="text-garage-cream/60 max-w-lg mb-12 text-sm md:text-base leading-relaxed">
              Create unforgettable experiences in our unique surroundings. From intimate board meetings to large product launches
              - we offer tailored solutions that combine professional service with a passion for automobiles.
            </p>
            <button className="w-fit px-12 py-5 border border-garage-gold text-garage-gold text-[11px] font-bold tracking-[0.3em] hover:bg-garage-gold hover:text-garage-green transition-all uppercase">
              READ MORE
            </button>
          </div>
        </section>
      </div>

      {/* Partners Section */}
      <section className="bg-garage-cream py-32 px-8">
        <div className="max-w-[1200px] mx-auto text-center">
          <h2 className="font-serif text-4xl md:text-6xl text-garage-green mb-8 uppercase">MEET OUR PARTNERS</h2>
          <p className="max-w-3xl mx-auto text-garage-green/40 text-sm md:text-base mb-24 leading-relaxed font-bold tracking-tight">
            MY GARAGE is proud to collaborate with the industry's leading specialists. Our partners are carefully selected
            for their dedication to quality and their expertise in everything from maintenance to financing.
          </p>
          <div className="grid grid-cols-2 md:grid-cols-5 gap-16 md:gap-24 items-center justify-items-center mb-20">
            {partnerLogos.map((logo, idx) => (
              <div key={idx} className="w-full flex flex-col items-center group">
                <img src={logo} alt="Partner" className="max-h-12 w-auto object-contain grayscale opacity-60 group-hover:opacity-100 transition-all duration-500" />
                <div className="w-12 h-[1px] bg-garage-green/20 mt-8 group-hover:w-full transition-all duration-700" />
              </div>
            ))}
          </div>
          <div className="flex justify-center">
            <div className="w-12 h-12 rounded-full border border-garage-gold flex items-center justify-center text-garage-gold group cursor-pointer hover:bg-garage-gold hover:text-garage-green transition-all">
              <ChevronDown className="w-6 h-6 group-hover:translate-y-1 transition-transform" />
            </div>
          </div>
        </div>
      </section>

      <Footer />
    </main>
  );
}

export default HomePage;
