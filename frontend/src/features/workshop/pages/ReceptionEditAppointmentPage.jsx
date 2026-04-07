import React, { useCallback, useEffect, useMemo, useState } from "react";
import { useNavigate, useParams } from "react-router-dom";
import {
  getAllCustomers,
  getAllCustomerVehicles,
  getAllVehicles,
} from "@features/admin/api/customerApi";
import {
  getAllServiceCategories,
  getAllStandardServices,
  getAppointmentDetail,
} from "@features/appointments/api/appointmentApi";
import ReceptionCreateAppointmentForm from "@features/workshop/components/ReceptionCreateAppointmentForm";
import { saveReceptionAppointment } from "@features/workshop/utils/receptionAppointmentBooking";
import { useAuth } from "@shared/core/context/AuthContext";
import { useBranch } from "@shared/core/context/BranchContext";
import "./Workshop.css";

const TEXT = {
  invalidId: "MÃ£ lá»‹ch háº¹n khÃ´ng há»£p lá»‡.",
  loadError: "KhÃ´ng thá»ƒ táº£i dá»¯ liá»‡u sá»­a lá»‹ch háº¹n.",
  notFound: "KhÃ´ng tÃ¬m tháº¥y lá»‹ch háº¹n cáº§n sá»­a.",
  needBranch: "Vui lÃ²ng chá»n Ä‘Ãºng chi nhÃ¡nh lÃ m viá»‡c trÆ°á»›c khi sá»­a lá»‹ch háº¹n.",
  updateError: "KhÃ´ng thá»ƒ cáº­p nháº­t lá»‹ch háº¹n.",
  notEditableStatus: "KhÃ´ng thá»ƒ sá»­a lá»‹ch khi xe Ä‘Ã£ Ä‘áº¿n hoáº·c lá»‹ch Ä‘Ã£ káº¿t thÃºc.",
  notEditableWindow: "KhÃ´ng thá»ƒ sá»­a lá»‹ch háº¹n vÃ¬ Ä‘Ã£ qua khung giá» Ä‘Ã£ chá»n lÃºc táº¡o.",
  loadingData: "Äang táº£i dá»¯ liá»‡u lá»‹ch háº¹n...",
  pageTitle: "Chá»‰nh sá»­a lá»‹ch háº¹n (Lá»… tÃ¢n)",
  back: "<- Quay láº¡i lá»‹ch háº¹n",
  submit: "LÆ°u cáº­p nháº­t",
  successTitle: "Cáº­p nháº­t lá»‹ch háº¹n thÃ nh cÃ´ng",
  successMessage: "Há»‡ thá»‘ng Ä‘Ã£ táº¡o lá»‹ch má»›i vÃ  thay tháº¿ lá»‹ch cÅ©.",
};

function parsePositiveId(value) {
  const parsed = Number(value);
  return Number.isFinite(parsed) && parsed > 0 ? parsed : 0;
}

function parseBranchId(value) {
  const parsed = Number(value);
  return Number.isFinite(parsed) && parsed > 0 ? parsed : 0;
}

function normalizeStatus(status) {
  return String(status || "").trim().toUpperCase();
}

function canEditAppointmentStatus(status) {
  const normalized = normalizeStatus(status);
  return normalized === "PENDING" || normalized === "CONFIRMED" || normalized === "LATE";
}

function hasAppointmentWindowPassed(appointment) {
  const endRaw = appointment?.endTime ?? appointment?.EndTime;
  if (!endRaw) return false;
  const end = new Date(endRaw);
  if (Number.isNaN(end.getTime())) return false;
  return Date.now() >= end.getTime();
}

function normalizeAppointmentItem(item) {
  const source = item || {};
  const pick = (...keys) => {
    for (const key of keys) {
      const value = source?.[key];
      if (value !== undefined && value !== null) return value;
    }
    return null;
  };
  const numeric = (value) => {
    const parsed = Number(value);
    return Number.isFinite(parsed) ? parsed : 0;
  };
  const text = (value) => (value === undefined || value === null ? "" : String(value));

  return {
    ...source,
    id: numeric(pick("id", "Id")),
    branchId: numeric(pick("branchId", "BranchId")),
    customerId: numeric(pick("customerId", "CustomerId")),
    customerName: text(pick("customerName", "CustomerName", "customerFullName", "CustomerFullName")),
    customerPhone: text(pick("customerPhone", "CustomerPhone")),
    startTime: pick("startTime", "StartTime"),
    endTime: pick("endTime", "EndTime"),
    status: text(pick("status", "Status")),
    vehicleId: numeric(pick("vehicleId", "VehicleId")),
    requestedServiceIds: text(pick("requestedServiceIds", "RequestedServiceIds")),
    customerNotes: text(pick("customerNotes", "CustomerNotes")),
  };
}

