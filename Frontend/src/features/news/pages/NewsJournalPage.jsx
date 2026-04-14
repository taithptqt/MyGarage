import React from "react";
import Header from "@shared/components/Header";
import Footer from "@shared/components/Footer";
import { Link } from "react-router-dom";
import { ArrowRight } from "lucide-react";

const NewsJournalPage = () => {
  return (
    <main className="bg-surface">
      <Header />
      
      {/* Section 1: Featured Story */}
      <section className="relative w-full h-[921px] flex items-center overflow-hidden bg-primary">
        <div className="absolute inset-0 z-0">
          <img alt="Featured story car" className="w-full h-full object-cover grayscale brightness-75" src="https://lh3.googleusercontent.com/aida-public/AB6AXuBkP4afV4OsLHCgw0aRm9xiyakhvh4s1fmSBZKyxKtssZic8-Zr6WXq624YyVwhthEd0emibwU-4Vn6L2ajdy6zvJJYz5bXFJuxpww4neZVEzuLWssCrnWTjcU2GoNFY2Xv-tgjw45gKvzPdgv5OaoNbSttkBRRfP9zZ7opTmacPbxbQKSR8gS2E7hn4C-Jvj7s03ymSUflOsmpoQGrWwqUlGzOTChAtNg7lJPu7nfbRr1UFDnlDSoohTIHx3dZq3R8V61RFqTTFiCL" />
        </div>
        <div className="relative z-10 px-6 md:px-20 w-full max-w-screen-2xl mx-auto">
          <div className="max-w-2xl bg-surface/85 backdrop-blur-md p-10 md:p-16 border-l-4 border-secondary">
            <span className="text-secondary font-sans uppercase tracking-widest text-xs mb-4 block font-bold">Journal Feature — Vol. 04</span>
            <h1 className="text-5xl md:text-7xl font-serif text-primary mb-8 leading-tight italic">The Art of <br /> the Drive</h1>
            <p className="text-on-surface-variant font-sans text-lg leading-relaxed mb-10 max-w-md opacity-80">
              A deep dive into the intersection of mid-century aesthetics and automotive engineering. Why the garage has become the modern sanctuary.
            </p>
            <Link className="inline-block px-10 py-4 border border-secondary text-secondary font-sans uppercase tracking-widest text-xs hover:bg-secondary hover:text-primary transition-all duration-300 font-bold" to="/news">
              Explore Chapter
            </Link>
          </div>
        </div>
      </section>

      {/* Section 2: Recent Updates */}
      <section className="bg-surface py-32 px-6 md:px-20">
        <div className="max-w-screen-2xl mx-auto">
          <div className="flex justify-between items-end mb-20">
            <div>
              <h2 className="text-5xl font-serif text-primary mb-4 italic uppercase">The Feed</h2>
              <div className="h-[1px] w-24 bg-secondary"></div>
            </div>
            <div className="text-secondary font-sans uppercase tracking-widest text-xs font-bold hidden md:block">
              Refined Perspective
            </div>
          </div>
          
          <div className="grid grid-cols-1 md:grid-cols-2 gap-x-16 gap-y-24">
            {/* Article Card 1 */}
            <article className="group cursor-pointer">
              <div className="aspect-square bg-surface-container-low mb-8 overflow-hidden">
                <img alt="Patina preservation" className="w-full h-full object-cover transition-transform duration-700 group-hover:scale-105" src="https://lh3.googleusercontent.com/aida-public/AB6AXuA70PeUHTfCu0wCw8d-MVCx7fyoXDU95zURoSaGIIyGakMCxZK5SFQvKUyCAuzhQL4i9XIOcWbxVURkbzvBqOl9cKZX3WWPFLz9j_38-OXfw8muZ5PqSSJ_zOi7pG7u3HT7MtZ5QsfsdkWTccsd8c_Cw6DZ6BEgowyPYdj1MVZPYWyvTImuv4npwfXefV1Xfz5rguttPePJl9d4WetoPg5URllvvEQH7UbAqY2MLyYjGiRLaZcpVTUtlX1LsSGK8tbuOFLsy_UVN46S" />
              </div>
              <div className="flex items-center space-x-4 mb-4">
                <span className="text-secondary font-sans uppercase tracking-widest text-[10px] font-bold">Oct 24, 2024</span>
                <span className="h-[1px] w-8 bg-secondary/20"></span>
                <span className="text-on-surface-variant font-sans uppercase tracking-widest text-[10px] font-bold">Restoration</span>
              </div>
              <h3 className="text-3xl font-serif text-primary mb-6 group-hover:text-secondary transition-colors duration-300 leading-tight">The Patina Preservation Debate</h3>
              <p className="text-on-surface-variant font-sans leading-relaxed mb-8 opacity-80">
                Should one restore to factory perfection or honor the history written in every scratch? We consult with master restorers from the Italian coast.
              </p>
              <div className="flex items-center space-x-2 text-secondary font-sans uppercase tracking-widest text-xs font-bold group-hover:translate-x-2 transition-transform duration-300">
                <span>Read More</span>
                <ArrowRight size={14} />
              </div>
            </article>

            {/* Article Card 2 */}
            <article className="group cursor-pointer">
              <div className="aspect-square bg-surface-container-low mb-8 overflow-hidden">
                <img alt="Glass box architecture" className="w-full h-full object-cover transition-transform duration-700 group-hover:scale-105" src="https://lh3.googleusercontent.com/aida-public/AB6AXuCyc7UMDSnZaFvNFZhGkyKCeq5jGTfFojxJW2ztchqgS8pqGSYVvhrgYh8-wjG_NMjUX-FOFBSn3HUk7EzJuewI8H76Oz0FzdGGTSPy01M73tJdKiU89EuMzvULXV3pXUzatJKHWbvv5rfwTb30baC064lYfwylvfQuutdpPXSnKfudohlB2SlY5KxBlj8u_Shgecen8c7CNzeyXOw8F1qCcKoPg_C6CffbNDYIkaxHk3h6kdfu1uxykcNqwxjKvfjuEmAB5v6x4WPy" />
              </div>
              <div className="flex items-center space-x-4 mb-4">
                <span className="text-secondary font-sans uppercase tracking-widest text-[10px] font-bold">Oct 18, 2024</span>
                <span className="h-[1px] w-8 bg-secondary/20"></span>
                <span className="text-on-surface-variant font-sans uppercase tracking-widest text-[10px] font-bold">Architecture</span>
              </div>
              <h3 className="text-3xl font-serif text-primary mb-6 group-hover:text-secondary transition-colors duration-300 leading-tight">The Glass Box Phenomenon</h3>
              <p className="text-on-surface-variant font-sans leading-relaxed mb-8 opacity-80">
                Exploring the shift from hidden garages to transparent exhibits. How residential design is evolving to showcase automotive collections.
              </p>
              <div className="flex items-center space-x-2 text-secondary font-sans uppercase tracking-widest text-xs font-bold group-hover:translate-x-2 transition-transform duration-300">
                <span>Read More</span>
                <ArrowRight size={14} />
              </div>
            </article>
          </div>
        </div>
      </section>

      {/* Signature Component: Asymmetric Split */}
      <section className="py-24 bg-surface-container-low overflow-hidden">
        <div className="max-w-screen-2xl mx-auto px-6 md:px-20 flex flex-col md:flex-row items-center">
          <div className="w-full md:w-[65%] z-0 relative">
             <div className="absolute -top-10 -left-10 w-40 h-40 bg-secondary/5 -z-10"></div>
            <img alt="Collector manifesto car" className="w-full aspect-video object-cover grayscale" src="https://lh3.googleusercontent.com/aida-public/AB6AXuB0ObIG4l8Ut-1zYaZV19QnULKBNz8AenzN7vPd1rfFiNp5a741jM1H4tcZhjgOlis9-zdi_mmRZmbU1KBQFs7RGNH2zSd5WZZAEKsVlZE2HtGpYT8aR7dkH_FjoIX7MsOXyR8a4gNrFfh1hg0EqUiSIi7zLIf7C1yuKRFN0NVIf3Y_K4vDMTiB7ETcrOFL5b5HoWtdfEkdB3AuJ-nkvGG_cUXVkIIjrh-hyiGVG3Yy_4bpgW0a5lFvGvEG1vyhEtSJ5nzNLRwBldiD" />
          </div>
          <div className="w-full md:w-[40%] md:-ml-24 z-10 bg-primary-container p-12 md:p-16 text-surface shadow-2xl">
            <h2 className="text-4xl font-serif mb-6 text-secondary italic">The Collector's Manifesto</h2>
            <p className="font-sans text-surface/70 leading-relaxed mb-8 text-lg">
              "We do not own these machines. We simply look after them for the next generation of enthusiasts who understand that design is eternal."
            </p>
            <div className="h-[1px] w-12 bg-secondary mb-4"></div>
            <p className="font-sans uppercase tracking-widest text-[10px] text-secondary font-bold">Founder's Note</p>
          </div>
        </div>
      </section>

      {/* Newsletter Section */}
      <section className="bg-surface py-32 px-6 md:px-20 text-center">
        <div className="max-w-2xl mx-auto border border-secondary/30 p-10 md:p-20 relative">
          <div className="absolute top-0 left-0 w-12 h-[1px] bg-secondary -translate-y-1/2 translate-x-1/2"></div>
          <div className="absolute bottom-0 right-0 w-12 h-[1px] bg-secondary translate-y-1/2 -translate-x-1/2"></div>
          
          <h2 className="text-4xl md:text-5xl font-serif text-primary mb-6 italic">Dispatch from <br /> the Sanctuary</h2>
          <p className="text-on-surface-variant font-sans mb-12 opacity-80 decoration-secondary/30 underline underline-offset-8">Monthly architectural and automotive insights delivered to your inbox.</p>
          <form className="flex flex-col space-y-8">
            <div className="relative">
              <input className="w-full bg-transparent border-b border-secondary/30 py-4 px-0 focus:border-secondary transition-colors text-primary placeholder:text-primary/20 font-sans uppercase tracking-widest text-xs outline-none" placeholder="Email Address" type="email" />
            </div>
            <button className="w-full py-5 bg-primary text-surface font-sans uppercase tracking-widest text-xs hover:bg-secondary hover:text-primary transition-all duration-300 font-bold" type="submit">
              Join the Archives
            </button>
          </form>
        </div>
      </section>

      <Footer />
    </main>
  );
};

export default NewsJournalPage;
