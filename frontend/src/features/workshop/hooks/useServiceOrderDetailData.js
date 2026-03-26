import { useCallback, useEffect, useMemo, useState } from "react";
import { serviceOrderApi } from "../api/serviceOrderApi";
import {
  calculateOrderTotal,
  parseCustomerNotes,
} from "../utils/serviceOrderDetailUtils";

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
      setOrder(oRes || null);
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

