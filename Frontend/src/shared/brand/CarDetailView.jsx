import { useRef, useState } from 'react';
import { Link } from 'react-router-dom';
import { mediaUrl, referenceLink } from './media';
import './car-detail.css';
import { carDescriptionVi, carValueVi, carVariantVi } from './car-locale.vi';

const copy={previous:'Ảnh trước',next:'Ảnh tiếp theo',enlarge:'Phóng to ảnh',close:'Đóng ảnh',technical:'Thông số kỹ thuật',description:'Mô tả xe',more:'Xem thêm thông số',less:'Thu gọn',contact:'Liên hệ đại lý',buy:'Tư vấn mua xe',inquiry:'Tìm hiểu về chiếc xe này',help:'Bạn quan tâm hoặc cần thêm thông tin về chiếc xe này? Hãy liên hệ để được tư vấn.',related:'Xe khác cùng đại lý',all:'Xem tất cả xe',year:'Năm sản xuất',mileage:'Quãng đường',monthly:'Giá thuê mỗi tháng',withoutTax:'Chưa bao gồm thuế',withTax:'Đã bao gồm thuế'};
const fields=[['Đăng ký lần đầu','registrationDate'],['Nhiên liệu','propellant'],['Màu sắc','color'],['Trọng lượng','totalWeight','kg'],['Nội thất','upholstery'],['Số cửa','numberOfDoors'],['Tăng tốc 0–100 km/h','acceleration0To100','giây'],['Tốc độ tối đa','topSpeed','km/h'],['Công suất','effect','hp'],['Mô-men xoắn','effectInNm','Nm'],['Hộp số','gearType'],['Số cấp số','numberOfGears'],['Dẫn động','driveWheels'],['Dung tích động cơ','motorVolume'],['Kiểu thân xe','bodyType'],['Kích thước dài × rộng × cao','dimensions']];
const number=value=>Number(value||0).toLocaleString('vi-VN');

export default function CarDetailView({car,related=[]}){
  const [index,setIndex]=useState(0),[expanded,setExpanded]=useState(false),[large,setLarge]=useState(false);
  const thumbs=useRef(null),images=car.images||[],title=`${car.make} ${car.model}`;
  const select=next=>{const selected=(next+images.length)%images.length;setIndex(selected);thumbs.current?.children[selected]?.scrollIntoView({behavior:'smooth',block:'nearest',inline:'nearest'})};
  const specs={...car,dimensions:[car.length,car.width,car.height].filter(Boolean).join(' × ')};
  const priceNote=car.priceType==='Leasing'?copy.monthly:car.priceType==='RetailPriceWithoutTax'?copy.withoutTax:copy.withTax;
  const sameDealer=related.filter(([,{car:item}])=>item.vehicleId!==car.vehicleId&&item.dealerId===car.dealerId).slice(0,4);
  return <div className="car-detail-page">
    <section className="car-detail-top car-detail-container">
      <div className="car-detail-gallery" onKeyDown={event=>{if(event.key==='ArrowLeft')select(index-1);if(event.key==='ArrowRight')select(index+1)}}>
        <div className="car-detail-photo">
          <button className="car-detail-enlarge" type="button" aria-label={copy.enlarge} onClick={()=>setLarge(true)}><img src={mediaUrl(images[index])} alt={`${title} — ${index+1}`}/></button>
          {images.length>1&&<><button className="car-photo-arrow previous" type="button" aria-label={copy.previous} onClick={()=>select(index-1)}>‹</button><button className="car-photo-arrow next" type="button" aria-label={copy.next} onClick={()=>select(index+1)}>›</button><span className="car-photo-count">{index+1} / {images.length}</span></>}
        </div>
        <div className="car-detail-thumbnails" ref={thumbs}>{images.map((src,i)=><button type="button" key={i} aria-label={`${title} ${i+1}`} aria-pressed={i===index} onClick={()=>select(i)}><img src={mediaUrl(src)} alt="" loading="lazy"/></button>)}</div>
      </div>
      <div className="car-detail-summary"><h1>{title}</h1><h2>{carVariantVi(car.variant)}</h2><div className="car-detail-pricing"><p>{Number(car.price)>0?`${number(car.price)} DKK`:copy.contact}</p>{Number(car.price)>0&&<span>{priceNote}</span>}</div><div className="car-detail-highlights"><span>{copy.year}: {car.year}</span><span>{copy.mileage}: {number(car.mileage)} km</span></div><div className="car-detail-dealer">{car.dealerName&&<h3>{car.dealerName}</h3>}{car.dealerPhoneNumber&&<a href={`tel:${car.dealerPhoneNumber}`}>Điện thoại: {car.dealerPhoneNumber}</a>}</div><div className="car-detail-actions"><a className="car-detail-button" href="#car-inquiry">{copy.contact}</a><Link className="car-detail-button" to="/contact" state={{topic:title}}>{copy.buy}</Link></div></div>
    </section>
    <section className="car-detail-columns car-detail-container"><div><h2>{copy.technical}</h2><dl>{fields.slice(0,expanded?fields.length:9).map(([label,key,unit])=>specs[key]&&<div key={key}><dt>{label}</dt><dd>{carValueVi(key,specs[key])}{unit?` ${unit}`:''}</dd></div>)}</dl><button className="car-detail-expand" type="button" aria-expanded={expanded} onClick={()=>setExpanded(value=>!value)}>{expanded?copy.less:copy.more} {expanded?'−':'+'}</button></div><div className="car-detail-description"><h2>{copy.description}</h2><p>{carDescriptionVi(car)}</p></div></section>
    <section className="car-detail-inquiry" id="car-inquiry"><div className="car-detail-container"><h2>{copy.inquiry}</h2><p>{copy.help}</p><h3>{car.dealerName}</h3><p>{[car.dealerAddressStreetLine1,car.dealerAddressZipCode,car.dealerAddressCity].filter(Boolean).join(', ')}</p><div className="car-detail-actions">{car.dealerPhoneNumber&&<a className="car-detail-button" href={`tel:${car.dealerPhoneNumber}`}>Gọi {car.dealerPhoneNumber}</a>}<Link className="car-detail-button" to="/contact" state={{topic:title}}>{copy.contact}</Link></div></div></section>
    {sameDealer.length>0&&<section className="car-detail-related car-detail-container"><h2>{copy.related}</h2><div>{sameDealer.map(([path,{car:item}])=><Link key={path} to={referenceLink(path)}><img src={mediaUrl(item.images?.[0])} alt={`${item.make} ${item.model}`} loading="lazy"/><h3>{item.make} {item.model}</h3><p>{carVariantVi(item.variant)}</p><p>{item.year} · {number(item.mileage)} km</p><strong>{Number(item.price)>0?`${number(item.price)} DKK`:copy.contact}</strong></Link>)}</div><Link className="car-detail-button" to="/discover/cars-for-sale">{copy.all}</Link></section>}
    {large&&<div className="car-detail-lightbox" role="dialog" aria-modal="true" aria-label={title} onClick={()=>setLarge(false)} onKeyDown={event=>{if(event.key==='Escape')setLarge(false)}}><button type="button" autoFocus aria-label={copy.close} onClick={()=>setLarge(false)}>×</button><img src={mediaUrl(images[index])} alt={title}/></div>}
  </div>;
}
