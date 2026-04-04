import React, { useEffect, useMemo, useState } from "react";
import {
  getVehicleBrands,
  getVehicleModels
} from "@features/appointments/api/appointmentApi";
import DateTimePicker from "@features/workshop/components/DateTimePicker";
import { formatLicensePlate } from "@features/workshop/utils/serviceOrderDetailUtils";

const SERVICE_MODE = {
  SHARED: "SHARED",
  CUSTOM: "CUSTOM"
};

const EMPTY_PLAN = {
  categoryIds: [],
  serviceIds: []
};

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
  customerNotes: ""
};

function normalizePhone(phone) {
  return String(phone || "").replace(/\s+/g, "");
}

function normalizeSearchText(value) {
  return String(value || "")
    .trim()
    .toLowerCase()
    .normalize("NFD")
    .replace(/[\u0300-\u036f]/g, "")
    .replace(/đ/g, "d");
}

function isValidPhone(phone) {
  return /^(0\d{9,10}|\+84\d{9,10})$/.test(normalizePhone(phone));
}

function toDateTimeParts(value) {
  if (!value) return { appointmentDate: "", startTime: "" };
  const parsed = new Date(value);
  if (Number.isNaN(parsed.getTime())) return { appointmentDate: "", startTime: "" };
  const appointmentDate = `${parsed.getFullYear()}-${String(parsed.getMonth() + 1).padStart(2, "0")}-${String(
    parsed.getDate()
  ).padStart(2, "0")}`;
  const startTime = `${String(parsed.getHours()).padStart(2, "0")}:${String(parsed.getMinutes()).padStart(2, "0")}`;
  return { appointmentDate, startTime };
}

function toNumberList(value) {
  if (!Array.isArray(value)) return [];
  return Array.from(
    new Set(
      value
        .map(Number)
        .filter((id) => Number.isFinite(id) && id > 0)
    )
  );
}

function buildInitialFormData(initialValues) {
  if (!initialValues) return { ...EMPTY_FORM_DATA };

  const selectedVehicleIds = Array.isArray(initialValues.selectedVehicleIds)
    ? toNumberList(initialValues.selectedVehicleIds)
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
    customerNotes: initialValues.customerNotes || ""
  };
}

function readField(obj, keys, fallback = null) {
  for (const key of keys) {
    if (obj?.[key] !== undefined && obj?.[key] !== null) return obj[key];
  }
  return fallback;
}

function normalizePlan(plan) {
  const categoryIds = toNumberList(plan?.categoryIds || []);
  const serviceIds = toNumberList(plan?.serviceIds || []);
  return { categoryIds, serviceIds };
}

function normalizeService(service) {
  const id = Number(readField(service, ["id", "Id"], 0));
  const categoryId = Number(readField(service, ["categoryId", "CategoryId"], 0));
  return {
    id,
    categoryId,
    name: String(readField(service, ["name", "Name"], "") || ""),
    minutes: Math.max(0, Number(readField(service, ["standardMinutes", "StandardMinutes"], 0) || 0)),
    price: Math.max(0, Number(readField(service, ["basePrice", "BasePrice"], 0) || 0))
  };
}

function getVehicleKeyFromId(vehicleId) {
  return `existing-${Number(vehicleId)}`;
}

function getVehicleIdFromKey(vehicleKey) {
  const match = String(vehicleKey || "").match(/^existing-(\d+)$/);
  return match ? Number(match[1]) : null;
}

function formatCurrency(value) {
  return `${Number(value || 0).toLocaleString("vi-VN")}đ`;
}

function buildCategoryOptions(serviceCategories, normalizedServices) {
  const map = new Map();

  (serviceCategories || []).forEach((category) => {
    const id = Number(readField(category, ["id", "Id"], 0));
    if (!Number.isFinite(id) || id <= 0) return;
    const name = String(readField(category, ["categoryName", "name", "Name"], "") || "").trim();
    if (!name) return;
    if (category?.isDeleted === true || category?.isActive === false) return;
    map.set(id, { id, name });
  });

  normalizedServices.forEach((service) => {
    if (!Number.isFinite(service.categoryId) || service.categoryId <= 0) return;
    if (!map.has(service.categoryId)) {
      map.set(service.categoryId, { id: service.categoryId, name: `Nhóm ${service.categoryId}` });
    }
  });

  return Array.from(map.values()).sort((a, b) => a.name.localeCompare(b.name, "vi"));
}

