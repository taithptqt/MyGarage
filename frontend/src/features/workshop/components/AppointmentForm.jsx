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
        finishTime = `${fh}:${fm} - Ngày ${fDate}`;
        warning = "Lưu ý: Thời gian hoàn thành dự kiến sang (những) ngày tiếp theo.";
      } else {
        finishTime = `${fh}:${fm}`;
        if (current.getHours() >= 13) {
          const [hStart] = formData.startTime.split(':').map(Number);
          if (hStart < 12) warning = "Cảnh báo: Xe sẽ được giao vào buổi chiều.";
        }
      }

      // Validation Errors
      const startTotal = sh * 60 + sm;
      const now = new Date();
      const todayStr = now.toISOString().split('T')[0];

      if (formData.appointmentDate === todayStr) {
        const currentMins = now.getHours() * 60 + now.getMinutes();
        if (startTotal < currentMins + 30) {
          error = "Thời gian hẹn phải cách thời gian hiện tại ít nhất 30 phút.";
        }
      }

      if (!error) {
        if (startTotal < 7 * 60) {
          error = "Giờ làm việc bắt đầu từ 07:00.";
        } else if (startTotal > 11.5 * 60 && startTotal < 13.5 * 60) {
          error = "Giờ nghỉ trưa 12:00 - 13:30. Vui lòng đặt lịch trước 11:30 hoặc sau 13:30";
        } else {
          const closeTime = 17.5 * 60; // Closing at 17:30
          if (startTotal > 17 * 60) {
            error = "Vui lòng đặt lịch trước 17:00 (trước giờ đóng cửa ít nhất 30 phút).";
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
        alert("Vui lòng nhập đầy đủ họ tên và SĐT của khách.");
        return;
      }
      // Info xe của khách vãng lai là tùy chọn, nhưng nếu nhập thì phải đầy đủ
      if ((formData.guestVehiclePlate || guestSelectedBrandId || formData.guestVehicleModelId) && (!formData.guestVehiclePlate || !guestSelectedBrandId || !formData.guestVehicleModelId)) {
        alert("Vui lòng nhập đầy đủ Biển số, Hãng và Dòng xe.");
        return;
      }
    } else {
      if (!formData.customerId) {
        alert("Vui lòng chọn khách hàng.");
        return;
      }
      if (!formData.isNewVehicle && !formData.vehicleId) {
        alert("Vui lòng chọn xe hoặc tích chọn 'Xe mới'.");
        return;
      }
      if (formData.isNewVehicle && (!formData.vehicleLicensePlate || !formData.vehicleModelId)) {
        alert("Vui lòng nhập đầy đủ thông tin xe mới (Biển số & Dòng xe).");
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
          Khách vãng lai (Chưa có tài khoản)
        </label>
      </div>

      {!formData.isQuickOrder ? (
        <>
          <div className="form-group">
            <label>Tìm kiếm Khách hàng *</label>
            <div style={{ position: 'relative' }}>
              <input
                type="text"
                placeholder="Nhập Họ tên hoặc SĐT..."
                value={search}
                onChange={(e) => setSearch(e.target.value)}
                required={!formData.customerId}
              />
              {filteredCustomers.length > 0 && (
                <div className="dropdown-panel" style={{ position: 'absolute', top: '100%', left: 0, right: 0, background: '#fff', border: '1px solid #e9edf7', borderRadius: '8px', zIndex: 10, boxShadow: '0 4px 12px rgba(0,0,0,0.1)' }}>
                  {filteredCustomers.map(c => (
                    <div key={c.id} className="dropdown-item" style={{ padding: '8px 12px', cursor: 'pointer', borderBottom: '1px solid #f4f7fe' }} onClick={() => selectCustomer(c)}>
                      <strong>{c.fullName}</strong> — {c.phone}
                    </div>
                  ))}
                </div>
              )}
            </div>
          </div>

          {formData.customerId && (
            <div className="customer-info-box" style={{ padding: '12px', background: '#e9edf7', borderRadius: '12px', marginBottom: '16px' }}>
              <p style={{ margin: 0, fontSize: '14px', color: '#2b3674' }}><strong>Khách hàng:</strong> {formData.customerName} ({formData.customerPhone})</p>

              <div style={{ marginTop: '10px', display: 'flex', alignItems: 'center', gap: '8px' }}>
                <input
                  type="checkbox"
                  id="isNewVeh"
                  checked={formData.isNewVehicle}
                  disabled={customerVehiclesFiltered.length === 0}
                  onChange={(e) => setFormData(prev => ({ ...prev, isNewVehicle: e.target.checked, vehicleId: e.target.checked ? "" : prev.vehicleId }))}
                />
                <label htmlFor="isNewVeh" style={{ fontSize: '13px', cursor: 'pointer', fontWeight: 600 }}>Sử dụng xe mới (chưa có trong danh sách)</label>
              </div>
            </div>
          )}

          {!formData.isNewVehicle ? (
            <div className="form-group">
              <label>Chọn xe của khách *</label>
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
                <option value="">-- Chọn xe --</option>
                {customerVehiclesFiltered.map(v => (
                  <option key={v.id} value={v.id}>
                    {v.licensePlate} {(v.brandName || v.modelName) ? `(${v.brandName || ""} ${v.modelName || ""})` : ""}
                  </option>
                ))}
              </select>
            </div>
          ) : (
            <div className="new-vehicle-fields" style={{ padding: '16px', background: '#fff9e6', borderRadius: '12px', border: '1px solid #ffe58f', marginBottom: '16px' }}>
              <h4 style={{ marginTop: 0, marginBottom: '12px', fontSize: '14px', color: '#856404' }}>Thông tin xe mới</h4>
              <div className="form-group">
                <label>Biển số xe *</label>
                <input type="text" name="vehicleLicensePlate" value={formData.vehicleLicensePlate} onChange={handleChange} onBlur={handlePlateBlur} required placeholder="VD: 30A-11111" style={{ textTransform: 'uppercase' }} />
              </div>
              <div className="form-row" style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '12px' }}>
                <div className="form-group">
                  <label>Hãng xe *</label>
                  <select value={selectedBrandId} onChange={(e) => setSelectedBrandId(e.target.value)} required style={{ background: '#1b254b', color: '#fff', border: '1px solid #4318ff' }}>
                    <option value="" style={{ background: '#1b254b', color: '#fff' }}>-- Chọn hãng --</option>
                    {brands.map(b => <option key={b.id} value={b.id} style={{ background: '#1b254b', color: '#fff' }}>{b.name}</option>)}
                  </select>
                </div>
                <div className="form-group">
                  <label>Dòng xe *</label>
                  <select name="vehicleModelId" value={formData.vehicleModelId} onChange={handleChange} required disabled={!selectedBrandId} style={{ background: '#1b254b', color: '#fff', border: '1px solid #4318ff' }}>
                    <option value="" style={{ background: '#1b254b', color: '#fff' }}>-- Chọn dòng xe --</option>
                    {models.filter(m => m.brandId === Number(selectedBrandId)).map(m => (
                      <option key={m.id} value={m.id} style={{ background: '#1b254b', color: '#fff' }}>{m.modelName}</option>
                    ))}
                  </select>
                </div>
              </div>
              <div className="form-row" style={{ display: 'grid', gridTemplateColumns: 'minmax(100px, 1fr) minmax(80px, 0.8fr) minmax(150px, 1.5fr)', gap: '12px' }}>
                <div className="form-group">
                  <label>Màu sắc</label>
                  <input type="text" name="vehicleColor" value={formData.vehicleColor} onChange={handleChange} placeholder="VD: Trắng" />
                </div>
                <div className="form-group">
                  <label>Năm SX *</label>
                  <select name="vehicleYear" value={formData.vehicleYear} onChange={handleChange} required style={{ background: '#1b254b', color: '#fff', border: '1px solid #4318ff' }}>
                    <option value="" style={{ background: '#1b254b', color: '#fff' }}>Chọn năm</option>
                    {years.map(y => <option key={y} value={y} style={{ background: '#1b254b', color: '#fff' }}>{y}</option>)}
                  </select>
                </div>
                <div className="form-group">
                  <label>Số khung (VIN)</label>
                  <input type="text" name="vehicleVin" value={formData.vehicleVin} onChange={handleChange} placeholder="Nhập số khung VIN..." />
                </div>
              </div>
            </div>
          )}
        </>
      ) : (
        <div className="guest-info-fields" style={{ padding: '16px', background: '#f4f7fe', borderRadius: '16px', marginBottom: '20px', border: '1px dashed #4318ff' }}>
          <div className="form-row" style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '12px' }}>
            <div className="form-group">
              <label>Họ tên khách *</label>
              <input type="text" name="guestName" value={formData.guestName} onChange={handleChange} required placeholder="VD: Nguyễn Văn A" />
            </div>
            <div className="form-group">
              <label>Số điện thoại *</label>
              <input type="text" name="guestPhone" value={formData.guestPhone} onChange={handleChange} required placeholder="VD: 0987654321" />
            </div>
          </div>

          <div style={{ borderTop: '1px solid #e9edf7', margin: '15px 0', paddingTop: '15px' }}>
            <strong>Thông tin xe vãng lai</strong>
          </div>

          <div className="form-group" style={{ marginTop: '10px' }}>
            <label>Biển số xe {(formData.guestVehiclePlate || guestSelectedBrandId) ? '*' : ''}</label>
            <input type="text" name="guestVehiclePlate" value={formData.guestVehiclePlate} onChange={handleChange} onBlur={handlePlateBlur} placeholder="VD: 30A-12345 (Tùy chọn)" required={!!guestSelectedBrandId} style={{ textTransform: 'uppercase', fontWeight: 700 }} />
          </div>

          <div className="form-row" style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '12px' }}>
            <div className="form-group">
              <label>Hãng xe {(formData.guestVehiclePlate || guestSelectedBrandId) ? '*' : ''}</label>
              <select value={guestSelectedBrandId} onChange={(e) => setGuestSelectedBrandId(e.target.value)} required={!!formData.guestVehiclePlate} style={{ background: '#1b254b', color: '#fff', border: '1px solid #4318ff' }}>
                <option value="" style={{ background: '#1b254b', color: '#fff' }}>-- Chọn hãng --</option>
                {brands.map(b => <option key={b.id} value={b.id} style={{ background: '#1b254b', color: '#fff' }}>{b.name}</option>)}
              </select>
            </div>
            <div className="form-group">
              <label>Dòng xe {(formData.guestVehiclePlate || guestSelectedBrandId) ? '*' : ''}</label>
              <select name="guestVehicleModelId" value={formData.guestVehicleModelId} onChange={handleChange} disabled={!guestSelectedBrandId} required={!!formData.guestVehiclePlate || !!guestSelectedBrandId} style={{ background: '#1b254b', color: '#fff', border: '1px solid #4318ff' }}>
                <option value="" style={{ background: '#1b254b', color: '#fff' }}>-- Chọn dòng xe --</option>
                {models.filter(m => m.brandId === Number(guestSelectedBrandId)).map(m => (
                  <option key={m.id} value={m.id} style={{ background: '#1b254b', color: '#fff' }}>{m.modelName}</option>
                ))}
              </select>
            </div>
          </div>

          <div className="form-row" style={{ display: 'grid', gridTemplateColumns: 'minmax(100px, 1fr) minmax(80px, 0.8fr) minmax(150px, 1.5fr)', gap: '12px' }}>
            <div className="form-group">
              <label>Màu sắc</label>
              <input type="text" name="guestVehicleColor" value={formData.guestVehicleColor} onChange={handleChange} placeholder="VD: Đen" />
            </div>
            <div className="form-group">
              <label>Năm SX *</label>
              <select name="guestVehicleYear" value={formData.guestVehicleYear} onChange={handleChange} required={!!formData.guestVehiclePlate || !!guestSelectedBrandId} style={{ background: '#1b254b', color: '#fff', border: '1px solid #4318ff' }}>
                <option value="" style={{ background: '#1b254b', color: '#fff' }}>Chọn năm</option>
                {years.map(y => <option key={y} value={y} style={{ background: '#1b254b', color: '#fff' }}>{y}</option>)}
              </select>
            </div>
            <div className="form-group">
              <label>Số khung (VIN)</label>
              <input type="text" name="guestVehicleVin" value={formData.guestVehicleVin} onChange={handleChange} placeholder="Nhập số khung VIN..." />
            </div>
          </div>
        </div>
      )}



      <div className="form-group">
        <label>Tìm & Chọn dịch vụ yêu cầu</label>
        <div style={{ position: 'relative', marginBottom: '10px' }}>
          <input
            type="text"
            placeholder="Gõ để tìm dịch vụ (VD: Thay nhớt, Rửa xe...)"
            value={serviceSearch}
            onChange={(e) => setServiceSearch(e.target.value)}
          />
          {filteredServices.length > 0 && (
            <div className="dropdown-panel" style={{ position: 'absolute', top: '100%', left: 0, right: 0, background: '#fff', border: '1px solid #e9edf7', borderRadius: '8px', zIndex: 10, boxShadow: '0 4px 12px rgba(0,0,0,0.1)' }}>
              {filteredServices.map(s => (
                <div key={s.id} className="dropdown-item" style={{ padding: '8px 12px', cursor: 'pointer', borderBottom: '1px solid #f4f7fe' }} onClick={() => handleServiceToggle(s.id)}>
                  {s.Name || s.name} <span style={{ float: 'right', color: '#05cd99', fontSize: '12px' }}>{s.basePrice?.toLocaleString()}đ</span>
                </div>
              ))}
            </div>
          )}
          {serviceSearch && filteredServices.length === 0 && (
            <div className="dropdown-panel" style={{ position: 'absolute', top: '100%', left: 0, right: 0, background: '#fff', border: '1px solid #e9edf7', borderRadius: '8px', zIndex: 10, padding: '12px', color: '#a3aed0', fontStyle: 'italic' }}>
              Không tìm thấy dịch vụ nào phù hợp với "{serviceSearch}"
            </div>
          )}
        </div>

        {formData.serviceIds.length > 0 && (
          <div className="selected-services-tags" style={{ display: 'flex', flexWrap: 'wrap', gap: '8px', padding: '10px', background: '#f4f7fe', borderRadius: '12px' }}>
            {formData.serviceIds.map(sid => {
              const s = services.find(srv => srv.id === sid);
              return (
                <div key={sid} style={{ background: '#4318ff', color: '#fff', padding: '4px 10px', borderRadius: '20px', fontSize: '13px', display: 'flex', alignItems: 'center', gap: '6px' }}>
                  {s?.Name || s?.name || "Unknown"} - {s?.StandardMinutes || s?.standardMinutes || 0} phút - {(s?.basePrice || s?.BasePrice || 0).toLocaleString()}đ
                  <span style={{ cursor: 'pointer', fontWeight: 'bold', marginLeft: '4px' }} onClick={() => handleServiceToggle(sid)}>×</span>
                </div>
              );
            })}
          </div>
        )}

        {formData.serviceIds.length > 0 && (
          <>
            <div style={{ marginTop: '12px', padding: '12px', background: '#e9edf7', borderRadius: '8px', color: '#1b254b', fontSize: '14px', display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
              <div>
                <strong>Tổng thời gian:</strong> {totalMinutes} phút
                {finishTimeStr && <span> (Dự kiến xong: <strong style={{ color: '#4318ff' }}>{finishTimeStr}</strong>)</span>}
              </div>
              <div>
                <strong>Tổng tiền:</strong> <span style={{ color: '#05cd99', fontWeight: 'bold', fontSize: '16px' }}>{totalPrice?.toLocaleString() || 0}đ</span>
              </div>
            </div>
            <div style={{ marginTop: '8px', fontSize: '12px', color: '#e6522c', fontStyle: 'italic', fontWeight: 500 }}>
              * Lưu ý: Giá trên chưa bao gồm vật tư/phụ tùng. Kỹ thuật viên sẽ báo giá chính xác sau khi kiểm tra xe trực tiếp.
            </div>
          </>
        )}
      </div>

      <div className="form-group" style={{ marginTop: '10px' }}>
        <label>Ngày & Giờ hẹn *</label>
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
          placeholder="Chọn ngày và giờ hẹn..."
        />
        {timeError && <div style={{ color: '#ee5d50', fontSize: '11px', fontWeight: 600, marginTop: '4px' }}>❌ {timeError}</div>}
        {warningMsg && !timeError && <div style={{ color: '#ff8f00', fontSize: '11px', fontWeight: 600, marginTop: '4px' }}>⚠️ {warningMsg}</div>}
        {!timeError && <div style={{ color: '#4318ff', fontSize: '11px', fontWeight: 600, marginTop: '8px', lineHeight: '1.4' }}>Thông tin: Gara hỗ trợ giao xe đến 19:00 nếu xe đã xong cho khách đã thanh toán nhưng đến lấy muộn, ngược lại giao xe vào hôm sau.</div>}
      </div>
      <div className="form-group" style={{ marginTop: '10px' }}>
        <label>Ghi chú</label>
        <textarea name="customerNotes" value={formData.customerNotes} onChange={handleChange} rows={2} placeholder="Yêu cầu riêng của khách..." style={{ resize: 'none' }} />
      </div>

      <div className="form-actions" style={{ marginTop: '20px' }}>
        <button type="button" className="btn-ghost" onClick={onCancel}>Hủy bỏ</button>
        <button type="submit" className="btn-primary" disabled={loading}>
          {loading ? "Đang xử lý..." : "Xác nhận đặt lịch"}
        </button>
      </div>
    </form>
  );
}

