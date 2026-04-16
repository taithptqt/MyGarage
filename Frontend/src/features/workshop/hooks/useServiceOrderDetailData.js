import { useCallback, useEffect, useMemo, useState } from "react";
import { serviceOrderApi } from "@features/workshop/api/serviceOrderApi";
import { getCustomerById, getVehicleById } from "@features/admin/api/customerApi";
import {
  calculateOrderTotal,
  parseCustomerNotes,
} from "@features/workshop/utils/serviceOrderDetailUtils";

function toPositiveNumber(value) {
  const parsed = Number(value);
  return Number.isFinite(parsed) && parsed > 0 ? parsed : null;
}

function toText(value) {
  return String(value ?? "").trim();
}

export function useServiceOrderDetailData(orderId) {
  const [order, setOrder] = useState(null);
  const [details, setDetails] = useState([]);
  const [diagnoses, setDiagnoses] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");

  const refresh = useCallback(async () => {
    const id = Number(orderId);
    if (!id) {
      setError("Mã lệnh sửa chữa không hợp lệ.");
      setLoading(false);
      return;
    }

    setLoading(true);
    setError("");
    try {
      const [oRes, dRes, diagRes] = await Promise.all([
        serviceOrderApi.getById(id),
        serviceOrderApi.getDetails(id),
        serviceOrderApi.getDiagnoses(id),
      ]);

      const customerId = toPositiveNumber(oRes?.customerId ?? oRes?.CustomerId);
      const vehicleId = toPositiveNumber(oRes?.vehicleId ?? oRes?.VehicleId);
      const [customerRes, vehicleRes] = await Promise.all([
        customerId ? getCustomerById(customerId).catch(() => null) : Promise.resolve(null),
        vehicleId ? getVehicleById(vehicleId).catch(() => null) : Promise.resolve(null),
      ]);

      const customerName = toText(
        oRes?.customerName ??
        oRes?.CustomerName ??
        customerRes?.fullName ??
        customerRes?.FullName ??
        oRes?.guestName ??
        oRes?.GuestName
      );
      const vehicleLicensePlate = toText(
        oRes?.vehicleLicensePlate ??
        oRes?.VehicleLicensePlate ??
        vehicleRes?.licensePlate ??
        vehicleRes?.LicensePlate ??
        oRes?.guestVehicleInfo ??
        oRes?.GuestVehicleInfo
      );

      setOrder(oRes ? { ...oRes, customerName, vehicleLicensePlate } : null);
      setDetails(dRes || []);
      setDiagnoses(diagRes || []);
    } catch (err) {
      setError(err?.message || "Không thể tải dữ liệu chi tiết lệnh sửa chữa.");
    } finally {
      setLoading(false);
    }
  }, [orderId]);

  useEffect(() => {
    refresh();
  }, [refresh]);

  const parsedNotes = useMemo(
    () => parseCustomerNotes(order?.customerNotes),
    [order?.customerNotes]
  );
  const totalAmount = useMemo(() => calculateOrderTotal(details), [details]);

  return {
    order,
    details,
    diagnoses,
    parsedNotes,
    totalAmount,
    loading,
    error,
    refresh,
  };
}




