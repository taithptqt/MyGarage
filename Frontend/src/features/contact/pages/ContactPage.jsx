import React from "react";
import Header from "@shared/components/Header";
import Footer from "@shared/components/Footer";
import "./ContactPage.css";

const ContactPage = () => {
  return (
    <main className="contact-page">
      <Header />

      <div className="contact-page__top-space" />

      <section className="container-xl contact-content">
        <div className="contact-details">
          <div>
            <p className="contact-kicker">Establish Connection</p>
            <h1>
              Get in <br /> Touch
            </h1>
          </div>

          <div className="contact-details__blocks">
            <div>
              <p className="contact-kicker">Headquarters</p>
              <address>
                WITTRUPVEJ 1,
                <br />
                7120 VEJLE EAST,
                <br />
                DENMARK
              </address>
            </div>

            <div>
              <p className="contact-kicker">Direct Correspondence</p>
              <a href="tel:+4569133911">+45 69 133 911</a>
              <a href="mailto:info@mygarage.dk">info@mygarage.dk</a>
            </div>

            <p className="contact-quote">
              "The collector is an artist in their own right, curating the silence between the objects."
            </p>
          </div>
        </div>

        <div className="contact-form-wrap">
          <form className="contact-form" onSubmit={(event) => event.preventDefault()}>
            <div className="contact-form__grid">
              <label>
                Legal Name
                <input type="text" placeholder="e.g. Julian Vane" />
              </label>
              <label>
                Electronic Mail
                <input type="email" placeholder="name@domain.com" />
              </label>
            </div>

            <label>
              Nature of Inquiry
              <select defaultValue="Acquisition Request">
                <option>Acquisition Request</option>
                <option>Private Collection Valuation</option>
                <option>Exhibition Inquiry</option>
                <option>Press &amp; Media</option>
              </select>
            </label>

            <label>
              Message
              <textarea rows="4" placeholder="Describe the architectural entity or service required..." />
            </label>

            <button type="submit">Submit Inquiry</button>
          </form>
        </div>
      </section>

      <section className="contact-map">
        <iframe
          title="MyGarage Location"
          src="https://www.openstreetmap.org/export/embed.html?bbox=105.8100%2C20.9950%2C105.9000%2C21.0600&layer=mapnik&marker=21.0285%2C105.8542"
          loading="lazy"
          referrerPolicy="no-referrer-when-downgrade"
        />
        <div className="contact-map__badge">
          <p>MYGARAGE</p>
          <span>Hà Nội, Việt Nam</span>
          <a href="https://www.openstreetmap.org/?mlat=21.0285&mlon=105.8542#map=15/21.0285/105.8542" target="_blank" rel="noreferrer">
            Open in map
          </a>
        </div>
      </section>

      <Footer />
    </main>
  );
};

export default ContactPage;
