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

  const isAssignmentPage = typeof orderId === "string";
  const assignmentServiceOrderId = toPositiveNumber(orderId);
  const assignmentFocus = new URLSearchParams(location.search).get("focus") || "";

  const handleOpenAssignment = (serviceOrderId, options = {}) => {
    const params = new URLSearchParams();
    const focus = String(options?.focus || "").trim();
    if (focus) params.set("focus", focus);
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
      onOpenQuote={(serviceOrderId) => navigate(`/workshop/foreman/orders/${serviceOrderId}/quote`)}
      onBack={() => navigate("/workshop/foreman")}
    />
  );
}
