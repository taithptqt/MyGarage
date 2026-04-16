import React, { useState, useEffect, useCallback } from "react";
import { request } from "@app/apiClient";
import { useAuth } from "@shared/core/context/AuthContext";
import "./MyPayslip.css";

function fmtCurrency(v) { return Number(v ?? 0).toLocaleString("vi-VN") + " ₫"; }

export default function MyPayslipPage() {
  const { user } = useAuth();
  const [payslips, setPayslips] = useState([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");
  const [selectedSlip, setSelectedSlip] = useState(null);

  const loadSlips = useCallback(async () => {
    setLoading(true);
    try {
      const res = await request({ url: "/api/payslips/my", method: "GET" });
      setPayslips(res?.items ?? res ?? []);
      // Auto select latest if exists
      const list = res?.items ?? res ?? [];
      if (list.length > 0 && !selectedSlip) {
        setSelectedSlip(list[0]);
      }
    } catch (e) {
      setError("Không thể tải danh sách phiếu lương.");
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => { loadSlips(); }, [loadSlips]);

  return (
    <div className="ps-page">
      <div className="ps-header">
        <h1 className="ps-title">📄 Phiếu Lương Của Tôi</h1>
        <p className="ps-subtitle">Xem lịch sử lương và chi tiết thu nhập từng tháng.</p>
      </div>

      <div className="ps-layout">
        {/* Left Side: List */}
        <div className="ps-sidebar">
          <h3 className="ps-sidebar-title">Lịch sử phiếu lương</h3>
          {loading ? <div className="p-4 text-slate-500">Đang tải...</div> : 
           payslips.length === 0 ? <div className="p-4 text-slate-500">Chưa có dữ liệu.</div> : (
            <ul className="ps-list">
              {payslips.map(slip => (
                <li 
                  key={slip.id} 
                  className={`ps-list-item ${selectedSlip?.id === slip.id ? 'active' : ''}`}
                  onClick={() => setSelectedSlip(slip)}
                >
                  <div className="ps-li-header">
                    <strong>Kỳ {slip.period}</strong>
                    <span className={`ps-st st-${(slip.status||"").toLowerCase()}`}>{slip.status}</span>
                  </div>
                  <div className="ps-li-amount">{fmtCurrency(slip.netSalary)}</div>
                </li>
              ))}
            </ul>
          )}
        </div>

        {/* Right Side: Detail */}
        <div className="ps-detail-area">
          {!selectedSlip ? (
            <div className="ps-empty">Chọn một phiếu lương để xem chi tiết.</div>
          ) : (
            <div className="ps-detail-card">
              <div className="ps-dc-header">
                <div className="ps-dc-brand">MY GARAGE</div>
                <div className="ps-dc-period">Phiếu lương: {selectedSlip.period}</div>
              </div>
              
              <div className="ps-dc-user">
                <div className="ps-dc-avatar">{(user?.fullName || "A").charAt(0)}</div>
                <div>
                  <h2 className="ps-dc-name">{user?.fullName || "Nhân viên"}</h2>
                  <div className="ps-dc-role">{user?.role || "Kỹ thuật viên"} / {user?.branchName || "Chi nhánh chính"}</div>
                </div>
              </div>

              <div className="ps-dc-grid">
                {/* Thu nhập */}
                <div className="ps-dc-section">
                  <h3 className="ps-dc-sec-title text-emerald-600">THU NHẬP (+)</h3>
                  <div className="ps-dc-row"><span className="ps-row-label">Lương cơ bản</span><span className="ps-row-val">{fmtCurrency(selectedSlip.baseSalary)}</span></div>
                  <div className="ps-dc-row"><span className="ps-row-label">Hoa hồng (Comm.)</span><span className="ps-row-val">{fmtCurrency(selectedSlip.commissionAmount)}</span></div>
                  <div className="ps-dc-row"><span className="ps-row-label">Làm thêm giờ (OT)</span><span className="ps-row-val">{fmtCurrency(selectedSlip.overtimeAmount)}</span></div>
                  <div className="ps-dc-row"><span className="ps-row-label">Thưởng khác</span><span className="ps-row-val">{fmtCurrency(selectedSlip.bonus)}</span></div>
                </div>

                {/* Khấu trừ */}
                <div className="ps-dc-section">
                  <h3 className="ps-dc-sec-title text-red-500">KHẤU TRỪ (-)</h3>
                  <div className="ps-dc-row"><span className="ps-row-label">Tạm ứng / Khấu trừ</span><span className="ps-row-val">{fmtCurrency(selectedSlip.deductions)}</span></div>
                  <div className="ps-dc-row"><span className="ps-row-label">Phạt vi phạm</span><span className="ps-row-val">{fmtCurrency(selectedSlip.penalty)}</span></div>
                </div>
              </div>

              <div className="ps-dc-total">
                <span>Thực lãnh:</span>
                <span className="ps-dc-total-val">{fmtCurrency(selectedSlip.netSalary)}</span>
              </div>

              <div className="ps-dc-footer">
                <p>Mọi thắc mắc về phiếu lương vui lòng liên hệ bộ phận HR (Nhân sự) trong vòng 3 ngày kể từ khi nhận phiếu.</p>
                <div className="pt-4 flex gap-2">
                  <button className="ps-btn ps-btn-print" onClick={() => window.print()}>🖨 In Phiếu</button>
                </div>
              </div>
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
