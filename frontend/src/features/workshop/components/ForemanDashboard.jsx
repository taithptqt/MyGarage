import React from "react";
import { useNavigate, useParams } from "react-router-dom";
import DispatchOpsDashboard from "./DispatchOpsDashboard";

function toPositiveNumber(value) {
  const parsed = Number(value);
  return Number.isFinite(parsed) && parsed > 0 ? parsed : null;
}

export default function ForemanDashboard() {
  const navigate = useNavigate();
  const { orderId } = useParams();

  const isAssignmentPage = typeof orderId === "string";
  const assignmentServiceOrderId = toPositiveNumber(orderId);

  return (
    <DispatchOpsDashboard
      mode="manager"
      pageMode={isAssignmentPage ? "assignment" : "board"}
      assignmentServiceOrderId={assignmentServiceOrderId}
      onOpenAssignment={(serviceOrderId) => navigate(`/workshop/foreman/dispatch/${serviceOrderId}`)}
      onBack={() => navigate("/workshop/foreman")}
    />
  );
}
