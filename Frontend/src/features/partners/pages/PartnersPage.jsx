import React from "react";
import Header from "@shared/components/Header";
import Footer from "@shared/components/Footer";

const PartnersPage = () => {
  const partners = [
    { name: "BENTLEY", division: "Heritage Division", italic: false },
    { name: "Rolls-Royce", division: "Bespoke Collective", italic: true },
    { name: "ASTON MARTIN", division: "Q Division", italic: false },
    { name: "PORSCHE", division: "Classic Partner", italic: false },
    { name: "RM SOTHEBY'S", division: "Auction Affiliate", italic: false },
    { name: "Ferrari", division: "Classiche Center", italic: true },
    { name: "MC LAREN", division: "Special Operations", italic: false },
    { name: "JAGUAR", division: "Heritage Trust", italic: false },
    { name: "BREITLING", division: "Chronometry", italic: false },
    { name: "Veuve Clicquot", division: "Lounge Partner", italic: true },
    { name: "SAVILLS", division: "Luxury Estates", italic: false },
    { name: "NETJETS", division: "Private Aviation", italic: false },
  ];

  return (
    <main className="bg-surface">
      <Header />
      
      {/* Header & Intro */}
      <section className="px-6 md:px-20 pt-56 pb-24 md:pb-32">
        <div className="max-w-screen-2xl mx-auto">
          <div className="grid grid-cols-1 md:grid-cols-12 gap-8 items-end">
            <div className="md:col-span-8">
              <span className="text-secondary font-sans uppercase tracking-[0.2em] text-xs mb-6 block font-bold">Our Affiliates</span>
              <h1 className="text-6xl md:text-8xl text-primary font-serif italic -tracking-tight leading-[0.9] uppercase">
                The Curated <br/> Ecosystem
              </h1>
            </div>
            <div className="md:col-span-4 md:pb-4">
              <p className="text-primary/70 font-sans text-lg leading-relaxed border-l border-secondary/30 pl-8 opacity-80">
                We collaborate exclusively with world-class specialists who share our uncompromising standards for architectural precision, heritage preservation, and mechanical excellence.
              </p>
            </div>
          </div>
        </div>
      </section>

      {/* Signature Component: Asymmetric Split */}
      <section className="px-6 md:px-20 mb-32 relative">
        <div className="max-w-screen-2xl mx-auto flex flex-col md:flex-row items-center">
          <div className="w-full md:w-3/5 aspect-square md:aspect-video bg-surface-container-low overflow-hidden relative">
            <img alt="Luxury Interior" className="w-full h-full object-cover grayscale hover:grayscale-0 transition-all duration-700" src="https://lh3.googleusercontent.com/aida-public/AB6AXuAwiaeCCw49Cy6KUftCF51T-9WzWME0l3YW9JWghMTBDEiGmGya1Cd41jzWp3gekKI4hzXHmTC_Xhz1cucCgyYCiEgdN0-SuILULE-BrsAGjuNWN-v_KXbs2Q21Xa-B2s4Ry9cxYWkHg1ErJxbIpopvMFgXZ5Hdf3IDMTyrZrJv23KexzTBzaXy82UT7BqYT4RqLtAzg4a81LXy0zKf52QN7B5VNr4DGvGQhw_PrfwYJu93NLU2RpvCuh7DGmr1Aick2Z5ZdVaKA7Dx" />
          </div>
          <div className="w-full md:w-2/5 bg-primary-container p-12 md:-ml-20 z-10 text-surface shadow-2xl">
            <h3 className="text-3xl font-serif italic text-secondary mb-6">A Shared Vision</h3>
            <p className="font-sans text-sm uppercase tracking-widest leading-loose text-surface/70 mb-8 font-bold">
              From master metalworkers to historic archivists, our partners represent the pinnacle of their respective crafts.
            </p>
            <button className="border border-secondary px-8 py-4 text-xs font-sans uppercase tracking-widest text-secondary hover:bg-secondary hover:text-primary transition-all duration-300 font-bold">
              Inquire About Alliance
            </button>
          </div>
        </div>
      </section>

      {/* Partner Grid */}
      <section className="bg-surface-container-low py-32 px-6 md:px-20">
        <div className="max-w-screen-2xl mx-auto">
          <div className="text-center mb-24">
            <h2 className="text-4xl md:text-5xl font-serif mb-4 uppercase text-primary italic">Strategic Alliances</h2>
            <div className="w-20 h-[1px] bg-secondary mx-auto"></div>
          </div>
          <div className="grid grid-cols-2 md:grid-cols-4 gap-0 border-t border-l border-secondary/10">
            {partners.map((partner, index) => (
              <div key={index} className="aspect-square flex flex-col items-center justify-center p-12 bg-surface border-r border-b border-secondary/10 group hover:bg-primary transition-all duration-500">
                <span className={`text-secondary font-serif text-3xl mb-2 opacity-80 group-hover:opacity-100 transition-opacity ${partner.italic ? 'italic' : ''}`}>
                  {partner.name}
                </span>
                <span className="text-[10px] font-sans uppercase tracking-widest text-primary/40 group-hover:text-secondary font-bold">
                  {partner.division}
                </span>
              </div>
            ))}
          </div>
        </div>
      </section>

      {/* Quote Section */}
      <section className="py-32 px-6 md:px-20 bg-surface">
        <div className="max-w-screen-2xl mx-auto flex flex-col items-center text-center">
          <div className="text-secondary text-5xl mb-8 font-serif opacity-30 italic">“</div>
          <h2 className="text-4xl md:text-5xl font-serif italic max-w-2xl leading-tight text-primary">
            "Excellence is never an accident. It is always the result of high intention, sincere effort, and intelligent execution."
          </h2>
          <div className="mt-12 w-[1px] h-24 bg-secondary"></div>
        </div>
      </section>

      <Footer />
    </main>
  );
};

export default PartnersPage;
