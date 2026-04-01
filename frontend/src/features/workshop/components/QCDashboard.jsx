import React, { useCallback, useEffect, useState, useMemo } from "react";
import { serviceOrderApi } from "@features/workshop/api/serviceOrderApi";
import { useAuth } from "@shared/core/context/AuthContext";
import { getAllStaff } from "@features/admin/api/staffApi";
import { Modal } from "@features/admin/components/AdminModals";
import { getStatusBadge } from "@features/admin/utils/adminUtils";
import { useBranch } from "@shared/core/context/BranchContext";
import { useSearch } from "@shared/core/context/SearchContext";
import TablePagination from "@shared/components/TablePagination";

export default function QCDashboard() {
  const { searchQuery } = useSearch();
  const { selectedBranchId } = useBranch();
  const { user } = useAuth();
  const [pendingOrders, setPendingOrders] = useState([]);
  const [staffId, setStaffId] = useState(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");
  const [activeInspection, setActiveInspection] = useState(null); // { orderId, failReason }
  const [currentPage, setCurrentPage] = useState(1);
  const [pageSize, setPageSize] = useState(10);

  const fetchData = useCallback(async () => {
    setLoading(true);
    try {
      const [oRes, sRes] = await Promise.all([
        serviceOrderApi.getAll(),
        getAllStaff()
      ]);
      
      const branchStaff = sRes.filter(s => !selectedBranchId || s.branchId === Number(selectedBranchId));
      const currentStaff = branchStaff.find(s => s.email === user.email || s.fullName === user.fullName);
      if (currentStaff) setStaffId(currentStaff.id);
      
      setPendingOrders((oRes || []).filter(o => o.status === "QC_PENDING"));
    } catch (err) {
      setError("Failed to load Quality Control tasks.");
    } finally {
      setLoading(false);
    }
  }, [user, selectedBranchId]);

  useEffect(() => {
    fetchData();
  }, [fetchData]);

  const handleInspect = async (orderId, passed, reason = "") => {
    try {
      const order = pendingOrders.find(o => o.id === orderId);
      
      // Update order status
      await serviceOrderApi.update(orderId, { 
        ...order, 
        status: passed ? "READY" : "REPAIRING",
        qcstatus: passed ? "PASSED" : "FAILED",
        qcinspectorId: staffId,
        qcverifiedAt: new Date().toISOString()
      });

      // Create inspection record
      await serviceOrderApi.createQcInspection({
        serviceOrderId: orderId,
        qcStaffId: staffId,
        status: passed ? "PASSED" : "FAILED",
        failReason: reason
      });

      setActiveInspection(null);
      fetchData();
    } catch (err) {
      alert("Lỗi xử lý Quality Control: " + err.message);
    }
  };

  const filteredOrders = useMemo(() => {
    const query = String(searchQuery || "").trim().toLowerCase();
    if (!query) return pendingOrders;
    return pendingOrders.filter(o => 
      (o.code || "").toLowerCase().includes(query) ||
      (o.customerName || "").toLowerCase().includes(query) ||
      String(o.customerId || "").toLowerCase().includes(query) ||
      (o.status || "").toLowerCase().includes(query)
    );
  }, [pendingOrders, searchQuery]);
  const pagedOrders = useMemo(() => {
    const start = (currentPage - 1) * pageSize;
    return filteredOrders.slice(start, start + pageSize);
  }, [filteredOrders, currentPage, pageSize]);

  useEffect(() => {
    setCurrentPage(1);
  }, [searchQuery, pendingOrders.length]);

  return (
    <div className="qc-dashboard">
      <div className="stats-bar compact-bar">
         <div className="stat-card compact">
            <div className="stat-value">{filteredOrders.length}</div>
            <div className="stat-label">XE CHỜ KIỂM ĐỊNH</div>
         </div>
      </div>

      {loading ? (
        <div className="loading-state"><div className="spinner" /></div>
      ) : (
        <div className="workshop-table-card">
           <table className="admin-table">
              <thead>
                <tr>
                   <th>Mã lệnh</th>
                   <th>Khách hàng</th>
                   <th>Trạng thái hiện tại</th>
                   <th className="th-center">Kiểm định</th>
                </tr>
              </thead>
              <tbody>
                {filteredOrders.length === 0 ? (
                  <tr><td colSpan="4" style={{ textAlign: 'center', padding: '40px', color: '#a3aed0' }}>Hiện tại không có xe nào khớp với tìm kiếm.</td></tr>
                ) : pagedOrders.map(order => (
                  <tr key={order.id}>
                    <td className="td-id">{order.code}</td>
                    <td style={{ fontWeight: 600 }}>{order.customerName || "-"}</td>
                    <td><span className={`badge ${getStatusBadge(order.status)}`}>{order.status}</span></td>
                    <td className="td-center">
                       <button className="btn-success" style={{ marginRight: '8px' }} onClick={() => handleInspect(order.id, true)}>??T ?</button>
                       <button className="btn-danger" onClick={() => setActiveInspection({ orderId: order.id, reason: "" })}>Hỏng ✖</button>
                    </td>
                  </tr>
                ))}
              </tbody>
           </table>
        </div>
      )}
      <TablePagination
        totalItems={filteredOrders.length}
        currentPage={currentPage}
        pageSize={pageSize}
        onPageChange={setCurrentPage}
        onPageSizeChange={(size) => {
          setPageSize(size);
          setCurrentPage(1);
        }}
      />

      {activeInspection && (
        <Modal title="Báo cáo lỗi kiểm định" onClose={() => setActiveInspection(null)}>
           <div className="form-group">
              <label>Lý do không đạt (Yêu cầu làm lại) *</label>
              <textarea 
                rows={4} 
                style={{ width: '100%', padding: '12px', border: '1px solid #e9edf7', borderRadius: '12px' }}
                placeholder="Mô tả các hạng mục chưa đạt yêu cầu..."
                value={activeInspection.reason}
                onChange={(e) => setActiveInspection({...activeInspection, reason: e.target.value})}
              />
           </div>
           <div className="form-actions" style={{ marginTop: '20px' }}>
              <button className="btn-ghost" onClick={() => setActiveInspection(null)}>Hủy</button>
              <button 
                className="btn-danger" 
                onClick={() => handleInspect(activeInspection.orderId, false, activeInspection.reason)}
                disabled={!activeInspection.reason}
              >
                Xác nhận & Chuyển trả Thợ
              </button>
           </div>
        </Modal>
      )}

      <style>{`
        .btn-success { background: #05cd99; color: #fff; border: none; padding: 8px 16px; border-radius: 10px; cursor: pointer; font-weight: 700; }
        .btn-danger { background: #ee5d50; color: #fff; border: none; padding: 8px 16px; border-radius: 10px; cursor: pointer; font-weight: 700; }
      `}</style>
    </div>
  );
}




