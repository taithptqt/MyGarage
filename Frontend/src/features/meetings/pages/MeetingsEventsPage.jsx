import React from "react";
import Header from "@shared/components/Header";
import Footer from "@shared/components/Footer";
import { Link } from "react-router-dom";
import { ArrowDown, Quote } from "lucide-react";

const MeetingsEventsPage = () => {
  return (
    <main className="bg-surface">
      <Header />
      
      {/* HERO SECTION */}
      <section className="min-h-[921px] flex flex-col justify-center items-center text-center px-6 relative overflow-hidden pt-32 bg-primary">
        <div className="absolute inset-0 opacity-40 mix-blend-overlay">
          <img className="w-full h-full object-cover" src="https://lh3.googleusercontent.com/aida-public/AB6AXuBl5k4p0SP_GO3YcO7p9YVtDNYs52T32IWr-VhKv33TNnWWvmX2q3L7lBMWEmo-0j40IOKCBLV8-1lxSFkNRiPXdlRX4BR3hfgFbDcXhrhpjcw8SgFEaP4W1P7Z2EgiqrcjayWZxAoXctU9DfSSls9UWZb2qht95P9A3vb0-SX6DoiQQZdeewrihbVlGgj0CMaKznK0KbWPoSEPaharMozGW3Aw5cHRJaZbAOC5fCqE8i1YBPWd7EiRQ_ZMd8UExla67Eud8V0V9D_1" alt="Meetings hero" />
        </div>
        <div className="relative z-10 max-w-6xl mx-auto">
          <span className="text-secondary font-sans uppercase tracking-[0.3em] text-sm mb-6 block font-bold">Curated Gatherings</span>
          <h1 className="text-white font-serif text-6xl md:text-8xl italic mb-12 tracking-tighter leading-none">MEETINGS AND <br /> EVENTS</h1>
          <div className="flex flex-col md:flex-row gap-6 justify-center items-center">
            <Link to="/booking" className="border border-secondary text-secondary font-sans uppercase px-12 py-4 tracking-widest hover:bg-secondary hover:text-primary transition-all duration-300 block text-center">Book Meeting</Link>
            <Link to="/news" className="border border-secondary text-secondary font-sans uppercase px-12 py-4 tracking-widest hover:bg-secondary hover:text-primary transition-all duration-300 block text-center">More About Events</Link>
            <Link to="/contact" className="border border-secondary text-secondary font-sans uppercase px-12 py-4 tracking-widest hover:bg-secondary hover:text-primary transition-all duration-300 block text-center">Inquire</Link>
          </div>
        </div>
      </section>

      {/* MEETINGS SECTION */}
      <section className="py-32 px-6 md:px-24 bg-surface">
        <div className="grid grid-cols-1 md:grid-cols-12 gap-16 mb-24 items-end max-w-screen-2xl mx-auto">
          <div className="md:col-span-7">
            <h2 className="font-serif text-4xl md:text-6xl text-primary leading-none uppercase mb-8">MEETINGS FROM 2 TO 20 PEOPLE</h2>
            <p className="text-on-surface-variant max-w-xl leading-relaxed text-lg font-sans opacity-80">Intimate precision for the modern archivist. Our executive suites are designed to facilitate high-stakes dialogue within a setting of architectural silence. Every detail, from the acoustics to the lighting, is calibrated for focus.</p>
          </div>
          <div className="md:col-span-5 flex justify-end">
            <span className="font-serif italic text-secondary text-2xl">Precision and Intimacy.</span>
          </div>
        </div>
        
        {/* Meeting Cards */}
        <div className="grid grid-cols-1 md:grid-cols-3 gap-8 mb-8 max-w-screen-2xl mx-auto">
          <div className="bg-primary-container p-12 flex flex-col justify-between min-h-[400px]">
            <div>
              <h3 className="text-secondary font-serif text-2xl mb-4">Meeting Room</h3>
              <p className="text-surface/70 font-sans">Hourly rentals for focused tactical sessions. Equipped with heritage-grade furnishings.</p>
            </div>
            <span className="text-secondary font-sans text-sm tracking-widest uppercase mt-8 block font-bold">From $120 / HR</span>
          </div>
          <div className="bg-primary-container p-12 flex flex-col justify-between min-h-[400px]">
            <div>
              <h3 className="text-secondary font-serif text-2xl mb-4">Morning Meeting</h3>
              <p className="text-surface/70 font-sans">Sunrise sessions including curated espresso service and architectural light play.</p>
            </div>
            <span className="text-secondary font-sans text-sm tracking-widest uppercase mt-8 block font-bold">From $450 / AM</span>
          </div>
          <div className="bg-primary-container p-12 flex flex-col justify-between min-h-[400px]">
            <div>
              <h3 className="text-secondary font-serif text-2xl mb-4">Day Meeting</h3>
              <p className="text-surface/70 font-sans">Comprehensive 8-hour access with archival storage and gourmet luncheon catering.</p>
            </div>
            <span className="text-secondary font-sans text-sm tracking-widest uppercase mt-8 block font-bold">From $850 / DAY</span>
          </div>
        </div>
        
        <div className="bg-primary-container p-8 md:p-16 flex flex-col md:flex-row justify-between items-start md:items-center max-w-screen-2xl mx-auto">
          <div className="max-w-xl">
            <h3 className="text-secondary font-serif text-3xl mb-4">All-Day Meeting</h3>
            <p className="text-surface/70 font-sans">The complete collector's experience. Includes exclusive access to the private library and evening cocktail transition.</p>
          </div>
          <Link to="/contact" className="mt-8 md:mt-0 border border-secondary text-secondary font-sans uppercase px-12 py-4 tracking-widest hover:bg-secondary hover:text-primary transition-all duration-300 whitespace-nowrap block text-center">Inquire Now</Link>
        </div>
      </section>

      {/* EVENTS SECTION */}
      <section className="py-32 px-6 md:px-24 bg-surface-container-low">
        <div className="mb-24 max-w-screen-2xl mx-auto">
          <span className="text-secondary font-sans uppercase tracking-widest text-xs mb-4 block font-bold">Large Scale Excellence</span>
          <h2 className="font-serif text-4xl md:text-6xl text-primary leading-none uppercase">EVENTS FROM 20 TO <br className="hidden md:block" /> 300 PEOPLE</h2>
        </div>
        
        <div className="grid grid-cols-1 md:grid-cols-3 gap-px bg-secondary/10 max-w-screen-2xl mx-auto">
          <div className="bg-surface-container-low p-12 hover:bg-surface transition-colors duration-300 group">
            <h3 className="font-serif text-2xl mb-6 text-primary">Classic Event</h3>
            <p className="text-on-surface-variant font-sans mb-8 opacity-80">Gala dinners and corporate milestones set within our main vault. Unparalleled acoustic engineering.</p>
            <Link className="text-secondary font-sans text-sm tracking-widest uppercase border-b border-secondary/30 hover:border-secondary transition-all" to="/contact">Details</Link>
          </div>
          <div className="bg-surface-container-low p-12 hover:bg-surface transition-colors duration-300 group">
            <h3 className="font-serif text-2xl mb-6 text-primary">Today's Event</h3>
            <p className="text-on-surface-variant font-sans mb-8 opacity-80">Modern product launches or media previews utilizing our integrated digital archive systems.</p>
            <Link className="text-secondary font-sans text-sm tracking-widest uppercase border-b border-secondary/30 hover:border-secondary transition-all" to="/contact">Details</Link>
          </div>
          <div className="bg-surface-container-low p-12 hover:bg-surface transition-colors duration-300 group">
            <h3 className="font-serif text-2xl mb-6 text-primary">All-Day Event</h3>
            <p className="text-on-surface-variant font-sans mb-8 opacity-80">Multi-session conferences requiring seamless transitions between presentation and social modes.</p>
            <Link className="text-secondary font-sans text-sm tracking-widest uppercase border-b border-secondary/30 hover:border-secondary transition-all" to="/contact">Details</Link>
          </div>
        </div>

        {/* Asymmetric Evening Card */}
        <div className="mt-32 grid grid-cols-1 md:grid-cols-12 items-center max-w-screen-2xl mx-auto">
          <div className="md:col-span-7 aspect-square relative overflow-hidden">
            <img className="w-full h-full object-cover" src="https://lh3.googleusercontent.com/aida-public/AB6AXuBYaZrznEPasTW9PxjhKTY5vc1fzPwEg9LQTQacX6cnSy3XAlX7qIFdGmIG6WU_rwIweqall_qupY54r3yEh1UhcnpBO-hHz3lUIPq0kfKji9RO3VfsQNPcN_vNt1prvuFz9j28lPV-4dMbAj4UVkdKvYD4VSmP5-L64l9XNKwQNMhqNere2UihzK3cbOZoiwZqWdZvPNk4F9fvX_bwCmAetqN_lgWWckUhxg8p__iOmdqqP-clZ_5F8WFHw_TMwQuZDXIUdKQVGg3H" alt="Evening event" />
          </div>
          <div className="md:col-span-6 md:-ml-24 bg-primary-container p-12 md:p-16 z-10 shadow-2xl">
            <h3 className="text-secondary font-serif text-4xl mb-8 leading-tight">Evening Event</h3>
            <p className="text-surface/70 leading-relaxed mb-10 font-sans">When the sun sets, the Collector's gallery transforms. Experience shadow-play and dramatic lighting that elevates any social gathering into a piece of performance art. Bespoke mixology included.</p>
            <Link to="/contact" className="border border-secondary text-secondary font-sans uppercase px-12 py-4 tracking-widest hover:bg-secondary hover:text-primary transition-all duration-300 block text-center w-fit">Request Quote</Link>
          </div>
        </div>
      </section>

      {/* VIDEO/IMAGE SECTION */}
      <section className="w-full h-[80vh] relative overflow-hidden">
        <img className="w-full h-full object-cover" src="https://lh3.googleusercontent.com/aida-public/AB6AXuAokcBc8Z-orVew78hSmrjIVxet_Lsb-roP2Jj6dmUJoyKrTxaKol_b4yZt99J3TLSp7IYV6jb1-1cVddaQB07fB455NknvnUyoeb2O-aKWCebb6cdwz-spUpIk2tuOkViYptUtSKgO5zLpCqjb5dfY1TDGxWhexfCyKQcCt6QrudwWkjH2xirmdlXlq8OGbELSr5yLLlYXa7SYz8XGVfMBsXZoF0a2hHx_O_gtEihvDgVHOSQob-qpDx76pDx3-9DyCYlX597WGF7N" alt="The Garage Sessions" />
        <div className="absolute inset-0 bg-primary/30"></div>
        <div className="absolute bottom-24 left-6 md:left-24">
          <h2 className="text-white font-serif text-5xl md:text-7xl italic tracking-tighter">The Garage Sessions</h2>
          <p className="text-secondary font-sans tracking-widest uppercase text-sm mt-4 font-bold">Cinematic Atmosphere</p>
        </div>
      </section>

      {/* BROCHURE SECTION */}
      <section className="py-32 text-center bg-surface">
        <Link className="group inline-block" to="/contact">
          <span className="text-on-surface-variant font-sans uppercase tracking-[0.4em] text-xs mb-4 block font-bold">Resources</span>
          <h2 className="font-serif text-3xl md:text-6xl text-primary hover:text-secondary transition-colors duration-300 border-b border-secondary/20 group-hover:border-secondary inline-block pb-4">READ OUR MEETING & EVENT BROCHURE</h2>
          <ArrowDown className="text-secondary w-12 h-12 mx-auto mt-12 group-hover:translate-y-2 transition-transform duration-300" />
        </Link>
      </section>

      {/* TESTIMONIAL SECTION */}
      <section className="py-32 bg-surface-container-low px-6 md:px-24">
        <h2 className="font-sans text-xs uppercase tracking-widest text-secondary text-center mb-24 font-bold">HEAR WHAT OUR GUESTS SAY</h2>
        <div className="grid grid-cols-1 md:grid-cols-2 gap-24 items-center max-w-6xl mx-auto">
          <div className="aspect-square bg-surface p-4 relative">
             <div className="absolute top-0 right-0 w-24 h-24 bg-secondary/5 -z-10"></div>
            <img className="w-full h-full object-cover grayscale" src="https://lh3.googleusercontent.com/aida-public/AB6AXuCQGNwDtMnliwXxbq8-XUWL4xjC5pPyEYux0-HF362_dpyGogCanbi8xCy7SbTZQw-jMiUWk_0hAM6Rxg0a3rh99a6UEbL-58NdqaCD5V-7deXLABTNYwDnaUKkEuqwTBfwVnRGnm-tjw5g9nd4SnPLwDdRes3ffsXWLojw-_IljZP3klDJ2KuEh-4i_OtDdldGsRnmaaPhq27fqSkKGbvO4RFzNv_ikqZa3X3caJ8el-JbyGlYHvdxI_-MG_frr1ybcKXEJguRQkyy" alt="Testimonial" />
          </div>
          <div>
            <span className="text-secondary opacity-30 block mb-8">
               <Quote size={64} fill="currentColor" />
            </span>
            <blockquote className="font-serif text-3xl md:text-4xl text-primary italic leading-tight mb-8">
              "The Architectural Collector isn't just a venue; it's a statement. Hosting our annual board meeting here redefined our internal culture of excellence."
            </blockquote>
            <p className="font-sans uppercase tracking-widest text-primary text-sm font-bold">— Julian Vancover, CEO at Vanguard Media</p>
          </div>
        </div>
      </section>

      <Footer />
    </main>
  );
};

export default MeetingsEventsPage;