function parseRequestedServiceIds(value) {
  return Array.from(
    new Set(
      String(value || "")
        .split(/[,|]/g)
        .map((item) => Number(String(item || "").trim()))
        .filter((id) => Number.isFinite(id) && id > 0)
    )
  );
}

function buildEditInitialValues(appointment) {
  if (!appointment) return null;

  const serviceIds = parseRequestedServiceIds(appointment.requestedServiceIds);
  const vehicleId = Number(appointment.vehicleId || 0);
  const selectedVehicleIds = vehicleId > 0 ? [vehicleId] : [];
  const vehicleServicePlans = vehicleId > 0
    ? [{
      vehicleKey: `existing-${vehicleId}`,
      vehicleId,
      serviceIds,
    }]
    : [];

  return {
    customerId: Number(appointment.customerId || 0) || "",
    customerName: appointment.customerName || "",
    customerPhone: appointment.customerPhone || "",
    selectedVehicleIds,
    startTime: appointment.startTime || "",
    endTime: appointment.endTime || "",
    requestedServiceIds: appointment.requestedServiceIds || "",
    serviceIds,
    vehicleServicePlans,
    customerNotes: appointment.customerNotes || "",
  };
}

function formatCurrentSlotText(appointment) {
  const start = new Date(appointment?.startTime || "");
  const end = new Date(appointment?.endTime || "");
  if (Number.isNaN(start.getTime()) || Number.isNaN(end.getTime())) return "";

  const date = start.toLocaleDateString("vi-VN");
  const startTime = start.toLocaleTimeString("vi-VN", { hour: "2-digit", minute: "2-digit" });
  const endTime = end.toLocaleTimeString("vi-VN", { hour: "2-digit", minute: "2-digit" });
  return `Lá»‹ch hiá»‡n táº¡i: ${date}, ${startTime} - ${endTime}`;
}

