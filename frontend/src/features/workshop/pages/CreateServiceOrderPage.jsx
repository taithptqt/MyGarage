import React, { useState, useMemo, useEffect, useCallback } from "react";
import { useNavigate } from "react-router-dom";
import { serviceOrderApi } from "@features/workshop/api/serviceOrderApi";
import {
  getAllCustomers,
  getAllVehicles,
  getAllCustomerVehicles,
} from "@features/admin/api/customerApi";
import { getAllStaff, getAllRoles } from "@features/admin/api/staffApi";
import { getAllServiceCategories, getAllStandardServices } from "@features/admin/api/serviceApi";
import { getVehicleBrands, getVehicleModels } from "@features/appointments/api/appointmentApi";
import { useBranch } from "@shared/core/context/BranchContext";
import { useAuth } from "@shared/core/context/AuthContext";
import DateTimePicker from "@features/workshop/components/DateTimePicker";
import { formatLicensePlate } from "@features/workshop/utils/serviceOrderDetailUtils";
import "./Workshop.css";

export default function CreateServiceOrderPage() {
  const navigate = useNavigate();
  const { selectedBranchId } = useBranch();
  const { user } = useAuth();

  const [loading, setLoading] = useState(false);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState("");
  const [customers, setCustomers] = useState([]);
  const [vehicles, setVehicles] = useState([]);
  const [customerVehicles, setCustomerVehicles] = useState([]);
  const [staff, setStaff] = useState([]);
  const [orderCategories, setOrderCategories] = useState([]);
  const [services, setServices] = useState([]);
  const [brands, setBrands] = useState([]);
  const [models, setModels] = useState([]);

  const [formData, setFormData] = useState({
    customerId: "",
    vehicleId: "",
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
    customerNotes: "",
    serviceAdvisorId: user?.id || "",
    orderCategory: "REPAIR",
    oldPartsReturnOption: "DISPOSE_ALL",
    requiredDepositAmount: 0,
    checkInTime: new Date().toISOString().slice(0, 16),
    estimatedHandover: "",
    promisedHandoverTime: "",
    isQuickOrder: false,
    isNewVehicle: false,
    serviceIds: [],
  });

  const [search, setSearch] = useState("");
  const [serviceSearch, setServiceSearch] = useState("");
  const [selectedBrandId, setSelectedBrandId] = useState("");
  const [guestSelectedBrandId, setGuestSelectedBrandId] = useState("");
  const [submitAttempted, setSubmitAttempted] = useState(false);
  const [fieldErrors, setFieldErrors] = useState({});
  const [symptomInputs, setSymptomInputs] = useState([""]);
  const [focusedSymptomIndex, setFocusedSymptomIndex] = useState(-1);

  const validateVietnamesePhone = (phone) => /^(0\d{9,10}|\+84\d{9,10})$/.test(String(phone || "").trim());

  const getApiErrorMessage = (err) => {
    const fallback = "Không thể tạo lệnh sửa chữa. Vui lòng kiểm tra dữ liệu và thử lại.";
    const data = err?.response?.data;
    const serverErrors = data?.errors && typeof data.errors === "object" ? Object.values(data.errors).flat() : [];
    if (serverErrors.length > 0) {
      return String(serverErrors[0] || fallback);
    }

    const raw =
      data?.message ||
      data?.error ||
      data?.detail ||
      data?.title ||
      err?.message ||
      "";
    const text = String(raw);
    const lowerText = text.toLowerCase();

    if (/duplicate|already exists|đã tồn tại|UNIQUE/i.test(text)) {
      return "Dữ liệu bị trùng (biển số hoặc mã lệnh đã tồn tại). Vui lòng kiểm tra lại.";
    }
    if (/foreign key|constraint|ràng buộc/i.test(text)) {
      if (lowerText.includes("customerid")) return "Khách hàng đã chọn không còn hợp lệ. Vui lòng chọn lại khách hàng.";
      if (lowerText.includes("vehicleid")) return "Xe đã chọn không còn hợp lệ. Vui lòng chọn lại xe.";
      if (lowerText.includes("serviceadvisorid")) return "Cố vấn dịch vụ đã chọn không còn hợp lệ. Vui lòng chọn lại.";
      if (lowerText.includes("executebranchid") || lowerText.includes("branchid")) {
        return "Chi nhánh thực hiện không hợp lệ. Vui lòng chọn lại chi nhánh làm việc.";
      }
      return `Dữ liệu liên kết chưa hợp lệ. ${text}`;
    }
    if (/timeout|network|failed to fetch/i.test(text)) {
      return "Không kết nđi được tơi máy chủ. Vui lòng thử lại sau.";
    }

    return text || fallback;
  };

  const years = useMemo(() => {
    const currentYear = new Date().getFullYear();
    const arr = [];
    for (let y = currentYear; y >= 1980; y -= 1) arr.push(y);
    return arr;
  }, []);

  const fetchData = useCallback(async () => {
    setLoading(true);
    try {
      const [cRes, vRes, cvRes, sRes, rRes, catRes, srvRes, brandsRes, modelsRes] = await Promise.all([
        getAllCustomers(),
        getAllVehicles(),
        getAllCustomerVehicles(),
        getAllStaff(),
        getAllRoles(),
        getAllServiceCategories(),
        getAllStandardServices(),
        getVehicleBrands(),
        getVehicleModels(),
      ]);
      setCustomers(cRes || []);
      setVehicles(vRes || []);
      setCustomerVehicles(cvRes || []);
      setOrderCategories(catRes || []);
      setServices(srvRes || []);
      setBrands(brandsRes || []);
      setModels(modelsRes || []);

      const filteredStaff = (sRes || []).filter((s) => {
        const role = (rRes || []).find((r) => r.id === s.roleId);
        const isMatchedRole = role && (role.roleCode === "RECEPTIONIST" || role.roleCode === "MANAGER");
        const isMatchedBranch = !selectedBranchId || s.branchId === Number(selectedBranchId);
        return isMatchedRole && isMatchedBranch;
      });
      setStaff(filteredStaff);
    } catch (err) {
      setError("Không thể tải dữ liệu ban đầu: " + err.message);
    } finally {
      setLoading(false);
    }
  }, [selectedBranchId]);

  useEffect(() => {
    fetchData();
  }, [fetchData]);

  const filteredCustomers = useMemo(() => {
    if (!search) return [];
    return customers
      .filter(
        (c) =>
          (c.fullName || "").toLowerCase().includes(search.toLowerCase()) ||
          (c.phone || "").includes(search)
      )
      .slice(0, 5);
  }, [customers, search]);

  const customerVehiclesFiltered = useMemo(() => {
    if (!formData.customerId) return [];
    const mappings = customerVehicles.filter((cv) => cv.customerId === Number(formData.customerId));
    return vehicles.filter((v) => mappings.some((m) => m.vehicleId === v.id));
  }, [vehicles, customerVehicles, formData.customerId]);

  const customerHasNoVehicles = !!formData.customerId && customerVehiclesFiltered.length === 0;

  const validateForm = useCallback((data) => {
    const errors = {};
    const now = new Date();
    const checkIn = data.checkInTime ? new Date(data.checkInTime) : null;
    const maxPastTime = new Date(now.getTime() - 30 * 60 * 1000);

    if (!data.serviceAdvisorId) errors.serviceAdvisorId = "Vui lòng chọn cđ vấn dịch vụ.";

    if (!checkIn || Number.isNaN(checkIn.getTime())) {
      errors.checkInTime = "Vui lòng nhập thời điểm xe vào xưởng hợp lệ.";
    } else if (checkIn < maxPastTime) {
      errors.checkInTime = "Thời điểm xe vào xưởng không được quá 30 phút trong quá khứ.";
    }

    if (data.estimatedHandover && checkIn) {
      const est = new Date(data.estimatedHandover);
      if (est <= checkIn) {
        errors.estimatedHandover = "Dự kiến hoàn thành phải sau thời điểm xe vào xưởng.";
      }
    }

    if (data.promisedHandoverTime && checkIn) {
      const promised = new Date(data.promisedHandoverTime);
      if (promised <= checkIn) {
        errors.promisedHandoverTime = "Ngày giờ cam kết trả xe phải sau thời điểm xe vào xưởng.";
      }
    }

    if (data.isQuickOrder) {
      if (!String(data.guestName || "").trim()) errors.guestName = "Vui lòng nhập họ tên khách vãng lai.";
      if (!String(data.guestPhone || "").trim()) {
        errors.guestPhone = "Vui lòng nhập số điện thoại khách vãng lai.";
      } else if (!validateVietnamesePhone(data.guestPhone)) {
        errors.guestPhone = "Sđ điện thoại không đúng định dạng Việt Nam.";
      }
      if (!String(data.guestVehiclePlate || "").trim()) errors.guestVehiclePlate = "Vui lòng nhập biển sđ xe.";
      if (!guestSelectedBrandId) errors.guestBrandId = "Vui lòng chọn hãng xe.";
      if (!data.guestVehicleModelId) errors.guestVehicleModelId = "Vui lòng chọn dòng xe.";
      if (!data.guestVehicleYear) errors.guestVehicleYear = "Vui lòng chọn nểm sản xuất.";
    } else {
      if (!data.customerId) errors.customerId = "Vui lòng chọn khách hàng.";

      if (!data.isNewVehicle) {
        if (!data.vehicleId) errors.vehicleId = "Vui lòng chọn xe đã lưu.";
      } else {
        if (!String(data.vehicleLicensePlate || "").trim()) errors.vehicleLicensePlate = "Vui lòng nhập biển sđ xe mơi.";
        if (!selectedBrandId) errors.brandId = "Vui lòng chọn hãng xe.";
        if (!data.vehicleModelId) errors.vehicleModelId = "Vui lòng chọn dòng xe.";
        if (!data.vehicleYear) errors.vehicleYear = "Vui lòng chọn nểm sản xuất.";
      }
    }

    if (!selectedBranchId) {
      errors.executeBranchId = "Vui lòng chọn chi nhánh làm việc trươc khi tạo lệnh.";
    }

    return errors;
  }, [guestSelectedBrandId, selectedBrandId, selectedBranchId]);
  const isFormValid = useMemo(() => Object.keys(validateForm(formData)).length === 0, [formData, validateForm]);
  const showError = (name) => submitAttempted && fieldErrors[name];

  const handleServiceToggle = (id) => {
    const serviceId = Number(id);
    setFormData((prev) => {
      const exists = prev.serviceIds.map(Number).includes(serviceId);
      return {
        ...prev,
        serviceIds: exists
          ? prev.serviceIds.filter((sid) => Number(sid) !== serviceId).map(Number)
          : [...prev.serviceIds.map(Number), serviceId],
      };
    });
    setServiceSearch("");
  };

  const filteredServices = useMemo(() => {
    if (!serviceSearch) return [];
    const selectedCategory = orderCategories.find((c) => (c.code || c.name) === formData.orderCategory);
    const catId = selectedCategory ? selectedCategory.id : null;
    return services
      .filter(
        (s) =>
          (!catId || s.categoryId === catId || s.CategoryId === catId) &&
          (s.Name || s.name || "").toLowerCase().includes(serviceSearch.toLowerCase()) &&
          !formData.serviceIds.map(Number).includes(Number(s.id))
      )
      .slice(0, 10);
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
      const next = { ...prev, [name]: type === "checkbox" ? checked : value };
      if (name === "orderCategory") next.serviceIds = [];
      if (submitAttempted) {
        setFieldErrors(validateForm(next));
      }
      return next;
    });
    if (error) setError("");
  };

  const handlePlateBlur = (e) => {
    const { name, value } = e.target;
    if (name === "guestVehiclePlate" || name === "vehicleLicensePlate") {
      setFormData((prev) => {
        const next = { ...prev, [name]: formatLicensePlate(value) };
        if (submitAttempted) {
          setFieldErrors(validateForm(next));
        }
        return next;
      });
    }
  };

  const selectCustomer = (customer) => {
    const mappings = customerVehicles.filter((cv) => cv.customerId === Number(customer.id));
    const forceNewVehicle = mappings.length === 0;
    const nextData = {
      ...formData,
      customerId: customer.id,
      guestName: customer.fullName,
      guestPhone: customer.phone,
      vehicleId: "",
      vehicleLicensePlate: "",
      vehicleModelId: "",
      vehicleColor: "",
      vehicleVin: "",
      vehicleYear: "",
      isNewVehicle: forceNewVehicle,
      isQuickOrder: false,
    };
    setFormData((prev) => ({
      ...prev,
      customerId: customer.id,
      guestName: customer.fullName,
      guestPhone: customer.phone,
      vehicleId: "",
      vehicleLicensePlate: "",
      vehicleModelId: "",
      vehicleColor: "",
      vehicleVin: "",
      vehicleYear: "",
      isNewVehicle: forceNewVehicle,
      isQuickOrder: false,
    }));
    if (submitAttempted) {
      setFieldErrors(validateForm(nextData));
    }
    setSelectedBrandId("");
    setSearch("");
  };

  const toggleQuickOrder = () => {
    const nextData = {
      ...formData,
      isQuickOrder: !formData.isQuickOrder,
      customerId: "",
      vehicleId: "",
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
      isNewVehicle: false,
    };
    setFormData((prev) => ({
      ...prev,
      isQuickOrder: !prev.isQuickOrder,
      customerId: "",
      vehicleId: "",
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
      isNewVehicle: false,
    }));
    if (submitAttempted) {
      setFieldErrors(validateForm(nextData));
    }
    setSelectedBrandId("");
    setGuestSelectedBrandId("");
    setSearch("");
  };

  const handleSubmit = async (e) => {
    e.preventDefault();
    if (saving) return;

    setSubmitAttempted(true);
    const errors = validateForm(formData);
    setFieldErrors(errors);
    if (Object.keys(errors).length > 0) {
      setError("Vui lòng xử lý các lđ-i trong biểu mẫu trươc khi lưu.");
      return;
    }

    setSaving(true);
    setError("");
    try {
      const normalizedSymptoms = symptomInputs
        .map((x) => String(x || "").trim())
        .filter(Boolean);
      const symptomPayload = normalizedSymptoms.length > 0 ? normalizedSymptoms : ["Không có ghi chú gì"];

      const payload = {
        ...formData,
        customerId: formData.customerId ? Number(formData.customerId) : null,
        vehicleId: formData.vehicleId ? Number(formData.vehicleId) : null,
        vehicleModelId: formData.vehicleModelId ? Number(formData.vehicleModelId) : null,
        guestVehicleModelId: formData.guestVehicleModelId ? Number(formData.guestVehicleModelId) : null,
        vehicleYear: formData.vehicleYear ? Number(formData.vehicleYear) : null,
        guestVehicleYear: formData.guestVehicleYear ? Number(formData.guestVehicleYear) : null,
        serviceAdvisorId: formData.serviceAdvisorId ? Number(formData.serviceAdvisorId) : null,
        requiredDepositAmount: formData.requiredDepositAmount ? Number(formData.requiredDepositAmount) : 0,
        estimatedHandover: formData.estimatedHandover || estimatedFinishIso || null,
        promisedHandoverTime: formData.promisedHandoverTime || null,
        checkInTime: formData.checkInTime || new Date().toISOString(),
        code: serviceOrderApi.generateCode(),
        status: "NEW",
        executeBranchId: selectedBranchId ? Number(selectedBranchId) : null,
        originBranchId: selectedBranchId ? Number(selectedBranchId) : null,
        customerNotes: JSON.stringify({ symptoms: symptomPayload }),
      };

      await serviceOrderApi.createWorkshop(payload);

      navigate("/workshop/reception");
    } catch (err) {
      setError(getApiErrorMessage(err));
    } finally {
      setSaving(false);
    }
  };

  if (loading) return <div className="loading-state"><div className="spinner" /><p>Đang tải dữ liệu...</p></div>;

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
        <button className="btn-back" onClick={() => navigate(-1)}>Quay lại Dashboard</button>
        <h2 className="page-title">Tiếp nhận xe và Tạo lệnh sửa chữa mơi</h2>
      </div>
      {error && <div className="alert alert-danger">{error}</div>}
      {submitAttempted && Object.keys(fieldErrors).length > 0 && (
        <div className="validation-summary" role="alert">
          <strong>Vui lòng xử lý các lđ-i sau:</strong>
          <ul>
            {Object.values(fieldErrors).map((msg, idx) => (
              <li key={`${msg}-${idx}`}>{msg}</li>
            ))}
          </ul>
        </div>
      )}
      <form className="order-form-grid" onSubmit={handleSubmit}>
        <div className="form-section shadow-sm">
          <h3 className="section-title">1. Thông tin Khách hàng và Xe</h3>
          <div className="quick-toggle-box">
            <label className="switch-label">
              <input type="checkbox" checked={formData.isQuickOrder} onChange={toggleQuickOrder} />
              <span className="switch-text">Khách vãng lai (Tiếp nhận nhanh)</span>
            </label>
          </div>

          {!formData.isQuickOrder ? (
            <>
              <div className="form-group">
                <label>Tìm kiếm Khách hàng *</label>
                <div style={{ position: "relative" }}>
                  <input
                    type="text"
                    className={showError("customerId") ? "input-invalid" : ""}
                    placeholder="Nhập tên hoặc sđ điện thoại..."
                    value={search}
                    onChange={(e) => setSearch(e.target.value)}
                    required={!formData.customerId}
                  />
                  {filteredCustomers.length > 0 && (
                    <div className="dropdown-panel">
                      {filteredCustomers.map((c) => (
                        <div key={c.id} className="dropdown-item" onClick={() => selectCustomer(c)}>
                          <strong>{c.fullName}</strong> - {c.phone}
                        </div>
                      ))}
                    </div>
                  )}
                </div>
                {showError("customerId") && <div className="field-error">{fieldErrors.customerId}</div>}
              </div>

              {formData.customerId && (
                <div className="selected-info">
                  <p><strong>Đã chọn:</strong> {formData.guestName} ({formData.guestPhone})</p>
                  <div style={{ display: "flex", alignItems: "center", gap: "8px" }}>
                    <input
                      type="checkbox"
                      id="isNewVehicle"
                      checked={formData.isNewVehicle}
                      disabled={customerHasNoVehicles}
                      onChange={(e) =>
                        setFormData((prev) => ({
                          ...prev,
                          isNewVehicle: e.target.checked,
                          vehicleId: "",
                          vehicleLicensePlate: "",
                          vehicleModelId: "",
                          vehicleColor: "",
                          vehicleVin: "",
                          vehicleYear: "",
                        }))
                      }
                    />
                    <label htmlFor="isNewVehicle">Sử dụng xe mơi</label>
                  </div>
                  {customerHasNoVehicles && <div style={{ color: "#e6522c" }}>Khách chưa có xe, bắt buộc nhập xe mơi.</div>}
                </div>
              )}

              {!formData.isNewVehicle ? (
                <div className="form-group">
                  <label>Chọn xe của khách *</label>
                  <select
                    name="vehicleId"
                    className={showError("vehicleId") ? "input-invalid" : ""}
                    value={formData.vehicleId}
                    onChange={handleChange}
                    required={!!formData.customerId && !formData.isNewVehicle}
                    disabled={!formData.customerId || customerHasNoVehicles}
                  >
                    <option value="">-- Chọn biển sđ xe --</option>
                    {customerVehiclesFiltered.map((v) => (
                      <option key={v.id} value={v.id}>
                        {v.licensePlate} {(v.brandName || v.modelName) ? `(${v.brandName || ""} ${v.modelName || ""})`.trim() : ""}
                      </option>
                    ))}
                  </select>
                  {showError("vehicleId") && <div className="field-error">{fieldErrors.vehicleId}</div>}
                </div>
              ) : (
                <>
                  <div className="form-group">
                    <label>Biển sđ xe *</label>
                    <input className={showError("vehicleLicensePlate") ? "input-invalid" : ""} name="vehicleLicensePlate" value={formData.vehicleLicensePlate} onChange={handleChange} onBlur={handlePlateBlur} required />
                    {showError("vehicleLicensePlate") && <div className="field-error">{fieldErrors.vehicleLicensePlate}</div>}
                  </div>
                  <div className="form-row" style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "12px" }}>
                    <div className="form-group">
                      <label>Hãng xe *</label>
                      <select className={showError("brandId") ? "input-invalid" : ""} value={selectedBrandId} onChange={(e) => { setSelectedBrandId(e.target.value); setFormData((p) => ({ ...p, vehicleModelId: "" })); }} required>
                        <option value="">-- Chọn hãng --</option>
                        {brands.map((b) => <option key={b.id} value={b.id}>{b.name}</option>)}
                      </select>
                      {showError("brandId") && <div className="field-error">{fieldErrors.brandId}</div>}
                    </div>
                    <div className="form-group">
                      <label>Dòng xe *</label>
                      <select className={showError("vehicleModelId") ? "input-invalid" : ""} name="vehicleModelId" value={formData.vehicleModelId} onChange={handleChange} required disabled={!selectedBrandId}>
                        <option value="">-- Chọn dòng xe --</option>
                        {models.filter((m) => m.brandId === Number(selectedBrandId)).map((m) => <option key={m.id} value={m.id}>{m.modelName}</option>)}
                      </select>
                      {showError("vehicleModelId") && <div className="field-error">{fieldErrors.vehicleModelId}</div>}
                    </div>
                  </div>
                  <div className="form-row" style={{ display: "grid", gridTemplateColumns: "1fr 1fr 1fr", gap: "12px" }}>
                    <div className="form-group"><label>Màu sắc</label><input name="vehicleColor" value={formData.vehicleColor} onChange={handleChange} /></div>
                    <div className="form-group">
                      <label>Nểm SX *</label>
                      <select className={showError("vehicleYear") ? "input-invalid" : ""} name="vehicleYear" value={formData.vehicleYear} onChange={handleChange} required>
                        <option value="">Chọn nểm</option>
                        {years.map((y) => <option key={y} value={y}>{y}</option>)}
                      </select>
                      {showError("vehicleYear") && <div className="field-error">{fieldErrors.vehicleYear}</div>}
                    </div>
                    <div className="form-group"><label>VIN</label><input name="vehicleVin" value={formData.vehicleVin} onChange={handleChange} /></div>
                  </div>
                </>
              )}
            </>
          ) : (
            <>
              <div className="form-row" style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "12px" }}>
                <div className="form-group">
                  <label>Họ tên *</label>
                  <input className={showError("guestName") ? "input-invalid" : ""} name="guestName" value={formData.guestName} onChange={handleChange} required />
                  {showError("guestName") && <div className="field-error">{fieldErrors.guestName}</div>}
                </div>
                <div className="form-group">
                  <label>Sđ điện thoại *</label>
                  <input className={showError("guestPhone") ? "input-invalid" : ""} name="guestPhone" value={formData.guestPhone} onChange={handleChange} required />
                  {showError("guestPhone") && <div className="field-error">{fieldErrors.guestPhone}</div>}
                </div>
              </div>
              <div className="form-group">
                <label>Biển sđ xe *</label>
                <input className={showError("guestVehiclePlate") ? "input-invalid" : ""} name="guestVehiclePlate" value={formData.guestVehiclePlate} onChange={handleChange} onBlur={handlePlateBlur} required />
                {showError("guestVehiclePlate") && <div className="field-error">{fieldErrors.guestVehiclePlate}</div>}
              </div>
              <div className="form-row" style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "12px" }}>
                <div className="form-group">
                  <label>Hãng xe *</label>
                  <select className={showError("guestBrandId") ? "input-invalid" : ""} value={guestSelectedBrandId} onChange={(e) => { setGuestSelectedBrandId(e.target.value); setFormData((p) => ({ ...p, guestVehicleModelId: "" })); }} required>
                    <option value="">-- Chọn hãng --</option>
                    {brands.map((b) => <option key={b.id} value={b.id}>{b.name}</option>)}
                  </select>
                  {showError("guestBrandId") && <div className="field-error">{fieldErrors.guestBrandId}</div>}
                </div>
                <div className="form-group">
                  <label>Dòng xe *</label>
                  <select className={showError("guestVehicleModelId") ? "input-invalid" : ""} name="guestVehicleModelId" value={formData.guestVehicleModelId} onChange={handleChange} disabled={!guestSelectedBrandId} required>
                    <option value="">-- Chọn dòng xe --</option>
                    {models.filter((m) => m.brandId === Number(guestSelectedBrandId)).map((m) => <option key={m.id} value={m.id}>{m.modelName}</option>)}
                  </select>
                  {showError("guestVehicleModelId") && <div className="field-error">{fieldErrors.guestVehicleModelId}</div>}
                </div>
              </div>
              <div className="form-row" style={{ display: "grid", gridTemplateColumns: "1fr 1fr 1fr", gap: "12px" }}>
                <div className="form-group"><label>Màu sắc</label><input name="guestVehicleColor" value={formData.guestVehicleColor} onChange={handleChange} /></div>
                <div className="form-group">
                  <label>Nểm SX *</label>
                  <select className={showError("guestVehicleYear") ? "input-invalid" : ""} name="guestVehicleYear" value={formData.guestVehicleYear} onChange={handleChange} required>
                    <option value="">Chọn nểm</option>
                    {years.map((y) => <option key={y} value={y}>{y}</option>)}
                  </select>
                  {showError("guestVehicleYear") && <div className="field-error">{fieldErrors.guestVehicleYear}</div>}
                </div>
                <div className="form-group"><label>VIN</label><input name="guestVehicleVin" value={formData.guestVehicleVin} onChange={handleChange} /></div>
              </div>
            </>
          )}
        </div>

        <div className="form-section shadow-sm">
          <h3 className="section-title">2. Thông tin Dịch vụ và Nghiệp vụ</h3>
          <div className="form-group">
            <label>Phân loại lệnh sửa chữa *</label>
            <select name="orderCategory" value={formData.orderCategory} onChange={handleChange} required>
              <option value="">-- Chọn phân loại --</option>
              {orderCategories.map((cat) => <option key={cat.id} value={cat.code || cat.name}>{cat.name}</option>)}
            </select>
          </div>

          <div className="form-group">
            <label>Tìm và chọn dịch vụ</label>
            <div style={{ position: "relative" }}>
              <input value={serviceSearch} onChange={(e) => setServiceSearch(e.target.value)} placeholder="Gõ để tìm dịch vụ..." />
              {filteredServices.length > 0 && (
                <div className="dropdown-panel service-search-dropdown">
                  {filteredServices.map((s) => (
                    <div key={s.id} className="dropdown-item service-search-item" onClick={() => handleServiceToggle(s.id)}>
                      {s.Name || s.name}
                    </div>
                  ))}
                </div>
              )}
            </div>
            {formData.serviceIds.length > 0 && (
              <div className="selected-services-wrap">
                {formData.serviceIds.map((sid) => {
                  const s = services.find((x) => x.id === sid);
                  return (
                    <button key={sid} type="button" className="service-chip" onClick={() => handleServiceToggle(sid)}>
                      <span>{s?.Name || s?.name}</span>
                      <span className="remove">đ-</span>
                    </button>
                  );
                })}
              </div>
            )}
            {formData.serviceIds.length > 0 && (
              <div className="service-summary-card">
                <div className="service-summary-item">
                  <span className="label">Tđ.ng thời gian</span>
                  <span className="value">{totalMinutes} phút</span>
                </div>
                <div className="service-summary-item">
                  <span className="label">Dự kiến xong</span>
                  <span className="value">{finishTimeStr || "--:--"}</span>
                </div>
                <div className="service-summary-item">
                  <span className="label">Tđ.ng tiền</span>
                  <span className="value">{totalPrice?.toLocaleString("vi-VN") || 0} đ</span>
                </div>
              </div>
            )}
          </div>

          <div className="form-group">
            <label>Cđ vấn dịch vụ *</label>
            <select className={showError("serviceAdvisorId") ? "input-invalid" : ""} name="serviceAdvisorId" value={formData.serviceAdvisorId} onChange={handleChange} required>
              <option value="">-- Chọn cđ vấn --</option>
              {staff.map((s) => <option key={s.id} value={s.id}>{s.fullName}</option>)}
            </select>
            {showError("serviceAdvisorId") && <div className="field-error">{fieldErrors.serviceAdvisorId}</div>}
          </div>
          <div className="form-group">
            <label>Xử lý phụ tùng cũ</label>
            <select name="oldPartsReturnOption" value={formData.oldPartsReturnOption} onChange={handleChange}>
              <option value="DISPOSE_ALL">Garage tự thanh lý/hủy</option>
              <option value="RETURN_ALL">Trả lại cho khách hàng</option>
              <option value="CUSTOM">Cho khách kiểm tra rôi quyết định</option>
            </select>
          </div>
          <div className="form-group">
            <label>Sđ tiền cọc (VND)</label>
            <input type="number" name="requiredDepositAmount" value={formData.requiredDepositAmount} onChange={handleChange} min="0" step="10000" />
          </div>
        </div>

        <div className="form-section shadow-sm full-width">
          <h3 className="section-title">3. Mô tả triệu chứng và Lịch trình</h3>
          <div className="form-group">
            <label>Mô tả triệu chứng</label>
            <div style={{ display: "grid", gap: "10px" }}>
              {symptomInputs.map((symptom, index) => (
                <div key={`symptom-${index}`} className="form-group" style={{ marginBottom: 0 }}>
                  <label style={{ fontSize: "13px" }}>Triệu chứng {index + 1}</label>
                  <textarea
                    rows={1}
                    value={symptom}
                    placeholder={`Nhập mô tả triệu chứng ${index + 1}...`}
                    onChange={(e) => handleSymptomChange(index, e.target.value)}
                    onFocus={() => handleSymptomFocus(index)}
                    onFocusCapture={() => setFocusedSymptomIndex(index)}
                    onBlur={() => setFocusedSymptomIndex(-1)}
                    disabled={index > 0 && !String(symptomInputs[index - 1] || "").trim()}
                    style={{
                      minHeight: "42px",
                      maxHeight: "72px",
                      resize: "vertical",
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
              <DateTimePicker value={formData.checkInTime} onChange={(val) => handleChange({ target: { name: "checkInTime", value: val } })} minDate={new Date(new Date().getTime() - 30 * 60 * 1000)} />
              {showError("checkInTime") && <div className="field-error">{fieldErrors.checkInTime}</div>}
            </div>
            <div className="form-group">
              <label>Ngày giờ cam kết trả xe</label>
              <DateTimePicker value={formData.promisedHandoverTime} onChange={(val) => handleChange({ target: { name: "promisedHandoverTime", value: val } })} />
              {showError("promisedHandoverTime") && <div className="field-error">{fieldErrors.promisedHandoverTime}</div>}
            </div>
            <div className="form-group">
              <label>Dự kiến hoàn thành</label>
              <DateTimePicker value={formData.estimatedHandover || estimatedFinishIso} onChange={(val) => handleChange({ target: { name: "estimatedHandover", value: val } })} />
              {showError("estimatedHandover") && <div className="field-error">{fieldErrors.estimatedHandover}</div>}
            </div>
          </div>
        </div>

        <div className="form-footer full-width">
          <button type="button" className="btn-secondary-outline" onClick={() => navigate(-1)}>Hủy và Quay lại</button>
          <button type="submit" className="btn-primary-large" disabled={saving || !isFormValid}>{saving ? "Đang xử lý..." : "Xác nhận và Khởi tạo Lệnh sửa chữa"}</button>
        </div>
      </form>
    </div>
  );
}











