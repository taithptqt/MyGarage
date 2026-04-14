import React from "react";
import { Link } from "react-router-dom";
import { MapPin, Phone, Mail, ArrowRight } from "lucide-react";
import "./Footer.css";

// Custom UI Icons (Manual SVGs to avoid version conflicts with old lucide-react)
const Facebook = () => <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><path d="M18 2h-3a5 5 0 0 0-5 5v3H7v4h3v8h4v-8h3l1-4h-4V7a1 1 0 0 1 1-1h3z"/></svg>;
const Instagram = () => <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><rect x="2" y="2" width="20" height="20" rx="5" ry="5"/><path d="M16 11.37A4 4 0 1 1 12.63 8 4 4 0 0 1 16 11.37z"/><line x1="17.5" y1="6.5" x2="17.51" y2="6.5"/></svg>;
const Linkedin = () => <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><path d="M16 8a6 6 0 0 1 6 6v7h-4v-7a2 2 0 0 0-2-2 2 2 0 0 0-2 2v7h-4v-7a6 6 0 0 1 6-6z"/><rect x="2" y="9" width="4" height="12"/><circle cx="4" cy="4" r="2"/></svg>;
const Navigation = ({ size = 14 }) => <svg width={size} height={size} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><polygon points="3 11 22 2 13 21 11 13 3 11"/></svg>;

const Footer = () => {
  return (
    <footer className="footer-premium">
      {/* Botanical Pattern Element */}
      <div className="footer-pattern" />

      <div className="footer-content-wrapper">
        {/* Tier 1: Contact Form & Info */}
        <div className="footer-tier-top">
          <div className="contact-info-block">
            <span className="gold-label">GET IN TOUCH</span>
            <h2 className="newsreader-title">CONTACT US</h2>
            <div className="info-details">
              <div className="info-item">
                <MapPin size={18} className="text-garage-gold" />
                <div>
                  <p className="item-label">OFFICE</p>
                  <p>Vejlevej 167, 7100 Vejle, Denmark</p>
                </div>
              </div>
              <div className="info-item">
                <Phone size={18} className="text-garage-gold" />
                <div>
                  <p className="item-label">PHONE</p>
                  <p>+45 098 765 4321</p>
                </div>
              </div>
              <div className="info-item">
                <Mail size={18} className="text-garage-gold" />
                <div>
                  <p className="item-label">EMAIL</p>
                  <p>contact@mygarage.dk</p>
                </div>
              </div>
            </div>
          </div>

          <form className="contact-form-premium">
            <div className="form-row">
              <div className="form-group">
                <label>YOUR NAME</label>
                <input type="text" placeholder="Full Name" />
              </div>
              <div className="form-group">
                <label>EMAIL ADDRESS</label>
                <input type="email" placeholder="email@example.com" />
              </div>
            </div>
            <div className="form-group">
              <label>MESSAGE</label>
              <textarea placeholder="How can we help you?" rows="3"></textarea>
            </div>
            <button type="submit" className="footer-submit-btn">
              SEND MESSAGE <Navigation size={14} className="ml-2" />
            </button>
          </form>
        </div>

        {/* Tier 2: Logo Divider */}
        <div className="footer-tier-middle">
          <div className="logo-divider">
            <div className="divider-line" />
            <div className="footer-logo">
              <span className="logo-main">MY GARAGE</span>
              <span className="logo-sub">AUTOMOTIVE PASSION</span>
            </div>
            <div className="divider-line" />
          </div>
        </div>

        {/* Tier 3: Navigation, Hours & Legal */}
        <div className="footer-tier-bottom">
          <div className="bottom-grid">
            {/* Newsletter & Social */}
            <div className="social-column">
              <h4 className="footer-sub-title">NEWSLETTER</h4>
              <p className="text-white/40 text-sm mb-6 max-w-xs">Join our newsletter to receive updates on exclusive car additions and events.</p>
              <div className="newsletter-box">
                <input type="email" placeholder="Your email address" />
                <button aria-label="Subscribe"><ArrowRight size={16} /></button>
              </div>
              <div className="social-links">
                <a href="#" aria-label="Facebook"><Facebook size={20} /></a>
                <a href="#" aria-label="Instagram"><Instagram size={20} /></a>
                <a href="#" aria-label="LinkedIn"><Linkedin size={20} /></a>
              </div>
            </div>

            {/* Navigation */}
            <div className="nav-column">
              <h4 className="footer-sub-title">NAVIGATION</h4>
              <div className="link-grid">
                <Link to="/about">About Us</Link>
                <Link to="/storage">Storage</Link>
                <Link to="/sales">Sales</Link>
                <Link to="/events">Events</Link>
                <Link to="/partners">Partners</Link>
                <Link to="/news">News</Link>
              </div>
            </div>

            {/* Opening Hours */}
            <div className="hours-column">
              <h4 className="footer-sub-title">OPENING HOURS</h4>
              <div className="hours-grid">
                <div className="hours-row">
                  <span>Monday - Friday</span>
                  <span>08:00 - 18:00</span>
                </div>
                <div className="hours-row">
                  <span>Saturday</span>
                  <span>10:00 - 16:00</span>
                </div>
                <div className="hours-row">
                  <span>Sunday</span>
                  <span className="text-garage-gold">Closed</span>
                </div>
              </div>
              <div className="legal-links mt-12 pt-8 border-t border-white/5">
                <Link to="/privacy">Privacy Policy</Link>
                <span className="text-white/10 mx-2">|</span>
                <Link to="/terms">Terms & Conditions</Link>
              </div>
            </div>
          </div>
        </div>

        {/* Final License/Payment Bar */}
        <div className="footer-final-bar">
          <p>© {new Date().getFullYear()} MY GARAGE. All rights reserved.</p>
          <div className="payment-icons">
            <img src="https://upload.wikimedia.org/wikipedia/commons/5/5e/Visa_Inc._logo.svg" alt="Visa" />
            <img src="https://upload.wikimedia.org/wikipedia/commons/2/2a/Mastercard-logo.svg" alt="Mastercard" />
          </div>
        </div>
      </div>
    </footer>
  );
};

export default Footer;