export default function ReceptionEditAppointmentPage() {
  const navigate = useNavigate();
  const { appointmentId: appointmentIdParam } = useParams();
  const { selectedBranchId } = useBranch();
  const { user } = useAuth();

  const appointmentId = parsePositiveId(appointmentIdParam);

  const [customers, setCustomers] = useState([]);
  const [vehicles, setVehicles] = useState([]);
  const [customerVehicles, setCustomerVehicles] = useState([]);
  const [services, setServices] = useState([]);
  const [serviceCategories, setServiceCategories] = useState([]);
  const [appointment, setAppointment] = useState(null);
  const [initialValues, setInitialValues] = useState(null);

  const [loading, setLoading] = useState(false);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState("");
  const [successAppt, setSuccessAppt] = useState(null);

  const userBranchId = parseBranchId(user?.branchId ?? user?.BranchId);
  const contextBranchId = parseBranchId(selectedBranchId);
  const effectiveBranchId = contextBranchId || userBranchId;
  const targetBranchId = parseBranchId(appointment?.branchId) || effectiveBranchId;

  const hasBranch = targetBranchId > 0;
  const canSubmit = hasBranch && appointmentId > 0 && Boolean(appointment);
  const isWindowPassed = hasAppointmentWindowPassed(appointment);
  const canEdit = canEditAppointmentStatus(appointment?.status) && !isWindowPassed;
  const editBlockedMessage = isWindowPassed ? TEXT.notEditableWindow : TEXT.notEditableStatus;
  const currentSlotText = useMemo(() => formatCurrentSlotText(appointment), [appointment]);

  const fetchPageData = useCallback(async () => {
    if (!appointmentId) {
      setError(TEXT.invalidId);
      return;
    }

    setLoading(true);
    setError("");
    try {
      const [detailRes, customerRes, vehicleRes, customerVehicleRes, serviceRes, serviceCategoryRes] = await Promise.all([
        getAppointmentDetail(appointmentId),
        getAllCustomers(),
        getAllVehicles(),
        getAllCustomerVehicles(),
        getAllStandardServices(),
        getAllServiceCategories(),
      ]);

      const normalizedAppt = normalizeAppointmentItem(detailRes);
      if (!normalizedAppt?.id) {
        throw new Error(TEXT.notFound);
      }

      setAppointment(normalizedAppt);
      setInitialValues(buildEditInitialValues(normalizedAppt));
      setCustomers(Array.isArray(customerRes) ? customerRes : []);
      setVehicles(Array.isArray(vehicleRes) ? vehicleRes : []);
      setCustomerVehicles(Array.isArray(customerVehicleRes) ? customerVehicleRes : []);
      setServices(Array.isArray(serviceRes) ? serviceRes : []);
      setServiceCategories(Array.isArray(serviceCategoryRes) ? serviceCategoryRes : []);
    } catch (err) {
      setError(err?.message || TEXT.loadError);
    } finally {
      setLoading(false);
    }
  }, [appointmentId]);

  useEffect(() => {
    fetchPageData();
  }, [fetchPageData]);

  const handleGoBack = useCallback(() => {
    navigate("/workshop/reception/appointments");
  }, [navigate]);

  useEffect(() => {
    if (!successAppt) return undefined;
    const timeoutId = setTimeout(() => {
      navigate("/workshop/reception/appointments", { replace: true });
    }, 2000);
    return () => clearTimeout(timeoutId);
  }, [successAppt, navigate]);

  useEffect(() => {
    if (!error && (!appointment || canSubmit)) return;
    if (typeof window !== "undefined" && typeof window.scrollTo === "function") {
      window.scrollTo({ top: 0, behavior: "smooth" });
    }
  }, [appointment, error, canSubmit]);

  const handleSubmit = useCallback(
    async (formData) => {
      if (!canSubmit) {
        setError(TEXT.needBranch);
        return;
      }

      const windowPassedAtSubmit = hasAppointmentWindowPassed(appointment);
      const statusEditable = canEditAppointmentStatus(appointment?.status);
      if (!statusEditable || windowPassedAtSubmit) {
        setError(windowPassedAtSubmit ? TEXT.notEditableWindow : TEXT.notEditableStatus);
        return;
      }

      setSaving(true);
      setError("");
      try {
        const summary = await saveReceptionAppointment({
          effectiveBranchId: targetBranchId,
          formData,
          customers,
          vehicles,
          customerVehicles,
          services,
          replaceAppointmentId: appointment.id,
        });
        setSuccessAppt(summary);
      } catch (err) {
        setError(err?.message || TEXT.updateError);
      } finally {
        setSaving(false);
      }
    },
    [appointment, canSubmit, customerVehicles, customers, services, targetBranchId, vehicles],
  );

  return (
    <div className="create-order-page reception-create-appointment-page reception-edit-appointment-page">
      <div className="page-header-actions reception-create-header">
        <button className="btn-back" type="button" onClick={handleGoBack}>
          {TEXT.back}
        </button>
        <div className="reception-create-header-copy">
          <h2 className="page-title">{TEXT.pageTitle}</h2>
          {appointment?.id ? (
            <p className="reception-create-header-note">
              {`MÃ£ lá»‹ch háº¹n #${appointment.id}`}
              {currentSlotText ? ` - ${currentSlotText}` : ""}
            </p>
          ) : null}
        </div>
      </div>

      {appointment && !hasBranch && <div className="validation-summary">{TEXT.needBranch}</div>}
      {error && <div className="validation-summary">{error}</div>}
      {!loading && appointment && !canEdit && <div className="validation-summary">{editBlockedMessage}</div>}

      {loading ? (
        <div className="workshop-table-card">
          <div className="loading-state">
            <div className="spinner" />
            <p>{TEXT.loadingData}</p>
          </div>
        </div>
      ) : appointment && initialValues && canEdit ? (
        <section className="form-section reception-create-appointment-form">
          <ReceptionCreateAppointmentForm
            customers={customers}
            vehicles={vehicles}
            customerVehicles={customerVehicles}
            serviceCategories={serviceCategories}
            services={services}
            branchId={targetBranchId}
            initialValues={initialValues}
            onSubmit={handleSubmit}
            onCancel={handleGoBack}
            loading={saving}
            submitLabel={TEXT.submit}
          />
        </section>
      ) : !error ? (
        <div className="workshop-table-card">
          <p>{TEXT.notFound}</p>
        </div>
      ) : null}

      {successAppt ? (
        <div className="reception-create-success-backdrop" role="status" aria-live="polite">
          <div className="reception-create-success-popup">
            <span className="reception-create-success-icon" aria-hidden="true">
              <svg viewBox="0 0 24 24" focusable="false">
                <path d="M9.2 16.3 4.9 12l1.4-1.4 2.9 2.9 8.5-8.5 1.4 1.4z" />
              </svg>
            </span>
            <h3>{TEXT.successTitle}</h3>
            <p>{TEXT.successMessage}</p>
          </div>
        </div>
      ) : null}
    </div>
  );
}

