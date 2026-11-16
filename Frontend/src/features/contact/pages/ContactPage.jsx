import { useEffect, useState } from 'react';
import { Link, useLocation } from 'react-router-dom';
import Header from '@shared/components/Header';
import Footer from '@shared/components/Footer';
import { getAllBranches } from '@features/appointments/api/appointmentApi';
import './ContactPage.css';
import '@shared/brand/reference-page.css';

const copy={branch:'Chi nhánh',loading:'Đang tải chi nhánh…',unavailable:'Chưa tải được thông tin chi nhánh.',
  emailHelp:'Mở bản nháp trong ứng dụng email của bạn để kiểm tra và gửi yêu cầu.',noEmail:'Chi nhánh chưa cập nhật email. Bạn có thể đặt lịch hoặc gọi trực tiếp.',booking:'Đặt lịch dịch vụ'};

export default function ContactPage(){
  const {state}=useLocation();
  const [branches,setBranches]=useState([]),[branchId,setBranchId]=useState(''),[loading,setLoading]=useState(true),[error,setError]=useState('');
  useEffect(()=>{
    let disposed=false;
    getAllBranches().then(result=>{
      if(disposed)return;
      const items=(Array.isArray(result)?result:result.items||[]).filter(item=>!item.isDeleted);
      setBranches(items);setBranchId(String(items[0]?.id||''));
    }).catch(()=>{if(!disposed)setError(copy.unavailable)}).finally(()=>{if(!disposed)setLoading(false)});
    return()=>{disposed=true};
  },[]);
  const branch=branches.find(item=>String(item.id)===branchId);
  const submit=event=>{
    event.preventDefault();if(!branch?.email)return;
    const form=new FormData(event.currentTarget);
    const subject=`MY GARAGE — ${form.get('subject')}`;
    const body=[String(form.get('name')),String(form.get('email')),'',String(form.get('message'))].join('\n');
    window.location.href=`mailto:${encodeURIComponent(branch.email)}?subject=${encodeURIComponent(subject)}&body=${encodeURIComponent(body)}`;
  };
  const lat=Number(branch?.lat),lng=Number(branch?.lng);
  const hasMap=branch?.lat!=null&&branch?.lng!=null&&Number.isFinite(lat)&&Number.isFinite(lng);
  return <main className="contact-page"><Header/><div className="contact-page__top-space"/>
    <section className="container-xl contact-content">
      <div className="contact-details">
        <div><p className="contact-kicker">Establish Connection</p><h1>Get in<br/>Touch</h1></div>
        <div className="contact-details__blocks">
          <label className="brand-branch-select">{copy.branch}<select value={branchId} onChange={event=>setBranchId(event.target.value)} disabled={loading||!branches.length}>
            {branches.map(item=><option key={item.id} value={item.id}>{item.name}</option>)}
          </select></label>
          {loading&&<p role="status">{copy.loading}</p>}{error&&<p role="alert">{error}</p>}
          {branch&&<><div><p className="contact-kicker">Headquarters</p><address>{branch.address}</address></div>
            <div><p className="contact-kicker">Direct Correspondence</p>{branch.hotline&&<a href={`tel:${branch.hotline.replace(/[^+\d]/g,'')}`}>{branch.hotline}</a>}{branch.email&&<a href={`mailto:${branch.email}`}>{branch.email}</a>}</div></>}
          <Link to="/booking" className="reference-button">{copy.booking}</Link>
        </div>
      </div>
      <div className="contact-form-wrap">
        <form className="contact-form" onSubmit={submit}>
          <div className="contact-form__grid">
            <label>Legal Name<input name="name" required maxLength={120} autoComplete="name" type="text" placeholder="e.g. Julian Vane"/></label>
            <label>Electronic Mail<input name="email" required maxLength={200} autoComplete="email" type="email" defaultValue={state?.email||''} placeholder="name@domain.com"/></label>
          </div>
          <label>Nature of Inquiry<select name="subject" defaultValue={state?.topic||'Acquisition Request'}><option>Acquisition Request</option><option>Private Collection Valuation</option><option>Exhibition Inquiry</option><option>Press &amp; Media</option><option>Newsletter</option></select></label>
          <label>Message<textarea name="message" rows="5" required maxLength={3000} placeholder="Describe the architectural entity or service required..."/></label>
          <button type="submit" disabled={!branch?.email}>Submit Inquiry</button>
          <p className="brand-form-note">{branch?.email?copy.emailHelp:copy.noEmail}</p>
        </form>
      </div>
    </section>
    {hasMap&&<section className="contact-map"><iframe title="MyGarage Location" src={`https://www.openstreetmap.org/export/embed.html?bbox=${lng-.025},${lat-.015},${lng+.025},${lat+.015}&layer=mapnik&marker=${lat},${lng}`} loading="lazy" referrerPolicy="no-referrer"/><div className="contact-map__badge"><p>MYGARAGE</p><span>{branch.name}</span><a href={`https://www.openstreetmap.org/?mlat=${lat}&mlon=${lng}#map=15/${lat}/${lng}`} target="_blank" rel="noreferrer">Open in map</a></div></section>}
    <Footer/>
  </main>;
}
