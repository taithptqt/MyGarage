import React, { useCallback, useEffect, useMemo, useState } from "react";
import { useLocation, useNavigate, useParams } from "react-router-dom";
import { useAuth } from "@shared/core/context/AuthContext";
import { serviceOrderApi } from "@features/workshop/api/serviceOrderApi";
import { useServiceOrderDetailData } from "@features/workshop/hooks/useServiceOrderDetailData";
import { formatOrderDateTime } from "@features/workshop/utils/serviceOrderDetailUtils";

function resolveBackConfig(pathname) {
  const normalizedPath = String(pathname || "").toLowerCase();
  if (normalizedPath.includes("/workshop/manager/")) {
    return { path: "/workshop/manager", label: "Quay lại quản đốc" };
  }
  if (normalizedPath.includes("/workshop/foreman/")) {
    return { path: "/workshop/foreman", label: "Quay lại quản đốc" };
  }
  if (normalizedPath.includes("/workshop/team-leader/")) {
    return { path: "/workshop/team-leader", label: "Quay lại tổ trưởng" };
  }

  return { path: "/workshop/reception", label: "Quay lại lễ tân" };
}

function normalizeMinutes(value) {
  const parsed = Number(value);
  if (!Number.isFinite(parsed)) return 0;
  return Math.max(0, Math.min(24 * 60, Math.round(parsed)));
}

function addMinutes(baseDate, minutes) {
  if (!baseDate) return null;
  const parsed = new Date(baseDate);
  if (Number.isNaN(parsed.getTime())) return null;
  parsed.setMinutes(parsed.getMinutes() + normalizeMinutes(minutes));
  return parsed;
}

function formatMoney(value) {
  return Number(value || 0).toLocaleString("vi-VN");
}

function formatApprovalState(state) {
  const normalized = String(state || "").trim().toUpperCase();
  if (normalized === "APPROVED") return "Đã duyệt";
  if (normalized === "REJECTED") return "Khách từ chối";
  return "Chờ khách duyệt";
}

function resolveCurrentStaffId(user) {
  const candidates = [
    user?.id,
    user?.staffId,
    user?.StaffId,
    user?.Id,
    user?.profile?.id,
    user?.profile?.staffId,
  ];
  for (const candidate of candidates) {
    const parsed = Number(candidate);
    if (Number.isFinite(parsed) && parsed > 0) return parsed;
  }
  return undefined;
}

