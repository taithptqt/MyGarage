import React from "react";
import { useLocation, useNavigate, useParams } from "react-router-dom";
import DispatchOpsDashboard from "./DispatchOpsDashboard";

function toPositiveNumber(value) {
  const parsed = Number(value);
  return Number.isFinite(parsed) && parsed > 0 ? parsed : null;
}

export default function ForemanDashboard() {
  const navigate = useNavigate();
  const location = useLocation();
  const { orderId } = useParams();
  const searchParams = new URLSearchParams(location.search || "");

  const isAssignmentPage = typeof orderId === "string";
  const assignmentServiceOrderId = toPositiveNumber(orderId);
  const assignmentFocus = searchParams.get("focus") || "";
  const fromHistoryContext = searchParams.get("from") === "history" || searchParams.has("from-history");

  const handleOpenAssignment = (serviceOrderId, options = {}) => {
    const params = new URLSearchParams();
    const focus = String(options?.focus || "").trim();
    const from = String(options?.from || "").trim().toLowerCase();
    if (focus) params.set("focus", focus);
    if (from === "history" || (fromHistoryContext && !from)) params.set("from", "history");
    const query = params.toString();
    navigate(`/workshop/foreman/dispatch/${serviceOrderId}${query ? `?${query}` : ""}`);
  };

  return (
    <DispatchOpsDashboard
      mode="manager"
      pageMode={isAssignmentPage ? "assignment" : "board"}
      assignmentServiceOrderId={assignmentServiceOrderId}
      assignmentFocus={assignmentFocus}
      onOpenAssignment={handleOpenAssignment}
      onOpenQuote={(serviceOrderId) =>
        navigate(
          `/workshop/foreman/orders/${serviceOrderId}/quote${fromHistoryContext ? "?from=history" : ""}`
        )
      }
      onBack={() => navigate(fromHistoryContext ? "/workshop/foreman/history" : "/workshop/foreman")}
    />
  );
}