function clonePlan(plan) {
  return { categoryIds: [...(plan?.categoryIds || [])], serviceIds: [...(plan?.serviceIds || [])] };
}

export default function ReceptionCreateAppointmentForm({
  customers = [],
  vehicles = [],
  customerVehicles = [],
  serviceCategories = [],
  onSubmit,
  onCancel,
  loading,
  services = [],
  initialValues = null,
  submitLabel = "Xác nhận đặt lịch"
}) {
  const [formData, setFormData] = useState(() => buildInitialFormData(initialValues));
  const [search, setSearch] = useState("");
  const [serviceSearch, setServiceSearch] = useState("");
  const [brands, setBrands] = useState([]);
  const [models, setModels] = useState([]);
  const [selectedBrandId, setSelectedBrandId] = useState("");
  const [guestSelectedBrandId, setGuestSelectedBrandId] = useState("");
  const [errorText, setErrorText] = useState("");

  const [serviceMode, setServiceMode] = useState(SERVICE_MODE.SHARED);
  const [sharedPlan, setSharedPlan] = useState({ ...EMPTY_PLAN });
  const [vehiclePlans, setVehiclePlans] = useState({});
  const [activePlanVehicleKey, setActivePlanVehicleKey] = useState("");
  const [activeCategoryId, setActiveCategoryId] = useState("");

  const years = useMemo(() => {
    const currentYear = new Date().getFullYear();
    const list = [];
    for (let y = currentYear; y >= 1980; y -= 1) list.push(y);
    return list;
  }, []);

  const normalizedServices = useMemo(
    () =>
      (services || [])
        .map(normalizeService)
        .filter(
          (service) =>
            Number.isFinite(service.id) &&
            service.id > 0 &&
            Number.isFinite(service.categoryId) &&
            service.categoryId > 0
        ),
    [services]
  );

  const serviceById = useMemo(() => {
    const map = new Map();
    normalizedServices.forEach((service) => map.set(service.id, service));
    return map;
  }, [normalizedServices]);

  const serviceCategoryMap = useMemo(() => {
    const map = new Map();
    normalizedServices.forEach((service) => map.set(service.id, service.categoryId));
    return map;
  }, [normalizedServices]);

  const categoryOptions = useMemo(
    () => buildCategoryOptions(serviceCategories, normalizedServices),
    [serviceCategories, normalizedServices]
  );

  const categoryNameById = useMemo(() => {
    const map = new Map();
    categoryOptions.forEach((category) => map.set(category.id, category.name));
    return map;
  }, [categoryOptions]);

  useEffect(() => {
    let ignore = false;

    (async () => {
      try {
        const [brandRes, modelRes] = await Promise.all([getVehicleBrands(), getVehicleModels()]);
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
    setServiceMode(SERVICE_MODE.SHARED);
    setSharedPlan({ ...EMPTY_PLAN });
    setVehiclePlans({});
    setActivePlanVehicleKey("");
    setActiveCategoryId("");
    setFormData(buildInitialFormData(initialValues));
  }, [initialValues]);

  const filteredCustomers = useMemo(() => {
    const term = normalizeSearchText(search);
    if (!term) return [];
    return (customers || [])
      .filter((customer) => {
        const name = normalizeSearchText(customer.fullName || customer.name || "");
        const phone = normalizePhone(customer.phone || "");
        return name.includes(term) || phone.includes(term);
      })
      .slice(0, 10);
  }, [customers, search]);

  const customerVehiclesFiltered = useMemo(() => {
    if (!formData.customerId) return [];
    const customerId = Number(formData.customerId);
    const linkedVehicleIds = new Set(
      (customerVehicles || [])
        .filter((item) => Number(item.customerId) === customerId)
        .map((item) => Number(item.vehicleId))
    );
    return (vehicles || []).filter((vehicle) => linkedVehicleIds.has(Number(vehicle.id)));
  }, [customerVehicles, vehicles, formData.customerId]);

  const selectedVehicleKeys = useMemo(() => {
    if (formData.isQuickOrder) return ["guest"];
    if (formData.isNewVehicle) return ["new-customer"];
    return toNumberList(formData.selectedVehicleIds).map((vehicleId) => getVehicleKeyFromId(vehicleId));
  }, [formData.isQuickOrder, formData.isNewVehicle, formData.selectedVehicleIds]);

  const hasMultipleSelectedVehicles = selectedVehicleKeys.length > 1;
  const effectiveServiceMode = hasMultipleSelectedVehicles ? serviceMode : SERVICE_MODE.SHARED;

  const vehicleLabelByKey = useMemo(() => {
    const map = new Map();
    map.set("guest", "Xe khách vãng lai");
    map.set("new-customer", "Xe mới của khách");
    customerVehiclesFiltered.forEach((vehicle) => {
      const key = getVehicleKeyFromId(vehicle.id);
      const plate = String(vehicle.licensePlate || "").trim() || "Không biển số";
      const model = String(vehicle.modelName || "").trim();
      const label = model ? `${plate} - ${model}` : plate;
      map.set(key, label);
    });
    return map;
  }, [customerVehiclesFiltered]);

  useEffect(() => {
    setVehiclePlans((prev) => {
      const next = {};
      selectedVehicleKeys.forEach((key) => {
        next[key] = normalizePlan(prev[key] || clonePlan(sharedPlan));
      });
      return next;
    });

    if (selectedVehicleKeys.length <= 1) {
      setServiceMode(SERVICE_MODE.SHARED);
    }

    if (selectedVehicleKeys.length > 0) {
      setActivePlanVehicleKey((prev) => {
        if (prev && selectedVehicleKeys.includes(prev)) return prev;
        return selectedVehicleKeys[0];
      });
    } else {
      setActivePlanVehicleKey("");
    }
  }, [selectedVehicleKeys, sharedPlan]);

  useEffect(() => {
    if (effectiveServiceMode !== SERVICE_MODE.CUSTOM) return;
    setVehiclePlans((prev) => {
      const next = { ...prev };
      selectedVehicleKeys.forEach((key) => {
        if (!next[key]) next[key] = clonePlan(sharedPlan);
      });
      return next;
    });
  }, [effectiveServiceMode, selectedVehicleKeys, sharedPlan]);

  const activePlanKey = useMemo(() => {
    if (effectiveServiceMode === SERVICE_MODE.SHARED) return "shared";
    return activePlanVehicleKey || selectedVehicleKeys[0] || "";
  }, [effectiveServiceMode, activePlanVehicleKey, selectedVehicleKeys]);

  const activePlan = useMemo(() => {
    if (effectiveServiceMode === SERVICE_MODE.SHARED) return normalizePlan(sharedPlan);
    return normalizePlan(vehiclePlans[activePlanKey] || EMPTY_PLAN);
  }, [effectiveServiceMode, sharedPlan, vehiclePlans, activePlanKey]);

  useEffect(() => {
    const selectedCategories = activePlan.categoryIds || [];
    if (selectedCategories.length === 0) {
      setActiveCategoryId("");
      return;
    }
    if (!activeCategoryId || !selectedCategories.includes(Number(activeCategoryId))) {
      setActiveCategoryId(String(selectedCategories[0]));
    }
  }, [activeCategoryId, activePlan.categoryIds]);

  const applyPlanUpdate = (updater) => {
    if (effectiveServiceMode === SERVICE_MODE.SHARED) {
      setSharedPlan((prev) => normalizePlan(updater(normalizePlan(prev))));
      return;
    }
    if (!activePlanKey) return;
    setVehiclePlans((prev) => ({
      ...prev,
      [activePlanKey]: normalizePlan(updater(normalizePlan(prev[activePlanKey] || EMPTY_PLAN)))
    }));
  };

  const toggleCategory = (categoryId) => {
    const id = Number(categoryId);
    if (!Number.isFinite(id) || id <= 0) return;

    applyPlanUpdate((plan) => {
      const categorySet = new Set(plan.categoryIds || []);
      if (categorySet.has(id)) {
        categorySet.delete(id);
      } else {
        categorySet.add(id);
      }

      const nextCategoryIds = Array.from(categorySet);
      const nextServiceIds = (plan.serviceIds || []).filter((serviceId) =>
        nextCategoryIds.includes(Number(serviceCategoryMap.get(serviceId)))
      );

      return {
        categoryIds: nextCategoryIds,
        serviceIds: nextServiceIds
      };
    });
  };

  const toggleService = (serviceId) => {
    const numericId = Number(serviceId);
    const categoryId = Number(serviceCategoryMap.get(numericId) || 0);
    if (!numericId || !categoryId) return;

    applyPlanUpdate((plan) => {
      const categorySet = new Set(plan.categoryIds || []);
      categorySet.add(categoryId);

      const serviceSet = new Set(plan.serviceIds || []);
      if (serviceSet.has(numericId)) {
        serviceSet.delete(numericId);
      } else {
        serviceSet.add(numericId);
      }

      return {
        categoryIds: Array.from(categorySet),
        serviceIds: Array.from(serviceSet)
      };
    });
  };

  const filteredServices = useMemo(() => {
    const term = normalizeSearchText(serviceSearch);
    const selectedCategories = new Set(activePlan.categoryIds || []);
    if (selectedCategories.size === 0) return [];

    return normalizedServices
      .filter((service) => selectedCategories.has(service.categoryId))
      .filter((service) => (!activeCategoryId ? true : String(service.categoryId) === String(activeCategoryId)))
      .filter((service) => (term ? normalizeSearchText(service.name).includes(term) : true))
      .sort((a, b) => a.name.localeCompare(b.name, "vi"));
  }, [activePlan.categoryIds, activeCategoryId, normalizedServices, serviceSearch]);

  const plansByVehicleKey = useMemo(() => {
    const map = new Map();
    selectedVehicleKeys.forEach((key) => {
      if (effectiveServiceMode === SERVICE_MODE.SHARED) {
        map.set(key, normalizePlan(sharedPlan));
      } else {
        map.set(key, normalizePlan(vehiclePlans[key] || EMPTY_PLAN));
      }
    });
    return map;
  }, [selectedVehicleKeys, effectiveServiceMode, sharedPlan, vehiclePlans]);

  const planSummaries = useMemo(() => {
    return selectedVehicleKeys.map((vehicleKey) => {
      const plan = plansByVehicleKey.get(vehicleKey) || EMPTY_PLAN;
      const selectedServices = toNumberList(plan.serviceIds)
        .map((id) => serviceById.get(id))
        .filter(Boolean);
      const minutes = selectedServices.reduce((sum, item) => sum + Number(item.minutes || 0), 0);
      const price = selectedServices.reduce((sum, item) => sum + Number(item.price || 0), 0);

      return {
        vehicleKey,
        vehicleId: getVehicleIdFromKey(vehicleKey),
        vehicleLabel: vehicleLabelByKey.get(vehicleKey) || vehicleKey,
        categoryIds: toNumberList(plan.categoryIds),
        serviceIds: toNumberList(plan.serviceIds),
        serviceNames: selectedServices.map((item) => item.name),
        totalMinutes: minutes,
        totalPrice: price
      };
    });
  }, [plansByVehicleKey, selectedVehicleKeys, serviceById, vehicleLabelByKey]);

  const totalMinutes = useMemo(
    () => planSummaries.reduce((sum, item) => sum + Number(item.totalMinutes || 0), 0),
    [planSummaries]
  );
  const totalPrice = useMemo(
    () => planSummaries.reduce((sum, item) => sum + Number(item.totalPrice || 0), 0),
    [planSummaries]
  );
  const completionMinutes = useMemo(
    () => Math.max(...planSummaries.map((item) => Number(item.totalMinutes || 0)), 0),
    [planSummaries]
  );

  const appointmentSummary = useMemo(() => {
    if (!formData.appointmentDate || !formData.startTime) return "";
    const start = new Date(`${formData.appointmentDate}T${formData.startTime}`);
    if (Number.isNaN(start.getTime())) return "";

    const end = new Date(start.getTime() + Math.max(completionMinutes, 30) * 60 * 1000);
    return `${start.toLocaleString("vi-VN")} - ${end.toLocaleTimeString("vi-VN", {
      hour: "2-digit",
      minute: "2-digit"
    })}`;
  }, [formData.appointmentDate, formData.startTime, completionMinutes]);

  const selectedServiceIdsUnion = useMemo(
    () => toNumberList(planSummaries.flatMap((item) => item.serviceIds)),
    [planSummaries]
  );

  const selectedCategoryIdsUnion = useMemo(
    () => toNumberList(planSummaries.flatMap((item) => item.categoryIds)),
    [planSummaries]
  );

  const selectedServicesForActivePlan = useMemo(() => {
    const ids = toNumberList(activePlan.serviceIds);
    return ids.map((id) => serviceById.get(id)).filter(Boolean);
  }, [activePlan.serviceIds, serviceById]);

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
    setServiceSearch("");
    setSelectedBrandId("");
    setGuestSelectedBrandId("");
    setSharedPlan({ ...EMPTY_PLAN });
    setVehiclePlans({});
    setActivePlanVehicleKey("");
    setActiveCategoryId("");
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
      guestVehicleYear: ""
    }));
  };

  const selectCustomer = (customer) => {
    setErrorText("");
    setSearch("");
    setSelectedBrandId("");
    setSharedPlan({ ...EMPTY_PLAN });
    setVehiclePlans({});
    setActivePlanVehicleKey("");
    setActiveCategoryId("");
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
      vehicleYear: ""
    }));
  };

  const toggleVehicleSelection = (vehicleId) => {
    const numericId = Number(vehicleId);
    setFormData((prev) => {
      const existing = new Set(toNumberList(prev.selectedVehicleIds));
      if (existing.has(numericId)) existing.delete(numericId);
      else existing.add(numericId);
      return { ...prev, selectedVehicleIds: Array.from(existing) };
    });
  };

  const validateBeforeSubmit = () => {
    if (!formData.appointmentDate || !formData.startTime) {
      return "Vui lòng chọn ngày giờ hẹn.";
    }

    if (formData.isQuickOrder) {
      if (!String(formData.guestName || "").trim()) return "Vui lòng nhập tên khách vãng lai.";
      if (!isValidPhone(formData.guestPhone)) return "Số điện thoại khách vãng lai không hợp lệ.";
      if (!String(formData.guestVehiclePlate || "").trim()) return "Vui lòng nhập biển số xe.";
      if (!formData.guestVehicleModelId) return "Vui lòng chọn dòng xe cho khách vãng lai.";
      if (!String(formData.guestVehicleVin || "").trim()) return "Vui lòng nhập số VIN cho xe vãng lai.";
    } else {
      if (!formData.customerId) return "Vui lòng chọn khách hàng.";
      if (!isValidPhone(formData.customerPhone)) return "Số điện thoại khách hàng không hợp lệ.";

      if (formData.isNewVehicle) {
        if (!String(formData.vehicleLicensePlate || "").trim()) return "Vui lòng nhập biển số xe mới.";
        if (!formData.vehicleModelId) return "Vui lòng chọn dòng xe mới.";
        if (!String(formData.vehicleVin || "").trim()) return "Vui lòng nhập số VIN cho xe mới.";
      } else if (toNumberList(formData.selectedVehicleIds).length === 0) {
        return "Vui lòng chọn ít nhất 1 xe của khách.";
      }
    }

    if (selectedVehicleKeys.length === 0) {
      return "Vui lòng chọn ít nhất một xe để gán dịch vụ.";
    }

    for (const plan of planSummaries) {
      if ((plan.categoryIds || []).length === 0) {
        return `Vui lòng chọn loại dịch vụ cho ${plan.vehicleLabel}.`;
      }
      if ((plan.serviceIds || []).length === 0) {
        return `Vui lòng chọn dịch vụ cụ thể cho ${plan.vehicleLabel}.`;
      }
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
      selectedVehicleIds: toNumberList(formData.selectedVehicleIds),
      serviceIds: selectedServiceIdsUnion,
      selectedCategoryIds: selectedCategoryIdsUnion,
      vehicleServicePlans: planSummaries.map((plan) => ({
        vehicleKey: plan.vehicleKey,
        vehicleId: plan.vehicleId,
        categoryIds: plan.categoryIds,
        serviceIds: plan.serviceIds,
        totalMinutes: plan.totalMinutes,
        totalPrice: plan.totalPrice
      })),
      servicePlanMode: effectiveServiceMode,
      totalMinutes: Math.max(completionMinutes, 30),
      fleetTotalMinutes: totalMinutes,
      fleetCompletionMinutes: completionMinutes,
      fleetTotalPrice: totalPrice
    });
  };

  const selectedVehicleCount = selectedVehicleKeys.length;

  return (
    <form className="admin-form reception-appt-form" onSubmit={handleSubmit}>
      <div className="reception-appt-quick-toggle">
        <input
          id="appt-quick-toggle"
          type="checkbox"
          checked={formData.isQuickOrder}
          onChange={toggleQuickOrder}
        />
        <label htmlFor="appt-quick-toggle">Đặt lịch cho khách vãng lai</label>
      </div>

      {!formData.isQuickOrder ? (
        <section className="reception-appt-section">
          <h4 className="reception-appt-section-title">Khách hàng và xe</h4>

          <div className="form-group">
            <label>Tìm khách hàng</label>
            <div className="search-input-wrapper">
              <input
                type="text"
                placeholder="Nhập tên hoặc số điện thoại..."
                value={search}
                onChange={(event) => setSearch(event.target.value)}
                required={!formData.customerId}
              />
              {filteredCustomers.length > 0 ? (
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
              ) : null}
            </div>
          </div>

          {formData.customerId ? (
            <div className="reception-customer-selected-card">
              <div>
                <strong>{formData.customerName}</strong> - {formData.customerPhone}
              </div>
              <label className="reception-checkbox-inline">
                <input
                  type="checkbox"
                  checked={formData.isNewVehicle}
                  onChange={(event) =>
                    setFormData((prev) => ({
                      ...prev,
                      isNewVehicle: event.target.checked,
                      selectedVehicleIds: event.target.checked ? [] : prev.selectedVehicleIds
                    }))
                  }
                />
                <span>Thêm xe mới chưa có trong danh sách</span>
              </label>
            </div>
          ) : null}

          {!formData.isNewVehicle ? (
            <div className="form-group">
              <label>Xe của khách (có thể chọn nhiều xe để đặt lịch nhóm)</label>
              {!formData.customerId ? (
                <div className="reception-muted-note">Hãy chọn khách hàng trước.</div>
              ) : customerVehiclesFiltered.length === 0 ? (
                <div className="reception-muted-note">Khách hàng chưa có xe đã lưu.</div>
              ) : (
                <div className="reception-vehicle-selector">
                  {customerVehiclesFiltered.map((vehicle) => {
                    const checked = toNumberList(formData.selectedVehicleIds).includes(Number(vehicle.id));
                    return (
                      <label key={vehicle.id} className={`reception-vehicle-option ${checked ? "selected" : ""}`}>
                        <input
                          type="checkbox"
                          checked={checked}
                          onChange={() => toggleVehicleSelection(vehicle.id)}
                        />
                        <span className="reception-vehicle-option-main">
                          <strong>{vehicle.licensePlate}</strong>
                          <small>
                            {vehicle.brandName || vehicle.modelName
                              ? `${vehicle.brandName || ""} ${vehicle.modelName || ""}`.trim()
                              : "Xe đã liên kết"}
                          </small>
                        </span>
                      </label>
                    );
                  })}
                </div>
              )}
            </div>
          ) : (
            <div className="reception-new-vehicle-box">
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
              <div className="reception-grid reception-grid-2">
                <div className="form-group">
                  <label>Hãng xe *</label>
                  <select value={selectedBrandId} onChange={(event) => setSelectedBrandId(event.target.value)}>
                    <option value="">-- Chọn hãng --</option>
                    {brands.map((brand) => (
                      <option key={brand.id} value={brand.id}>
                        {brand.name}
                      </option>
                    ))}
                  </select>
                </div>
                <div className="form-group">
                  <label>Dòng xe *</label>
                  <select
                    name="vehicleModelId"
                    value={formData.vehicleModelId}
                    onChange={handleChange}
                    disabled={!selectedBrandId}
                  >
                    <option value="">-- Chọn dòng xe --</option>
                    {models
                      .filter((model) => Number(model.brandId) === Number(selectedBrandId))
                      .map((model) => (
                        <option key={model.id} value={model.id}>
                          {model.modelName}
                        </option>
                      ))}
                  </select>
                </div>
              </div>
              <div className="reception-grid reception-grid-3">
                <div className="form-group">
                  <label>Màu sắc</label>
                  <input name="vehicleColor" value={formData.vehicleColor} onChange={handleChange} />
                </div>
                <div className="form-group">
                  <label>Năm sản xuất</label>
                  <select name="vehicleYear" value={formData.vehicleYear} onChange={handleChange}>
                    <option value="">-- Chọn năm --</option>
                    {years.map((year) => (
                      <option key={year} value={year}>
                        {year}
                      </option>
                    ))}
                  </select>
                </div>
                <div className="form-group">
                  <label>VIN *</label>
                  <input name="vehicleVin" value={formData.vehicleVin} onChange={handleChange} />
                </div>
              </div>
            </div>
          )}
        </section>
      ) : (
        <section className="reception-appt-section">
          <h4 className="reception-appt-section-title">Khách vãng lai</h4>
          <div className="reception-new-vehicle-box">
            <div className="reception-grid reception-grid-2">
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
              />
            </div>
            <div className="reception-grid reception-grid-2">
              <div className="form-group">
                <label>Hãng xe *</label>
                <select value={guestSelectedBrandId} onChange={(event) => setGuestSelectedBrandId(event.target.value)}>
                  <option value="">-- Chọn hãng --</option>
                  {brands.map((brand) => (
                    <option key={brand.id} value={brand.id}>
                      {brand.name}
                    </option>
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
                      <option key={model.id} value={model.id}>
                        {model.modelName}
                      </option>
                    ))}
                </select>
              </div>
            </div>
            <div className="reception-grid reception-grid-3">
              <div className="form-group">
                <label>Màu sắc</label>
                <input name="guestVehicleColor" value={formData.guestVehicleColor} onChange={handleChange} />
              </div>
              <div className="form-group">
                <label>Năm sản xuất</label>
                <select name="guestVehicleYear" value={formData.guestVehicleYear} onChange={handleChange}>
                  <option value="">-- Chọn năm --</option>
                  {years.map((year) => (
                    <option key={year} value={year}>
                      {year}
                    </option>
                  ))}
                </select>
              </div>
              <div className="form-group">
                <label>VIN *</label>
                <input name="guestVehicleVin" value={formData.guestVehicleVin} onChange={handleChange} />
              </div>
            </div>
          </div>
        </section>
      )}

      <section className="reception-appt-section">
        <h4 className="reception-appt-section-title">Loại dịch vụ và dịch vụ cụ thể</h4>

        {hasMultipleSelectedVehicles ? (
          <div className="reception-mode-switch">
            <button
              type="button"
              className={effectiveServiceMode === SERVICE_MODE.SHARED ? "active" : ""}
              onClick={() => setServiceMode(SERVICE_MODE.SHARED)}
            >
              Dùng chung cho tất cả xe
            </button>
            <button
              type="button"
              className={effectiveServiceMode === SERVICE_MODE.CUSTOM ? "active" : ""}
              onClick={() => setServiceMode(SERVICE_MODE.CUSTOM)}
            >
              Tùy chỉnh theo từng xe
            </button>
          </div>
        ) : null}

        {effectiveServiceMode === SERVICE_MODE.CUSTOM && selectedVehicleKeys.length > 1 ? (
          <div className="reception-vehicle-tabs">
            {selectedVehicleKeys.map((vehicleKey) => (
              <button
                key={vehicleKey}
                type="button"
                className={activePlanVehicleKey === vehicleKey ? "active" : ""}
                onClick={() => setActivePlanVehicleKey(vehicleKey)}
              >
                {vehicleLabelByKey.get(vehicleKey) || vehicleKey}
              </button>
            ))}
          </div>
        ) : null}

        <div className="reception-category-grid">
          {categoryOptions.map((category) => {
            const selected = activePlan.categoryIds.includes(Number(category.id));
            return (
              <button
                key={category.id}
                type="button"
                className={`reception-category-chip ${selected ? "selected" : ""}`}
                onClick={() => toggleCategory(category.id)}
              >
                {category.name}
              </button>
            );
          })}
        </div>

        {activePlan.categoryIds.length > 0 ? (
          <>
            <div className="reception-selected-categories">
              {activePlan.categoryIds.map((categoryId) => (
                <button
                  key={`cat-${categoryId}`}
                  type="button"
                  className={`reception-selected-category ${String(activeCategoryId) === String(categoryId) ? "active" : ""}`}
                  onClick={() => setActiveCategoryId(String(categoryId))}
                >
                  <span>{categoryNameById.get(categoryId) || `Nhóm ${categoryId}`}</span>
                  <i
                    onClick={(event) => {
                      event.stopPropagation();
                      toggleCategory(categoryId);
                    }}
                  >
                    ×
                  </i>
                </button>
              ))}
            </div>

            <div className="form-group">
              <label>Tìm dịch vụ</label>
              <input
                type="text"
                placeholder="Tìm theo tên dịch vụ..."
                value={serviceSearch}
                onChange={(event) => setServiceSearch(event.target.value)}
              />
            </div>

            <div className="reception-service-list">
              {filteredServices.length === 0 ? (
                <div className="reception-muted-note">
                  Không tìm thấy dịch vụ phù hợp trong nhóm đã chọn.
                </div>
              ) : (
                filteredServices.map((service) => {
                  const selected = activePlan.serviceIds.includes(service.id);
                  return (
                    <button
                      key={service.id}
                      type="button"
                      className={`reception-service-card ${selected ? "selected" : ""}`}
                      onClick={() => toggleService(service.id)}
                    >
                      <div className="reception-service-card-main">
                        <strong>{service.name}</strong>
                        <small>{service.minutes > 0 ? `${service.minutes} phút` : "Chưa có thời lượng"}</small>
                      </div>
                      <span>{formatCurrency(service.price)}</span>
                    </button>
                  );
                })
              )}
            </div>
          </>
        ) : (
          <div className="reception-muted-note">Hãy chọn ít nhất một loại dịch vụ trước khi chọn dịch vụ cụ thể.</div>
        )}

        {selectedServicesForActivePlan.length > 0 ? (
          <div className="reception-selected-services">
            {selectedServicesForActivePlan.map((service) => (
              <button
                key={`selected-${service.id}`}
                type="button"
                className="reception-selected-service-chip"
                onClick={() => toggleService(service.id)}
              >
                {service.name} ×
              </button>
            ))}
          </div>
        ) : null}
      </section>

      <div className="reception-summary-box">
        <div>
          <strong>Tổng thời gian:</strong> {totalMinutes} phút
        </div>
        <div>
          <strong>Thời gian hoàn tất dự kiến:</strong> {Math.max(completionMinutes, 30)} phút
        </div>
        <div>
          <strong>Tổng giá ước tính:</strong> {formatCurrency(totalPrice)}
        </div>
        <div>
          <strong>Số xe:</strong> {selectedVehicleCount} {selectedVehicleCount > 1 ? "(lịch nhóm)" : "(lịch thường)"}
        </div>
      </div>

      {planSummaries.length > 1 ? (
        <div className="reception-plan-summary-grid">
          {planSummaries.map((plan) => (
            <article key={`plan-summary-${plan.vehicleKey}`} className="reception-plan-summary-card">
              <h5>{plan.vehicleLabel}</h5>
              <p>{plan.serviceNames.length > 0 ? plan.serviceNames.join(", ") : "Chưa có dịch vụ"}</p>
              <small>
                {plan.totalMinutes} phút - {formatCurrency(plan.totalPrice)}
              </small>
            </article>
          ))}
        </div>
      ) : null}

      <section className="reception-appt-section">
        <div className="form-group">
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
          {appointmentSummary ? <div className="reception-muted-note">{appointmentSummary}</div> : null}
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
      </section>

      {errorText ? (
        <div className="reception-error-banner">{errorText}</div>
      ) : null}

      <div className="form-actions reception-form-actions">
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
