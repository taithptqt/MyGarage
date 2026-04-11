import React, { useCallback, useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";
import {
  getAllCustomers,
  getAllCustomerVehicles,
  getAllVehicles,
} from "@features/admin/api/customerApi";
import { getAllPackages, getAllServiceCategories, getAllStandardServices } from "@features/appointments/api/appointmentApi";
import ReceptionCreateAppointmentForm from "@features/workshop/components/ReceptionCreateAppointmentForm";
import { saveReceptionAppointment } from "@features/workshop/utils/receptionAppointmentBooking";
import { mergeBookableServicesAndPackages } from "@features/appointments/utils/bookingPackages";
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
  successMessage: "L\u1ecbch h\u1eb9n \u0111\u00e3 \u0111\u01b0\u1ee3c t\u1ea1o th\u00e0nh c\u00f4ng.",
  infoNote: "",
  submit: "X\u00e1c nh\u1eadn \u0111\u1eb7t l\u1ecbch",
};

function parseBranchId(value) {
  const parsed = Number(value);
  return Number.isFinite(parsed) && parsed > 0 ? parsed : 0;
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

  const userBranchId = parseBranchId(user?.branchId ?? user?.BranchId);
  const contextBranchId = parseBranchId(selectedBranchId);
  const effectiveBranchId = contextBranchId || userBranchId;
  const canSubmit = effectiveBranchId > 0;

  const fetchReferenceData = useCallback(async () => {
    setLoading(true);
    setError("");
    try {
      const [customerRes, vehicleRes, customerVehicleRes, serviceRes, packageRes, serviceCategoryRes] = await Promise.all([
        getAllCustomers(),
        getAllVehicles(),
        getAllCustomerVehicles(),
        getAllStandardServices(),
        getAllPackages().catch(() => []),
        getAllServiceCategories(),
      ]);
      setCustomers(Array.isArray(customerRes) ? customerRes : []);
      setVehicles(Array.isArray(vehicleRes) ? vehicleRes : []);
      setCustomerVehicles(Array.isArray(customerVehicleRes) ? customerVehicleRes : []);
      setServices(mergeBookableServicesAndPackages(Array.isArray(serviceRes) ? serviceRes : [], Array.isArray(packageRes) ? packageRes : []));
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

  useEffect(() => {
    if (!successAppt) return undefined;
    const timeoutId = setTimeout(() => {
      navigate("/workshop/reception/appointments", { replace: true });
    }, 2000);
    return () => clearTimeout(timeoutId);
  }, [successAppt, navigate]);

  useEffect(() => {
    if (!error && canSubmit) return;

    if (typeof window !== "undefined" && typeof window.scrollTo === "function") {
      window.scrollTo({ top: 0, behavior: "smooth" });
    }
  }, [error, canSubmit]);

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
      } catch (err) {
        setError(err?.message || TEXT.createError);
      } finally {
        setSaving(false);
      }
    },
    [canSubmit, effectiveBranchId, customers, vehicles, customerVehicles, services],
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
      ) : (
        <section className="form-section reception-create-appointment-form">
          <ReceptionCreateAppointmentForm
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
