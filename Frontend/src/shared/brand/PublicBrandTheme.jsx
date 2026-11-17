import { useEffect, useLayoutEffect } from 'react';
import { useLocation } from 'react-router-dom';
import './public-brand.css';
import './public-typography.css';
import './public-navigation.css';

const fontRoot='/brand/fonts/';
const fonts=['Garage Sans','Garage Display'];
export const isPublicRoute=pathname=>!/^\/(login|admin(?:-legacy)?|accounting|workshop|dispatch-center)(?:\/|$)/i.test(pathname);
const titles={'/home':'MY GARAGE','/storage/garage-3d':'Khám phá garage 3D','/contact':'Liên hệ','/news':'Tin tức','/booking':'Đặt lịch','/booking/form':'Đặt lịch',
  '/parts-shop':'Part Shop','/parts-cart':'Giỏ phụ tùng','/my-garage-dashboard':'Garage','/my-vehicles':'Xe của tôi','/my-notifications':'Thông báo','/my-retail-orders':'Đơn phụ tùng'};

export default function PublicBrandTheme(){
  const {pathname}=useLocation();
  const enabled=isPublicRoute(pathname);
  useLayoutEffect(()=>{
    if(enabled)document.documentElement.dataset.publicUi='garage';
    else delete document.documentElement.dataset.publicUi;
    return()=>{delete document.documentElement.dataset.publicUi};
  },[enabled]);
  useEffect(()=>{
    if(!enabled)return;
    window.scrollTo({top:0,left:0,behavior:'instant'});
    if(titles[pathname])document.title=pathname==='/home'?titles[pathname]:`${titles[pathname]} | MyGarage`;
  },[pathname,enabled]);
  return enabled?<style>{fonts.map(family=>`@font-face{font-family:"${family}";src:url(${JSON.stringify(fontRoot+'Manrope-Variable.ttf')}) format("truetype");font-style:normal;font-weight:200 800;font-stretch:normal;font-display:swap}`).join('\n')}</style>:null;
}
