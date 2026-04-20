import React from "react";
import Header from "@shared/components/Header";
import Footer from "@shared/components/Footer";
import { Link } from "react-router-dom";
import { ArrowRight } from "lucide-react";
import "./NewsJournalPage.css";

const articles = [
  {
    date: "Oct 24, 2024",
    category: "Restoration",
    title: "The Patina Preservation Debate",
    description:
      "Should one restore to factory perfection or honor the history written in every scratch? We consult with master restorers from the Italian coast.",
    image:
      "https://lh3.googleusercontent.com/aida-public/AB6AXuA70PeUHTfCu0wCw8d-MVCx7fyoXDU95zURoSaGIIyGakMCxZK5SFQvKUyCAuzhQL4i9XIOcWbxVURkbzvBqOl9cKZX3WWPFLz9j_38-OXfw8muZ5PqSSJ_zOi7pG7u3HT7MtZ5QsfsdkWTccsd8c_Cw6DZ6BEgowyPYdj1MVZPYWyvTImuv4npwfXefV1Xfz5rguttPePJl9d4WetoPg5URllvvEQH7UbAqY2MLyYjGiRLaZcpVTUtlX1LsSGK8tbuOFLsy_UVN46S"
  },
  {
    date: "Oct 18, 2024",
    category: "Architecture",
    title: "The Glass Box Phenomenon",
    description:
      "Exploring the shift from hidden garages to transparent exhibits. How residential design is evolving to showcase automotive collections.",
    image:
      "https://lh3.googleusercontent.com/aida-public/AB6AXuCyc7UMDSnZaFvNFZhGkyKCeq5jGTfFojxJW2ztchqgS8pqGSYVvhrgYh8-wjG_NMjUX-FOFBSn3HUk7EzJuewI8H76Oz0FzdGGTSPy01M73tJdKiU89EuMzvULXV3pXUzatJKHWbvv5rfwTb30baC064lYfwylvfQuutdpPXSnKfudohlB2SlY5KxBlj8u_Shgecen8c7CNzeyXOw8F1qCcKoPg_C6CffbNDYIkaxHk3h6kdfu1uxykcNqwxjKvfjuEmAB5v6x4WPy"
  }
];

const NewsJournalPage = () => {
  return (
    <main className="news-page">
      <Header />

      <section className="news-hero">
        <img
          alt="Featured story car"
          className="news-hero__image"
          src="https://lh3.googleusercontent.com/aida-public/AB6AXuBkP4afV4OsLHCgw0aRm9xiyakhvh4s1fmSBZKyxKtssZic8-Zr6WXq624YyVwhthEd0emibwU-4Vn6L2ajdy6zvJJYz5bXFJuxpww4neZVEzuLWssCrnWTjcU2GoNFY2Xv-tgjw45gKvzPdgv5OaoNbSttkBRRfP9zZ7opTmacPbxbQKSR8gS2E7hn4C-Jvj7s03ymSUflOsmpoQGrWwqUlGzOTChAtNg7lJPu7nfbRr1UFDnlDSoohTIHx3dZq3R8V61RFqTTFiCL"
        />
        <div className="news-hero__overlay" />
        <div className="container-xl news-hero__content-wrap">
          <div className="news-hero__content">
            <p className="news-kicker">Journal Feature - Vol. 04</p>
            <h1>The Art of the Drive</h1>
            <p>
              A deep dive into the intersection of mid-century aesthetics and automotive engineering. Why the garage has become the
              modern sanctuary.
            </p>
            <Link to="/news" className="news-outline-btn">
              Explore Chapter
            </Link>
          </div>
        </div>
      </section>

      <section className="news-feed">
        <div className="container-xl">
          <div className="news-section-head">
            <h2>The Feed</h2>
            <p>Refined Perspective</p>
          </div>

          <div className="news-article-grid">
            {articles.map((article) => (
              <article key={article.title} className="news-article-card">
                <img className="news-article-card__image" src={article.image} alt={article.title} />
                <div className="news-article-card__meta">
                  <span>{article.date}</span>
                  <i />
                  <span>{article.category}</span>
                </div>
                <h3>{article.title}</h3>
                <p>{article.description}</p>
                <button type="button" className="news-link-btn">
                  Read More <ArrowRight size={14} />
                </button>
              </article>
            ))}
          </div>
        </div>
      </section>

      <section className="news-manifesto">
        <div className="container-xl news-manifesto__grid">
          <img
            className="news-manifesto__image"
            alt="Collector manifesto car"
            src="https://lh3.googleusercontent.com/aida-public/AB6AXuB0ObIG4l8Ut-1zYaZV19QnULKBNz8AenzN7vPd1rfFiNp5a741jM1H4tcZhjgOlis9-zdi_mmRZmbU1KBQFs7RGNH2zSd5WZZAEKsVlZE2HtGpYT8aR7dkH_FjoIX7MsOXyR8a4gNrFfh1hg0EqUiSIi7zLIf7C1yuKRFN0NVIf3Y_K4vDMTiB7ETcrOFL5b5HoWtdfEkdB3AuJ-nkvGG_cUXVkIIjrh-hyiGVG3Yy_4bpgW0a5lFvGvEG1vyhEtSJ5nzNLRwBldiD"
          />
          <div className="news-manifesto__content">
            <h3>The Collector&apos;s Manifesto</h3>
            <p>
              "We do not own these machines. We simply look after them for the next generation of enthusiasts who understand that design
              is eternal."
            </p>
            <span>Founder&apos;s Note</span>
          </div>
        </div>
      </section>

      <section className="news-newsletter">
        <div className="news-newsletter__card">
          <h3>Dispatch from the Sanctuary</h3>
          <p>Monthly architectural and automotive insights delivered to your inbox.</p>
          <form>
            <input type="email" placeholder="Email Address" />
            <button type="submit">Join the Archives</button>
          </form>
        </div>
      </section>

      <Footer />
    </main>
  );
};

export default NewsJournalPage;
