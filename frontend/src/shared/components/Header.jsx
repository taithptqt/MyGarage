import React, { useState, useEffect } from "react";
import { Link } from "react-router-dom";
import { Search, Globe, ChevronDown } from "lucide-react";

const Header = ({ overlay = false }) => {
  const [isScrolled, setIsScrolled] = useState(false);
  const [isMenuOpen, setIsMenuOpen] = useState(false);

  useEffect(() => {
    const handleScroll = () => {
      setIsScrolled(window.scrollY > 50);
    };
    window.addEventListener("scroll", handleScroll);
    return () => window.removeEventListener("scroll", handleScroll);
  }, []);

  const navLinks = [
    { name: "ABOUT MY GARAGE", path: "/about" },
    { name: "STORAGE", path: "/storage" },
    { name: "SALES", path: "/sales" },
    { name: "MEETINGS AND EVENTS", path: "/events" },
    { name: "PARTNERS", path: "/partners" },
    { name: "NEWS", path: "/news" },
    { name: "CONTACT", path: "/contact" },
  ];

  return (
    <div className="absolute top-0 left-0 w-full z-50">
      {/* Top Utility Bar */}
      <div className={`bg-garage-green/40 backdrop-blur-sm border-b border-white/5 py-2 px-8 flex justify-between items-center transition-opacity duration-500 ${isScrolled ? "opacity-0 invisible" : "opacity-100 visible"}`}>
        <div className="text-[10px] text-garage-gold cursor-default font-sans tracking-widest font-bold">
          098 765 4321 English
        </div>
        <div className="flex items-center gap-6">
          <div className="flex items-center gap-3 text-[10px] text-white/70 font-bold tracking-widest">
            <span className="text-garage-gold cursor-default">FAQ</span>
            <span className="text-white/20">|</span>
            <span className="text-garage-gold cursor-default">JOB</span>
          </div>
          <button className="text-white/70 hover:text-garage-gold">
            <Search size={14} />
          </button>
        </div>
      </div>

      {/* Main Navigation */}
      <nav
        className={`w-full transition-all duration-500 ${isScrolled
          ? "bg-garage-green/95 backdrop-blur-lg py-3 shadow-2xl translate-y-[40px]"
          : "bg-transparent py-6"
          }`}
      >
        <div className="max-w-[1440px] mx-auto px-8 relative">
          <div className="flex items-center justify-between md:justify-center gap-12 lg:gap-20">
            {/* Left Links */}
            <div className="hidden md:flex items-center gap-8 lg:gap-12 flex-1 justify-end">
              {navLinks.slice(0, 3).map((link) => (
                <Link
                  key={link.name}
                  to={link.path}
                  className="text-[11px] font-bold tracking-[0.25em] text-white hover:text-garage-gold transition-colors"
                >
                  {link.name}
                </Link>
              ))}
            </div>

            {/* Centered Logo */}
            <Link to="/" className="flex-shrink-0 group flex flex-col items-center">
              <span className="font-serif text-3xl lg:text-4xl text-garage-gold tracking-[0.15em] leading-none mb-1">
                MY
              </span>
              <span className="text-[9px] font-bold tracking-[0.6em] text-garage-gold leading-none uppercase">
                Garage
              </span>
            </Link>

            {/* Right Links */}
            <div className="hidden md:flex items-center gap-8 lg:gap-12 flex-1">
              {navLinks.slice(3).map((link) => (
                <Link
                  key={link.name}
                  to={link.path}
                  className="text-[11px] font-bold tracking-[0.25em] text-white hover:text-garage-gold transition-colors"
                >
                  {link.name}
                </Link>
              ))}
            </div>

            {/* Mobile Toggle */}
            <button
              onClick={() => setIsMenuOpen(!isMenuOpen)}
              className="md:hidden group flex flex-col items-end gap-1.5 focus:outline-none"
            >
              <span className={`h-0.5 bg-white transition-all duration-300 ${isMenuOpen ? "w-8 translate-y-2 rotate-45" : "w-8 group-hover:w-6"}`} />
              <span className={`h-0.5 bg-white transition-all duration-300 ${isMenuOpen ? "opacity-0" : "w-6 group-hover:w-8"}`} />
              <span className={`h-0.5 bg-white transition-all duration-300 ${isMenuOpen ? "w-8 -translate-y-2 -rotate-45" : "w-4 group-hover:w-8"}`} />
            </button>
          </div>
        </div>
      </nav>

      {/* Fullscreen Overlay Menu (Mobile) */}
      <div className={`fixed inset-0 bg-garage-green z-[100] transition-all duration-700 ${isMenuOpen ? "opacity-100 pointer-events-auto" : "opacity-0 pointer-events-none"}`}>
        <div className="h-full flex flex-col items-center justify-center space-y-6">
          {navLinks.map((link) => (
            <Link
              key={link.name}
              to={link.path}
              onClick={() => setIsMenuOpen(false)}
              className="font-serif text-3xl text-garage-cream hover:text-garage-gold transition-colors tracking-widest uppercase"
            >
              {link.name}
            </Link>
          ))}
          <button
            onClick={() => setIsMenuOpen(false)}
            className="stencil-font text-garage-gold pt-12 hover:text-white tracking-[0.3em]"
          >
            [ CLOSE ]
          </button>
        </div>
      </div>
    </div>
  );
};

export default Header;
