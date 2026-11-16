import descriptions from './car-descriptions.vi.json';

const values={El:'Điện',Benzin:'Xăng',Diesel:'Dầu diesel',Gul:'Vàng',Sortmetal:'Đen ánh kim',Sort:'Đen',Hvid:'Trắng','Gray metal':'Xám ánh kim',Green:'Xanh lá',Grey:'Xám','Silver metal':'Bạc ánh kim','Green metal':'Xanh lá ánh kim',Koksmetal:'Xám than ánh kim','Pink metal':'Hồng ánh kim','Blue metal':'Xanh dương ánh kim',Red:'Đỏ',Beigemetal:'Be ánh kim',Carbonsortmetal:'Đen carbon ánh kim',Orange:'Cam','Light blue metal':'Xanh dương nhạt ánh kim',Blue:'Xanh dương','Dark grey':'Xám đậm',Læder:'Da','Artificial leather':'Da nhân tạo','Part leather':'Da kết hợp',Stof:'Vải',A:'Tự động',M:'Số sàn'};
export const carDescriptionVi=car=>descriptions[car.vehicleId]||'';
export function carValueVi(key,value){
  if(['propellant','color','upholstery','gearType'].includes(key))return values[value]||value;
  if(key==='bodyType')return String(value).replace(/Halvkombi/g,'Hatchback').replace(/St\.car/g,'Xe wagon').replace(/Mikro/g,'Xe cỡ nhỏ').replace(/Cabriolet/g,'Xe mui trần').replace(/Coupe/g,'Xe coupe').replace(/Sedan/g,'Xe sedan').replace(/Minibus/g,'Xe nhiều chỗ').replace(/Sport/g,'thể thao').replace(/Aut\./g,'tự động');
  if(key==='driveWheels')return value==='4'?'4 bánh':value==='2'?'2 bánh':value;
  return value;
}
export const carVariantVi=value=>String(value||'').replace(/\baut\./gi,'tự động').replace(/\bTouring\b/g,'Wagon').replace(/\bCabriolet\b/g,'Mui trần');
