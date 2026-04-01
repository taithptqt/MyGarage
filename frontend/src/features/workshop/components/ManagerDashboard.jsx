import React, { useCallback, useEffect, useState, useMemo } from "react";
import { serviceOrderApi } from "@features/workshop/api/serviceOrderApi";
import { getAllStaff, getAllRoles } from "@features/admin/api/staffApi";
import { Modal } from "@features/admin/components/AdminModals";
import { getStatusBadge } from "@features/admin/utils/adminUtils";
import { useBranch } from "@shared/core/context/BranchContext";

export default function ManagerDashboard() {
  const { searchQuery } = useSearch();
  const { selectedBranchId } = useBranch();
  const [orders, setOrders] = useState([]);
  const [technicians, setTechnicians] = useState([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");
  const [selectedOrder, setSelectedOrder] = useState(null); // For assignment modal

  const fetchData = useCallback(async () => {
    setLoading(true);
    setError("");
    try {
      const [oRes, sRes, rRes] = await Promise.all([
        serviceOrderApi.getAll(),
        getAllStaff(),
        getAllRoles()
      ]);
      
      // Filter active workshop orders
      const activeStatus = ["NEW", "RECEPTION", "DIAGNOSING", "WAITING_APPROVAL", "REPAIRING", "QC_PENDING", "READY"];
      setOrders((oRes || []).filter(o => activeStatus.includes(o.status)));
      
      // Filter technicians from current branch
      const techStaff = sRes.filter(s => {
        const role = rRes.find(r => r.id === s.roleId);
        const code = (role?.roleCode || "").toUpperCase();
        const isTechRole = code === "TECH" || code === "TECH_LEAD"; // Fallback for various tech roles
        const isMatchedBranch = !selectedBranchId || s.branchId === Number(selectedBranchId);
        return isTechRole && isMatchedBranch;
      });
      setTechnicians(techStaff);
    } catch (err) {
      setError(err.message || "Failed to load manager data.");
    } finally {
      setLoading(false);
    }
  }, [selectedBranchId]);

  useEffect(() => {
    fetchData();
  }, [fetchData]);

  const handleUpdateStatus = async (orderId, newStatus) => {
    try {
      const order = orders.find(o => o.id === orderId);
      await serviceOrderApi.update(orderId, { ...order, status: newStatus });
      fetchData();
    } catch (err) {
      alert("Lđ-i khi cập nhật trạng thái: " + err.message);
    }
  };

  const filteredOrders = useMemo(() => {
    const query = String(searchQuery || "").trim().toLowerCase();
    if (!query) return orders;
    return orders.filter(o => 
      (o.code || "").toLowerCase().includes(query) ||
      (o.customerName || "").toLowerCase().includes(query) ||
      String(o.customerId || "").toLowerCase().includes(query) ||
      (o.status || "").toLowerCase().includes(query)
    );
  }, [orders, searchQuery]);

  const ordersByStatus = useMemo(() => {
    const groups = {
      NEW: [],
      RECEPTION: [],
      DIAGNOSING: [],
      WAITING_APPROVAL: [],
      REPAIRING: [],
      QC_PENDING: [],
      READY: []
    };
    filteredOrders.forEach(o => { if (groups[o.status]) groups[o.status].push(o); });
    return groups;
  }, [filteredOrders]);

  return (
    <div className="manager-dashboard">
      <div className="workflow-overview">
        {Object.entries(ordersByStatus).map(([status, list]) => (
          <div key={status} className="workflow-column">
             <div className="column-header">
               <span className={`badge ${getStatusBadge(status)}`}>{status}</span>
               <span className="count">{list.length}</span>
             </div>
             <div className="column-content">
               {list.map(order => (
                 <div key={order.id} className="order-card-mini" onClick={() => setSelectedOrder(order)}>
                    <div className="card-code">{order.code}</div>
                    <div className="card-customer">{order.customerName || "-"}</div>
                    <div className="card-footer">
                       <span className="card-time">đOs {order.checkInTime ? new Date(order.checkInTime).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }) : "-"}</span>
                    </div>
                 </div>
               ))}
               {list.length === 0 && <div className="empty-slot">Trđng</div>}
             </div>
          </div>
        ))}
      </div>

      {selectedOrder && (
        <Modal title={`Điều phđi lệnh: ${selectedOrder.code}`} onClose={() => setSelectedOrder(null)}>
           <div className="order-details-summary">
              <p><strong>Khách hàng:</strong> {selectedOrder.customerName || "-"}</p>
              <p><strong>Yêu cầu:</strong> {selectedOrder.customerNotes || "Không có ghi chú"}</p>
           </div>
           
           <div className="workflow-actions" style={{ marginTop: '20px', padding: '16px', border: '1px solid #e9edf7', borderRadius: '12px' }}>
              <label style={{ display: 'block', marginBottom: '8px', fontWeight: 600 }}>Chuyển trạng thái tiếp theo:</label>
              <div style={{ display: 'flex', gap: '8px' }}>
                {(selectedOrder.status === "NEW" || selectedOrder.status === "RECEPTION") && <button className="btn-primary" onClick={() => handleUpdateStatus(selectedOrder.id, "DIAGNOSING")}>đzđ️ Bắt đầu chẩn đoán</button>}
                {selectedOrder.status === "DIAGNOSING" && <button className="btn-primary" onClick={() => handleUpdateStatus(selectedOrder.id, "WAITING_APPROVAL")}>đzđ️ Gửi báo giá</button>}
                {selectedOrder.status === "REPAIRING" && <button className="btn-primary" onClick={() => handleUpdateStatus(selectedOrder.id, "QC_PENDING")}>đzđ️ Chuyển QC</button>}
              </div>
           </div>

           <div className="technician-assignment" style={{ marginTop: '20px' }}>
              <label style={{ display: 'block', marginBottom: '8px', fontWeight: 600 }}>Phân công Thợ (Technician):</label>
              <select 
                style={{ width: '100%', padding: '10px', borderRadius: '8px', border: '1px solid #e9edf7' }}
                value={selectedOrder.technicianId || ""}
                onChange={(e) => {
                  // In real app, update technicianId in ServiceOrder or Details
                  alert("Đã phân công cho thợ ID: " + e.target.value);
                }}
              >
                <option value="">-- Chọn thợ --</option>
                {technicians.map(t => (
                  <option key={t.id} value={t.id}>{t.fullName}</option>
                ))}
              </select>
           </div>

           <div className="form-actions" style={{ marginTop: '24px' }}>
              <button className="btn-ghost" onClick={() => setSelectedOrder(null)}>Đóng</button>
           </div>
        </Modal>
      )}

      <style>{`
        .workflow-overview {
          display: flex;
          gap: 20px;
          overflow-x: auto;
          padding-bottom: 20px;
          min-height: 400px;
        }
        .workflow-column {
          flex: 0 0 250px;
          background: #f4f7fe;
          border-radius: 20px;
          padding: 16px;
          display: flex;
          flex-direction: column;
        }
        .column-header {
          display: flex;
          justify-content: space-between;
          align-items: center;
          margin-bottom: 16px;
          padding: 0 4px;
        }
        .column-header .count {
          background: #e9edf7;
          padding: 2px 8px;
          border-radius: 8px;
          font-size: 12px;
          font-weight: 700;
        }
        .column-content {
          display: flex;
          flex-direction: column;
          gap: 12px;
          flex: 1;
        }
        .order-card-mini {
          background: #ffffff;
          padding: 12px;
          border-radius: 12px;
          box-shadow: 0 4px 12px rgba(0,0,0,0.05);
          cursor: pointer;
          transition: transform 0.2s;
        }
        .order-card-mini:hover {
          transform: translateY(-2px);
        }
        .card-code {
          font-weight: 800;
          color: #4318ff;
          font-size: 14px;
        }
        .card-customer {
          font-size: 13px;
          font-weight: 600;
          margin-top: 4px;
        }
        .card-footer {
          margin-top: 8px;
          display: flex;
          justify-content: space-between;
          font-size: 11px;
          color: #a3aed0;
        }
        .empty-slot {
          text-align: center;
          padding: 20px;
          color: #a3aed0;
          font-style: italic;
          font-size: 13px;
          border: 2px dashed #e9edf7;
          border-radius: 12px;
        }
      `}</style>
    </div>
  );
}





