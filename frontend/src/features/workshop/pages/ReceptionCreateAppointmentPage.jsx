import React, { useCallback, useEffect, useMemo, useState } from "react";
import { useNavigate } from "react-router-dom";
import {
  getAllCustomers,
  getAllCustomerVehicles,
  getAllVehicles,
} from "@features/admin/api/customerApi";
import { getAllServiceCategories, getAllStandardServices } from "@features/appointments/api/appointmentApi";
import ReceptionCreateAppointmentForm from "@features/workshop/components/ReceptionCreateAppointmentForm";
import { saveReceptionAppointment } from "@features/workshop/utils/receptionAppointmentBooking";
import { useAuth } from "@shared/core/context/AuthContext";
import { useBranch } from "@shared/core/context/BranchContext";
import "./Workshop.css";

const TEXT = {
  loadError: "Kh\u00f4ng th\u1ec3 t\u1ea3i d\u1eef li\u1ec7u t\u1ea1o l\u1ecbch h\u1eb9n.",
  needBranch:
    "Vui l\u00f2ng ch\u1ecdn \u0111\u00fang chi nh\u00e1nh l\u00e0m vi\u1ec7c tr\u01b0\u1edbc khi t\u1ea1o l\u1ecbch h\u1eb9n.",
  createError: "Kh\u00f4ng th\u1ec3 t\u1ea1o l\u1ecbch h\u1eb9n.",
  loadingData: "\u0110ang t\u1ea3i d\u1eef li\u1ec7u \u0111\u1eb7t l\u1ecbch...",
  pageTitle: "T\u1ea1o l\u1ecbch h\u1eb9n m\u1edbi (L\u1ec5 t\u00e2n)",
  back: "<- Quay l\u1ea1i l\u1ecbch h\u1eb9n",
  successTitle: "\u0110\u1eb7t l\u1ecbch th\u00e0nh c\u00f4ng",
  customer: "Kh\u00e1ch h\u00e0ng",
  plate: "Bi\u1ec3n s\u1ed1 xe",
  appointmentType: "Lo\u1ea1i l\u1ecbch",
  groupType: "L\u1ecbch nh\u00f3m",
  receiveAt: "Th\u1eddi gian nh\u1eadn xe",
  services: "D\u1ecbch v\u1ee5 y\u00eau c\u1ea7u",
  nonePreset: "Ch\u01b0a \u0111\u1ecbnh tr\u01b0\u1edbc",
  backToList: "V\u1ec1 danh s\u00e1ch l\u1ecbch h\u1eb9n",
  createAnother: "T\u1ea1o l\u1ecbch h\u1eb9n kh\u00e1c",
  infoTitle: "Th\u00f4ng tin \u0111\u1eb7t l\u1ecbch",
  infoNote: "",
  submit: "X\u00e1c nh\u1eadn \u0111\u1eb7t l\u1ecbch",
};

function parseBranchId(value) {
  const parsed = Number(value);
  return Number.isFinite(parsed) && parsed > 0 ? parsed : 0;
}

function formatAppointmentDate(value) {
  const date = new Date(value || "");
  if (Number.isNaN(date.getTime())) return "--/--/----";
  return date.toLocaleDateString("vi-VN");
}

function formatAppointmentTime(value) {
  const date = new Date(value || "");
  if (Number.isNaN(date.getTime())) return "--:--";
  return date.toLocaleTimeString("vi-VN", { hour: "2-digit", minute: "2-digit" });
}

