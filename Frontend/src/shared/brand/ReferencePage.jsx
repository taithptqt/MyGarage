import { Fragment, useEffect, useMemo, useState } from 'react';
import { Link, useLocation, useNavigate } from 'react-router-dom';
import Header from '@shared/components/Header';
import Footer from '@shared/components/Footer';
import pages from './reference-pages.json';
import { mediaUrl, referenceLink } from './media';
import './reference-page.css';
import CarDetailView from './CarDetailView';

const labels={more:'Tìm hiểu thêm',contact:'Liên hệ tư vấn',booking:'Đặt lịch dịch vụ',back:'Trang chủ',missing:'Nội dung đang cập nhật',request:'Gửi yêu cầu',previous:'Ảnh trước',next:'Ảnh tiếp theo',video:'Video MY GARAGE'};

export function safeMarkup(markup){
  const doc=new DOMParser().parseFromString(markup||'','text/html');
  doc.querySelectorAll('script,style,iframe,object,embed,form,input,button,meta,link').forEach(node=>node.remove());
  for(const node of doc.body.querySelectorAll('*')){
    for(const attr of [...node.attributes])if(!['href','src','alt','title','colspan','rowspan'].includes(attr.name))node.removeAttribute(attr.name);
    for(const name of ['href','src']){
      const value=node.getAttribute(name);if(!value)continue;
      if(!/^(https?:|mailto:|tel:|\/|#)/i.test(value)){node.removeAttribute(name);continue}
      node.setAttribute(name,name==='src'?mediaUrl(value):referenceLink(value));
    }
    if(node.tagName==='IMG')node.setAttribute('loading','lazy');
  }
  return doc.body.innerHTML;
}

function RichText({html}){
  const navigate=useNavigate(),markup=useMemo(()=>safeMarkup(html),[html]);
  if(!html)return null;
  return <div className="reference-copy" dangerouslySetInnerHTML={{__html:markup}} onClick={event=>{
    const anchor=event.target.closest('a');if(!anchor||event.button!==0||event.ctrlKey||event.metaKey||event.shiftKey||event.altKey)return;
    const href=anchor.getAttribute('href');if(href?.startsWith('/')&&!href.startsWith('//')){event.preventDefault();navigate(href)}
  }}/>;
}

export function ReferenceMedia({image,video,hero=false}){
  if(video?.mediaItemUrl)return <video className={hero?'reference-hero-media':'reference-media'} src={mediaUrl(video)} poster={mediaUrl(image)||undefined} autoPlay={hero} loop={hero} muted playsInline controls={!hero} preload={hero?'metadata':'none'}/>;
  return image?<img className={hero?'reference-hero-media':'reference-media'} src={mediaUrl(image)} alt={image.altText||''} loading={hero?'eager':'lazy'}/>:null;
}

function Action({link}){return link?.url?<Link className="reference-button" to={referenceLink(link.url)}>{link.title||labels.more}</Link>:null}

function ImageCarousel({slides=[]}){
  const [index,setIndex]=useState(0),slide=slides[index];
  if(!slide)return null;
  const previous=()=>setIndex(value=>(value+slides.length-1)%slides.length),next=()=>setIndex(value=>(value+1)%slides.length);
  return <div className="reference-carousel" tabIndex={0} onKeyDown={event=>{if(event.key==='ArrowLeft'){event.preventDefault();previous()}if(event.key==='ArrowRight'){event.preventDefault();next()}}}>
    <ReferenceMedia image={slide.image||slide.poster} video={slide.video}/><RichText html={slide.column}/>
    <div className="reference-gallery-controls"><button onClick={previous} aria-label={labels.previous}>←</button><span aria-live="polite">{index+1} / {slides.length}</span><button onClick={next} aria-label={labels.next}>→</button></div>
  </div>;
}

function Partners({items=[],heading,slider=false}){
  const [slide,setSlide]=useState(0),[paused,setPaused]=useState(false);
  const [reducedMotion,setReducedMotion]=useState(false);
  useEffect(()=>{
    const query=window.matchMedia('(prefers-reduced-motion: reduce)');
    const update=()=>setReducedMotion(query.matches);
    update();query.addEventListener('change',update);
    return()=>query.removeEventListener('change',update);
  },[]);
  useEffect(()=>{
    if(!slider||paused||reducedMotion||items.length<2)return;
    // Keep Glide's slide-then-pause rhythm, slightly faster as requested.
    const timer=window.setTimeout(()=>setSlide(value=>value+1),slide===0?1200:2400);
    return()=>window.clearTimeout(timer);
  },[slider,paused,reducedMotion,slide,items.length]);
  const logos=items.map(item=>{
    const info=item.acf_partner?.groupInfo;
    return <Link to={referenceLink(item.uri)} key={item.uri||item.title}><img src={mediaUrl(info?.logoGrey||info?.logoGold||info?.logo)} alt={info?.name||item.title} loading="lazy"/><span>{item.title}</span></Link>;
  });
  return <section className={`reference-section reference-partners${slider?' reference-partners-slider':''}`}><h2>{heading}</h2>{slider?<div className="reference-logo-window" onMouseEnter={()=>setPaused(true)} onMouseLeave={()=>setPaused(false)} onFocus={()=>setPaused(true)} onBlur={event=>{if(!event.currentTarget.contains(event.relatedTarget))setPaused(false)}}><div className={`reference-logo-track${slide===0?' is-reset':''}`} style={{'--logo-count':items.length,'--logo-slide':slide}} onTransitionEnd={event=>{if(event.target===event.currentTarget&&slide>=items.length)setSlide(0)}}><div className="reference-logo-group">{logos}</div><div className="reference-logo-group" aria-hidden="true">{items.map(item=>{const info=item.acf_partner?.groupInfo;return <div className="reference-logo-copy" key={item.uri||item.title}><img src={mediaUrl(info?.logoGrey||info?.logoGold||info?.logo)} alt="" loading="lazy"/></div>})}</div></div></div>:<div className="reference-logo-grid">{logos}</div>}</section>;
}

function ContentBlock({block}){
  const type=block.__typename?.split('_').at(-1);
  if(type==='OneColumnImage')return <>{block.repeaterSections?.map((item,index)=><section id={index===0?block.anchor:undefined} className={`reference-split ${item.alignment==='left'?'reverse':''} tone-${block.selectBgcolor}`} key={index}>
    <ReferenceMedia image={item.image} video={item.video}/><div className="reference-split-copy"><h2>{item.heading}</h2><RichText html={item.column}/><Action link={item.link}/></div>
  </section>)}</>;
  if(type==='PartnerGrid')return <Partners items={block.relPartners} heading={block.heading} slider={block.slider}/>;
  if(type==='Section')return <section id={block.anchor} className="reference-full-media"><ReferenceMedia image={block.image} video={block.video}/><Action link={block.link}/></section>;
  if(type==='VideoBoxes')return <section id={block.anchor} className="reference-section reference-boxes">{block.boxes?.map((item,index)=><article key={index}><ReferenceMedia image={item.image} video={item.video}/><RichText html={item.text}/><Action link={item.link}/></article>)}</section>;
  if(type==='Carousel')return <section id={block.anchor} className="reference-section"><h2>{block.heading}</h2><ImageCarousel slides={block.repeaterSlides}/></section>;
  if(type==='Gallery')return <section id={block.anchor} className="reference-section reference-gallery"><h2>{block.heading}</h2>{(block.images||[]).map((item,index)=><ReferenceMedia key={index} image={item.image||item}/>)}</section>;
  if(type==='Packages')return <section id={block.anchor} className="reference-section"><RichText html={block.text}/><div className="reference-packages">{block.items?.map((item,index)=><article key={index}><h3>{item.title}</h3><ul>{item.features?.map((feature,i)=><li key={i}>{feature.title}</li>)}</ul><p className="reference-price">{item.price}</p><Link className="reference-button" to="/contact">{labels.request}</Link></article>)}</div></section>;
  if(type==='Form')return <section id={block.anchor} className="reference-section reference-cta"><Link className="reference-button" to="/contact">{labels.contact}</Link><Link className="reference-button" to="/booking">{labels.booking}</Link></section>;
  if(type==='Counter')return <section className="reference-section reference-counters">{block.counters?.map((item,index)=><div key={index}><strong>{item.number||item.count||item.value}</strong><RichText html={item.text||item.heading||item.label}/></div>)}</section>;
  if(type==='FeaturedCars')return <FeaturedCars block={block}/>;
  if(type==='Employee'||type==='EmployeesOverview'){
    const items=block.employees||[block.objEmployee].filter(Boolean);
    return <section className="reference-section reference-boxes">{items.map((item,index)=>{const info=item.acf_employee?.groupInfo||item.acf_employee||item;return <article key={index}><ReferenceMedia image={info.image}/><h3>{item.title||info.name}</h3><p>{info.position||info.title}</p>{info.email&&<a href={`mailto:${info.email}`}>{info.email}</a>}</article>})}</section>;
  }
  if(type==='Video')return <section className="reference-section">{/^\d+$/.test(block.videoid||'')?<iframe className="reference-video-embed" title={labels.video} src={`https://player.vimeo.com/video/${block.videoid}?dnt=1&title=0&byline=0&portrait=0`} loading="lazy" allow="fullscreen; picture-in-picture" allowFullScreen/>:<ReferenceMedia image={block.poster}/>}</section>;
  return <section id={block.anchor} className={`reference-section reference-prose tone-${block.selectBgcolor}`}><h2>{block.heading}</h2><h3>{block.subheading}</h3><RichText html={block.column||block.text}/><ReferenceMedia image={block.image}/></section>;
}

function FeaturedCars({block}){
  const [group,setGroup]=useState(0);
  const cars=useMemo(()=>Object.entries(pages).filter(([,data])=>data.car).filter(([,data],index,all)=>all.findIndex(([,item])=>item.car.vehicleId===data.car.vehicleId)===index),[]);
  const groups=Math.ceil(cars.length/4),visible=cars.slice(group*4,group*4+4);
  return <section className="reference-section reference-featured-cars">
    <div className="featured-cars-heading"><h2>{block.heading}</h2><div className="featured-cars-controls"><button type="button" aria-label={labels.previous} disabled={group===0} onClick={()=>setGroup(value=>value-1)}>‹</button><button type="button" aria-label={labels.next} disabled={group===groups-1} onClick={()=>setGroup(value=>value+1)}>›</button></div></div>
    <div className="reference-car-grid featured-cars-grid">{visible.map(([path,{car}])=><Link className="brand-card-link featured-car" key={path} to={referenceLink(path)}>
      <img src={mediaUrl(car.images?.[0])} alt={`${car.make} ${car.model}`} loading="lazy"/>
      <div className="featured-car-info"><h3>{car.make} {car.model}</h3><p className="featured-car-variant">{car.variant}</p><p className="featured-car-facts">NĂM: {car.year} <span>|</span> KM: {Number(car.mileage||0).toLocaleString('vi-VN')}</p><p className="featured-car-price">{Number(car.price)>0?`${Number(car.price).toLocaleString('vi-VN')} DKK`:labels.contact}</p>{Number(car.price)>0&&<small>{car.priceType==='RetailPriceWithoutTax'?'Chưa bao gồm thuế':car.priceType?.includes('Leas')?'Mỗi tháng':'Đã bao gồm thuế'}</small>}</div>
    </Link>)}</div><Action link={block.link}/>
  </section>;
}

export function CarCollection({limit=12}){
  const [query,setQuery]=useState('');
  const cars=Object.entries(pages).filter(([,data])=>data.car).filter(([,data])=>`${data.car.make} ${data.car.model}`.toLowerCase().includes(query.toLowerCase()));
  const unique=cars.filter(([,data],index,all)=>all.findIndex(([,item])=>item.car.vehicleId===data.car.vehicleId)===index).slice(0,limit);
  return <>{limit>4&&<input className="reference-search" aria-label="Tìm xe" placeholder="Tìm xe" value={query} onChange={event=>setQuery(event.target.value)}/>}<div className="reference-car-grid">{unique.map(([path,{car}])=><Link className="brand-card-link" key={path} to={referenceLink(path)}><img src={mediaUrl(car.images?.[0])} alt={`${car.make} ${car.model}`} loading="lazy"/><h3>{car.make} {car.model}</h3><p>{car.variant}</p><small>{car.year} · {Number(car.mileage||0).toLocaleString('vi-VN')} km</small></Link>)}</div></>;
}

function CarDetails({car}){
  const [index,setIndex]=useState(0),images=car.images||[];
  const title=`${car.make} ${car.model}`;
  return <><section className="reference-car-detail reference-section"><div className="reference-car-view"><img src={mediaUrl(images[index])} alt={title}/><div className="reference-gallery-controls"><button aria-label={labels.previous} disabled={index===0} onClick={()=>setIndex(index-1)}>←</button><span>{index+1} / {images.length}</span><button aria-label={labels.next} disabled={index>=images.length-1} onClick={()=>setIndex(index+1)}>→</button></div><div className="reference-thumbs">{images.map((url,i)=><button key={i} aria-label={`${title} ${i+1}`} aria-pressed={i===index} onClick={()=>setIndex(i)}><img src={mediaUrl(url)} alt="" loading="lazy"/></button>)}</div></div><div><h1>{title}</h1><p>{car.variant}</p><p className="reference-price">{Number.isFinite(Number(car.price))&&Number(car.price)>0?`${Number(car.price).toLocaleString('vi-VN')} DKK`:labels.contact}</p><dl>{[['Năm',car.year],['Quãng đường',car.mileage&&`${Number(car.mileage).toLocaleString('vi-VN')} km`],['Màu',car.color],['Nhiên liệu',car.propellant],['Công suất',car.effect&&`${car.effect} hp`]].map(([label,value])=>value&&<div key={label}><dt>{label}</dt><dd>{value}</dd></div>)}</dl><Link className="reference-button" to="/contact">{labels.contact}</Link><p className="reference-note">Thông tin xe từ bộ giao diện tham khảo.</p></div></section><section className="reference-section reference-prose"><RichText html={car.carComment}/></section></>;
}

export default function ReferencePage({sourcePath,pageData,className='',afterFirstBlock=null}){
  const {pathname,hash}=useLocation(),path=sourcePath||`/en${pathname.replace(/^\/discover/,'')}`;
  useEffect(()=>{
    if(!hash)return;
    try{document.getElementById(decodeURIComponent(hash.slice(1)))?.scrollIntoView({behavior:'smooth'})}catch{}
  },[path,hash]);
  const data=pageData?{page:pageData}:pages[path.replace(/\/$/,'')];
  const entry=data?.page||data?.partner||data?.post||data?.blogPost;
  const header=entry?.acf_page_header?.groupHeader;
  const partner=entry?.acf_partner;
  const post=entry?.acf_post;
  const blocks=entry?.acf_components?.flexComponents||[];
  const title=header?.heading||post?.heading||entry?.title;
  useEffect(()=>{
    const text=data?.car?`${data.car.make} ${data.car.model}`:title;
    document.title=text?`${String(text).replace(/<[^>]+>/g,'')} | MyGarage`:'MyGarage';
  },[title,data]);
  const allPartners=path==='/en/partners'?Object.entries(pages).filter(([,item])=>item.partner).map(([uri,item])=>({...item.partner,uri})):[];
  const postImage=post?.image?.[0];
  const hasHero=!!(header?.image||header?.video||postImage);
  return <main className={`reference-page ${className}`} key={path}><Header overlay={hasHero}/>
    {data?.car?<CarDetailView key={path} car={data.car} related={Object.entries(pages).filter(([,item])=>item.car).filter(([,item],index,all)=>all.findIndex(([,other])=>other.car.vehicleId===item.car.vehicleId)===index)}/>:<>
      <header className={`reference-hero ${hasHero?'has-media':''}`}><ReferenceMedia image={header?.image||postImage} video={header?.video} hero/>{hasHero&&<div className="reference-shade"/>}<div className="reference-hero-copy"><p>MY GARAGE</p><h1>{title||labels.missing}</h1><RichText html={header?.text}/>{header?.buttons?.map((item,index)=><Action key={index} link={item.button||item.link||item}/>)}</div></header>
      {post&&<section className="reference-section reference-prose"><RichText html={post.content}/></section>}
      {!!post?.gallery?.length&&<section className="reference-section reference-gallery">{post.gallery.map((image,index)=><ReferenceMedia key={index} image={image}/>)}</section>}
      {partner&&<section className="reference-section reference-prose"><img className="reference-partner-logo" src={mediaUrl(partner.groupInfo?.logoGold||partner.groupInfo?.logo)} alt={entry.title}/><RichText html={partner.groupContact?.excerpt}/>{partner.groupInfo?.email&&<a href={`mailto:${partner.groupInfo.email}`}>{partner.groupInfo.email}</a>}</section>}
      {blocks.map((block,index)=><Fragment key={index}><ContentBlock block={block}/>{index===0&&afterFirstBlock}</Fragment>)}
      {allPartners.length>0&&!blocks.some(block=>block.__typename.endsWith('_PartnerGrid'))&&<Partners items={allPartners}/>}
      {/cars-for-sale/.test(path)&&<section className="reference-section"><CarCollection limit={300}/></section>}
      {!data&&<section className="reference-section"><Link className="reference-button" to="/home">{labels.back}</Link></section>}
    </>}
    <Footer/>
  </main>;
}
