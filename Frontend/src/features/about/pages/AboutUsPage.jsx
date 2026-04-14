import React from "react";
import Header from "@shared/components/Header";
import Footer from "@shared/components/Footer";

const AboutUsPage = () => {
  return (
    <main className="bg-surface">
      <Header />

      <section className="bg-primary-container min-h-[80vh] flex flex-col md:flex-row items-stretch overflow-hidden pt-[100px]">
        <div className="flex-1 flex flex-col justify-center px-8 md:px-20 py-24 order-2 md:order-1">
          <span className="font-sans text-xs tracking-[0.2em] text-surface/50 uppercase mb-8 font-bold">ABOUT US</span>
          <h1 className="font-serif italic text-5xl md:text-7xl text-surface leading-tight mb-12 max-w-2xl">
            We Don't Just Sell Cars. <br />
            We Curate Legacies.
          </h1>
          <p className="font-sans text-surface/80 text-lg leading-relaxed max-w-xl opacity-80">
            At MyGarage, we treat rare vehicles as more than mere machinery. They are physical manifestations of culture, art, and human ambition.
            Our sanctuary is dedicated to those who recognize that a car's true value lies in its provenance and the mechanical poetry of its heritage.
          </p>
        </div>
        <div className="flex-1 min-h-[400px] md:min-h-0 order-1 md:order-2 relative">
          <img
            className="absolute inset-0 w-full h-full object-cover grayscale"
            src="https://lh3.googleusercontent.com/aida-public/AB6AXuBDHSPMbmcjeTIeXEE32qEb62z4WxqG3pfRjC_HfQUWn-bgoo96t380MBz_vwwEVi-C6hpdGigD_yzEYD24f62cjTL_MfXojfj4n6lz4GNMoNsoNL2qNcRSPSrwPdOWpPu2-gCRsA8F7WIZN37kiAg4glmH4E0RFX63BizjIKHRAYm8i9wR6m7flZUOsokWECzb9Xpd2uqcBnHT6dR7_wQL7ddU5eXZGlruOBewhdaXWGzrNsNZmds__jn_JtFWX5nPWWCxwRZ5QT3O"
            alt="Curator inspecting car"
          />
        </div>
      </section>

      <section className="bg-surface py-32 px-8 md:px-20">
        <div className="max-w-screen-2xl mx-auto grid grid-cols-1 md:grid-cols-2 gap-20 items-center">
          <div className="space-y-8">
            <h2 className="font-serif text-5xl text-primary uppercase leading-tight">
              Founded on <br /> Obsession.
            </h2>
            <div className="space-y-6 font-sans text-primary/70 leading-relaxed text-lg">
              <p>
                The genesis of MyGarage began not in a boardroom, but in the grease-stained workshops of childhood dreams. Our founders shared one vision:
                create a space where significant automobiles are preserved with the reverence they deserve.
              </p>
              <p>
                What started as a private collection evolved into a globally recognized brokerage. We saw a gap in the market for a service that prioritized
                the narrative of a vehicle over the velocity of its sale.
              </p>
              <p>
                Today, MyGarage stands as a testament to mechanical art. We have brokered some of the most elusive examples of automotive history while keeping
                the same obsession that sparked our first engine decades ago.
              </p>
            </div>
          </div>
          <div className="h-[700px] relative overflow-hidden">
            <img
              className="w-full h-full object-cover grayscale-[20%]"
              src="https://lh3.googleusercontent.com/aida-public/AB6AXuDYR8OV7VKYc8ElRvaJqYYYXpokOBA6fRCl1mjPSXx7FviGEFyHW45IoYjcLK6q_4_dEz4iLQbckcsLFhxLhKEAF67p1gWsQxZnCZ87DiyEtop39mBtZP48K7AHtWg5EfhVDtCtA8jKzHc1ePFyCL1pN3cmJSout7b-Qh7Ykb8LRXIAPXBzqzZM_mYOxQBFHwKsAMV0NWjUwOIzygHvavrp6jL-EjF40_20F3PTXrYlzZWLxuV_TO3wih3cRJ21XQEJN1Rnz2TqcLnQ"
              alt="Founder with car"
            />
          </div>
        </div>
      </section>

      <section className="bg-surface-container py-32 px-8 md:px-20">
        <div className="max-w-4xl mx-auto">
          <h2 className="font-serif text-5xl text-primary mb-24 uppercase">Our Principles.</h2>
          <div className="space-y-24">
            <div className="flex flex-col md:flex-row gap-8 items-baseline">
              <span className="font-serif italic text-6xl text-secondary leading-none">01</span>
              <div className="flex-1">
                <h3 className="font-serif text-3xl mb-4 text-primary uppercase">Integrity Before Commission</h3>
                <p className="font-sans text-primary/70 text-lg opacity-80">We prioritize the right home for a vehicle over the fastest transaction, every single time.</p>
              </div>
            </div>
            <div className="flex flex-col md:flex-row gap-8 items-baseline">
              <span className="font-serif italic text-6xl text-secondary leading-none">02</span>
              <div className="flex-1">
                <h3 className="font-serif text-3xl mb-4 text-primary uppercase">Rarity Over Volume</h3>
                <p className="font-sans text-primary/70 text-lg opacity-80">Our collection includes only specimens that represent a peak in design or engineering.</p>
              </div>
            </div>
            <div className="flex flex-col md:flex-row gap-8 items-baseline">
              <span className="font-serif italic text-6xl text-secondary leading-none">03</span>
              <div className="flex-1">
                <h3 className="font-serif text-3xl mb-4 text-primary uppercase">Provenance Is Everything</h3>
                <p className="font-sans text-primary/70 text-lg opacity-80">Every car has a story; we document, verify, and preserve that historical thread.</p>
              </div>
            </div>
            <div className="flex flex-col md:flex-row gap-8 items-baseline">
              <span className="font-serif italic text-6xl text-secondary leading-none">04</span>
              <div className="flex-1">
                <h3 className="font-serif text-3xl mb-4 text-primary uppercase">Silence Is a Feature</h3>
                <p className="font-sans text-primary/70 text-lg opacity-80">Our sanctuary is a place of quiet reflection, allowing the mechanical details to speak for themselves.</p>
              </div>
            </div>
          </div>
        </div>
      </section>

      <section className="bg-primary-container py-24 px-8 md:px-20">
        <div className="max-w-screen-2xl mx-auto grid grid-cols-2 md:grid-cols-4 gap-12 text-center md:text-left">
          <div>
            <div className="font-serif text-5xl text-surface mb-2">2009</div>
            <div className="font-sans text-xs tracking-[0.2em] text-surface/50 uppercase font-bold">FOUNDED</div>
          </div>
          <div>
            <div className="font-serif text-5xl text-surface mb-2">EUR 400M+</div>
            <div className="font-sans text-xs tracking-[0.2em] text-surface/50 uppercase font-bold">CARS BROKERED IN VALUE</div>
          </div>
          <div>
            <div className="font-serif text-5xl text-surface mb-2">80+</div>
            <div className="font-sans text-xs tracking-[0.2em] text-surface/50 uppercase font-bold">COUNTRIES SERVED</div>
          </div>
          <div>
            <div className="font-serif text-5xl text-surface mb-2">3</div>
            <div className="font-sans text-xs tracking-[0.2em] text-surface/50 uppercase font-bold">CONTINENTS</div>
          </div>
        </div>
      </section>

      <section className="bg-surface py-32 px-8 md:px-20">
        <div className="max-w-screen-2xl mx-auto">
          <h2 className="font-serif text-5xl text-primary mb-20 text-center md:text-left uppercase">
            The People Behind <br /> the Passion
          </h2>
          <div className="grid grid-cols-1 md:grid-cols-3 gap-16">
            <div className="space-y-6">
              <div className="aspect-square bg-surface-container relative overflow-hidden">
                <img
                  className="w-full h-full object-cover grayscale hover:scale-105 transition-transform duration-700"
                  src="https://lh3.googleusercontent.com/aida-public/AB6AXuDmJzpj6cUAMcoQO-zufFusMEn-jjYMmEtKpEvJlMcrb3DbwiGHplRRwKzUy-Mni2WSvKhkWQgl2XADyFvD1mTCCB0d3atzmwpKUaAHpR1a5SxYvf2Dkf_hOzuOEfmjkOW-vAneoo2Yf9eCRBxwE1iNdmn_BAWawxcg7ZjfcATdDX4kvRuEEdJmPeGaq81N5pL6MLOjMRnt3RGO9Den_3r4WG0pknAfBAjaORv1a7CTNelZiZPSpEDTnMo2nxVGOY7_gjWdOO9nT6N4"
                  alt="Julian Vane"
                />
              </div>
              <div className="space-y-2">
                <h4 className="font-serif text-2xl text-primary">Julian Vane</h4>
                <p className="font-sans text-xs tracking-widest text-secondary uppercase font-bold">Chief Curator</p>
                <p className="font-sans text-primary/70 text-sm leading-relaxed opacity-80">
                  Specialist in post-war Italian design with over 20 years of experience in high-value appraisals.
                </p>
              </div>
            </div>
            <div className="space-y-6">
              <div className="aspect-square bg-surface-container relative overflow-hidden">
                <img
                  className="w-full h-full object-cover grayscale hover:scale-105 transition-transform duration-700"
                  src="https://lh3.googleusercontent.com/aida-public/AB6AXuAxRRoFRh_y1UR-bmFmCqZ0DnT4_XQ0BuFAcfZXLGjVyAoebmxrAFsCZQsYNf-4skJs7dZm-3GVw-4ragJllMwRia7otlUnfTArO_tLvLwFaVBszza4CW4RVttc5reroammGOsfWQ6-3MiYbR-SybXZirHV2aFsdnJ-TDEn7AmXByw8ME_pG3QOvFg0FyICHarurJPYa1POnQtcEQPYemohr4EXSj9rL-nPZgMoSfFtpvXbDJtjgd9FU1bgJUzJShHPVCm3MIDU2Rs4"
                  alt="Elena Rossi"
                />
              </div>
              <div className="space-y-2">
                <h4 className="font-serif text-2xl text-primary">Elena Rossi</h4>
                <p className="font-sans text-xs tracking-widest text-secondary uppercase font-bold">Acquisitions Director</p>
                <p className="font-sans text-primary/70 text-sm leading-relaxed opacity-80">Directing our global search for hidden barn finds and legendary competition cars since 2014.</p>
              </div>
            </div>
            <div className="space-y-6">
              <div className="aspect-square bg-surface-container relative overflow-hidden">
                <img
                  className="w-full h-full object-cover grayscale hover:scale-105 transition-transform duration-700"
                  src="https://lh3.googleusercontent.com/aida-public/AB6AXuCrXU3NBD8DbvJrVylM_xnn-O-sNKuWZxleJVmheOQMlfuYJjwCbJhho71R_svucGPPgnUmk8xGPEmmLvv3YOQjNNi6tvx1gVWQHeOU3uMNYL_XFvNmAPJpCJPqMmVK_LpaJUh7X8tgm6uDh9ABRgKVFUJ8d4ZZ9IOM03nhuUJxNM8o9yAz_tsCTZ5F3QRIoMQUCSZAdGU-NHCaKzBKTeID7SXaoHTAK87jeHLKK8guIx7TT8WWpcXk9J8ELiI4Fn1I6uQvlnabVSQr"
                  alt="Marcus Thorne"
                />
              </div>
              <div className="space-y-2">
                <h4 className="font-serif text-2xl text-primary">Marcus Thorne</h4>
                <p className="font-sans text-xs tracking-widest text-secondary uppercase font-bold">Technical Advisor</p>
                <p className="font-sans text-primary/70 text-sm leading-relaxed opacity-80">
                  Our master mechanic who oversees the mechanical integrity and maintenance of all stored vehicles.
                </p>
              </div>
            </div>
          </div>
        </div>
      </section>

      <section className="bg-surface-container py-24 px-8 md:px-20 border-y border-secondary/10">
        <div className="max-w-screen-2xl mx-auto flex flex-col items-center">
          <span className="font-sans text-xs tracking-[0.3em] text-primary/40 uppercase mb-12 font-bold">AS SEEN IN</span>
          <div className="flex flex-wrap justify-center items-center gap-12 md:gap-24 opacity-60">
            <span className="font-serif font-bold text-2xl text-secondary">Robb Report</span>
            <span className="font-serif italic text-2xl text-secondary">Octane</span>
            <span className="font-serif font-bold text-2xl text-secondary">Motor Trend</span>
            <span className="font-serif italic text-2xl text-secondary">Top Gear</span>
            <span className="font-serif font-bold text-2xl text-secondary">THE DRIVE</span>
          </div>
        </div>
      </section>

      <section className="bg-surface py-32 px-8 md:px-20 text-center">
        <div className="max-w-2xl mx-auto">
          <h2 className="font-serif text-5xl md:text-6xl text-primary mb-8 uppercase">
            Begin a <br /> Conversation.
          </h2>
          <p className="font-sans text-primary/70 text-lg mb-12 opacity-80">
            Whether you're looking to buy, consign, or store - we'd love to hear from you. Our specialists are ready to provide bespoke advice for your collection.
          </p>
          <div className="flex flex-col sm:flex-row justify-center gap-6">
            <button className="border border-secondary text-secondary px-10 py-4 font-sans text-xs uppercase tracking-widest hover:bg-secondary/10 transition-colors font-bold">
              VIEW INVENTORY
            </button>
            <button className="bg-secondary text-primary px-10 py-4 font-sans text-xs uppercase tracking-widest hover:bg-secondary/90 transition-colors font-bold">
              CONTACT US
            </button>
          </div>
        </div>
      </section>

      <Footer />
    </main>
  );
};

export default AboutUsPage;
