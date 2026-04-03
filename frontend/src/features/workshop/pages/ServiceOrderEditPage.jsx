import React, { useState, useEffect, useCallback, useMemo } from "react";
import { useParams, useNavigate } from "react-router-dom";
import { serviceOrderApi } from "@features/workshop/api/serviceOrderApi";
import { getAllStaff, getAllRoles } from "@features/admin/api/staffApi";
import { getAllServiceCategories, getAllStandardServices } from "@features/admin/api/serviceApi";
import { useBranch } from "@shared/core/context/BranchContext";
import DateTimePicker from "@features/workshop/components/DateTimePicker";
import "./Workshop.css";

export default function ServiceOrderEditPage() {
  const { orderId } = useParams();
  const navigate = useNavigate();
  const { selectedBranchId } = useBranch();

  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState("");

  const [order, setOrder] = useState(null);
  const [staff, setStaff] = useState([]);
  const [orderCategories, setOrderCategories] = useState([]);
  const [services, setServices] = useState([]);
  const [initialServiceDetails, setInitialServiceDetails] = useState([]);


  const [formData, setFormData] = useState({
    customerNotes: "",
    serviceAdvisorId: "",
    orderCategory: "",
    oldPartsReturnOption: "DISPOSE_ALL",
    requiredDepositAmount: 0,
    checkInTime: "",
    estimatedHandover: "",
    promisedHandoverTime: "",
    serviceIds: []
  });

  const [serviceSearch, setServiceSearch] = useState("");
  const [symptomInputs, setSymptomInputs] = useState([""]);
  const [focusedSymptomIndex, setFocusedSymptomIndex] = useState(0);

  const fetchData = useCallback(async () => {
    setLoading(true);
    try {
      const [orderRes, sRes, rRes, catRes, srvRes, detailsRes] = await Promise.all([
        serviceOrderApi.getById(orderId),
        getAllStaff(),
        getAllRoles(),
        getAllServiceCategories(),
        getAllStandardServices(),
        serviceOrderApi.getDetails(orderId).catch(() => [])
      ]);

      if (!orderRes) throw new Error("Không tìm thấy lệnh sửa chữa.");

      setOrder(orderRes);
      setOrderCategories(catRes || []);
      setServices(srvRes || []);

      const existingSvcs = (detailsRes || []).filter((d) => {
        const t = String(d?.itemType || "").toUpperCase();
        const isLaborOrService = t === "LABOR" || t === "SERVICE";
        const isFromDiagnosis = Boolean(d?.diagnoseRecommendationId);
        return isLaborOrService && !isFromDiagnosis;
      });
      const uniqueSvcMap = new Map();
      existingSvcs.forEach((d) => {
        const sid = Number(d.standardServiceId);
        if (sid > 0 && !uniqueSvcMap.has(sid)) uniqueSvcMap.set(sid, d);
      });
      const uniqueExistingSvcs = Array.from(uniqueSvcMap.values());
      setInitialServiceDetails(uniqueExistingSvcs);
      const existingSvcIds = uniqueExistingSvcs.map((d) => Number(d.standardServiceId)).filter((x) => x > 0);

      const filteredStaff = sRes.filter((s) => {
        const role = rRes.find((r) => r.id === s.roleId);
        const isMatchedRole = role && (role.roleCode === "RECEPTIONIST" || role.roleCode === "MANAGER");
        const isMatchedBranch = !selectedBranchId || s.branchId === Number(selectedBranchId);
        return isMatchedRole && isMatchedBranch;
      });
      setStaff(filteredStaff);

      setFormData({
        customerNotes: orderRes.customerNotes || "",
        serviceAdvisorId: orderRes.serviceAdvisorId || "",
        orderCategory: orderRes.orderCategory || "",
        oldPartsReturnOption: orderRes.oldPartsReturnOption || "DISPOSE_ALL",
        requiredDepositAmount: orderRes.requiredDepositAmount || 0,
        checkInTime: orderRes.checkInTime ? orderRes.checkInTime.slice(0, 16) : "",
        estimatedHandover: orderRes.estimatedHandover ? orderRes.estimatedHandover.slice(0, 16) : "",
        promisedHandoverTime: orderRes.promisedHandoverTime ? orderRes.promisedHandoverTime.slice(0, 16) : "",
        serviceIds: existingSvcIds
      });

      const parsedSymptoms = (() => {
        try {
          const raw = String(orderRes.customerNotes || "").trim();
          if (!raw) return [];
          const parsed = JSON.parse(raw);
          if (Array.isArray(parsed?.symptoms)) {
            return parsed.symptoms.map((x) => String(x || "").trim()).filter(Boolean);
          }
          if (Array.isArray(parsed)) {
            return parsed.map((x) => String(x || "").trim()).filter(Boolean);
          }
          if (typeof parsed === "string" && parsed.trim()) {
            return [parsed.trim()];
          }
          return [];
        } catch {
          const plain = String(orderRes.customerNotes || "").trim();
          return plain ? [plain] : [];
        }
      })();
      setSymptomInputs(parsedSymptoms.length > 0 ? [...parsedSymptoms, ""] : [""]);
    } catch (err) {
      setError(err.message);
    } finally {
      setLoading(false);
    }
  }, [orderId, selectedBranchId]);

  useEffect(() => {
    fetchData();
  }, [fetchData]);

  const handleServiceToggle = (id) => {
    const serviceId = Number(id);
    setFormData(prev => {
      const exists = prev.serviceIds.map(Number).includes(serviceId);
      return {
        ...prev,
        serviceIds: exists
          ? prev.serviceIds.filter((sid) => Number(sid) !== serviceId).map(Number)
          : [...prev.serviceIds.map(Number), serviceId]
      };
    });
    setServiceSearch("");
  };

  const filteredServices = useMemo(() => {
    if (!serviceSearch) return [];
    const selectedCategory = orderCategories.find(c => (c.code || c.name) === formData.orderCategory);
    const catId = selectedCategory ? selectedCategory.id : null;

    return services.filter(s =>
      (!catId || s.categoryId === catId || s.CategoryId === catId) &&
      ((s.Name || s.name || "").toLowerCase().includes(serviceSearch.toLowerCase())) &&
      !formData.serviceIds.map(Number).includes(Number(s.id))
    ).slice(0, 10);
  }, [services, serviceSearch, formData.serviceIds, formData.orderCategory, orderCategories]);

  const calculateEstimatedFinish = useCallback((startText, totalMinutes) => {
    if (!startText || !totalMinutes || totalMinutes <= 0) {
      return { finishText: "", finishIso: "" };
    }
    const current = new Date(startText);
    if (Number.isNaN(current.getTime())) {
      return { finishText: "", finishIso: "" };
    }

    const alignToWindow = (value) => {
      let t = new Date(value);
      while (t.getDay() === 0) {
        t.setDate(t.getDate() + 1);
        t.setHours(8, 0, 0, 0);
      }
      const openMins = 8 * 60;
      const closeMins = 20 * 60;
      const curMins = t.getHours() * 60 + t.getMinutes();
      if (curMins < openMins) t.setHours(8, 0, 0, 0);
      if (curMins >= closeMins) {
        t.setDate(t.getDate() + 1);
        t.setHours(8, 0, 0, 0);
        while (t.getDay() === 0) {
          t.setDate(t.getDate() + 1);
          t.setHours(8, 0, 0, 0);
        }
      }
      return t;
    };

    let t = alignToWindow(current);
    let remaining = Number(totalMinutes);
    while (remaining > 0) {
      t = alignToWindow(t);
      const closeToday = new Date(t);
      closeToday.setHours(20, 0, 0, 0);
      const available = Math.max(0, Math.floor((closeToday.getTime() - t.getTime()) / 60000));
      if (available <= 0) {
        t.setDate(t.getDate() + 1);
        t.setHours(8, 0, 0, 0);
        continue;
      }
      const consume = Math.min(remaining, available);
      t.setMinutes(t.getMinutes() + consume);
      remaining -= consume;
      if (remaining > 0) {
        t.setDate(t.getDate() + 1);
        t.setHours(8, 0, 0, 0);
      }
    }

    const hh = String(t.getHours()).padStart(2, "0");
    const mm = String(t.getMinutes()).padStart(2, "0");
    const dd = String(t.getDate()).padStart(2, "0");
    const mo = String(t.getMonth() + 1).padStart(2, "0");
    const yy = t.getFullYear();

    return {
      finishText: `${hh}:${mm} - Ngày ${dd}-${mo}`,
      finishIso: `${yy}-${mo}-${dd}T${hh}:${mm}:00`,
    };
  }, []);

  const { totalMinutes, totalPrice, finishTimeStr, estimatedFinishIso } = useMemo(() => {
    const selectedServices = services.filter((s) => formData.serviceIds.map(Number).includes(Number(s.id)));
    const total = selectedServices.reduce((sum, s) => sum + (s.StandardMinutes || s.standardMinutes || 60), 0);
    const priceSum = selectedServices.reduce((sum, s) => sum + (s.basePrice || s.BasePrice || 0), 0);
    const { finishText, finishIso } = calculateEstimatedFinish(formData.checkInTime, total);
    return { totalMinutes: total, totalPrice: priceSum, finishTimeStr: finishText, estimatedFinishIso: finishIso };
  }, [formData.serviceIds, formData.checkInTime, services, calculateEstimatedFinish]);

  const handleChange = (e) => {
    const { name, value, type, checked } = e.target;
    setFormData((prev) => {
      return { ...prev, [name]: type === 'checkbox' ? checked : value };
    });
  };

  const handleSubmit = async (e) => {
    e.preventDefault();
    setSaving(true);
    try {
      const normalizedSymptoms = symptomInputs
        .map((x) => String(x || "").trim())
        .filter(Boolean);
      const symptomPayload = normalizedSymptoms.length > 0 ? normalizedSymptoms : ["Không có ghi chú gì"];

      const payload = {
        ...order,
        ...formData,
        customerNotes: JSON.stringify({ symptoms: symptomPayload }),
        serviceAdvisorId: formData.serviceAdvisorId ? Number(formData.serviceAdvisorId) : null,
        requiredDepositAmount: Number(formData.requiredDepositAmount) || 0,
        checkInTime: formData.checkInTime || new Date().toISOString(),
        estimatedHandover: formData.estimatedHandover || estimatedFinishIso || null,
        promisedHandoverTime: formData.promisedHandoverTime || null,
      };

      await serviceOrderApi.update(orderId, payload);

      // Đông bộ service details theo danh sách đang chọn (xử lý cả dữ liệu trùng cũ)
      const currentIds = Array.from(new Set(formData.serviceIds.map(Number).filter((x) => x > 0)));
      const latestDetails = await serviceOrderApi.getDetails(orderId).catch(() => []);
      const editableDetails = (latestDetails || []).filter((d) => {
        const t = String(d?.itemType || "").toUpperCase();
        const isLaborOrService = t === "LABOR" || t === "SERVICE";
        return isLaborOrService && !d?.diagnoseRecommendationId;
      });

      const groupedByServiceId = editableDetails.reduce((acc, d) => {
        const sid = Number(d?.standardServiceId || 0);
        if (sid <= 0) return acc;
        if (!acc[sid]) acc[sid] = [];
        acc[sid].push(d);
        return acc;
      }, {});

      const detailPromises = [];

      // Vơi dịch vụ đang chọn: giữ 1 dòng, xóa dòng trùng
      currentIds.forEach((sid) => {
        const rows = groupedByServiceId[sid] || [];
        if (rows.length > 1) {
          rows.slice(1).forEach((d) => detailPromises.push(serviceOrderApi.deleteDetail(d.id)));
        }
      });

      // Vơi dịch vụ không còn chọn: xóa toàn bộ dòng
      Object.keys(groupedByServiceId).map(Number).forEach((sid) => {
        if (!currentIds.includes(sid)) {
          (groupedByServiceId[sid] || []).forEach((d) => detailPromises.push(serviceOrderApi.deleteDetail(d.id)));
        }
      });

      // Vơi dịch vụ chọn mơi chưa có dòng: thêm mơi
      currentIds.forEach((sid) => {
        const rows = groupedByServiceId[sid] || [];
        if (rows.length === 0) {
          const service = services.find((s) => Number(s.id) === sid);
          if (service) {
            detailPromises.push(serviceOrderApi.createDetail({
              serviceOrderId: orderId,
              standardServiceId: sid,
              itemType: "LABOR",
              itemName: service.Name || service.name,
              description: "Dịch vụ khách yêu cầu trươc (Edit)",
              quantity: 1,
              unit: "Lần",
              unitPriceAtTime: service.BasePrice || service.basePrice || 0,
              subTotal: service.BasePrice || service.basePrice || 0,
              taxRate: service.TaxRate || service.taxRate || 0,
              finalAmount: service.BasePrice || service.basePrice || 0,
              payerType: "CUSTOMER",
              status: "APPROVED"
            }));
          }
        }
      });

      if (detailPromises.length > 0) {
        await Promise.all(detailPromises);
      }

      navigate(`/workshop/reception?viewOrderId=${orderId}`);
    } catch (err) {
      setError("Lđ-i khi cập nhật: " + err.message);
    } finally {
      setSaving(false);
    }
  };

  if (loading) return <div className="loading-state"><div className="spinner" /><p>Đang tải dữ liệu lệnh...</p></div>;

  const handleSymptomChange = (index, value) => {
    setSymptomInputs((prev) => {
      const next = [...prev];
      next[index] = value;
      const isLast = index === next.length - 1;
      if (isLast && String(value || "").trim() && next.length < 12) {
        next.push("");
      }
      return next;
    });
  };

  const handleSymptomFocus = (index) => {
    setFocusedSymptomIndex(index);
  };

  return (
    <div className="create-order-page">
      <div className="page-header-actions">
        <button className="btn-back" onClick={() => navigate(-1)}>ệđ Quay lại Dashboard</button>
        <h2 className="page-title">Chệnh sửa thông tin lệnh sửa chữa: {order?.code}</h2>
      </div>

      {error && <div className="alert alert-danger">{error}</div>}

      <form className="order-form-grid" onSubmit={handleSubmit}>
        {/* Section 1: Customer & Vehicle (Read Only / Basic Edit) */}
        <div className="form-section shadow-sm">
          <h3 className="section-title">1. Thông tin chung</h3>
          
          <div className="form-group">
            <label>Phân loại lệnh sửa chữa *</label>
            <select name="orderCategory" value={formData.orderCategory} onChange={handleChange} required>
              <option value="">-- Chọn phân loại --</option>
              {orderCategories.map(cat => (
                <option key={cat.id} value={cat.code || cat.name}>{cat.name}</option>
              ))}
            </select>
          </div>

          <div className="form-group" style={{ marginTop: '20px', padding: '16px', background: '#f4f7fe', borderRadius: '12px', border: '1px solid #e9edf7' }}>
            <label>Tìm & Chọn Cụ Thể Nghiệp Vụ Yêu Cầu</label>
            <div style={{ position: 'relative', marginBottom: '10px' }}>
              <input
                type="text"
                placeholder={`Gõ để tìm nghiệp vụ ${formData.orderCategory ? '('.concat(formData.orderCategory).concat(') ') : ''}...`}
                value={serviceSearch}
                onChange={(e) => setServiceSearch(e.target.value)}
                disabled={!formData.orderCategory}
              />
              {filteredServices.length > 0 && (
                <div className="dropdown-panel" style={{ position: 'absolute', top: '100%', left: 0, right: 0, background: '#fff', border: '1px solid #e9edf7', borderRadius: '8px', zIndex: 10, boxShadow: '0 4px 12px rgba(0,0,0,0.1)' }}>
                  {filteredServices.map(s => (
                    <div key={s.id} className="dropdown-item" style={{ padding: '8px 12px', cursor: 'pointer', borderBottom: '1px solid #f4f7fe' }} onClick={() => handleServiceToggle(s.id)}>
                      <strong style={{ color: '#2b3674' }}>{s.Name || s.name}</strong> 
                      <span style={{ float: 'right', color: '#05cd99', fontSize: '13px', fontWeight: 600 }}>{(s.BasePrice || s.basePrice || 0).toLocaleString()}đ</span>
                    </div>
                  ))}
                </div>
              )}
              {serviceSearch && filteredServices.length === 0 && formData.orderCategory && (
                <div className="dropdown-panel" style={{ position: 'absolute', top: '100%', left: 0, right: 0, background: '#fff', border: '1px solid #e9edf7', borderRadius: '8px', zIndex: 10, padding: '12px', color: '#a3aed0', fontStyle: 'italic' }}>
                  Không tìm thấy dịch vụ nào phù hợp trong danh mục này.
                </div>
              )}
            </div>

            {formData.serviceIds.length > 0 && (
                <div className="selected-services-tags" style={{ display: 'flex', flexWrap: 'wrap', gap: '8px' }}>
                {formData.serviceIds.map(sid => {
                  const s = services.find(srv => Number(srv.id) === Number(sid));
                  return (
                    <div key={sid} style={{ background: '#4318ff', color: '#fff', padding: '6px 12px', borderRadius: '20px', fontSize: '13px', display: 'flex', alignItems: 'center', gap: '8px', boxShadow: '0 2px 6px rgba(67, 24, 255, 0.3)' }}>
                      {s?.Name || s?.name || "Ký hiệu không xác định"} - {(s?.basePrice || s?.BasePrice || 0).toLocaleString()}đ
                      <span style={{ cursor: 'pointer', fontWeight: 'bold', marginLeft: '4px', background: 'rgba(255,255,255,0.2)', width: '20px', height: '20px', display: 'inline-flex', alignItems: 'center', justifyContent: 'center', borderRadius: '50%' }} onClick={() => handleServiceToggle(sid)}>đ-</span>
                    </div>
                  );
                })}
              </div>
            )}

            {formData.serviceIds.length > 0 && (
              <>
                <div style={{ marginTop: '16px', padding: '12px', background: '#fff', borderRadius: '8px', color: '#1b254b', fontSize: '14px', display: 'flex', justifyContent: 'space-between', alignItems: 'center', border: '1px solid rgba(67, 24, 255, 0.1)' }}>
                  <div>
                    <strong>Tđ.ng thời gian:</strong> {totalMinutes} phút
                    {finishTimeStr && <span style={{ display: 'block', fontSize: '12px', color: '#a3aed0', marginTop: '4px' }}>Dự kiến xong: <strong style={{ color: '#4318ff' }}>{finishTimeStr}</strong></span>}
                  </div>
                  <div style={{ textAlign: 'right' }}>
                    <div style={{ fontSize: '12px', color: '#a3aed0' }}>Tđ.ng tiền tạm tính</div>
                    <span style={{ color: '#05cd99', fontWeight: 'bold', fontSize: '20px' }}>{totalPrice?.toLocaleString() || 0}đ</span>
                  </div>
                </div>
              </>
            )}
          </div>

          <div className="form-group" style={{ marginTop: '16px' }}>
            <label>Cđ vấn dịch vụ (Service Advisor) *</label>
            <select 
              name="serviceAdvisorId" 
              value={formData.serviceAdvisorId} 
              onChange={handleChange}
              required
            >
              <option value="">-- Chọn cđ vấn --</option>
              {staff.map(s => (
                <option key={s.id} value={s.id}>{s.fullName}</option>
              ))}
            </select>
          </div>
        </div>

        {/* Section 2: Order Details */}
        <div className="form-section shadow-sm">
          <h3 className="section-title">2. Nghiệp vụ & Tài chính</h3>
          
          <div className="form-group">
            <label>Xử lý phụ tùng cũ</label>
            <select name="oldPartsReturnOption" value={formData.oldPartsReturnOption} onChange={handleChange}>
              <option value="DISPOSE_ALL">Garage tự thanh lý/hủy</option>
              <option value="RETURN_ALL">Trả lại cho khách hàng</option>
              <option value="CUSTOM">Chờ khách kiểm tra rôi quyết định</option>
            </select>
          </div>

          <div className="form-group">
            <label>Sđ tiền cọc yêu cầu (VNĐ)</label>
            <input
              type="number"
              name="requiredDepositAmount"
              value={formData.requiredDepositAmount}
              onChange={handleChange}
              min="0"
              step="10000"
            />
          </div>
        </div>

        {/* Section 3: Timeline & Notes */}
        <div className="form-section shadow-sm full-width">
          <h3 className="section-title">3. Mô tả thêm và Lịch trình</h3>

          <div className="form-group">
            <label>Mô tả thêm</label>
            <div className="symptom-list" style={{ display: "grid", gap: "10px" }}>
              {symptomInputs.map((symptom, index) => (
                <div key={`symptom-edit-${index}`} className="form-group" style={{ marginBottom: 0 }}>
                  <input
                    type="text"
                    value={symptom}
                    placeholder={`Nhập mô tả ${index + 1}...`}
                    onChange={(e) => handleSymptomChange(index, e.target.value)}
                    onFocus={() => handleSymptomFocus(index)}
                    disabled={index > 0 && !String(symptomInputs[index - 1] || "").trim()}
                    style={{
                      opacity: String(symptom || "").trim() || focusedSymptomIndex === index ? 1 : 0.5,
                      transition: "opacity 0.2s ease",
                      cursor: index > 0 && !String(symptomInputs[index - 1] || "").trim() ? "not-allowed" : "text",
                    }}
                  />
                </div>
              ))}
            </div>
          </div>

          <div className="timeline-grid">
            <div className="form-group">
              <label>Thời điểm xe vào xưởng *</label>
              <DateTimePicker 
                value={formData.checkInTime} 
                onChange={(val) => handleChange({ target: { name: 'checkInTime', value: val } })}
                placeholder="Chọn ngày giờ vào xưởng"
              />
            </div>
            <div className="form-group">
              <label>Ngày giờ cam kết trả xe</label>
              <DateTimePicker 
                value={formData.promisedHandoverTime} 
                onChange={(val) => handleChange({ target: { name: 'promisedHandoverTime', value: val } })}
                placeholder="Chọn ngày giờ cam kết"
              />
            </div>
            <div className="form-group">
              <label>Dự kiến hoàn thành (Ươc tính)</label>
              <DateTimePicker 
                value={formData.estimatedHandover || estimatedFinishIso} 
                onChange={(val) => handleChange({ target: { name: 'estimatedHandover', value: val } })}
                placeholder="Chọn ngày giờ dự kiến xong"
              />
            </div>
          </div>
        </div>

        <div className="form-footer full-width">
          <button type="button" className="btn-secondary-outline" onClick={() => navigate(-1)}>Hủy và Quay lại</button>
          <button type="submit" className="btn-primary-large" disabled={saving}>
            {saving ? "Đang xử lý..." : "Lưu thay đđ.i"}
          </button>
        </div>
      </form>
    </div>
  );
}














