import React, { useEffect, useMemo, useState } from "react";
import {
  getVehicleBrands,
  getVehicleModels,
} from "@features/appointments/api/appointmentApi";
import DateTimePicker from "@features/workshop/components/DateTimePicker";
import { formatLicensePlate } from "@features/workshop/utils/serviceOrderDetailUtils";

function normalizePhone(phone) {
  return String(phone || "").replace(/\s+/g, "");
}

function isValidPhone(phone) {
  return /^(0\d{9,10}|\+84\d{9,10})$/.test(normalizePhone(phone));
}

const EMPTY_FORM_DATA = {
  customerId: "",
  customerName: "",
  customerPhone: "",
  selectedVehicleIds: [],
  isQuickOrder: false,
  isNewVehicle: false,

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

  appointmentDate: "",
  startTime: "",
  serviceIds: [],
  customerNotes: "",
};

function toDateTimeParts(value) {
  if (!value) return { appointmentDate: "", startTime: "" };
  const parsed = new Date(value);
  if (Number.isNaN(parsed.getTime())) return { appointmentDate: "", startTime: "" };
  const appointmentDate = `${parsed.getFullYear()}-${String(parsed.getMonth() + 1).padStart(2, "0")}-${String(parsed.getDate()).padStart(2, "0")}`;
  const startTime = `${String(parsed.getHours()).padStart(2, "0")}:${String(parsed.getMinutes()).padStart(2, "0")}`;
  return { appointmentDate, startTime };
}

function buildInitialFormData(initialValues) {
  if (!initialValues) return { ...EMPTY_FORM_DATA };

  const serviceIds = Array.isArray(initialValues.serviceIds)
    ? initialValues.serviceIds.map(Number).filter((id) => id > 0)
    : String(initialValues.requestedServiceIds || "")
      .split(",")
      .map((value) => Number(String(value).trim()))
      .filter((id) => id > 0);

  const selectedVehicleIds = Array.isArray(initialValues.selectedVehicleIds)
    ? initialValues.selectedVehicleIds.map(Number).filter((id) => id > 0)
    : Number(initialValues.vehicleId) > 0
      ? [Number(initialValues.vehicleId)]
      : [];

  const { appointmentDate, startTime } = toDateTimeParts(initialValues.startTime);

  return {
    ...EMPTY_FORM_DATA,
    customerId: initialValues.customerId ? Number(initialValues.customerId) : "",
    customerName: initialValues.customerName || "",
    customerPhone: initialValues.customerPhone || "",
    selectedVehicleIds,
    appointmentDate,
    startTime,
    serviceIds,
    customerNotes: initialValues.customerNotes || "",
  };
}

