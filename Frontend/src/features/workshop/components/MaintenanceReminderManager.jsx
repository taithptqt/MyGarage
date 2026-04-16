import { useCallback, useEffect, useState } from "react";
import { executionApi } from "@features/workshop/api/executionApi";

export default function MaintenanceReminderManager() {
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");
  const [message, setMessage] = useState("");
  const [dueToday, setDueToday] = useState({ count: 0, items: [] });
  const [form, setForm] = useState({
    customerId: "",
    vehicleId: "",
    serviceOrderId: "",
    reminderType: "NextPeriodicService",
    scheduledAt: "",
    reminderMileage: "",
    channel: "SMS",
    estimatedServices: "",
  });

  const loadDueToday = useCallback(async () => {
    setLoading(true);
    setError("");
    try {
      const data = await executionApi.getMaintenanceRemindersDueToday();
      setDueToday({
        count: Number(data?.count || 0),
        items: Array.isArray(data?.items) ? data.items : [],
      });
    } catch (err) {
      setError(err?.message || "Không thể tải danh sách nhắc hôm nay.");
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    loadDueToday();
  }, [loadDueToday]);

  const handleSchedule = async (event) => {
    event.preventDefault();
    setError("");
    setMessage("");
    try {
      await executionApi.scheduleMaintenanceReminder({
        customerId: Number(form.customerId),
        vehicleId: Number(form.vehicleId),
        serviceOrderId: form.serviceOrderId ? Number(form.serviceOrderId) : null,
        reminderType: form.reminderType,
        scheduledAt: form.scheduledAt ? new Date(form.scheduledAt).toISOString() : null,
        reminderMileage: form.reminderMileage ? Number(form.reminderMileage) : null,
        channel: form.channel,
        estimatedServices: form.estimatedServices
          ? form.estimatedServices.split(",").map((item) => item.trim()).filter(Boolean)
          : null,
      });
      setMessage("Đã tạo lịch nhắc bảo dưỡng.");
      setForm((prev) => ({ ...prev, estimatedServices: "", reminderMileage: "", scheduledAt: "" }));
      loadDueToday();
    } catch (err) {
      setError(err?.message || "Không thể tạo lịch nhắc.");
    }
  };

  const handleProcessDue = async () => {
    setError("");
    setMessage("");
    try {
      const result = await executionApi.processMaintenanceRemindersDue({ batchSize: 200 });
      setMessage(`Đã xử lý ${Number(result?.processed || 0)} lịch nhắc (gửi: ${Number(result?.sent || 0)}).`);
      loadDueToday();
    } catch (err) {
      setError(err?.message || "Không thể xử lý lịch nhắc đến hạn.");
    }
  };

  return (
    <div className="management-content">
      <header className="admin-header header-actions-only">
        <h1>Quản lý nhắc bảo dưỡng</h1>
        <div className="bay-header-actions">
          <button className="btn-ghost" type="button" onClick={loadDueToday}>Làm mới</button>
          <button className="btn-primary" type="button" onClick={handleProcessDue}>Xử lý đến hạn</button>
        </div>
      </header>

      {error ? <div className="alert alert-error">{error}</div> : null}
      {message ? <div className="alert">{message}</div> : null}

      <div className="stats-bar bay-stat-grid">
        <div className="stat-card">
          <div className="stat-value">{dueToday.count}</div>
          <div className="stat-label">Lịch đến hạn hôm nay</div>
        </div>
      </div>

      <section className="inventory-panel">
        <div className="inventory-panel-head">
          <h3>Tạo lịch nhắc thủ công</h3>
          <p>Nhập nhanh trường bắt buộc để bổ sung lịch chăm sóc khách.</p>
        </div>
        <form className="inventory-panel-body inventory-form-grid" onSubmit={handleSchedule}>
          <input className="search-input" placeholder="CustomerId" value={form.customerId} onChange={(e) => setForm((p) => ({ ...p, customerId: e.target.value }))} required />
          <input className="search-input" placeholder="VehicleId" value={form.vehicleId} onChange={(e) => setForm((p) => ({ ...p, vehicleId: e.target.value }))} required />
          <input className="search-input" placeholder="ServiceOrderId (tuỳ chọn)" value={form.serviceOrderId} onChange={(e) => setForm((p) => ({ ...p, serviceOrderId: e.target.value }))} />
          <select className="search-input" value={form.reminderType} onChange={(e) => setForm((p) => ({ ...p, reminderType: e.target.value }))}>
            <option value="PostServiceFollowUp">Hậu mãi sau sửa chữa</option>
            <option value="NextOilChange">Nhắc thay dầu</option>
            <option value="NextPeriodicService">Nhắc bảo dưỡng định kỳ</option>
            <option value="Anniversary">Nhắc theo mốc thời gian</option>
          </select>
          <input className="search-input" type="datetime-local" value={form.scheduledAt} onChange={(e) => setForm((p) => ({ ...p, scheduledAt: e.target.value }))} />
          <input className="search-input" placeholder="Km nhắc (tuỳ chọn)" value={form.reminderMileage} onChange={(e) => setForm((p) => ({ ...p, reminderMileage: e.target.value }))} />
          <select className="search-input" value={form.channel} onChange={(e) => setForm((p) => ({ ...p, channel: e.target.value }))}>
            <option value="SMS">SMS</option>
            <option value="EMAIL">Email</option>
            <option value="PUSH">Push</option>
          </select>
          <input className="search-input" placeholder="Dịch vụ dự kiến (cách nhau dấu phẩy)" value={form.estimatedServices} onChange={(e) => setForm((p) => ({ ...p, estimatedServices: e.target.value }))} />
          <button className="btn-primary" type="submit">Tạo lịch nhắc</button>
        </form>
      </section>

      <div className="table-wrapper">
        <table className="admin-table">
          <thead>
            <tr>
              <th>ID</th>
              <th>Khách</th>
              <th>Xe</th>
              <th>Loại</th>
              <th>Kênh</th>
              <th>Lịch gửi</th>
              <th>Trạng thái</th>
            </tr>
          </thead>
          <tbody>
            {loading ? (
              <tr><td colSpan={7}><div className="empty-state"><p>Đang tải...</p></div></td></tr>
            ) : dueToday.items.length === 0 ? (
              <tr><td colSpan={7}><div className="empty-state"><p>Không có lịch nhắc đến hạn.</p></div></td></tr>
            ) : (
              dueToday.items.map((item) => (
                <tr key={item.id}>
                  <td>{item.id}</td>
                  <td>{item.customerId}</td>
                  <td>{item.vehicleId}</td>
                  <td>{item.reminderType}</td>
                  <td>{item.channel}</td>
                  <td>{new Date(item.scheduledAt).toLocaleString("vi-VN", { timeZone: "Asia/Ho_Chi_Minh" })}</td>
                  <td>{item.status}</td>
                </tr>
              ))
            )}
          </tbody>
        </table>
      </div>
    </div>
  );
}

