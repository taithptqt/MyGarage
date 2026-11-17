import React from "react";
import { FLEET_SERVICE_PRICE_LABEL, SERVICE_PRICE_DISCLAIMER, SERVICE_PRICE_LABEL } from "@shared/constants/servicePricing";
import { formatCurrency, formatDateTimeValue, hasPlanCompletionWarning } from "./createServiceOrderHelpers";

export default function CreateServiceOrderPlanSummary({
  totalPrice,
  estimatedHandover,
  suggestedFleetCompletionIso,
  isInsuranceOrderEnabled,
  insuranceDraftBreakdown,
  selectedVehicleCount,
  planSummaries,
  insuranceSummaryByVehicleKey,
}) {
  return (
    <>
              <div className="reception-summary-inline" role="status" aria-live="polite">
                <div className="reception-summary-inline-row">
                  <span>{FLEET_SERVICE_PRICE_LABEL}</span>
                  <strong>{formatCurrency(totalPrice)}</strong>
                </div>
                <p className="reception-plan-summary-warning reception-summary-inline-warning">{`* ${SERVICE_PRICE_DISCLAIMER}`}</p>
                <div className="reception-summary-inline-row">
                  <span>Thời gian dự kiến hoàn tất toàn bộ xe</span>
                  <strong>{formatDateTimeValue(estimatedHandover || suggestedFleetCompletionIso)}</strong>
                </div>
                {isInsuranceOrderEnabled ? (
                  <>
                    <div className="reception-summary-inline-row">
                      <span>Bảo hiểm dự kiến trả</span>
                      <strong>{formatCurrency(insuranceDraftBreakdown.insuranceRequestedAmount)}</strong>
                    </div>
                    <div className="reception-summary-inline-row">
                      <span>Khách dự kiến trả</span>
                      <strong>{formatCurrency(insuranceDraftBreakdown.customerPayAmount)}</strong>
                    </div>
                  </>
                ) : null}
                <div className="reception-summary-inline-row">
                  <span>Số xe</span>
                  <strong>
                    {selectedVehicleCount}
                    {selectedVehicleCount > 1 ? " (lịch nhóm)" : " (lịch thường)"}
                  </strong>
                </div>
              </div>

              {planSummaries.length > 0 ? (
                <div className="reception-plan-summary-grid">
                  {planSummaries.map((plan) => {
                    const insuranceDetails = insuranceSummaryByVehicleKey.get(plan.vehicleKey) || null;
                    const insuranceSummary = insuranceDetails?.summary || null;

                    return (
                    <article key={`plan-summary-${plan.vehicleKey}`} className="reception-plan-summary-card">
                      <div className="reception-plan-summary-head">
                        <h5 className="reception-plan-summary-title">{plan.vehicleLabel}</h5>
                        <span className="reception-plan-summary-badge">
                          {`${plan.categoryIds.length} loại - ${plan.serviceIds.length} dịch vụ`}
                        </span>
                      </div>

                      <div className="reception-plan-summary-block">
                        <p className="reception-plan-summary-block-label">DỊCH VỤ ĐÃ CHỌN</p>
                        {Array.isArray(plan.serviceGroupsByCategory) && plan.serviceGroupsByCategory.length > 0 ? (
                          <ul className="reception-plan-summary-list">
                            {plan.serviceGroupsByCategory.map((group) => (
                              <li key={`${plan.vehicleKey}-${group.categoryName}`} className="reception-plan-summary-list-item">
                                <span className="reception-plan-summary-list-group">{group.categoryName}</span>
                                <ul className="reception-plan-summary-sublist">
                                  {(group.serviceNames || []).map((serviceName) => (
                                    <li
                                      key={`${plan.vehicleKey}-${group.categoryName}-${serviceName}`}
                                      className="reception-plan-summary-sublist-item"
                                    >
                                      {serviceName}
                                    </li>
                                  ))}
                                </ul>
                              </li>
                            ))}
                          </ul>
                        ) : (
                          <p className="reception-plan-summary-text">Chưa có dịch vụ</p>
                        )}
                      </div>

                      {insuranceSummary ? (
                        <div className="reception-plan-summary-block">
                          <p className="reception-plan-summary-block-label">BẢO HIỂM</p>
                          <div className="reception-plan-summary-time-row">
                            <span>Công ty bảo hiểm:</span>
                            <strong>{insuranceSummary.companyName}</strong>
                          </div>
                          <div className="reception-plan-summary-time-row">
                            <span>Mức miễn thường:</span>
                            <strong>{insuranceSummary.deductibleLabel}</strong>
                          </div>
                          <div className="reception-plan-summary-time-row">
                            <span>Hình thức xử lý:</span>
                            <strong>{insuranceSummary.claimModeLabel}</strong>
                          </div>
                          <div className="reception-plan-summary-time-row">
                            <span>Hồ sơ bảo hiểm:</span>
                            <strong>{insuranceSummary.assetSummaryLabel}</strong>
                          </div>
                          <p className="reception-plan-summary-text">
                            {`Ghi chú bảo hiểm: ${insuranceSummary.intakeNote}`}
                          </p>
                        </div>
                      ) : null}

                      <div className="reception-plan-summary-block">
                        <p className="reception-plan-summary-block-label">KHUNG GIỜ</p>
                        <div className="reception-plan-summary-time-row">
                          <span>Giờ bắt đầu:</span>
                          <strong>{plan.startTimeLabel}</strong>
                        </div>
                        <div className="reception-plan-summary-time-row">
                          <span>Giờ kết thúc:</span>
                          <strong>{plan.endTimeLabel}</strong>
                        </div>
                        <div className="reception-plan-summary-time-row">
                          <span>Tổng thời lượng:</span>
                          <strong>{`${plan.serviceMinutes} phút`}</strong>
                        </div>
                      </div>

                      {hasPlanCompletionWarning(plan) ? (
                        <p className="reception-plan-summary-warning">
                          {`* ${plan.completionWarningText}`}
                        </p>
                      ) : null}

                      <div className="reception-plan-summary-footer">
                        <span>{SERVICE_PRICE_LABEL}</span>
                        <strong>{formatCurrency(plan.totalPrice)}</strong>
                      </div>
                    </article>
                  )})}
                </div>
              ) : null}
    </>
  );
}
