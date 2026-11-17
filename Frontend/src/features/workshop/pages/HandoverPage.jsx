import React, { useCallback, useEffect, useMemo, useState } from "react";
import { useNavigate, useParams } from "react-router-dom";
import { request } from "@app/apiClient";
import { useAuth } from "@shared/core/context/AuthContext";
import "./Handover.css";

/* -- helpers -- */
function fmt(v) {
  if (!v) return "--";
  return new Date(v).toLocaleString("vi-VN", {
    day: "2-digit", month: "2-digit", year: "numeric",
    hour: "2-digit", minute: "2-digit", timeZone: "Asia/Ho_Chi_Minh",
  });
}
function fmtCurrency(v) {
  if (!v && v !== 0) return "--";
  return Number(v).toLocaleString("vi-VN") + " đ";
}

const STATUS_CHIPS = {
  PASSED:  { label: "QC đạt",      bg: "#d1fae5", color: "#065f46" },
  FAILED:  { label: "QC không đạt", bg: "#fee2e2", color: "#991b1b" },
  PAID:    { label: "Đã thanh toán", bg: "#d1fae5", color: "#065f46" },
  PENDING: { label: "Chờ thanh toán", bg: "#fef9c3", color: "#713f12" },
  PARTIAL: { label: "Thanh toán một phần", bg: "#fffbeb", color: "#92400e" },
  READY_FOR_PICKUP: { label: "Sẵn sàng giao xe", bg: "#dbeafe", color: "#1e40af" },
};

/* -- sub-components -- */
function ReadinessItem({ icon, label, ok, detail }) {
  return (
    <div className={`ho-ready-item ${ok ? "ho-ready-ok" : "ho-ready-fail"}`}>
      <span className="ho-ready-icon">{ok ? "✓" : "✕"}</span>
      <div className="ho-ready-info">
        <span className="ho-ready-label">{label}</span>
        {detail && <span className="ho-ready-detail">{detail}</span>}
      </div>
      <span className="ho-ready-status">{ok ? "Sẵn sàng" : "Chưa đạt"}</span>
    </div>
  );
}

function ChecklistRow({ id, label, checked, onChange, disabled }) {
  return (
    <label className={`ho-checklist-row ${checked ? "ho-cl-checked" : ""} ${disabled ? "ho-cl-disabled" : ""}`}>
      <input
        type="checkbox"
        id={id}
        checked={checked}
        onChange={e => onChange(e.target.checked)}
        disabled={disabled}
        className="ho-cl-checkbox"
      />
      <span className="ho-cl-label">{label}</span>
    </label>
  );
}