export default function AppointmentForm({
  customers = [],
  vehicles = [],
  customerVehicles = [],
  onSubmit,
  onCancel,
  loading,
  services = [],
  initialValues = null,
  submitLabel = "Xác nhận đặt lịch",
}) {
  const [formData, setFormData] = useState(() => buildInitialFormData(initialValues));
  const [search, setSearch] = useState("");
  const [serviceSearch, setServiceSearch] = useState("");
  const [brands, setBrands] = useState([]);
  const [models, setModels] = useState([]);
  const [selectedBrandId, setSelectedBrandId] = useState("");
  const [guestSelectedBrandId, setGuestSelectedBrandId] = useState("");
  const [errorText, setErrorText] = useState("");

  const years = useMemo(() => {
    const currentYear = new Date().getFullYear();
    const list = [];
    for (let y = currentYear; y >= 1980; y -= 1) list.push(y);
    return list;
  }, []);

  useEffect(() => {
    let ignore = false;

    (async () => {
      try {
        const [brandRes, modelRes] = await Promise.all([
          getVehicleBrands(),
          getVehicleModels(),
        ]);
        if (ignore) return;
        setBrands(Array.isArray(brandRes) ? brandRes : []);
        setModels(Array.isArray(modelRes) ? modelRes : []);
      } catch {
        if (!ignore) {
          setBrands([]);
          setModels([]);
        }
      }
    })();

    return () => {
      ignore = true;
    };
  }, []);

  useEffect(() => {
    setErrorText("");
    setSearch("");
    setServiceSearch("");
    setSelectedBrandId("");
    setGuestSelectedBrandId("");
    setFormData(buildInitialFormData(initialValues));
  }, [initialValues]);

  const filteredCustomers = useMemo(() => {
    const term = String(search || "").trim().toLowerCase();
    if (!term) return [];
    return customers
      .filter((customer) =>
        String(customer.fullName || "").toLowerCase().includes(term) ||
        String(customer.phone || "").includes(term))
      .slice(0, 8);
  }, [customers, search]);

  const customerVehiclesFiltered = useMemo(() => {
    if (!formData.customerId) return [];
    const customerId = Number(formData.customerId);
    const linkedVehicleIds = new Set(
      customerVehicles
        .filter((item) => Number(item.customerId) === customerId)
        .map((item) => Number(item.vehicleId)),
    );
    return vehicles.filter((vehicle) => linkedVehicleIds.has(Number(vehicle.id)));
  }, [customerVehicles, vehicles, formData.customerId]);

  const filteredServices = useMemo(() => {
    const term = String(serviceSearch || "").trim().toLowerCase();
    if (!term) return [];
    const selectedIds = new Set(formData.serviceIds.map(Number));
    return services
      .filter((service) => !selectedIds.has(Number(service.id)))
      .filter((service) => String(service.Name || service.name || "").toLowerCase().includes(term))
      .slice(0, 12);
  }, [services, serviceSearch, formData.serviceIds]);

  const selectedServices = useMemo(() => {
    const selectedIds = new Set(formData.serviceIds.map(Number));
    return services.filter((service) => selectedIds.has(Number(service.id)));
  }, [services, formData.serviceIds]);

  const totalMinutes = useMemo(() => (
    selectedServices.reduce(
      (sum, service) => sum + Number(service.StandardMinutes || service.standardMinutes || 60),
      0,
    )
  ), [selectedServices]);

  const totalPrice = useMemo(() => (
    selectedServices.reduce(
      (sum, service) => sum + Number(service.BasePrice || service.basePrice || 0),
      0,
    )
  ), [selectedServices]);

  const selectedVehicleCount = formData.isQuickOrder
    ? 1
    : formData.isNewVehicle
      ? 1
      : formData.selectedVehicleIds.length;

  const appointmentSummary = useMemo(() => {
    if (!formData.appointmentDate || !formData.startTime) return "";
    const start = new Date(`${formData.appointmentDate}T${formData.startTime}`);
    if (Number.isNaN(start.getTime())) return "";
    const end = new Date(start.getTime() + totalMinutes * 60 * 1000);
    return `${start.toLocaleString("vi-VN")} - ${end.toLocaleTimeString("vi-VN", { hour: "2-digit", minute: "2-digit" })}`;
  }, [formData.appointmentDate, formData.startTime, totalMinutes]);

  const handleChange = (event) => {
    const { name, value } = event.target;
    setErrorText("");
    setFormData((prev) => ({ ...prev, [name]: value }));
  };

  const handlePlateBlur = (event) => {
    const { name, value } = event.target;
    setFormData((prev) => ({ ...prev, [name]: formatLicensePlate(value) }));
  };

  const toggleQuickOrder = () => {
    setErrorText("");
    setSearch("");
    setSelectedBrandId("");
    setGuestSelectedBrandId("");
    setFormData((prev) => ({
      ...prev,
      isQuickOrder: !prev.isQuickOrder,
      isNewVehicle: false,
      customerId: "",
      customerName: "",
      customerPhone: "",
      selectedVehicleIds: [],
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
    }));
  };

  const selectCustomer = (customer) => {
    setErrorText("");
    setSearch("");
    setSelectedBrandId("");
    const customerId = Number(customer.id);
    setFormData((prev) => ({
      ...prev,
      customerId,
      customerName: customer.fullName || "",
      customerPhone: customer.phone || "",
      isNewVehicle: false,
      selectedVehicleIds: [],
      vehicleLicensePlate: "",
      vehicleModelId: "",
      vehicleColor: "",
      vehicleVin: "",
      vehicleYear: "",
    }));
  };

  const toggleVehicleSelection = (vehicleId) => {
    const numericId = Number(vehicleId);
    setFormData((prev) => {
      const existing = new Set(prev.selectedVehicleIds.map(Number));
      if (existing.has(numericId)) {
        existing.delete(numericId);
      } else {
        existing.add(numericId);
      }
      return { ...prev, selectedVehicleIds: Array.from(existing) };
    });
  };

  const toggleService = (serviceId) => {
    const numericId = Number(serviceId);
    setFormData((prev) => {
      const existing = new Set(prev.serviceIds.map(Number));
      if (existing.has(numericId)) {
        existing.delete(numericId);
      } else {
        existing.add(numericId);
      }
      return { ...prev, serviceIds: Array.from(existing) };
    });
    setServiceSearch("");
  };

  const validateBeforeSubmit = () => {
    if (!formData.appointmentDate || !formData.startTime) {
      return "Vui lòng chọn ngày giờ hẹn.";
    }
    if (formData.serviceIds.length === 0) {
      return "Vui lòng chọn ít nhất 1 dịch vụ.";
    }

    if (formData.isQuickOrder) {
      if (!String(formData.guestName || "").trim()) return "Vui lòng nhập tên khách vãng lai.";
      if (!isValidPhone(formData.guestPhone)) return "Số điện thoại khách vãng lai không hợp lệ.";
      if (!String(formData.guestVehiclePlate || "").trim()) return "Vui lòng nhập biển số xe.";
      if (!formData.guestVehicleModelId) return "Vui lòng chọn dòng xe cho khách vãng lai.";
      if (!String(formData.guestVehicleVin || "").trim()) return "Vui lòng nhập số VIN cho xe vãng lai.";
      return "";
    }

    if (!formData.customerId) return "Vui lòng chọn khách hàng.";
    if (!isValidPhone(formData.customerPhone)) return "Số điện thoại khách hàng không hợp lệ.";

    if (formData.isNewVehicle) {
      if (!String(formData.vehicleLicensePlate || "").trim()) return "Vui lòng nhập biển số xe mới.";
      if (!formData.vehicleModelId) return "Vui lòng chọn dòng xe mới.";
      if (!String(formData.vehicleVin || "").trim()) return "Vui lòng nhập số VIN cho xe mới.";
      return "";
    }

    if (formData.selectedVehicleIds.length === 0) {
      return "Vui lòng chọn ít nhất 1 xe của khách.";
    }
    return "";
  };

  const handleSubmit = (event) => {
    event.preventDefault();
    const validationError = validateBeforeSubmit();
    if (validationError) {
      setErrorText(validationError);
      return;
    }

    onSubmit({
      ...formData,
      customerId: formData.customerId ? Number(formData.customerId) : null,
      selectedVehicleIds: formData.selectedVehicleIds.map(Number).filter((id) => id > 0),
      serviceIds: formData.serviceIds.map(Number).filter((id) => id > 0),
      vehicleModelId: formData.vehicleModelId ? Number(formData.vehicleModelId) : null,
      guestVehicleModelId: formData.guestVehicleModelId ? Number(formData.guestVehicleModelId) : null,
      totalMinutes,
    });
  };

  return (
    <form className="admin-form" onSubmit={handleSubmit}>
      <div style={{ display: "flex", alignItems: "center", gap: 8, marginBottom: 16 }}>
        <input
          id="appt-quick-toggle"
          type="checkbox"
          checked={formData.isQuickOrder}
          onChange={toggleQuickOrder}
        />
        <label htmlFor="appt-quick-toggle" style={{ fontWeight: 600 }}>
          Đặt lịch cho khách vãng lai
        </label>
      </div>

      {!formData.isQuickOrder ? (
        <>
          <div className="form-group">
            <label>Tìm khách hàng</label>
            <div style={{ position: "relative" }}>
              <input
                type="text"
                placeholder="Nhập tên hoặc số điện thoại..."
                value={search}
                onChange={(event) => setSearch(event.target.value)}
                required={!formData.customerId}
              />
              {filteredCustomers.length > 0 && (
                <div className="dropdown-panel" style={{ position: "absolute", top: "100%", left: 0, right: 0, zIndex: 20 }}>
                  {filteredCustomers.map((customer) => (
                    <div
                      key={customer.id}
                      className="dropdown-item"
                      onClick={() => selectCustomer(customer)}
                    >
                      <strong>{customer.fullName}</strong> - {customer.phone}
                    </div>
                  ))}
                </div>
              )}
            </div>
          </div>

          {formData.customerId && (
            <div style={{ background: "#f4f7fe", borderRadius: 12, padding: 12, marginBottom: 14 }}>
              <div style={{ fontSize: 13, marginBottom: 8 }}>
                <strong>{formData.customerName}</strong> - {formData.customerPhone}
              </div>
              <label style={{ display: "flex", alignItems: "center", gap: 8, fontSize: 13 }}>
                <input
                  type="checkbox"
                  checked={formData.isNewVehicle}
                  onChange={(event) =>
                    setFormData((prev) => ({
                      ...prev,
                      isNewVehicle: event.target.checked,
                      selectedVehicleIds: event.target.checked ? [] : prev.selectedVehicleIds,
                    }))}
                />
                Xe mới (chưa có trong danh sách)
              </label>
            </div>
          )}

          {!formData.isNewVehicle ? (
            <div className="form-group">
              <label>Xe của khách (có thể chọn nhiều xe để đặt lịch nhóm)</label>
              {!formData.customerId ? (
                <div style={{ fontSize: 13, color: "#a3aed0" }}>Hãy chọn khách hàng trước.</div>
              ) : customerVehiclesFiltered.length === 0 ? (
                <div style={{ fontSize: 13, color: "#a3aed0" }}>Khách hàng chưa có xe đã lưu.</div>
              ) : (
                <div style={{ display: "grid", gap: 8, maxHeight: 200, overflowY: "auto", border: "1px solid #e9edf7", borderRadius: 10, padding: 10 }}>
                  {customerVehiclesFiltered.map((vehicle) => {
                    const checked = formData.selectedVehicleIds.map(Number).includes(Number(vehicle.id));
                    return (
                      <label key={vehicle.id} style={{ display: "flex", alignItems: "center", gap: 8, fontSize: 13 }}>
                        <input
                          type="checkbox"
                          checked={checked}
                          onChange={() => toggleVehicleSelection(vehicle.id)}
                        />
                        <span>
                          <strong>{vehicle.licensePlate}</strong>
                          {(vehicle.brandName || vehicle.modelName) ? ` (${vehicle.brandName || ""} ${vehicle.modelName || ""})` : ""}
                        </span>
                      </label>
                    );
                  })}
                </div>
              )}
            </div>
          ) : (
            <div style={{ border: "1px solid #ffe2ad", background: "#fff9f2", borderRadius: 12, padding: 12 }}>
              <div className="form-group">
                <label>Biển số xe mới *</label>
                <input
                  type="text"
                  name="vehicleLicensePlate"
                  value={formData.vehicleLicensePlate}
                  onChange={handleChange}
                  onBlur={handlePlateBlur}
                  placeholder="VD: 30A-12345"
                />
              </div>
              <div className="form-row" style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 12 }}>
                <div className="form-group">
                  <label>Hãng xe *</label>
                  <select value={selectedBrandId} onChange={(event) => setSelectedBrandId(event.target.value)}>
                    <option value="">-- Chọn hãng --</option>
                    {brands.map((brand) => (
                      <option key={brand.id} value={brand.id}>{brand.name}</option>
                    ))}
                  </select>
                </div>
                <div className="form-group">
                  <label>Dòng xe *</label>
                  <select name="vehicleModelId" value={formData.vehicleModelId} onChange={handleChange} disabled={!selectedBrandId}>
                    <option value="">-- Chọn dòng xe --</option>
                    {models
                      .filter((model) => Number(model.brandId) === Number(selectedBrandId))
                      .map((model) => (
                        <option key={model.id} value={model.id}>{model.modelName}</option>
                      ))}
                  </select>
                </div>
              </div>
              <div className="form-row" style={{ display: "grid", gridTemplateColumns: "1fr 1fr 1fr", gap: 12 }}>
                <div className="form-group">
                  <label>Màu sắc</label>
                  <input name="vehicleColor" value={formData.vehicleColor} onChange={handleChange} />
                </div>
                <div className="form-group">
                  <label>Năm SX</label>
                  <select name="vehicleYear" value={formData.vehicleYear} onChange={handleChange}>
                    <option value="">-- Chọn năm --</option>
                    {years.map((year) => <option key={year} value={year}>{year}</option>)}
                  </select>
                </div>
                <div className="form-group">
                  <label>VIN *</label>
                  <input name="vehicleVin" value={formData.vehicleVin} onChange={handleChange} />
                </div>
              </div>
            </div>
          )}
        </>
      ) : (
        <div style={{ border: "1px dashed #c8d8ff", background: "#f7faff", borderRadius: 12, padding: 12 }}>
          <div className="form-row" style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 12 }}>
            <div className="form-group">
              <label>Tên khách *</label>
              <input name="guestName" value={formData.guestName} onChange={handleChange} />
            </div>
            <div className="form-group">
              <label>Số điện thoại *</label>
              <input name="guestPhone" value={formData.guestPhone} onChange={handleChange} />
            </div>
          </div>
          <div className="form-group">
            <label>Biển số xe *</label>
            <input
              name="guestVehiclePlate"
              value={formData.guestVehiclePlate}
              onChange={handleChange}
              onBlur={handlePlateBlur}
              style={{ textTransform: "uppercase" }}
            />
          </div>
          <div className="form-row" style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 12 }}>
            <div className="form-group">
              <label>Hãng xe *</label>
              <select value={guestSelectedBrandId} onChange={(event) => setGuestSelectedBrandId(event.target.value)}>
                <option value="">-- Chọn hãng --</option>
                {brands.map((brand) => (
                  <option key={brand.id} value={brand.id}>{brand.name}</option>
                ))}
              </select>
            </div>
            <div className="form-group">
              <label>Dòng xe *</label>
              <select
                name="guestVehicleModelId"
                value={formData.guestVehicleModelId}
                onChange={handleChange}
                disabled={!guestSelectedBrandId}
              >
                <option value="">-- Chọn dòng xe --</option>
                {models
                  .filter((model) => Number(model.brandId) === Number(guestSelectedBrandId))
                  .map((model) => (
                    <option key={model.id} value={model.id}>{model.modelName}</option>
                  ))}
              </select>
            </div>
          </div>
          <div className="form-row" style={{ display: "grid", gridTemplateColumns: "1fr 1fr 1fr", gap: 12 }}>
            <div className="form-group">
              <label>Màu sắc</label>
              <input name="guestVehicleColor" value={formData.guestVehicleColor} onChange={handleChange} />
            </div>
            <div className="form-group">
              <label>Năm SX</label>
              <select name="guestVehicleYear" value={formData.guestVehicleYear} onChange={handleChange}>
                <option value="">-- Chọn năm --</option>
                {years.map((year) => <option key={year} value={year}>{year}</option>)}
              </select>
            </div>
            <div className="form-group">
              <label>VIN *</label>
              <input name="guestVehicleVin" value={formData.guestVehicleVin} onChange={handleChange} />
            </div>
          </div>
        </div>
      )}

      <div className="form-group" style={{ marginTop: 14 }}>
        <label>Dịch vụ yêu cầu</label>
        <div style={{ position: "relative" }}>
          <input
            type="text"
            placeholder="Tìm dịch vụ..."
            value={serviceSearch}
            onChange={(event) => setServiceSearch(event.target.value)}
          />
          {filteredServices.length > 0 && (
            <div className="dropdown-panel" style={{ position: "absolute", top: "100%", left: 0, right: 0, zIndex: 20 }}>
              {filteredServices.map((service) => (
                <div key={service.id} className="dropdown-item" onClick={() => toggleService(service.id)}>
                  {service.Name || service.name}
                </div>
              ))}
            </div>
          )}
        </div>
        {formData.serviceIds.length > 0 && (
          <div style={{ marginTop: 8, display: "flex", flexWrap: "wrap", gap: 8 }}>
            {selectedServices.map((service) => (
              <button
                key={service.id}
                type="button"
                className="toolbar-action-btn toolbar-action-outline"
                onClick={() => toggleService(service.id)}
              >
                {service.Name || service.name} x
              </button>
            ))}
          </div>
        )}
      </div>

      <div style={{ marginTop: 12, background: "#f4f7fe", borderRadius: 10, padding: 10, fontSize: 13 }}>
        <div><strong>Tổng thời gian:</strong> {totalMinutes} phút</div>
        <div><strong>Tổng giá ước tính:</strong> {totalPrice.toLocaleString("vi-VN")}đ</div>
        <div><strong>Số xe:</strong> {selectedVehicleCount} {selectedVehicleCount > 1 ? "(lịch nhóm)" : "(lịch thường)"}</div>
      </div>

      <div className="form-group" style={{ marginTop: 12 }}>
        <label>Ngày giờ hẹn *</label>
        <DateTimePicker
          value={formData.appointmentDate && formData.startTime ? `${formData.appointmentDate}T${formData.startTime}` : ""}
          onChange={(value) => {
            if (!value) {
              setFormData((prev) => ({ ...prev, appointmentDate: "", startTime: "" }));
              return;
            }
            const date = new Date(value);
            const d = `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, "0")}-${String(date.getDate()).padStart(2, "0")}`;
            const t = `${String(date.getHours()).padStart(2, "0")}:${String(date.getMinutes()).padStart(2, "0")}`;
            setFormData((prev) => ({ ...prev, appointmentDate: d, startTime: t }));
          }}
          minDate={new Date()}
          placeholder="Chọn ngày giờ..."
        />
        {appointmentSummary && (
          <div style={{ marginTop: 8, fontSize: 12, color: "#445b7c" }}>
            {appointmentSummary}
          </div>
        )}
      </div>

      <div className="form-group">
        <label>Ghi chú</label>
        <textarea
          name="customerNotes"
          rows={3}
          value={formData.customerNotes}
          onChange={handleChange}
          placeholder="Mô tả thêm, yêu cầu khác..."
          style={{ resize: "vertical" }}
        />
      </div>

      {errorText && (
        <div style={{ background: "#fee2e2", color: "#9f1d1d", borderRadius: 8, padding: 10, marginBottom: 12, fontSize: 13 }}>
          {errorText}
        </div>
      )}

      <div className="form-actions" style={{ marginTop: 18 }}>
        <button type="button" className="btn-ghost" onClick={onCancel}>
          Hủy
        </button>
        <button type="submit" className="btn-primary" disabled={loading}>
          {loading ? "Đang xử lý..." : submitLabel}
        </button>
      </div>
    </form>
  );
}
