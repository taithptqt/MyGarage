import React from "react";
import Header from "@shared/components/Header";
import Footer from "@shared/components/Footer";
import { Link } from "react-router-dom";
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
    image: "https://www.hdcarwallpapers.com/walls/ferrari_f8_tributo_2019_4k_5k_2-HD.jpg"
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
      <section className="bg-surface py-12 px-8">
        <div className="max-w-screen-2xl mx-auto grid grid-cols-2 md:grid-cols-6 gap-12 items-center justify-items-center opacity-40">
          {brandLogos.map((brand) => (
            <div key={brand.id} className="w-full h-8 flex flex-col items-center justify-center group pointer-events-none">
              <img src={brand.src} alt="Brand" className="max-h-full max-w-[120px] object-contain grayscale brightness-0" />
            </div>
          ))}
        </div>
      </section>

      {/* Sales Studio Section */}
      <section className="bg-surface py-32 px-6 md:px-20">
        <div className="max-w-screen-2xl mx-auto">
          <div className="flex items-center justify-between mb-24">
            <h2 className="font-serif text-4xl md:text-6xl text-primary uppercase tracking-tight flex items-center gap-8">
              SALES STUDIO
              <ArrowRight className="text-secondary w-12 h-12 md:w-16 md:h-16 stroke-1" />
            </h2>
          </div>
          <div className="grid grid-cols-1 md:grid-cols-3 gap-12">
            {featuredCars.map((car) => (
              <div key={car.title} className="bg-surface-container-low p-1 transition-all duration-700 group cursor-pointer border-b border-transparent hover:border-secondary/30">
                <div className="aspect-[4/3] overflow-hidden bg-primary/5 relative">
                  <img src={car.image} alt={car.title} className="w-full h-full object-cover transition-transform duration-1000 group-hover:scale-105 grayscale group-hover:grayscale-0" />
                  <div className="absolute inset-0 bg-primary/0 group-hover:bg-primary/5 transition-colors duration-500" />
                </div>
                <div className="p-8 space-y-6 text-center md:text-left">
                  <span className="text-secondary text-[10px] font-bold tracking-[0.4em] uppercase block">{car.tag}</span>
                  <h3 className="font-serif text-2xl text-primary leading-tight uppercase">{car.title}</h3>
                  <div className="flex justify-center md:justify-start gap-6 text-[9px] font-bold text-primary/40 tracking-[0.2em] pt-6 uppercase italic">
                    <span>{car.year}</span>
                    <span>—</span>
                    <span>{car.mileage}</span>
                  </div>
                  <p className="text-secondary font-serif text-3xl pt-2">{car.price}</p>
                </div>
              </div>
            ))}
          </div>
          <div className="mt-20 text-center">
            <Link to="/parts-shop" onClick={() => window.scrollTo({ top: 0, behavior: 'smooth' })} className="text-[11px] font-bold tracking-[0.3em] text-primary hover:text-secondary uppercase transition-all relative group inline-block">
              VIEW ALL CARS FOR SALE
              <div className="absolute -bottom-2 left-0 w-full h-[1px] bg-secondary scale-x-50 group-hover:scale-x-100 transition-transform duration-500"></div>
            </Link>
          </div>
        </div>
      </section>

      {/* Storage & Events Sections with Botanical Layout */}
      <div className="bg-primary">
        {/* Storage Section */}
        <section className="flex flex-col md:flex-row overflow-hidden relative border-none">
          <div className="absolute inset-0 opacity-10 pointer-events-none botanical-pattern" />
          <div className="w-full md:w-1/2 h-[600px] md:h-[800px] overflow-hidden grayscale">
            <img src="https://mygarage.skaevt.com/wp-content/uploads/2024/11/Gentlemans_Garage_MG_2024-9.jpg" alt="Storage" className="w-full h-full object-cover" />
          </div>
          <div className="w-full md:w-1/2 flex flex-col justify-center p-12 md:p-32 z-10">
            <span className="text-secondary text-[10px] font-bold tracking-[0.5em] uppercase mb-8">SECURE FACILITY</span>
            <h2 className="font-serif text-5xl md:text-7xl lg:text-8xl text-surface mb-12 uppercase leading-[0.85] italic">STORAGE</h2>
            <p className="text-surface/60 max-w-lg mb-12 text-sm md:text-base leading-relaxed opacity-80">
              Our specially designed storage facilities ensure the optimal setting for your vehicle collection.
              With precise climate control and 24/7 security monitoring, your investment is in the safest hands.
            </p>
            <Link to="/storage" onClick={() => window.scrollTo({ top: 0, behavior: 'smooth' })} className="w-fit px-12 py-5 border border-secondary text-secondary text-[11px] font-bold tracking-[0.3em] hover:bg-secondary hover:text-primary transition-all uppercase block text-center mt-4">
              EXPLORE FACILITY
            </Link>
          </div>
        </section>

        {/* Events Section */}
        <section className="flex flex-col md:flex-row-reverse overflow-hidden relative border-none">
          <div className="absolute inset-0 opacity-10 pointer-events-none botanical-pattern-alt" />
          <div className="w-full md:w-1/2 h-[600px] md:h-[800px] overflow-hidden grayscale">
            <img src="https://mygarage.skaevt.com/wp-content/uploads/2024/11/Meeting_Garage_MG_2024-8-e1732700312672.jpg" alt="Events" className="w-full h-full object-cover" />
          </div>
          <div className="w-full md:w-1/2 flex flex-col justify-center p-12 md:p-32 z-10">
            <span className="text-secondary text-[10px] font-bold tracking-[0.5em] uppercase mb-8">EXCLUSIVE SPACES</span>
            <h2 className="font-serif text-5xl md:text-7xl lg:text-8xl text-surface mb-12 uppercase leading-[0.85] italic">
              MEETINGS <br /> & EVENTS
            </h2>
            <p className="text-surface/60 max-w-lg mb-12 text-sm md:text-base leading-relaxed opacity-80">
              Create unforgettable experiences in our unique surroundings. From intimate board meetings to large product launches
              - we offer tailored solutions that combine professional service with a passion for automobiles.
            </p>
            <Link to="/meetings-events" onClick={() => window.scrollTo({ top: 0, behavior: 'smooth' })} className="w-fit px-12 py-5 border border-secondary text-secondary text-[11px] font-bold tracking-[0.3em] hover:bg-secondary hover:text-primary transition-all uppercase block text-center mt-4">
              VIEW VENUES
            </Link>
          </div>
        </section>
      </div>

      {/* Partners Section */}
      <section className="bg-surface py-32 px-6 md:px-20">
        <div className="max-w-screen-2xl mx-auto text-center">
          <h2 className="font-serif text-4xl md:text-6xl text-primary mb-12 uppercase italic leading-tight">THE PARTNER <br /> NETWORK</h2>
          <p className="max-w-3xl mx-auto text-primary/40 text-sm md:text-base mb-24 leading-relaxed font-bold tracking-tight uppercase">
            MY GARAGE is proud to collaborate with the industry's leading specialists. Our partners are carefully selected
            for their dedication to quality and their expertise in everything from maintenance to financing.
          </p>
          <div className="grid grid-cols-2 md:grid-cols-5 gap-16 md:gap-24 items-center justify-items-center mb-24 grayscale opacity-60">
            {partnerLogos.map((logo, idx) => (
              <div key={idx} className="w-full flex flex-col items-center group transition-all duration-700 hover:opacity-100">
                <img src={logo} alt="Partner" className="max-h-12 w-auto object-contain" />
              </div>
            ))}
          </div>
          <div className="flex justify-center">
            <div className="w-12 h-12 border border-secondary flex items-center justify-center text-secondary group cursor-pointer hover:bg-secondary hover:text-primary transition-all">
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