/* -- main -- */
export default function HandoverPage() {
  const { orderId } = useParams();
  const { user }    = useAuth();
  const navigate    = useNavigate();

  const [order, setOrder]       = useState(null);
  const [readiness, setReadiness] = useState(null);
  const [loading, setLoading]   = useState(true);
  const [saving, setSaving]     = useState(false);
  const [error, setError]       = useState("");
  const [success, setSuccess]   = useState("");
  const [confirmed, setConfirmed] = useState(false);

  /* handover checklist state */
  const [checks, setChecks] = useState({
    keyReturned:       false,
    partsExplained:    false,
    invoiceProvided:   false,
    warrantyExplained: false,
    vehicleInspected:  false,
    nextServiceTold:   false,
  });
  const [notes, setNotes]       = useState("");
  const [receiverName, setReceiverName] = useState("");
  const [receiverPhone, setReceiverPhone] = useState("");

  /* -- fetch -- */
  const fetchData = useCallback(async () => {
    setLoading(true);
    setError("");
    try {
      const [orderRes, readinessRes] = await Promise.all([
        request({ url: `/api/v1/service-orders/${orderId}`, method: "GET" }),
        request({ url: `/api/v1/service-orders/${orderId}/handover-readiness`, method: "GET" })
          .catch(() => null),
      ]);

      const raw = orderRes?.data ?? orderRes;
      setOrder(raw);

      if (readinessRes) {
        setReadiness(readinessRes?.data ?? readinessRes);
      } else {
        /* fallback: derive readiness from order fields */
        setReadiness({
          qcPassed:    ["PASSED","QC_PASSED","PASS"].includes(String(raw?.qcStatus ?? raw?.QcStatus ?? raw?.qcstatus ?? "").toUpperCase()),
          paymentDone: ["PAID"].includes(String(raw?.paymentStatus ?? raw?.PaymentStatus ?? "").toUpperCase()),
          invoiceReady: !!(raw?.invoiceNumber ?? raw?.InvoiceNumber),
          bayReleased:  false,
        });
      }

      /* Pre-fill receiver from customer */
      setReceiverName(raw?.customerName ?? raw?.CustomerName ?? "");
      setReceiverPhone(raw?.customerPhone ?? raw?.CustomerPhone ?? "");
    } catch (err) {
      setError("Không thể tải thông tin lệnh. Kiểm tra kết nối.");
      console.error(err);
    } finally {
      setLoading(false);
    }
  }, [orderId]);

  useEffect(() => { fetchData(); }, [fetchData]);

  /* -- validation -- */
  const allChecked = Object.values(checks).every(Boolean);
  const qcOk      = readiness?.qcPassed ?? readiness?.QcPassed ?? readiness?.isQcPassed ?? readiness?.IsQcPassed ?? false;
  const payOk     = readiness?.paymentDone ?? readiness?.PaymentDone ?? readiness?.isPaid ?? readiness?.IsPaid ?? false;
  const canHandover = (readiness?.canHandover ?? readiness?.CanHandover ?? (qcOk && payOk)) && allChecked && receiverName.trim();

  /* -- submit -- */
  const handleHandover = async () => {
    if (!window.confirm("Xác nhận bàn giao xe cho khách?")) return;
    setSaving(true);
    setError("");
    setSuccess("");
    try {
      await request({
        url: `/api/v1/service-orders/${orderId}/handovers`,
        method: "POST",
        data: {
          staffId:       user?.id ?? user?.staffId,
          handedOverBy:  user?.id ?? user?.staffId,
          receiverName:  receiverName.trim(),
          receiverPhone: receiverPhone.trim() || null,
          notes:         notes.trim() || null,
          checklistItems: Object.entries(checks).map(([k, v]) => ({ key: k, completed: v })),
          handoverTime:  new Date().toISOString(),
        },
      });
      setConfirmed(true);
      setSuccess("Bàn giao xe thành công! Cảm ơn khách hàng.");
    } catch (err) {
      setError(err?.response?.data?.message || err?.message || "Không thể hoàn tất bàn giao xe.");
    } finally {
      setSaving(false);
    }
  };

  /* -- status chip helper -- */
  const makeChip = (key) => {
    const m = STATUS_CHIPS[key] || { label: key, bg: "#f1f5f9", color: "#64748b" };
    return (
      <span className="ho-chip" style={{ background: m.bg, color: m.color }}>
        {m.label}
      </span>
    );
  };

  if (loading) return (
    <div className="ho-page">
      <div className="ho-loading"><div className="ho-spinner" /><span>Đang tải thông tin bàn giao...</span></div>
    </div>
  );

  const soCode   = order?.code ?? order?.Code ?? `SO-${orderId}`;
  const plate    = order?.vehicleLicensePlate ?? order?.VehicleLicensePlate ?? "--";
  const model    = order?.vehicleDescriptor ?? order?.vehicleModelName ?? "";
  const customer = order?.customerName ?? order?.CustomerName ?? "--";
  const invoice  = order?.invoiceNumber ?? order?.InvoiceNumber ?? null;
  const totalAmt = order?.totalAmount ?? order?.TotalAmount ?? order?.grandTotal ?? 0;
  const paidAmt  = order?.paidAmount ?? order?.PaidAmount ?? 0;
  const qcStatus = String(order?.qcStatus ?? order?.QcStatus ?? "").toUpperCase();
  const payStatus = String(order?.paymentStatus ?? order?.PaymentStatus ?? "PENDING").toUpperCase();

  return (
    <div className="ho-page">
      {/* header */}
      <div className="ho-header">
        <div>
          <h1 className="ho-title">Bàn giao xe cho khách</h1>
          <p className="ho-subtitle">Lệnh: <strong>{soCode}</strong> - {plate} - {customer}</p>
        </div>
        <button className="ho-btn-back" onClick={() => navigate(-1)}>Quay lại</button>
      </div>

      {error   && <div className="ho-alert ho-alert-error">{error}</div>}
      {success && <div className="ho-alert ho-alert-success">{success}</div>}

      {/* -- SUCCESS view -- */}
      {confirmed ? (
        <div className="ho-success-card">
          <div className="ho-success-icon">🎉</div>
          <h2>Bàn giao thành công!</h2>
          <p>Xe <strong>{plate}</strong> đã được bàn giao cho <strong>{receiverName}</strong> lúc {fmt(new Date())}.</p>
          <div className="ho-success-actions">
            <button className="ho-btn-primary" onClick={() => navigate("/workshop/reception")}>
              Về danh sách lệnh
            </button>
            <button className="ho-btn-secondary" onClick={() => navigate("/workshop/foreman/board")}>
              Về Kanban xưởng
            </button>
          </div>
        </div>
      ) : (
        <div className="ho-body">
          {/* LEFT panel */}
          <div className="ho-left">
            {/* order summary */}
            <div className="ho-card">
              <div className="ho-card-head">Thông tin lệnh sửa chữa</div>
              <div className="ho-info-grid">
                <div className="ho-info-row"><span className="ho-lbl">Mã lệnh</span><span className="ho-val ho-code">{soCode}</span></div>
                <div className="ho-info-row"><span className="ho-lbl">Biển số</span><span className="ho-val">{plate}</span></div>
                {model && <div className="ho-info-row"><span className="ho-lbl">Xe</span><span className="ho-val">{model}</span></div>}
                <div className="ho-info-row"><span className="ho-lbl">Khách hàng</span><span className="ho-val">{customer}</span></div>
                <div className="ho-info-row"><span className="ho-lbl">QC</span><span className="ho-val">{makeChip(qcStatus || "PENDING")}</span></div>
                <div className="ho-info-row"><span className="ho-lbl">Thanh toán</span><span className="ho-val">{makeChip(payStatus)}</span></div>
                {invoice && <div className="ho-info-row"><span className="ho-lbl">Hóa đơn</span><span className="ho-val ho-code">{invoice}</span></div>}
                <div className="ho-info-row"><span className="ho-lbl">Tổng tiền</span><span className="ho-val">{fmtCurrency(totalAmt)}</span></div>
                {paidAmt > 0 && <div className="ho-info-row"><span className="ho-lbl">Đã thu</span><span className="ho-val" style={{ color: "#10b981", fontWeight: 800 }}>{fmtCurrency(paidAmt)}</span></div>}
                {Number(totalAmt) - Number(paidAmt) > 0 && (
                  <div className="ho-info-row">
                    <span className="ho-lbl">Còn lại</span>
                    <span className="ho-val" style={{ color: "#ef4444", fontWeight: 800 }}>{fmtCurrency(Number(totalAmt) - Number(paidAmt))}</span>
                  </div>
                )}
              </div>
            </div>

            {/* readiness */}
            <div className="ho-card">
              <div className="ho-card-head">Điều kiện bàn giao</div>
              <div className="ho-ready-list">
                <ReadinessItem label="QC kiểm tra đạt" ok={qcOk} detail={!qcOk ? "Cần hoàn thành kiểm tra QC" : "Xe đã qua kiểm tra"} />
                <ReadinessItem label="Thanh toán hoàn tất" ok={payOk} detail={!payOk ? "Cần thanh toán đầy đủ trước khi giao" : "Thu đủ tiền"} />
                <ReadinessItem label="Hóa đơn" ok={!!invoice} detail={invoice ? `Số HĐ: ${invoice}` : "Chưa xuất hóa đơn"} />
              </div>
            </div>

            {/* receiver info */}
            <div className="ho-card">
              <div className="ho-card-head">Thông tin người nhận</div>
              <div className="ho-form-grid">
                <div className="ho-form-group">
                  <label>Tên người nhận <span className="ho-req">*</span></label>
                  <input
                    type="text"
                    className="ho-input"
                    placeholder="Nhập tên người nhận xe"
                    value={receiverName}
                    onChange={e => setReceiverName(e.target.value)}
                  />
                </div>
                <div className="ho-form-group">
                  <label>Số điện thoại</label>
                  <input
                    type="tel"
                    className="ho-input"
                    placeholder="0909xxxxxx"
                    value={receiverPhone}
                    onChange={e => setReceiverPhone(e.target.value)}
                  />
                </div>
              </div>
              <div className="ho-form-group" style={{ marginTop: 12 }}>
                <label>Ghi chú bàn giao</label>
                <textarea
                  className="ho-textarea"
                  rows={2}
                  placeholder="Lưu ý cho khách, tình trạng đặc biệt..."
                  value={notes}
                  onChange={e => setNotes(e.target.value)}
                />
              </div>
            </div>
          </div>

          {/* RIGHT panel */}
          <div className="ho-right">
            <div className="ho-card">
              <div className="ho-card-head">Danh sách bàn giao</div>
              <p className="ho-cl-hint">Xác nhận từng bước trước khi bàn giao xe:</p>
              <div className="ho-checklist">
                <ChecklistRow id="keyReturned"       label="Đã trả chìa khóa xe + điều khiển từ xa" checked={checks.keyReturned}       onChange={v => setChecks(p => ({ ...p, keyReturned: v }))}       />
                <ChecklistRow id="vehicleInspected"  label="Kiểm tra xe cùng khách hàng"           checked={checks.vehicleInspected}  onChange={v => setChecks(p => ({ ...p, vehicleInspected: v }))}  />
                <ChecklistRow id="partsExplained"    label="Đã giải thích công việc đã thực hiện"  checked={checks.partsExplained}    onChange={v => setChecks(p => ({ ...p, partsExplained: v }))}    />
                <ChecklistRow id="invoiceProvided"   label="Đã giao hóa đơn / phiếu sửa chữa"      checked={checks.invoiceProvided}   onChange={v => setChecks(p => ({ ...p, invoiceProvided: v }))}   />
                <ChecklistRow id="warrantyExplained" label="Đã giải thích điều khoản bảo hành"     checked={checks.warrantyExplained} onChange={v => setChecks(p => ({ ...p, warrantyExplained: v }))} />
                <ChecklistRow id="nextServiceTold"   label="Đã nhắc lịch bảo dưỡng tiếp theo"      checked={checks.nextServiceTold}   onChange={v => setChecks(p => ({ ...p, nextServiceTold: v }))}   />
              </div>
              {allChecked && (
                <div className="ho-all-checked">Đã hoàn tất tất cả bước bàn giao</div>
              )}
            </div>

            {/* validation summary */}
            <div className={`ho-validate-card ${canHandover ? "ho-validate-ok" : "ho-validate-block"}`}>
              <div className="ho-validate-title">
                {canHandover ? "Sẵn sàng bàn giao" : "Chưa đủ điều kiện bàn giao"}
              </div>
              {!canHandover && (
                <ul className="ho-validate-list">
                  {!qcOk   && <li>QC chưa đạt</li>}
                  {!payOk  && <li>Chưa thanh toán đầy đủ</li>}
                  {!allChecked && <li>Chưa hoàn thành danh sách bàn giao ({Object.values(checks).filter(Boolean).length}/{Object.keys(checks).length})</li>}
                  {!receiverName.trim() && <li>Chưa nhập tên người nhận</li>}
                </ul>
              )}
            </div>

            <button
              className="ho-btn-handover"
              onClick={handleHandover}
              disabled={!canHandover || saving}
            >
              {saving ? "Đang xử lý..." : "XÁC NHẬN GIAO XE"}
            </button>
          </div>
        </div>
      )}
    </div>
  );
}


