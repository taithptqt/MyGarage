import React, { useCallback, useEffect, useState, useMemo } from "react";
import { serviceOrderApi } from "@features/workshop/api/serviceOrderApi";
import { getAllStaff, getAllRoles } from "@features/admin/api/staffApi";
import { Modal } from "@features/admin/components/AdminModals";
import { getStatusBadge } from "@features/admin/utils/adminUtils";
import { useBranch } from "@shared/core/context/BranchContext";

export default function ForemanDashboard() {
  const { searchQuery } = useSearch();
  const { selectedBranchId } = useBranch();
  const [orders, setOrders] = useState([]);
  const [technicians, setTechnicians] = useState([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");
  const [selectedOrder, setSelectedOrder] = useState(null);

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
      
      // Filter for Technicians from current branch
      const techStaff = sRes.filter(s => {
        const role = rRes.find(r => r.id === s.roleId);
        const code = (role?.roleCode || "").toUpperCase();
        const name = (role?.roleName || "").toLowerCase();
        const isTechRole = code === "TECH" || name.includes("tech") || name.includes("ky thuat");
        const isMatchedBranch = !selectedBranchId || s.branchId === Number(selectedBranchId);
        return isTechRole && isMatchedBranch;
      });
      setTechnicians(techStaff);
    } catch (err) {
      setError(err.message || "Không thể tải dữ liệu Quản đđc.");
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
    filteredOrders.forEach(o => { 
      if (groups[o.status]) groups[o.status].push(o); 
    });
    return groups;
  }, [filteredOrders]);

  if (loading && orders.length === 0) return <div className="loading-container">Đang tải...</div>;

  return (
    <div className="foreman-dashboard">
      <div className="dashboard-stats" style={{ display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)', gap: '16px', marginBottom: '24px' }}>
          <div className="stat-card" style={{ background: '#fff', padding: '16px', borderRadius: '16px', boxShadow: '0 4px 12px rgba(0,0,0,0.05)' }}>
              <span style={{ color: '#a3aed0', fontSize: '14px' }}>Tđ.ng lệnh đang xử lý</span>
              <h3 style={{ margin: '8px 0 0', fontSize: '24px' }}>{orders.length}</h3>
          </div>
          <div className="stat-card" style={{ background: '#fff', padding: '16px', borderRadius: '16px', boxShadow: '0 4px 12px rgba(0,0,0,0.05)' }}>
              <span style={{ color: '#a3aed0', fontSize: '14px' }}>Chờ chẩn đoán</span>
              <h3 style={{ margin: '8px 0 0', fontSize: '24px', color: '#ffb547' }}>{ordersByStatus.NEW.length + ordersByStatus.RECEPTION.length}</h3>
          </div>
          <div className="stat-card" style={{ background: '#fff', padding: '16px', borderRadius: '16px', boxShadow: '0 4px 12px rgba(0,0,0,0.05)' }}>
              <span style={{ color: '#a3aed0', fontSize: '14px' }}>Đang sửa chữa</span>
              <h3 style={{ margin: '8px 0 0', fontSize: '24px', color: '#4318ff' }}>{ordersByStatus.REPAIRING.length}</h3>
          </div>
          <div className="stat-card" style={{ background: '#fff', padding: '16px', borderRadius: '16px', boxShadow: '0 4px 12px rgba(0,0,0,0.05)' }}>
              <span style={{ color: '#a3aed0', fontSize: '14px' }}>Quality Control</span>
              <h3 style={{ margin: '8px 0 0', fontSize: '24px', color: '#01b574' }}>{ordersByStatus.QC_PENDING.length}</h3>
          </div>
      </div>

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
              <p><strong>Tình trạng hiện tại:</strong> <span className={`badge ${getStatusBadge(selectedOrder.status)}`}>{selectedOrder.status}</span></p>
              <p><strong>Yêu cầu:</strong> {selectedOrder.customerNotes || "Không có ghi chú"}</p>
           </div>
           
           <div className="workflow-actions" style={{ marginTop: '20px', padding: '16px', border: '1px solid #e9edf7', borderRadius: '12px' }}>
              <label style={{ display: 'block', marginBottom: '8px', fontWeight: 600 }}>Thao tác điều phđi:</label>
              <div style={{ display: 'flex', gap: '8px', flexWrap: 'wrap' }}>
                {(selectedOrder.status === "NEW" || selectedOrder.status === "RECEPTION") && <button className="btn-primary" onClick={() => handleUpdateStatus(selectedOrder.id, "DIAGNOSING")}>đzđ️ Bắt đầu chẩn đoán</button>}
                {selectedOrder.status === "DIAGNOSING" && <button className="btn-primary" onClick={() => handleUpdateStatus(selectedOrder.id, "WAITING_APPROVAL")}>đo. Phê duyệt kết quả & Gửi báo giá</button>}
                {selectedOrder.status === "WAITING_APPROVAL" && <button className="btn-primary" onClick={() => handleUpdateStatus(selectedOrder.id, "REPAIRING")}>đzđ️ Xác nhận sửa chữa</button>}
                {selectedOrder.status === "REPAIRING" && <button className="btn-primary" onClick={() => handleUpdateStatus(selectedOrder.id, "QC_PENDING")}>đzđ️ Chuyển sang QC</button>}
                {selectedOrder.status === "QC_PENDING" && <button className="btn-success" onClick={() => handleUpdateStatus(selectedOrder.id, "READY")}>ởđđ Hoàn tất, chờ giao xe</button>}
              </div>
           </div>

           <div className="technician-assignment" style={{ marginTop: '20px' }}>
              <label style={{ display: 'block', marginBottom: '8px', fontWeight: 600 }}>Phân công Thợ thực hiện:</label>
              <select 
                style={{ width: '100%', padding: '10px', borderRadius: '8px', border: '1px solid #e9edf7' }}
                value={selectedOrder.technicianId || ""}
                onChange={async (e) => {
                  try {
                    await serviceOrderApi.update(selectedOrder.id, { ...selectedOrder, technicianId: e.target.value });
                    alert("Đã cập nhật phân công thợ.");
                    fetchData();
                  } catch (err) {
                    alert("Lỗi phân công: " + err.message);
                  }
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
        .foreman-dashboard {
          padding: 8px;
        }
        .workflow-overview {
          display: flex;
          gap: 20px;
          overflow-x: auto;
          padding-bottom: 20px;
          min-height: 500px;
        }
        .workflow-column {
          flex: 0 0 280px;
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
          padding: 16px;
          border-radius: 12px;
          box-shadow: 0 4px 12px rgba(0,0,0,0.05);
          cursor: pointer;
          transition: all 0.2s;
          border-left: 4px solid transparent;
        }
        .order-card-mini:hover {
          transform: translateY(-2px);
          box-shadow: 0 6px 16px rgba(0,0,0,0.1);
          border-left-color: #4318ff;
        }
        .card-code {
          font-weight: 800;
          color: #1b2559;
          font-size: 15px;
        }
        .card-customer {
          font-size: 13px;
          font-weight: 600;
          margin-top: 6px;
          color: #2b3674;
        }
        .card-footer {
          margin-top: 10px;
          display: flex;
          justify-content: space-between;
          font-size: 11px;
          color: #a3aed0;
        }
        .empty-slot {
          text-align: center;
          padding: 30px;
          color: #a3aed0;
          font-style: italic;
          font-size: 13px;
          border: 2px dashed #d1d9e2;
          border-radius: 12px;
        }
      `}</style>
    </div>
  );
}





