import React from "react";
import "./TechnicianTaskCard.css";

const TechnicianTaskCard = ({
  licensePlate,
  carModel,
  serviceOrderId,
  bayCode,
  taskTypeLabel,
  status = "PENDING", // PENDING | IN_PROGRESS | COMPLETED
  waitingMinutes = 0,
  processingMinutes = 0,
  completedAt = null,
  onStart,
  onComplete,
  onOpen,
  isActionLoading = false,
}) => {
  const isPending = status === "PENDING" || status === "NEW";
  const isInProgress = status === "IN_PROGRESS";
  const isCompleted = status === "COMPLETED";

  const formattedSO = serviceOrderId?.length > 8 ? `...${serviceOrderId.slice(-8)}` : serviceOrderId;

  const handleAction = (event) => {
    event.stopPropagation();
    if (isPending && onStart) onStart();
    if (isInProgress && onComplete) onComplete();
  };

  const handleCardClick = () => {
    if (!isPending && onOpen) onOpen();
  };

  const statusClass = status === "NEW" || status === "PENDING" ? "pending" : String(status).toLowerCase();
  const typeLabel = String(taskTypeLabel || "").toLowerCase();
  const isDiagnosis = typeLabel.includes("chẩn") || typeLabel.includes("chan");

  return (
    <div className={`card-container ${statusClass} ${!isPending ? "is-clickable" : ""}`} onClick={handleCardClick}>
      <div className="card-header">
        <div className="header-left">
          <div className="header-plate">{licensePlate || "--"}</div>
          <div className={`job-type-badge ${isDiagnosis ? "diag" : "repair"}`}>{taskTypeLabel}</div>
        </div>

        <div className="header-right">
          <div className="bay-location-badge">{bayCode || "N/A"}</div>
        </div>
      </div>

      <div className="card-body">
        <div className="body-car-model">{carModel || "--"}</div>
        <div className="body-so-code">SO: {formattedSO || "--"}</div>
      </div>

      <div className="card-footer">
        <div className="footer-time">
          {isPending && (
            <span>
              Đã chờ: <strong>{waitingMinutes}</strong> phút
            </span>
          )}
          {isInProgress && (
            <span>
              Đang xử lý: <strong>{processingMinutes}</strong> phút
            </span>
          )}
          {isCompleted && (
            <span className="completion-text">
              <span className="check-icon">✓</span> Hoàn thành{" "}
              {completedAt
                ? new Date(completedAt).toLocaleTimeString("vi-VN", { hour: "2-digit", minute: "2-digit" })
                : ""}
            </span>
          )}
        </div>

        <div className="footer-actions">
          {isPending && (
            <button className="action-btn btn-accept" onClick={handleAction} disabled={isActionLoading}>
              {isActionLoading ? "..." : "NHẬN VIỆC"}
            </button>
          )}
          {isInProgress && (
            <button className="action-btn btn-complete" onClick={handleAction} disabled={isActionLoading}>
              {isActionLoading ? "..." : "HOÀN THÀNH"}
            </button>
          )}
        </div>
      </div>
    </div>
  );
};

export default TechnicianTaskCard;
