import React from "react";
import { Link } from "react-router-dom";
import { Home, Phone, Mail, MapPin, Camera, Globe, ArrowRight } from "lucide-react";
import "./Footer.css";

const Footer = () => {
  return (
    <footer className="bg-primary-container text-surface px-6 md:px-24 py-24">
      {/* TOP SECTION (CONTACT / FORM) */}
      <div className="max-w-screen-2xl mx-auto grid grid-cols-1 lg:grid-cols-2 gap-24 mb-32">
        {/* Contact Details */}
        <div className="space-y-12">
          <h3 className="text-secondary text-6xl md:text-7xl font-serif uppercase tracking-tight leading-none">CONTACT <br /> US</h3>
          <div className="space-y-6">
            <p className="font-sans text-lg font-bold tracking-widest text-secondary">MY GARAGE A/S</p>
            <div className="space-y-4">
              <div className="flex items-start space-x-4">
                <Home size={20} className="text-secondary mt-1" />
                <span className="font-sans text-sm opacity-80">Wittrupvej 1, 7120 Vejle East</span>
              </div>
              <div className="flex items-start space-x-4">
                <Phone size={20} className="text-secondary mt-1" />
                <span className="font-sans text-sm opacity-80">+45 69 133 911</span>
              </div>
              <div className="flex items-start space-x-4">
                <Mail size={20} className="text-secondary mt-1" />
                <span className="font-sans text-sm opacity-80">info@mygarage.dk</span>
              </div>
              <div className="flex items-start space-x-4">
                <span className="text-secondary font-sans text-[10px] mt-1.5 font-bold tracking-widest">CVR</span>
                <span className="font-sans text-sm opacity-80">CVR DK-36953217</span>
              </div>
              <div className="flex items-start space-x-4">
                <MapPin size={20} className="text-secondary mt-1" />
                <a className="font-sans text-sm opacity-80 underline decoration-secondary/30 hover:decoration-secondary transition-all" href="#">Directions</a>
              </div>
            </div>
          </div>
        </div>

        {/* Contact Form */}
        <div className="pt-8 lg:pt-20">
          <form className="space-y-10">
            <div className="grid grid-cols-1 md:grid-cols-2 gap-10">
              <div className="space-y-2">
                <label className="block text-secondary text-[10px] font-sans uppercase tracking-widest">NAME</label>
                <input className="footer-input w-full bg-transparent border-b border-secondary/30 focus:border-secondary py-2 outline-none transition-colors" type="text" />
              </div>
              <div className="space-y-2">
                <label className="block text-secondary text-[10px] font-sans uppercase tracking-widest">EMAIL</label>
                <input className="footer-input w-full bg-transparent border-b border-secondary/30 focus:border-secondary py-2 outline-none transition-colors" type="email" />
              </div>
            </div>
            <div className="space-y-2">
              <label className="block text-secondary text-[10px] font-sans uppercase tracking-widest">MESSAGE</label>
              <textarea className="footer-input w-full bg-transparent border-b border-secondary/30 focus:border-secondary py-2 outline-none transition-colors resize-none" rows="1"></textarea>
            </div>
            <div className="flex items-center space-x-3 py-4">
              <input className="w-4 h-4 bg-transparent border-secondary text-secondary focus:ring-0 rounded-none cursor-pointer" type="checkbox" id="consent" />
              <label htmlFor="consent" className="text-[11px] font-sans text-surface/70 cursor-pointer">
                Yes please, I would like to be contacted. <a className="text-secondary underline decoration-secondary/40" href="#">Read consent form</a>
              </label>
            </div>
            <div className="flex justify-end">
              <button className="border border-secondary text-surface px-12 py-4 text-[11px] font-sans uppercase tracking-widest hover:bg-secondary hover:text-primary transition-all duration-300" type="submit">
                SEND MESSAGE
              </button>
            </div>
          </form>
        </div>
      </div>

      {/* BOTTOM SECTION (STAY UPDATED / HOURS) */}
      <div className="max-w-screen-2xl mx-auto border-t border-secondary/20 pt-24 grid grid-cols-1 lg:grid-cols-2 gap-24">
        {/* Stay Updated */}
        <div className="space-y-12">
          <h4 className="text-secondary text-3xl font-serif tracking-wide uppercase">STAY UPDATED</h4>
          <div className="flex space-x-6">
            <a className="text-surface hover:text-secondary transition-colors" href="#"><Globe size={28} /></a>
            <a className="text-surface hover:text-secondary transition-colors font-bold text-2xl font-sans" href="#">in</a>
            <a className="text-surface hover:text-secondary transition-colors" href="#"><Camera size={28} /></a>
          </div>
          <div className="flex flex-col space-y-4 max-w-xs">
            <button className="border border-secondary text-surface px-8 py-5 text-[10px] font-sans uppercase tracking-[0.2em] hover:bg-secondary hover:text-primary transition-all text-left">
              SUBSCRIBE TO OUR NEWSLETTER
            </button>
            <Link to="/news" onClick={() => window.scrollTo({ top: 0, behavior: 'smooth' })} className="border border-secondary text-surface px-8 py-5 text-[10px] font-sans uppercase tracking-[0.2em] hover:bg-secondary hover:text-primary transition-all text-left">
              READ MAGAZINE
            </Link>
          </div>
        </div>

        {/* Opening Hours & Links */}
        <div className="space-y-12">
          <h4 className="text-secondary text-3xl font-serif tracking-wide uppercase">OPENING HOURS</h4>
          <div className="space-y-6 text-sm font-sans opacity-80 leading-relaxed">
            <p>Weekdays: Monday – Friday: 08:00 – 17:00</p>
            <p>Weekend: First Sunday of each month: 11:00 – 15:00</p>
            <p>Closed on holidays</p>
          </div>
          <p className="text-[11px] text-secondary/60 font-sans tracking-wide">See an extended overview of holidays at MY GARAGE</p>
          <p className="text-sm font-sans opacity-80 leading-relaxed max-w-md italic">
            Visitors are always welcome at MY GARAGE – however, we encourage you to announce your visit if you plan to come with more than eight people.
          </p>
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-y-4 gap-x-12 pt-8">
            <Link onClick={() => window.scrollTo({ top: 0, behavior: 'smooth' })} className="text-secondary text-[13px] hover:text-surface transition-colors font-sans" to="/privacy">Privacy Policy</Link>
            <Link onClick={() => window.scrollTo({ top: 0, behavior: 'smooth' })} className="text-secondary text-[13px] hover:text-surface transition-colors font-sans" to="/consent">Declaration of consent</Link>
            <Link onClick={() => window.scrollTo({ top: 0, behavior: 'smooth' })} className="text-secondary text-[13px] hover:text-surface transition-colors font-sans" to="/jobs">Jobs</Link>
            <Link onClick={() => window.scrollTo({ top: 0, behavior: 'smooth' })} className="text-secondary text-[13px] hover:text-surface transition-colors font-sans" to="/knowledge">Knowledge</Link>
            <Link onClick={() => window.scrollTo({ top: 0, behavior: 'smooth' })} className="text-secondary text-[13px] hover:text-surface transition-colors font-sans" to="/terms">Terms of Trade</Link>
            <Link onClick={() => window.scrollTo({ top: 0, behavior: 'smooth' })} className="text-secondary text-[13px] hover:text-surface transition-colors font-sans" to="/sitemap">Sitemap</Link>
          </div>
        </div>
      </div>

      {/* COPYRIGHT & PAYMENT */}
      <div className="max-w-screen-2xl mx-auto border-t border-secondary/10 mt-24 pt-12 flex flex-col md:flex-row justify-between items-center space-y-8 md:space-y-0">
        <div className="text-[10px] font-sans tracking-[0.3em] opacity-30 uppercase">
          © {new Date().getFullYear()} MY GARAGE. ALL RIGHTS RESERVED.
        </div>
        <div className="flex items-center space-x-6 grayscale opacity-60">
          <div className="bg-white px-2 py-1 rounded-none"><span className="text-blue-800 font-bold italic text-[10px]">VISA</span></div>
          <div className="bg-white px-2 py-1 rounded-none flex items-center space-x-1">
            <div className="w-2 h-2 rounded-full bg-red-600"></div>
            <div className="w-2 h-2 rounded-full bg-yellow-500 -ml-1"></div>
            <span className="text-black font-bold text-[8px]">mastercard</span>
          </div>
          <div className="bg-blue-500 px-2 py-1 rounded-none"><span className="text-white font-bold text-[10px]">MobilePay</span></div>
        </div>
      </div>
    </footer>
  );
};

export default Footer;