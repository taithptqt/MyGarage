import { useMemo, useState } from 'react';
import { Link } from 'react-router-dom';
import Header from '@shared/components/Header';
import Footer from '@shared/components/Footer';
import stories from '@shared/brand/reference-stories.json';
import { mediaUrl, referenceLink } from '@shared/brand/media';
import '@shared/brand/reference-page.css';

const labels={title:'TIN TỨC',search:'Tìm bài viết',more:'Đọc tiếp',load:'Xem thêm',empty:'Chưa có bài viết phù hợp.'};

export default function NewsJournalPage(){
  const [query,setQuery]=useState(''),[limit,setLimit]=useState(9);
  const filtered=useMemo(()=>stories.filter(item=>item.title.toLowerCase().includes(query.toLowerCase())),[query]);
  const featured=stories[0];
  return <main className="reference-page"><Header overlay/>
    <header className="reference-hero has-media">
      {featured?.image&&<img className="reference-hero-media" src={mediaUrl(featured.image)} alt=""/>}
      <div className="reference-shade"/>
      <div className="reference-hero-copy"><p>MY GARAGE</p><h1>{labels.title}</h1>{featured&&<><h2>{featured.title}</h2><Link className="reference-button" to={referenceLink(featured.route)}>{labels.more}</Link></>}</div>
    </header>
    <section className="reference-section">
      <input type="search" className="reference-search" placeholder={labels.search} aria-label={labels.search} value={query} onChange={event=>{setQuery(event.target.value);setLimit(9)}}/>
      <div className="reference-story-grid">{filtered.slice(0,limit).map(item=><article key={item.route}>
        <Link to={referenceLink(item.route)} className="brand-card-link"><img src={mediaUrl(item.image)} alt={item.title} loading="lazy"/><h2>{item.title}</h2></Link>
        <p>{item.excerpt}</p><Link className="reference-story-link" to={referenceLink(item.route)}>{labels.more} →</Link>
      </article>)}</div>
      {!filtered.length&&<p>{labels.empty}</p>}
      {filtered.length>limit&&<button type="button" className="reference-button" onClick={()=>setLimit(value=>value+9)}>{labels.load}</button>}
    </section><Footer/>
  </main>;
}
