import React from "react";

function formatCurrency(value) {
  return `${Number(value || 0).toLocaleString("vi-VN")}đ`;
}

export default function InsuranceBillingOverview({
  summary,
  title = "Tổng hợp bảo hiểm",
  companyNames = [],
  note = "",
}) {
  if (!summary?.hasInsurance) return null;

  const companyText = Array.isArray(companyNames) && companyNames.length > 0
    ? companyNames.join(", ")
    : "Chưa xác định công ty bảo hiểm";

  return (
    <section className="so-detail-section workshop-insurance-overview">
      <h3 className="so-detail-section-title">
        <span>{title}</span>
      </h3>

      <div className="workshop-insurance-company-info">
        <span>Công ty bảo hiểm: </span>
        <strong>{companyText}</strong>
      </div>

      <div className="workshop-insurance-summary-grid">
        <article className="workshop-insurance-summary-card">
          <span>Tổng giá trị</span>
          <strong>{formatCurrency(summary.totalAmount)}</strong>
        </article>
        <article className="workshop-insurance-summary-card">
          <span>Bảo hiểm yêu cầu</span>
          <strong>{formatCurrency(summary.insuranceRequestedAmount)}</strong>
        </article>
        <article className="workshop-insurance-summary-card">
          <span>Bảo hiểm đã duyệt</span>
          <strong>{formatCurrency(summary.insuranceApprovedAmount)}</strong>
        </article>
        <article className="workshop-insurance-summary-card">
          <span>Miễn thường</span>
          <strong>{formatCurrency(summary.insuranceDeductibleAmount)}</strong>
        </article>
        <article className="workshop-insurance-summary-card">
          <span>Garage hỗ trợ</span>
          <strong>{formatCurrency(summary.garageAbsorbedAmount)}</strong>
        </article>
        <article className="workshop-insurance-summary-card">
          <span>Khách cần trả</span>
          <strong>{formatCurrency(summary.customerPayAmount)}</strong>
        </article>
      </div>

      {summary.pendingInsuranceItemCount > 0 ? (
        <p className="workshop-insurance-helper">
          Còn {summary.pendingInsuranceItemCount} hạng mục đang ở trạng thái chờ gửi hoặc chờ duyệt bảo hiểm.
        </p>
      ) : null}
      {note ? <p className="workshop-insurance-helper">{note}</p> : null}
    </section>
  );
}