export default function ReceptionCreateAppointmentPage() {
  const navigate = useNavigate();
  const { selectedBranchId } = useBranch();
  const { user } = useAuth();

  const [customers, setCustomers] = useState([]);
  const [vehicles, setVehicles] = useState([]);
  const [customerVehicles, setCustomerVehicles] = useState([]);
  const [services, setServices] = useState([]);
  const [serviceCategories, setServiceCategories] = useState([]);

  const [loading, setLoading] = useState(false);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState("");
  const [successAppt, setSuccessAppt] = useState(null);
  const [formKey, setFormKey] = useState(0);

  const userBranchId = parseBranchId(user?.branchId ?? user?.BranchId);
  const contextBranchId = parseBranchId(selectedBranchId);
  const effectiveBranchId = userBranchId || contextBranchId;
  const canSubmit = effectiveBranchId > 0;

  const fetchReferenceData = useCallback(async () => {
    setLoading(true);
    setError("");
    try {
      const [customerRes, vehicleRes, customerVehicleRes, serviceRes, serviceCategoryRes] = await Promise.all([
        getAllCustomers(),
        getAllVehicles(),
        getAllCustomerVehicles(),
        getAllStandardServices(),
        getAllServiceCategories(),
      ]);
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
  }, [effectiveBranchId]);

  useEffect(() => {
    fetchReferenceData();
  }, [fetchReferenceData]);

  const handleGoBack = useCallback(() => {
    navigate("/workshop/reception/appointments");
  }, [navigate]);

  const handleCreateAnother = useCallback(() => {
    setSuccessAppt(null);
    setError("");
    setFormKey((prev) => prev + 1);
  }, []);

  const handleSubmit = useCallback(
    async (formData) => {
      if (!canSubmit) {
        setError(TEXT.needBranch);
        return;
      }

      setSaving(true);
      setError("");

      try {
        const summary = await saveReceptionAppointment({
          effectiveBranchId,
          formData,
          customers,
          vehicles,
          customerVehicles,
          services,
        });
        setSuccessAppt(summary);
        await fetchReferenceData();
      } catch (err) {
        setError(err?.message || TEXT.createError);
      } finally {
        setSaving(false);
      }
    },
    [canSubmit, effectiveBranchId, customers, vehicles, customerVehicles, services, fetchReferenceData],
  );

  const successServiceNames = useMemo(
    () =>
      Array.isArray(successAppt?.services)
        ? successAppt.services.map((service) => service?.Name || service?.name).filter(Boolean)
        : [],
    [successAppt],
  );

  return (
    <div className="create-order-page reception-create-appointment-page">
      <div className="page-header-actions reception-create-header">
        <button className="btn-back" type="button" onClick={handleGoBack}>
          {TEXT.back}
        </button>
        <div className="reception-create-header-copy">
          <h2 className="page-title">{TEXT.pageTitle}</h2>
          {TEXT.infoNote ? <p className="reception-create-header-note">{TEXT.infoNote}</p> : null}
        </div>
      </div>

      {!canSubmit && <div className="validation-summary">{TEXT.needBranch}</div>}
      {error && <div className="validation-summary">{error}</div>}

      {loading ? (
        <div className="workshop-table-card">
          <div className="loading-state">
            <div className="spinner" />
            <p>{TEXT.loadingData}</p>
          </div>
        </div>
      ) : successAppt ? (
        <section className="form-section reception-create-appointment-success">
          <h3 className="section-title">{TEXT.successTitle}</h3>
          <div className="appointment-success-summary-card">
            <p>
              <strong>{`${TEXT.customer}:`}</strong> {successAppt.customerName} - {successAppt.customerPhone}
            </p>
            <p>
              <strong>{`${TEXT.plate}:`}</strong>{" "}
              <span className="appointment-success-plate">{successAppt.vehicleLicensePlate}</span>
            </p>
            {Number(successAppt.groupVehicleCount || 0) > 1 && (
              <p>
                <strong>{`${TEXT.appointmentType}:`}</strong>{" "}
                {`${TEXT.groupType} (${successAppt.groupVehicleCount} xe)`}
              </p>
            )}
            <p>
              <strong>{`${TEXT.receiveAt}:`}</strong>{" "}
              {`${formatAppointmentTime(successAppt.startTime)} ng\u00e0y ${formatAppointmentDate(successAppt.startTime)}`}
            </p>
            <p>
              <strong>{`${TEXT.services}:`}</strong>{" "}
              {successServiceNames.length > 0 ? successServiceNames.join(", ") : TEXT.nonePreset}
            </p>
          </div>
          <div className="form-footer">
            <button type="button" className="btn-secondary-outline" onClick={handleGoBack}>
              {TEXT.backToList}
            </button>
            <button type="button" className="btn-primary-large" onClick={handleCreateAnother}>
              {TEXT.createAnother}
            </button>
          </div>
        </section>
      ) : (
        <section className="form-section reception-create-appointment-form">
          <ReceptionCreateAppointmentForm
            key={formKey}
            customers={customers}
            vehicles={vehicles}
            customerVehicles={customerVehicles}
            serviceCategories={serviceCategories}
            services={services}
            branchId={effectiveBranchId}
            onSubmit={handleSubmit}
            onCancel={handleGoBack}
            loading={saving}
            submitLabel={TEXT.submit}
          />
        </section>
      )}
    </div>
  );
}
