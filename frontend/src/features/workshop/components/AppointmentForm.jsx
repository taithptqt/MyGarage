import React, { useState, useMemo, useEffect } from "react";
import {
  getVehicleBrands,
  getVehicleModels
} from "../../appointments/api/appointmentApi";
import DateTimePicker from "./DateTimePicker";
import { formatLicensePlate } from "../utils/serviceOrderDetailUtils";

export default function AppointmentForm({ customers = [], vehicles = [], customerVehicles = [], branchId, onSubmit, onCancel, loading, services = [] }) {
  const [formData, setFormData] = useState({
    customerId: "",
    vehicleId: "",
    customerName: "",
    customerPhone: "",
    vehicleLicensePlate: "",
    vehicleModelId: "",
    vehicleColor: "",
    vehicleVin: "",
    vehicleYear: "",
    guestName: "",
    guestPhone: "",
    guestVehiclePlate: "",
    guestVehicleModelId: "",
    guestVehicleColor: "",
    guestVehicleVin: "",
    guestVehicleYear: "",
    isQuickOrder: false,
    isNewVehicle: false,
    appointmentDate: "",
    startTime: "",
    serviceIds: [],
    customerNotes: ""
  });

  const [search, setSearch] = useState("");
  const [serviceSearch, setServiceSearch] = useState("");
  const [brands, setBrands] = useState([]);
  const [models, setModels] = useState([]);
  const [selectedBrandId, setSelectedBrandId] = useState("");
  const [guestSelectedBrandId, setGuestSelectedBrandId] = useState("");

  const years = useMemo(() => {
    const currentYear = new Date().getFullYear();
    const arr = [];
    for (let y = currentYear; y >= 1980; y--) arr.push(y);
    return arr;
  }, []);

  useEffect(() => {
    const fetchData = async () => {
      try {
        const [bRes, mRes] = await Promise.all([
          getVehicleBrands(),
          getVehicleModels()
        ]);
        setBrands(bRes || []);
        setModels(mRes || []);
      } catch (err) { console.error("Failed to fetch reference data", err); }
    };
    fetchData();
  }, []);

  const filteredCustomers = useMemo(() => {
    if (!search) return [];
    return customers.filter(c =>
      (c.fullName || "").toLowerCase().includes(search.toLowerCase()) ||
      (c.phone || "").includes(search)
    ).slice(0, 5);
  }, [customers, search]);

  const customerVehiclesFiltered = useMemo(() => {
    if (!formData.customerId) return [];
    // Filter mappings for this customer
    const mappings = customerVehicles.filter(cv => cv.customerId === Number(formData.customerId));
    // Find corresponding vehicles
    return vehicles.filter(v => mappings.some(m => m.vehicleId === v.id));
  }, [vehicles, customerVehicles, formData.customerId]);

  const handleChange = (e) => {
    const { name, value } = e.target;
    setFormData(prev => ({ ...prev, [name]: value }));
  };

  const selectCustomer = (customer) => {
    const mappings = customerVehicles.filter(cv => cv.customerId === Number(customer.id));
    const forceNewVeh = mappings.length === 0;

    setFormData(prev => ({
      ...prev,
      customerId: customer.id,
      customerName: customer.fullName,
      customerPhone: customer.phone,
      vehicleId: "",
      isNewVehicle: forceNewVeh,
      vehicleModelId: "",
      vehicleColor: "",
      vehicleVin: "",
      vehicleYear: ""
    }));
    setSearch("");
    setSelectedBrandId("");
  };


  const handlePlateBlur = (e) => {
    const { name, value } = e.target;
    setFormData(prev => ({ ...prev, [name]: formatLicensePlate(value) }));
  };

  const handleServiceToggle = (id) => {
    setFormData(prev => {
      const exists = prev.serviceIds.includes(id);
      return {
        ...prev,
        serviceIds: exists ? prev.serviceIds.filter(sid => sid !== id) : [...prev.serviceIds, id]
      };
    });
    setServiceSearch(""); // Clear search after seleciton
  };

  const filteredServices = useMemo(() => {
    if (!serviceSearch) return [];
    console.log("Services in form:", services); // Debug
    return services.filter(s =>
      ((s.Name || s.name || "").toLowerCase().includes(serviceSearch.toLowerCase())) &&
      !formData.serviceIds.includes(s.id)
    ).slice(0, 10);
  }, [services, serviceSearch, formData.serviceIds]);

  const toggleQuickOrder = () => {
    setFormData(prev => ({
      ...prev,
      isQuickOrder: !prev.isQuickOrder,
      customerId: "",
      vehicleId: "",
      guestName: "",
      guestPhone: "",
      guestVehiclePlate: "",
      guestVehicleModel: "",
      isNewVehicle: false
    }));
    setSearch("");
  };

  const { totalMinutes, isOvernight, timeError, warningMsg, totalPrice, finishTimeStr } = useMemo(() => {
    const selectedServices = services.filter(s => formData.serviceIds.includes(s.id));
    const total = selectedServices.reduce((sum, s) => sum + (s.StandardMinutes || s.standardMinutes || 60), 0);
    const priceSum = selectedServices.reduce((sum, s) => sum + (s.basePrice || s.BasePrice || 0), 0);

    let error = "";
    let overnight = false;
    let warning = "";
    let finishTime = "";

    if (formData.startTime && formData.appointmentDate) {
      const [sh, sm] = formData.startTime.split(':').map(Number);

      // Helper to calculate next working time
      let current = new Date(`${formData.appointmentDate}T${formData.startTime}`);
      let remaining = total;

      while (remaining > 0) {
        const day = current.getDay();
        const isSunday = day === 0;
        const openMins = 7 * 60;
        const lunchStart = 12 * 60;
        const lunchEnd = 13.5 * 60;
        const closeMins = isSunday ? 12 * 60 : 17.5 * 60; // 17:30

        const curMins = current.getHours() * 60 + current.getMinutes();

        if (curMins < openMins) {
          current.setHours(7, 0, 0, 0);
          continue;
        }
        if (curMins >= closeMins) {
          current.setDate(current.getDate() + 1);
          current.setHours(7, 0, 0, 0);
          overnight = true;
          continue;
        }
        if (curMins >= lunchStart && curMins < lunchEnd) {
          current.setHours(13, 30, 0, 0);
          continue;
        }

        let toNext = (curMins < lunchStart) ? (lunchStart - curMins) : (closeMins - curMins);
        let consume = Math.min(remaining, toNext);
        current.setMinutes(current.getMinutes() + consume);
        remaining -= consume;
      }

      // Check if it's the same day as scheduled
      const schedDate = new Date(formData.appointmentDate);
      if (current.getDate() !== schedDate.getDate() || current.getMonth() !== schedDate.getMonth()) {
        overnight = true;
      }

      const fh = current.getHours().toString().padStart(2, '0');
      const fm = current.getMinutes().toString().padStart(2, '0');
      const fDate = current.toLocaleDateString('vi-VN', { day: '2-digit', month: '2-digit' });

      if (overnight) {
        finishTime = `${fh}:${fm} - NgÃ y ${fDate}`;
        warning = "LÆ°u Ã½: Thá»i gian hoÃ n thÃ nh dá»± kiáº¿n sang (nhá»¯ng) ngÃ y tiáº¿p theo.";
      } else {
        finishTime = `${fh}:${fm}`;
        if (current.getHours() >= 13) {
          const [hStart] = formData.startTime.split(':').map(Number);
          if (hStart < 12) warning = "Cáº£nh bÃ¡o: Xe sáº½ Ä‘Æ°á»£c giao vÃ o buá»•i chiá»u.";
        }
      }

      // Validation Errors
      const startTotal = sh * 60 + sm;
      const now = new Date();
      const todayStr = now.toISOString().split('T')[0];

      if (formData.appointmentDate === todayStr) {
        const currentMins = now.getHours() * 60 + now.getMinutes();
        if (startTotal < currentMins + 30) {
          error = "Thá»i gian háº¹n pháº£i cÃ¡ch thá»i gian hiá»‡n táº¡i Ã­t nháº¥t 30 phÃºt.";
        }
      }

      if (!error) {
        if (startTotal < 7 * 60) {
          error = "Giá» lÃ m viá»‡c báº¯t Ä‘áº§u tá»« 07:00.";
        } else if (startTotal > 11.5 * 60 && startTotal < 13.5 * 60) {
          error = "Giá» nghá»‰ trÆ°a 12:00 - 13:30. Vui lÃ²ng Ä‘áº·t lá»‹ch trÆ°á»›c 11:30 hoáº·c sau 13:30";
        } else {
          const closeTime = 17.5 * 60; // Closing at 17:30
          if (startTotal > 17 * 60) {
            error = "Vui lÃ²ng Ä‘áº·t lá»‹ch trÆ°á»›c 17:00 (trÆ°á»›c giá» Ä‘Ã³ng cá»­a Ã­t nháº¥t 30 phÃºt).";
          }
        }
      }
    }

    return { totalMinutes: total, isOvernight: overnight, timeError: error, warningMsg: warning, totalPrice: priceSum, finishTimeStr: finishTime };
  }, [formData.serviceIds, formData.startTime, formData.appointmentDate, services]);

  const handleSubmit = (e) => {
    e.preventDefault();
    if (timeError) {
      alert(timeError);
      return;
    }

    if (formData.isQuickOrder) {
      if (!formData.guestName || !formData.guestPhone) {
        alert("Vui lÃ²ng nháº­p Ä‘áº§y Ä‘á»§ há» tÃªn vÃ  SÄT cá»§a khÃ¡ch.");
        return;
      }
      // Info xe cá»§a khÃ¡ch vÃ£ng lai lÃ  tÃ¹y chá»n, nhÆ°ng náº¿u nháº­p thÃ¬ pháº£i Ä‘áº§y Ä‘á»§
      if ((formData.guestVehiclePlate || guestSelectedBrandId || formData.guestVehicleModelId) && (!formData.guestVehiclePlate || !guestSelectedBrandId || !formData.guestVehicleModelId)) {
        alert("Vui lÃ²ng nháº­p Ä‘áº§y Ä‘á»§ Biá»ƒn sá»‘, HÃ£ng vÃ  DÃ²ng xe.");
        return;
      }
    } else {
      if (!formData.customerId) {
        alert("Vui lÃ²ng chá»n khÃ¡ch hÃ ng.");
        return;
      }
      if (!formData.isNewVehicle && !formData.vehicleId) {
        alert("Vui lÃ²ng chá»n xe hoáº·c tÃ­ch chá»n 'Xe má»›i'.");
        return;
      }
      if (formData.isNewVehicle && (!formData.vehicleLicensePlate || !formData.vehicleModelId)) {
        alert("Vui lÃ²ng nháº­p Ä‘áº§y Ä‘á»§ thÃ´ng tin xe má»›i (Biá»ƒn sá»‘ & DÃ²ng xe).");
        return;
      }
    }

    onSubmit({ ...formData, totalMinutes });
  };

  return (
    <form className="admin-form" onSubmit={handleSubmit}>
      <div className="quick-toggle" style={{ marginBottom: '20px', display: 'flex', alignItems: 'center', gap: '10px' }}>
        <input
          type="checkbox"
          id="isQuickOrderAppt"
          checked={formData.isQuickOrder}
          onChange={toggleQuickOrder}
          style={{ width: '18px', height: '18px', cursor: 'pointer' }}
        />
        <label htmlFor="isQuickOrderAppt" style={{ cursor: 'pointer', fontWeight: 600 }}>
          KhÃ¡ch vÃ£ng lai (ChÆ°a cÃ³ tÃ i khoáº£n)
        </label>
      </div>

      {!formData.isQuickOrder ? (
        <>
          <div className="form-group">
            <label>TÃ¬m kiáº¿m KhÃ¡ch hÃ ng *</label>
            <div style={{ position: 'relative' }}>
              <input
                type="text"
                placeholder="Nháº­p Há» tÃªn hoáº·c SÄT..."
                value={search}
                onChange={(e) => setSearch(e.target.value)}
                required={!formData.customerId}
              />
              {filteredCustomers.length > 0 && (
                <div className="dropdown-panel" style={{ position: 'absolute', top: '100%', left: 0, right: 0, background: '#fff', border: '1px solid #e9edf7', borderRadius: '8px', zIndex: 10, boxShadow: '0 4px 12px rgba(0,0,0,0.1)' }}>
                  {filteredCustomers.map(c => (
                    <div key={c.id} className="dropdown-item" style={{ padding: '8px 12px', cursor: 'pointer', borderBottom: '1px solid #f4f7fe' }} onClick={() => selectCustomer(c)}>
                      <strong>{c.fullName}</strong> â€” {c.phone}
                    </div>
                  ))}
                </div>
              )}
            </div>
          </div>

          {formData.customerId && (
            <div className="customer-info-box" style={{ padding: '12px', background: '#e9edf7', borderRadius: '12px', marginBottom: '16px' }}>
              <p style={{ margin: 0, fontSize: '14px', color: '#2b3674' }}><strong>KhÃ¡ch hÃ ng:</strong> {formData.customerName} ({formData.customerPhone})</p>

              <div style={{ marginTop: '10px', display: 'flex', alignItems: 'center', gap: '8px' }}>
                <input
                  type="checkbox"
                  id="isNewVeh"
                  checked={formData.isNewVehicle}
                  disabled={customerVehiclesFiltered.length === 0}
                  onChange={(e) => setFormData(prev => ({ ...prev, isNewVehicle: e.target.checked, vehicleId: e.target.checked ? "" : prev.vehicleId }))}
                />
                <label htmlFor="isNewVeh" style={{ fontSize: '13px', cursor: 'pointer', fontWeight: 600 }}>Sá»­ dá»¥ng xe má»›i (chÆ°a cÃ³ trong danh sÃ¡ch)</label>
              </div>
            </div>
          )}

          {!formData.isNewVehicle ? (
            <div className="form-group">
              <label>Chá»n xe cá»§a khÃ¡ch *</label>
              <select
                name="vehicleId"
                value={formData.vehicleId}
                onChange={(e) => {
                  const v = vehicles.find(veh => veh.id === Number(e.target.value));
                  setFormData(prev => ({ ...prev, vehicleId: e.target.value, vehicleLicensePlate: v?.licensePlate || "" }));
                }}
                required={formData.customerId && !formData.isNewVehicle}
                disabled={!formData.customerId}
              >
                <option value="">-- Chá»n xe --</option>
                {customerVehiclesFiltered.map(v => (
                  <option key={v.id} value={v.id}>
                    {v.licensePlate} {(v.brandName || v.modelName) ? `(${v.brandName || ""} ${v.modelName || ""})` : ""}
                  </option>
                ))}
              </select>
            </div>
          ) : (
            <div className="new-vehicle-fields" style={{ padding: '16px', background: '#fff9e6', borderRadius: '12px', border: '1px solid #ffe58f', marginBottom: '16px' }}>
              <h4 style={{ marginTop: 0, marginBottom: '12px', fontSize: '14px', color: '#856404' }}>ThÃ´ng tin xe má»›i</h4>
              <div className="form-group">
                <label>Biá»ƒn sá»‘ xe *</label>
                <input type="text" name="vehicleLicensePlate" value={formData.vehicleLicensePlate} onChange={handleChange} onBlur={handlePlateBlur} required placeholder="VD: 30A-11111" style={{ textTransform: 'uppercase' }} />
              </div>
              <div className="form-row" style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '12px' }}>
                <div className="form-group">
                  <label>HÃ£ng xe *</label>
                  <select value={selectedBrandId} onChange={(e) => setSelectedBrandId(e.target.value)} required style={{ background: '#1b254b', color: '#fff', border: '1px solid #4318ff' }}>
                    <option value="" style={{ background: '#1b254b', color: '#fff' }}>-- Chá»n hÃ£ng --</option>
                    {brands.map(b => <option key={b.id} value={b.id} style={{ background: '#1b254b', color: '#fff' }}>{b.name}</option>)}
                  </select>
                </div>
                <div className="form-group">
                  <label>DÃ²ng xe *</label>
                  <select name="vehicleModelId" value={formData.vehicleModelId} onChange={handleChange} required disabled={!selectedBrandId} style={{ background: '#1b254b', color: '#fff', border: '1px solid #4318ff' }}>
                    <option value="" style={{ background: '#1b254b', color: '#fff' }}>-- Chá»n dÃ²ng xe --</option>
                    {models.filter(m => m.brandId === Number(selectedBrandId)).map(m => (
                      <option key={m.id} value={m.id} style={{ background: '#1b254b', color: '#fff' }}>{m.modelName}</option>
                    ))}
                  </select>
                </div>
              </div>
              <div className="form-row" style={{ display: 'grid', gridTemplateColumns: 'minmax(100px, 1fr) minmax(80px, 0.8fr) minmax(150px, 1.5fr)', gap: '12px' }}>
                <div className="form-group">
                  <label>MÃ u sáº¯c</label>
                  <input type="text" name="vehicleColor" value={formData.vehicleColor} onChange={handleChange} placeholder="VD: Tráº¯ng" />
                </div>
                <div className="form-group">
                  <label>NÄƒm SX *</label>
                  <select name="vehicleYear" value={formData.vehicleYear} onChange={handleChange} required style={{ background: '#1b254b', color: '#fff', border: '1px solid #4318ff' }}>
                    <option value="" style={{ background: '#1b254b', color: '#fff' }}>Chá»n nÄƒm</option>
                    {years.map(y => <option key={y} value={y} style={{ background: '#1b254b', color: '#fff' }}>{y}</option>)}
                  </select>
                </div>
                <div className="form-group">
                  <label>Sá»‘ khung (VIN)</label>
                  <input type="text" name="vehicleVin" value={formData.vehicleVin} onChange={handleChange} placeholder="Nháº­p sá»‘ khung VIN..." />
                </div>
              </div>
            </div>
          )}
        </>
      ) : (
        <div className="guest-info-fields" style={{ padding: '16px', background: '#f4f7fe', borderRadius: '16px', marginBottom: '20px', border: '1px dashed #4318ff' }}>
          <div className="form-row" style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '12px' }}>
            <div className="form-group">
              <label>Há» tÃªn khÃ¡ch *</label>
              <input type="text" name="guestName" value={formData.guestName} onChange={handleChange} required placeholder="VD: Nguyá»…n VÄƒn A" />
            </div>
            <div className="form-group">
              <label>Sá»‘ Ä‘iá»‡n thoáº¡i *</label>
              <input type="text" name="guestPhone" value={formData.guestPhone} onChange={handleChange} required placeholder="VD: 0987654321" />
            </div>
          </div>

          <div style={{ borderTop: '1px solid #e9edf7', margin: '15px 0', paddingTop: '15px' }}>
            <strong>ThÃ´ng tin xe vÃ£ng lai</strong>
          </div>

          <div className="form-group" style={{ marginTop: '10px' }}>
            <label>Biá»ƒn sá»‘ xe {(formData.guestVehiclePlate || guestSelectedBrandId) ? '*' : ''}</label>
            <input type="text" name="guestVehiclePlate" value={formData.guestVehiclePlate} onChange={handleChange} onBlur={handlePlateBlur} placeholder="VD: 30A-12345 (TÃ¹y chá»n)" required={!!guestSelectedBrandId} style={{ textTransform: 'uppercase', fontWeight: 700 }} />
          </div>

          <div className="form-row" style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '12px' }}>
            <div className="form-group">
              <label>HÃ£ng xe {(formData.guestVehiclePlate || guestSelectedBrandId) ? '*' : ''}</label>
              <select value={guestSelectedBrandId} onChange={(e) => setGuestSelectedBrandId(e.target.value)} required={!!formData.guestVehiclePlate} style={{ background: '#1b254b', color: '#fff', border: '1px solid #4318ff' }}>
                <option value="" style={{ background: '#1b254b', color: '#fff' }}>-- Chá»n hÃ£ng --</option>
                {brands.map(b => <option key={b.id} value={b.id} style={{ background: '#1b254b', color: '#fff' }}>{b.name}</option>)}
              </select>
            </div>
            <div className="form-group">
              <label>DÃ²ng xe {(formData.guestVehiclePlate || guestSelectedBrandId) ? '*' : ''}</label>
              <select name="guestVehicleModelId" value={formData.guestVehicleModelId} onChange={handleChange} disabled={!guestSelectedBrandId} required={!!formData.guestVehiclePlate || !!guestSelectedBrandId} style={{ background: '#1b254b', color: '#fff', border: '1px solid #4318ff' }}>
                <option value="" style={{ background: '#1b254b', color: '#fff' }}>-- Chá»n dÃ²ng xe --</option>
                {models.filter(m => m.brandId === Number(guestSelectedBrandId)).map(m => (
                  <option key={m.id} value={m.id} style={{ background: '#1b254b', color: '#fff' }}>{m.modelName}</option>
                ))}
              </select>
            </div>
          </div>

          <div className="form-row" style={{ display: 'grid', gridTemplateColumns: 'minmax(100px, 1fr) minmax(80px, 0.8fr) minmax(150px, 1.5fr)', gap: '12px' }}>
            <div className="form-group">
              <label>MÃ u sáº¯c</label>
              <input type="text" name="guestVehicleColor" value={formData.guestVehicleColor} onChange={handleChange} placeholder="VD: Äen" />
            </div>
            <div className="form-group">
              <label>NÄƒm SX *</label>
              <select name="guestVehicleYear" value={formData.guestVehicleYear} onChange={handleChange} required={!!formData.guestVehiclePlate || !!guestSelectedBrandId} style={{ background: '#1b254b', color: '#fff', border: '1px solid #4318ff' }}>
                <option value="" style={{ background: '#1b254b', color: '#fff' }}>Chá»n nÄƒm</option>
                {years.map(y => <option key={y} value={y} style={{ background: '#1b254b', color: '#fff' }}>{y}</option>)}
              </select>
            </div>
            <div className="form-group">
              <label>Sá»‘ khung (VIN)</label>
              <input type="text" name="guestVehicleVin" value={formData.guestVehicleVin} onChange={handleChange} placeholder="Nháº­p sá»‘ khung VIN..." />
            </div>
          </div>
        </div>
      )}



      <div className="form-group">
        <label>TÃ¬m & Chá»n dá»‹ch vá»¥ yÃªu cáº§u</label>
        <div style={{ position: 'relative', marginBottom: '10px' }}>
          <input
            type="text"
            placeholder="GÃµ Ä‘á»ƒ tÃ¬m dá»‹ch vá»¥ (VD: Thay nhá»›t, Rá»­a xe...)"
            value={serviceSearch}
            onChange={(e) => setServiceSearch(e.target.value)}
          />
          {filteredServices.length > 0 && (
            <div className="dropdown-panel" style={{ position: 'absolute', top: '100%', left: 0, right: 0, background: '#fff', border: '1px solid #e9edf7', borderRadius: '8px', zIndex: 10, boxShadow: '0 4px 12px rgba(0,0,0,0.1)' }}>
              {filteredServices.map(s => (
                <div key={s.id} className="dropdown-item" style={{ padding: '8px 12px', cursor: 'pointer', borderBottom: '1px solid #f4f7fe' }} onClick={() => handleServiceToggle(s.id)}>
                  {s.Name || s.name} <span style={{ float: 'right', color: '#05cd99', fontSize: '12px' }}>{s.basePrice?.toLocaleString()}Ä‘</span>
                </div>
              ))}
            </div>
          )}
          {serviceSearch && filteredServices.length === 0 && (
            <div className="dropdown-panel" style={{ position: 'absolute', top: '100%', left: 0, right: 0, background: '#fff', border: '1px solid #e9edf7', borderRadius: '8px', zIndex: 10, padding: '12px', color: '#a3aed0', fontStyle: 'italic' }}>
              KhÃ´ng tÃ¬m tháº¥y dá»‹ch vá»¥ nÃ o phÃ¹ há»£p vá»›i "{serviceSearch}"
            </div>
          )}
        </div>

        {formData.serviceIds.length > 0 && (
          <div className="selected-services-tags" style={{ display: 'flex', flexWrap: 'wrap', gap: '8px', padding: '10px', background: '#f4f7fe', borderRadius: '12px' }}>
            {formData.serviceIds.map(sid => {
              const s = services.find(srv => srv.id === sid);
              return (
                <div key={sid} style={{ background: '#4318ff', color: '#fff', padding: '4px 10px', borderRadius: '20px', fontSize: '13px', display: 'flex', alignItems: 'center', gap: '6px' }}>
                  {s?.Name || s?.name || "Unknown"} - {s?.StandardMinutes || s?.standardMinutes || 0} phÃºt - {(s?.basePrice || s?.BasePrice || 0).toLocaleString()}Ä‘
                  <span style={{ cursor: 'pointer', fontWeight: 'bold', marginLeft: '4px' }} onClick={() => handleServiceToggle(sid)}>Ã—</span>
                </div>
              );
            })}
          </div>
        )}

        {formData.serviceIds.length > 0 && (
          <>
            <div style={{ marginTop: '12px', padding: '12px', background: '#e9edf7', borderRadius: '8px', color: '#1b254b', fontSize: '14px', display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
              <div>
                <strong>Tá»•ng thá»i gian:</strong> {totalMinutes} phÃºt
                {finishTimeStr && <span> (Dá»± kiáº¿n xong: <strong style={{ color: '#4318ff' }}>{finishTimeStr}</strong>)</span>}
              </div>
              <div>
                <strong>Tá»•ng tiá»n:</strong> <span style={{ color: '#05cd99', fontWeight: 'bold', fontSize: '16px' }}>{totalPrice?.toLocaleString() || 0}Ä‘</span>
              </div>
            </div>
            <div style={{ marginTop: '8px', fontSize: '12px', color: '#e6522c', fontStyle: 'italic', fontWeight: 500 }}>
              * LÆ°u Ã½: GiÃ¡ trÃªn chÆ°a bao gá»“m váº­t tÆ°/phá»¥ tÃ¹ng. Ká»¹ thuáº­t viÃªn sáº½ bÃ¡o giÃ¡ chÃ­nh xÃ¡c sau khi kiá»ƒm tra xe trá»±c tiáº¿p.
            </div>
          </>
        )}
      </div>

      <div className="form-group" style={{ marginTop: '10px' }}>
        <label>NgÃ y & Giá» háº¹n *</label>
        <DateTimePicker
          value={formData.appointmentDate && formData.startTime ? `${formData.appointmentDate}T${formData.startTime}` : ""}
          onChange={(val) => {
            if (!val) {
              setFormData(prev => ({ ...prev, appointmentDate: "", startTime: "" }));
              return;
            }
            const d = new Date(val);
            const dateStr = d.toISOString().split('T')[0];
            const timeStr = `${d.getHours().toString().padStart(2, '0')}:${d.getMinutes().toString().padStart(2, '0')}`;
            setFormData(prev => ({ ...prev, appointmentDate: dateStr, startTime: timeStr }));
          }}
          minDate={new Date()}
          placeholder="Chá»n ngÃ y vÃ  giá» háº¹n..."
        />
        {timeError && <div style={{ color: '#ee5d50', fontSize: '11px', fontWeight: 600, marginTop: '4px' }}>âŒ {timeError}</div>}
        {warningMsg && !timeError && <div style={{ color: '#ff8f00', fontSize: '11px', fontWeight: 600, marginTop: '4px' }}>âš ï¸ {warningMsg}</div>}
        {!timeError && <div style={{ color: '#4318ff', fontSize: '11px', fontWeight: 600, marginTop: '8px', lineHeight: '1.4' }}>â„¹ï¸ Gara há»— trá»£ giao xe Ä‘áº¿n 19:00 náº¿u xe Ä‘Ã£ xong cho khÃ¡ch Ä‘Ã£ thanh toÃ¡n nhÆ°ng Ä‘áº¿n láº¥y muá»™n, ngÆ°á»£c láº¡i giao xe vÃ o hÃ´m sau.</div>}
      </div>
      <div className="form-group" style={{ marginTop: '10px' }}>
        <label>Ghi chÃº</label>
        <textarea name="customerNotes" value={formData.customerNotes} onChange={handleChange} rows={2} placeholder="YÃªu cáº§u riÃªng cá»§a khÃ¡ch..." style={{ resize: 'none' }} />
      </div>

      <div className="form-actions" style={{ marginTop: '20px' }}>
        <button type="button" className="btn-ghost" onClick={onCancel}>Há»§y bá»</button>
        <button type="submit" className="btn-primary" disabled={loading}>
          {loading ? "Äang xá»­ lÃ½..." : "XÃ¡c nháº­n Ä‘áº·t lá»‹ch"}
        </button>
      </div>
    </form>
  );
}

