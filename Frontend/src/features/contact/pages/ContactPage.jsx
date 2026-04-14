import React from "react";
import Header from "@shared/components/Header";
import Footer from "@shared/components/Footer";

const ContactPage = () => {
  return (
    <main className="bg-surface">
      <Header />

      <div className="pt-56 pb-0">
        <section className="max-w-[1440px] mx-auto px-6 md:px-20 grid grid-cols-1 md:grid-cols-12 gap-20 mb-32">
          <div className="md:col-span-5 space-y-16">
            <div className="space-y-6">
              <span className="gold-label">Establish Connection</span>
              <h1 className="newsreader-serif text-5xl md:text-7xl text-primary leading-tight">
                Get in <br /> Touch
              </h1>
            </div>
            <div className="space-y-12">
              <div className="group">
                <p className="gold-label mb-3">Headquarters</p>
                <address className="not-italic text-lg text-on-surface-variant font-sans font-light leading-relaxed max-w-xs">
                  WITTRUPVEJ 1,
                  <br />
                  7120 VEJLE EAST,
                  <br />
                  DENMARK
                </address>
              </div>
              <div className="group">
                <p className="gold-label mb-3">Direct Correspondence</p>
                <div className="flex flex-col space-y-2">
                  <a className="text-xl text-primary hover:text-secondary transition-colors duration-300 font-serif" href="tel:+4569133911">
                    +45 69 133 911
                  </a>
                  <a className="text-xl text-primary hover:text-secondary transition-colors duration-300 font-serif" href="mailto:info@mygarage.dk">
                    info@mygarage.dk
                  </a>
                </div>
              </div>
              <div className="pt-8 border-t border-secondary/20 max-w-xs">
                <p className="text-on-surface-variant font-serif text-sm leading-relaxed italic opacity-70">
                  "The collector is an artist in their own right, curating the silence between the objects."
                </p>
              </div>
            </div>
          </div>

          <div className="md:col-span-7 bg-surface-container-low p-8 md:p-16 relative">
            <div className="absolute -top-4 -left-4 w-24 h-24 bg-secondary/5 -z-10"></div>
            <form className="space-y-12">
              <div className="grid grid-cols-1 md:grid-cols-2 gap-12">
                <div className="relative">
                  <label className="block text-primary font-sans text-[10px] uppercase tracking-widest mb-2 font-bold">Legal Name</label>
                  <input
                    className="w-full bg-transparent border-b border-primary/20 py-3 text-primary font-sans focus:border-secondary transition-colors outline-none px-0 placeholder:text-primary/20"
                    placeholder="e.g. Julian Vane"
                    type="text"
                  />
                </div>
                <div className="relative">
                  <label className="block text-primary font-sans text-[10px] uppercase tracking-widest mb-2 font-bold">Electronic Mail</label>
                  <input
                    className="w-full bg-transparent border-b border-primary/20 py-3 text-primary font-sans focus:border-secondary transition-colors outline-none px-0 placeholder:text-primary/20"
                    placeholder="name@domain.com"
                    type="email"
                  />
                </div>
              </div>
              <div className="relative">
                <label className="block text-primary font-sans text-[10px] uppercase tracking-widest mb-2 font-bold">Nature of Inquiry</label>
                <select className="w-full bg-transparent border-b border-primary/20 py-3 text-primary font-sans focus:border-secondary transition-colors outline-none px-0 appearance-none cursor-pointer">
                  <option>Acquisition Request</option>
                  <option>Private Collection Valuation</option>
                  <option>Exhibition Inquiry</option>
                  <option>Press &amp; Media</option>
                </select>
              </div>
              <div className="relative">
                <label className="block text-primary font-sans text-[10px] uppercase tracking-widest mb-2 font-bold">Message</label>
                <textarea
                  className="w-full bg-transparent border-b border-primary/20 py-3 text-primary font-sans focus:border-secondary transition-colors outline-none px-0 placeholder:text-primary/20 resize-none"
                  placeholder="Describe the architectural entity or service required..."
                  rows="4"
                ></textarea>
              </div>
              <div className="pt-4">
                <button className="inline-block px-12 py-5 border border-secondary text-secondary font-sans uppercase tracking-[0.2em] text-xs hover:bg-secondary hover:text-primary transition-all duration-300 active:scale-[0.98]" type="submit">
                  Submit Inquiry
                </button>
              </div>
            </form>
          </div>
        </section>

        <section className="w-full h-[600px] bg-surface-container relative overflow-hidden">
          <img
            alt="Location Map"
            className="w-full h-full object-cover grayscale opacity-60 mix-blend-multiply"
            src="https://lh3.googleusercontent.com/aida-public/AB6AXuBOhE2JruU8k0fuqs7WHeMwCa0bZhpiK5hdOiA7fe1qE1FZ2BU2HYRn_owTpN1Y9q76KwxAcd444aDRXRMBTIEU8sPLK7GCkvm0SbzrZponc4ZtxL9taiKz3D_e4REkieSChlIEIDWdTFxEhp6L8YrSok3pMOjfST252LgXh_tA_cMBTPMNi7Sombtxy07v5ukIZ91U79WRiQZ5sTvHiBbeyYNIxArFVxedjRRiGWXrZGswOHY7pWPqAaua-C7N4TJjpeDGyd92kIP2"
          />
          <div className="absolute inset-0 bg-primary/5 pointer-events-none"></div>
          <div className="absolute top-1/2 left-1/2 -translate-x-1/2 -translate-y-1/2 flex flex-col items-center">
            <div className="w-4 h-4 bg-secondary"></div>
            <div className="mt-4 bg-white/90 backdrop-blur-md px-6 py-4 shadow-2xl">
              <p className="font-sans text-[10px] uppercase tracking-widest text-primary font-bold">THE SANCTUARY</p>
              <p className="font-sans text-xs text-on-surface-variant">Vejle, Denmark</p>
            </div>
          </div>
        </section>
      </div>

      <Footer />
    </main>
  );
};

export default ContactPage;