export default function ServiceOrderQuotePage() {
  const navigate = useNavigate();
  const location = useLocation();
  const { orderId } = useParams();
  const { user } = useAuth();
  const backConfig = useMemo(() => resolveBackConfig(location.pathname), [location.pathname]);

  const {
    order,
    details,
    diagnoses,
    totalAmount,
    loading,
    error,
    refresh,
  } = useServiceOrderDetailData(orderId);

  const latestDiagnosis = diagnoses[0] || null;
  const staffId = useMemo(() => resolveCurrentStaffId(user), [user]);
  const numericOrderId = Number(orderId);

  const [handoverSnapshot, setHandoverSnapshot] = useState(null);
  const [loadingHandoverSnapshot, setLoadingHandoverSnapshot] = useState(false);
  const [handoverError, setHandoverError] = useState("");

  const [additionalRepairMinutes, setAdditionalRepairMinutes] = useState(30);
  const [quoteChannel, setQuoteChannel] = useState("APP");
  const [quoteSummary, setQuoteSummary] = useState("");
  const [quoteAttachmentUrl, setQuoteAttachmentUrl] = useState("");
  const [decisionChannel, setDecisionChannel] = useState("APP");
  const [decisionNote, setDecisionNote] = useState("");
  const [submittingSnapshot, setSubmittingSnapshot] = useState(false);
  const [submittingDecision, setSubmittingDecision] = useState(false);

  const loadLatestHandoverSnapshot = useCallback(async () => {
    if (!numericOrderId) return;
    setLoadingHandoverSnapshot(true);
    setHandoverError("");
    try {
      const response = await serviceOrderApi.getLatestDiagnosisHandoverSnapshot(numericOrderId);
      setHandoverSnapshot(response || null);
      if (response?.quoteSummary) {
        setQuoteSummary(response.quoteSummary);
      }
      if (response?.quoteChannel) {
        setQuoteChannel(String(response.quoteChannel).toUpperCase());
      }
      if (response?.quoteAttachmentUrl) {
        setQuoteAttachmentUrl(response.quoteAttachmentUrl);
      }
    } catch (err) {
      const message = String(err?.message || "");
      if (message.toLowerCase().includes("404")) {
        setHandoverSnapshot(null);
      } else {
        setHandoverError(message || "Không thể tải snapshot T2.");
      }
    } finally {
      setLoadingHandoverSnapshot(false);
    }
  }, [numericOrderId]);

  useEffect(() => {
    loadLatestHandoverSnapshot();
  }, [loadLatestHandoverSnapshot]);

  const fallbackT1 = order?.estimatedHandover || order?.promisedHandoverTime || order?.checkInTime || null;
  const effectiveT1 = handoverSnapshot?.t1EstimatedHandover || fallbackT1;
  const draftT2 = useMemo(
    () => addMinutes(effectiveT1, additionalRepairMinutes),
    [effectiveT1, additionalRepairMinutes]
  );
  const hasPendingCustomerDecision =
    String(handoverSnapshot?.approvalState || "").toUpperCase() === "PENDING";

  const handleCreateSnapshot = useCallback(async () => {
    if (!numericOrderId) {
      setHandoverError("Mã lệnh sửa chữa không hợp lệ.");
      return;
    }

    const normalizedAdditionalMinutes = normalizeMinutes(additionalRepairMinutes);
    setSubmittingSnapshot(true);
    setHandoverError("");
    try {
      const response = await serviceOrderApi.createDiagnosisHandoverSnapshot(numericOrderId, {
        additionalRepairMinutes: normalizedAdditionalMinutes,
        quoteChannel,
        quoteSummary,
        quoteAttachmentUrl: quoteAttachmentUrl || undefined,
        createdByStaffId: staffId,
      });
      setHandoverSnapshot(response || null);
      await refresh();
    } catch (err) {
      setHandoverError(err?.message || "Không thể tạo snapshot báo giá T2.");
    } finally {
      setSubmittingSnapshot(false);
    }
  }, [
    additionalRepairMinutes,
    numericOrderId,
    quoteAttachmentUrl,
    quoteChannel,
    quoteSummary,
    refresh,
    staffId,
  ]);

  const handleCustomerDecision = useCallback(async (approved) => {
    if (!numericOrderId || !handoverSnapshot?.snapshotId) {
      return;
    }

    setSubmittingDecision(true);
    setHandoverError("");
    try {
      const response = await serviceOrderApi.decideDiagnosisHandoverSnapshot(
        numericOrderId,
        handoverSnapshot.snapshotId,
        {
          approved,
          decisionChannel,
          decisionNote: decisionNote || undefined,
          decidedByStaffId: staffId,
        }
      );
      setHandoverSnapshot(response || null);
      await refresh();
    } catch (err) {
      setHandoverError(err?.message || "Không thể ghi nhận quyết định của khách.");
    } finally {
      setSubmittingDecision(false);
    }
  }, [decisionChannel, decisionNote, handoverSnapshot?.snapshotId, numericOrderId, refresh, staffId]);

  return (
    <div className="workshop-table-card reception-quote-page">
      <div className="reception-quote-head">
        <div>
          <h2 className="reception-quote-title">Báo giá & xác nhận khách (T2)</h2>
          <p className="reception-quote-subtitle">
            Lệnh <strong>{order?.code || `#${orderId}`}</strong> · Snapshot báo giá v1 sau chuẩn đoán
          </p>
        </div>
        <button type="button" className="btn-back" onClick={() => navigate(backConfig.path)}>
          {backConfig.label}
        </button>
      </div>

      {loading ? (
        <div className="loading-state">
          <div className="spinner" />
          <p>Đang tải dữ liệu báo giá...</p>
        </div>
      ) : error ? (
        <div className="validation-summary">{error}</div>
      ) : (
        <div className="reception-quote-layout">
          <section className="reception-quote-summary-card">
            <article>
              <span>Khách hàng</span>
              <strong>{order?.customerName || "-"}</strong>
            </article>
            <article>
              <span>Biển số xe</span>
              <strong>{order?.vehicleLicensePlate || "-"}</strong>
            </article>
            <article>
              <span>Trạng thái lệnh</span>
              <strong>{order?.status || "-"}</strong>
            </article>
            <article>
              <span>Dự kiến hiện tại</span>
              <strong>{formatOrderDateTime(order?.estimatedHandover)}</strong>
            </article>
          </section>

          <section className="reception-quote-timeline-card">
            <div className="reception-quote-timeline-row">
              <p>T1 (sau check-in)</p>
              <strong>{formatOrderDateTime(effectiveT1)}</strong>
            </div>
            <div className="reception-quote-timeline-row">
              <p>T2 (thêm sửa chữa)</p>
              <strong>
                {handoverSnapshot?.t2EstimatedHandover
                  ? formatOrderDateTime(handoverSnapshot.t2EstimatedHandover)
                  : formatOrderDateTime(draftT2)}
              </strong>
            </div>
            <div className="reception-quote-timeline-row">
              <p>Trạng thái duyệt khách</p>
              <strong>{formatApprovalState(handoverSnapshot?.approvalState)}</strong>
            </div>
            {handoverSnapshot?.customerDecisionAt ? (
              <div className="reception-quote-timeline-row">
                <p>Thời điểm xác nhận</p>
                <strong>{formatOrderDateTime(handoverSnapshot.customerDecisionAt)}</strong>
              </div>
            ) : null}
          </section>

          <section className="reception-quote-action-card">
            <h3>Snapshot báo giá v1 (T2)</h3>
            <div className="reception-quote-form-grid">
              <label>
                <span>Phút sửa thêm</span>
                <input
                  type="number"
                  min={0}
                  max={24 * 60}
                  value={additionalRepairMinutes}
                  onChange={(event) => setAdditionalRepairMinutes(event.target.value)}
                  disabled={submittingSnapshot}
                />
              </label>
              <label>
                <span>Kênh gửi báo giá</span>
                <select
                  value={quoteChannel}
                  onChange={(event) => setQuoteChannel(String(event.target.value || "APP").toUpperCase())}
                  disabled={submittingSnapshot}
                >
                  <option value="APP">Ứng dụng</option>
                  <option value="ZALO">Zalo</option>
                  <option value="PHONE">Điện thoại</option>
                  <option value="DIRECT">Trực tiếp tại quầy</option>
                </select>
              </label>
              <label className="full-row">
                <span>Tóm tắt báo giá v1</span>
                <textarea
                  rows={3}
                  value={quoteSummary}
                  onChange={(event) => setQuoteSummary(event.target.value)}
                  placeholder="Mô tả ngắn hạng mục sửa thêm và chi phí dự kiến"
                  disabled={submittingSnapshot}
                />
              </label>
              <label className="full-row">
                <span>Link ảnh lỗi (tuỳ chọn)</span>
                <input
                  type="text"
                  value={quoteAttachmentUrl}
                  onChange={(event) => setQuoteAttachmentUrl(event.target.value)}
                  placeholder="https://..."
                  disabled={submittingSnapshot}
                />
              </label>
            </div>
            <div className="reception-quote-action-row">
              <button
                type="button"
                className="btn-primary"
                onClick={handleCreateSnapshot}
                disabled={submittingSnapshot}
              >
                {submittingSnapshot ? "Đang gửi snapshot..." : "Gửi báo giá v1 (T2)"}
              </button>
              <button
                type="button"
                className="toolbar-action-btn toolbar-action-outline"
                onClick={loadLatestHandoverSnapshot}
                disabled={loadingHandoverSnapshot || submittingSnapshot}
              >
                {loadingHandoverSnapshot ? "Đang tải..." : "Làm mới snapshot"}
              </button>
            </div>
            {handoverError ? <p className="reception-quote-error">{handoverError}</p> : null}
          </section>

          {handoverSnapshot ? (
            <section className="reception-quote-action-card">
              <h3>Quyết định của khách</h3>
              <div className="reception-quote-form-grid">
                <label>
                  <span>Kênh xác nhận</span>
                  <select
                    value={decisionChannel}
                    onChange={(event) => setDecisionChannel(String(event.target.value || "APP").toUpperCase())}
                    disabled={submittingDecision || !hasPendingCustomerDecision}
                  >
                    <option value="APP">Ứng dụng</option>
                    <option value="ZALO">Zalo</option>
                    <option value="PHONE">Điện thoại</option>
                    <option value="DIRECT">Trực tiếp tại quầy</option>
                  </select>
                </label>
                <label className="full-row">
                  <span>Ghi chú quyết định</span>
                  <textarea
                    rows={3}
                    value={decisionNote}
                    onChange={(event) => setDecisionNote(event.target.value)}
                    placeholder="Ví dụ: Khách duyệt qua app lúc 10:35"
                    disabled={submittingDecision || !hasPendingCustomerDecision}
                  />
                </label>
              </div>

              <div className="reception-quote-action-row">
                <button
                  type="button"
                  className="btn-success"
                  disabled={submittingDecision || !hasPendingCustomerDecision}
                  onClick={() => handleCustomerDecision(true)}
                >
                  {submittingDecision ? "Đang xử lý..." : "Khách duyệt"}
                </button>
                <button
                  type="button"
                  className="btn-danger"
                  disabled={submittingDecision || !hasPendingCustomerDecision}
                  onClick={() => handleCustomerDecision(false)}
                >
                  {submittingDecision ? "Đang xử lý..." : "Khách từ chối"}
                </button>
              </div>
              {!hasPendingCustomerDecision ? (
                <p className="reception-quote-note">
                  Snapshot đã có quyết định: <strong>{formatApprovalState(handoverSnapshot?.approvalState)}</strong>.
                </p>
              ) : null}
            </section>
          ) : null}

          <section className="reception-quote-action-card">
            <h3>Tóm tắt chuẩn đoán</h3>
            <p className="reception-quote-note">
              {latestDiagnosis?.diagnosisDetails || "Chưa có thông tin chuẩn đoán chi tiết."}
            </p>
          </section>

          <section className="reception-quote-table-card">
            <div className="reception-quote-table-head">
              <h3>Chi tiết báo giá (view-only)</h3>
              <strong>{formatMoney(totalAmount)} đ</strong>
            </div>
            {details.length === 0 ? (
              <p className="reception-quote-note">Chưa có hạng mục báo giá.</p>
            ) : (
              <div className="workshop-table-card">
                <table className="admin-table">
                  <thead>
                    <tr>
                      <th>Nội dung</th>
                      <th>SL</th>
                      <th>Đơn giá</th>
                      <th>Thành tiền</th>
                    </tr>
                  </thead>
                  <tbody>
                    {details.map((item, index) => {
                      const quantity = Number(item.quantity || 1);
                      const unitPrice = Number(item.unitPrice || item.unitPriceAtTime || 0);
                      const amount = Number(item.finalAmount || quantity * unitPrice);
                      return (
                        <tr key={item.id || index}>
                          <td>{item.itemName || "Dịch vụ/Phụ tùng"}</td>
                          <td>{quantity}</td>
                          <td>{formatMoney(unitPrice)}</td>
                          <td style={{ fontWeight: 700 }}>{formatMoney(amount)}</td>
                        </tr>
                      );
                    })}
                  </tbody>
                </table>
              </div>
            )}
          </section>
        </div>
      )}
    </div>
  );
}
