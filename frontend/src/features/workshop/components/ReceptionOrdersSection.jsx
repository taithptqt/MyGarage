import React, { useCallback, useEffect, useMemo, useState } from "react";
import { useNavigate, useLocation } from "react-router-dom";
import { serviceOrderApi } from "@features/workshop/api/serviceOrderApi";
import { getAllCustomers, getAllVehicles } from "@features/admin/api/customerApi";
import { getAllStaff, getAllRoles } from "@features/admin/api/staffApi";
import DateRangePickerModal, { getDateRangeButtonLabel } from "@features/admin/components/DateRangePickerModal";
import { getStatusBadge } from "@features/admin/utils/adminUtils";
import { useSearch } from "@shared/core/context/SearchContext";
import { useBranch } from "@shared/core/context/BranchContext";
import { useAuth } from "@shared/core/context/AuthContext";
import TablePagination from "@shared/components/TablePagination";
import FilterDropdown from "@shared/components/FilterDropdown";
import { getStatusLabelVi, toStatusOptionVi } from "@shared/core/utils/statusLabelUtils";
import ServiceOrderDetailModal from "@features/workshop/components/ServiceOrderDetailModal";

function parseBranchId(value) {
  const parsed = Number(value);
  return Number.isFinite(parsed) && parsed > 0 ? parsed : 0;
}

function toDateText(dateObj) {
  if (!(dateObj instanceof Date) || Number.isNaN(dateObj.getTime())) return "";
  const y = dateObj.getFullYear();
  const m = String(dateObj.getMonth() + 1).padStart(2, "0");
  const d = String(dateObj.getDate()).padStart(2, "0");
  return `${y}-${m}-${d}`;
}

function toStartOfDay(dateText) {
  if (!dateText) return null;
  const d = new Date(`${dateText}T00:00:00`);
  return Number.isNaN(d.getTime()) ? null : d;
}

function toEndOfDay(dateText) {
  if (!dateText) return null;
  const d = new Date(`${dateText}T23:59:59.999`);
  return Number.isNaN(d.getTime()) ? null : d;
}

function normalizeStatus(status) {
  return String(status || "").trim().toUpperCase();
}

export default function ReceptionOrdersSection() {
  const navigate = useNavigate();
  const location = useLocation();
  const { searchQuery } = useSearch();
  const { selectedBranchId } = useBranch();
  const { user } = useAuth();

  const [orders, setOrders] = useState([]);
  const [customers, setCustomers] = useState([]);
  const [vehicles, setVehicles] = useState([]);
  const [staff, setStaff] = useState([]);
  const [loading, setLoading] = useState(false);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState("");
  const [modal, setModal] = useState(null);
  const [selectedOrderForView, setSelectedOrderForView] = useState(null);
  const [orderStatusFilter, setOrderStatusFilter] = useState("ALL");
  const [orderSortBy, setOrderSortBy] = useState("CREATED_DESC");
  const [dateFrom, setDateFrom] = useState(() => toDateText(new Date()));
  const [dateTo, setDateTo] = useState(() => toDateText(new Date()));
  const [ordersPage, setOrdersPage] = useState(1);
  const [ordersPageSize, setOrdersPageSize] = useState(10);

  const userBranchId = parseBranchId(user?.branchId ?? user?.BranchId);
  const contextBranchId = parseBranchId(selectedBranchId);
  const effectiveBranchId = userBranchId || contextBranchId;

  const fetchData = useCallback(async () => {
    setLoading(true);
    setError("");
    try {
      const [oRes, cRes, vRes, sRes, rRes] = await Promise.all([
        serviceOrderApi.getAll(),
        getAllCustomers(),
        getAllVehicles(),
        getAllStaff(),
        getAllRoles(),
      ]);

      setOrders(Array.isArray(oRes) ? oRes : []);
      setCustomers(Array.isArray(cRes) ? cRes : []);
      setVehicles(Array.isArray(vRes) ? vRes : []);

      const filteredStaff = (Array.isArray(sRes) ? sRes : []).filter((item) => {
        const role = (Array.isArray(rRes) ? rRes : []).find((r) => r.id === item.roleId);
        const isReceptionist = role && role.roleCode === "RECEPTIONIST";
        const staffBranchId = Number(item.branchId ?? item.BranchId ?? 0);
        const branchMatched = !effectiveBranchId || staffBranchId === effectiveBranchId;
        return isReceptionist && branchMatched;
      });
      setStaff(filteredStaff);
    } catch (err) {
      setError(err?.message || "Không thể tải dữ liệu tiếp nhận.");
    } finally {
      setLoading(false);
    }
  }, [effectiveBranchId]);

  useEffect(() => {
    fetchData();
  }, [fetchData]);

  const getOrderCustomerName = useCallback((order) => {
    if (order.customerName) return order.customerName;
    const customer = customers.find((c) => Number(c.id) === Number(order.customerId));
    return customer?.fullName || "-";
  }, [customers]);

  const getOrderVehiclePlate = useCallback((order) => {
    if (order.vehicleLicensePlate) return order.vehicleLicensePlate;
    const vehicle = vehicles.find((v) => Number(v.id) === Number(order.vehicleId));
    return vehicle?.licensePlate || "N/A";
  }, [vehicles]);

  const orderStatusOptions = useMemo(() => {
    const statuses = Array.from(
      new Set((orders || []).map((order) => normalizeStatus(order.status)).filter(Boolean))
    );
    return statuses.sort((a, b) => a.localeCompare(b));
  }, [orders]);

  const filteredOrders = useMemo(() => {
    const query = String(searchQuery || "").trim().toLowerCase();
    const queryCompact = query.replace(/[\s.-]/g, "");
    const startDate = toStartOfDay(dateFrom);
    const endDate = toEndOfDay(dateTo);

    const list = (orders || []).filter((order) => {
      const byStatus = orderStatusFilter === "ALL" || normalizeStatus(order.status) === orderStatusFilter;
      const createdAtTime = new Date(order.createdAt || 0).getTime();
      const byDate =
        (!startDate || createdAtTime >= startDate.getTime()) &&
        (!endDate || createdAtTime <= endDate.getTime());

      const plate = String(getOrderVehiclePlate(order) || "").toLowerCase();
      const plateCompact = plate.replace(/[\s.-]/g, "");
      const byQuery =
        !query ||
        String(order.code || "").toLowerCase().includes(query) ||
        getOrderCustomerName(order).toLowerCase().includes(query) ||
        String(order.status || "").toLowerCase().includes(query) ||
        plate.includes(query) ||
        (queryCompact && plateCompact.includes(queryCompact));

      return byStatus && byDate && byQuery;
    });

    return list.sort((a, b) => {
      const createdA = new Date(a.createdAt || 0).getTime();
      const createdB = new Date(b.createdAt || 0).getTime();
      const checkInA = new Date(a.checkInTime || 0).getTime();
      const checkInB = new Date(b.checkInTime || 0).getTime();
      const scheduledA = new Date(a.estimatedHandover || 0).getTime();
      const scheduledB = new Date(b.estimatedHandover || 0).getTime();

      if (orderSortBy === "CREATED_ASC") {
        if (createdA !== createdB) return createdA - createdB;
      } else if (orderSortBy === "CHECKIN_DESC") {
        if (checkInB !== checkInA) return checkInB - checkInA;
      } else if (orderSortBy === "CHECKIN_ASC") {
        if (checkInA !== checkInB) return checkInA - checkInB;
      } else if (orderSortBy === "SCHEDULE_DESC") {
        if (scheduledB !== scheduledA) return scheduledB - scheduledA;
      } else if (createdB !== createdA) {
        return createdB - createdA;
      }

      return Number(b.id || 0) - Number(a.id || 0);
    });
  }, [orders, searchQuery, getOrderCustomerName, getOrderVehiclePlate, orderStatusFilter, orderSortBy, dateFrom, dateTo]);

  const pagedOrders = useMemo(() => {
    const start = (ordersPage - 1) * ordersPageSize;
    return filteredOrders.slice(start, start + ordersPageSize);
  }, [filteredOrders, ordersPage, ordersPageSize]);

  const activeOrdersCount = useMemo(
    () => orders.filter((order) => !["COMPLETED", "CANCELED", "CANCELLED"].includes(normalizeStatus(order.status))).length,
    [orders]
  );

  const statusPermitUpdate = useCallback(
    (status) => !["COMPLETED", "CANCELED", "CANCELLED"].includes(normalizeStatus(status)),
    []
  );

  const handleCompleteOrder = useCallback(async (order) => {
    if (!statusPermitUpdate(order.status)) {
      alert("Lệnh này đã kết thúc hoặc đã hủy, không thể bàn giao.");
      return;
    }
    if (!window.confirm(`Xác nhận bàn giao xe ${order.code} cho khách?`)) return;

    setSaving(true);
    try {
      await serviceOrderApi.update(order.id, { ...order, status: "COMPLETED" });
      await fetchData();
    } catch (err) {
      setError("Lỗi khi bàn giao: " + err.message);
    } finally {
      setSaving(false);
    }
  }, [fetchData, statusPermitUpdate]);

  const handleCancelOrder = useCallback(async (order) => {
    if (["COMPLETED", "CANCEL_REQUESTED", "CANCELLED"].includes(order.status)) {
      alert("Lệnh này không thể hủy ở trạng thái hiện tại.");
      return;
    }
    const reason = window.prompt(`Lý do hủy lệnh ${order.code}:`, "Khách hàng yêu cầu hủy");
    if (reason === null) return;
    if (!window.confirm("Bạn có chắc chắn muốn hủy lệnh này? Thao tác này không thể hoàn tác.")) return;

    setSaving(true);
    try {
      await serviceOrderApi.update(order.id, { ...order, status: "CANCELLED" });
      await fetchData();
    } catch (err) {
      setError("Lỗi khi hủy lệnh: " + err.message);
    } finally {
      setSaving(false);
    }
  }, [fetchData]);

  useEffect(() => {
    const params = new URLSearchParams(location.search);
    const viewOrderId = Number(params.get("viewOrderId") || 0);
    if (!viewOrderId || !Array.isArray(orders) || orders.length === 0) return;
    const found = orders.find((order) => Number(order.id) === viewOrderId);
    if (!found) return;

    setSelectedOrderForView(found);
    setModal("order_detail");

    params.delete("viewOrderId");
    const nextSearch = params.toString();
    navigate(
      { pathname: location.pathname, search: nextSearch ? `?${nextSearch}` : "" },
      { replace: true }
    );
  }, [location.pathname, location.search, navigate, orders]);

  const openDateRangePicker = useCallback(() => {
    setModal("date_range");
  }, []);

  const applyDateRange = useCallback(({ dateFrom: nextDateFrom, dateTo: nextDateTo }) => {
    setDateFrom(nextDateFrom || "");
    setDateTo(nextDateTo || "");
    setModal(null);
  }, []);

  const defaultDateFilter = useMemo(() => toDateText(new Date()), []);
  const dateFilterButtonLabel = useMemo(
    () => getDateRangeButtonLabel(dateFrom, dateTo, { todayDateText: defaultDateFilter }),
    [dateFrom, dateTo, defaultDateFilter]
  );
  const canResetToolbarFilters = orderStatusFilter !== "ALL" || orderSortBy !== "CREATED_DESC" || Boolean(dateFrom || dateTo);

  const resetToolbarFilters = useCallback(() => {
    setOrderStatusFilter("ALL");
    setOrderSortBy("CREATED_DESC");
    setDateFrom("");
    setDateTo("");
  }, []);

  return (
    <div className="reception-dashboard">
      <div className="stats-bar">
        <div className="stat-card">
          <div className="stat-value">{activeOrdersCount}</div>
          <div className="stat-label">Xe đang ở xưởng</div>
        </div>
        <div className="stat-card">
          <div className="stat-value">{orders.filter((order) => ["NEW", "RECEPTION"].includes(order.status)).length}</div>
          <div className="stat-label">Mới tiếp nhận</div>
        </div>
        <div className="stat-card">
          <div className="stat-value" style={{ color: "#ffb547" }}>{orders.filter((order) => order.status === "WAITING_APPROVAL").length}</div>
          <div className="stat-label">Chờ báo giá</div>
        </div>
        <div className="stat-card">
          <div className="stat-value" style={{ color: "#05cd99" }}>{orders.filter((order) => order.status === "READY").length}</div>
          <div className="stat-label">Sẵn sàng bàn giao</div>
        </div>
      </div>

      {error && (
        <div style={{ padding: "12px", background: "#fee", color: "#e6522c", borderRadius: "8px", marginBottom: "16px", fontWeight: 600 }}>
          Lỗi: {error}
        </div>
      )}

      <OrdersTableContent
        navigate={navigate}
        orderStatusFilter={orderStatusFilter}
        setOrderStatusFilter={setOrderStatusFilter}
        orderStatusOptions={orderStatusOptions}
        orderSortBy={orderSortBy}
        setOrderSortBy={setOrderSortBy}
        openDateRangePicker={openDateRangePicker}
        dateFilterButtonLabel={dateFilterButtonLabel}
        canResetToolbarFilters={canResetToolbarFilters}
        onResetToolbarFilters={resetToolbarFilters}
        filteredOrders={filteredOrders}
        loading={loading}
        pagedOrders={pagedOrders}
        setSelectedOrderForView={setSelectedOrderForView}
        setModal={setModal}
        getOrderCustomerName={getOrderCustomerName}
        getOrderVehiclePlate={getOrderVehiclePlate}
        statusPermitUpdate={statusPermitUpdate}
        handleCompleteOrder={handleCompleteOrder}
        handleCancelOrder={handleCancelOrder}
        ordersPage={ordersPage}
        ordersPageSize={ordersPageSize}
        setOrdersPage={setOrdersPage}
        setOrdersPageSize={setOrdersPageSize}
        getStatusBadge={getStatusBadge}
      />

      {modal === "date_range" && (
        <DateRangePickerModal
          isOpen={modal === "date_range"}
          onClose={() => setModal(null)}
          initialDateFrom={dateFrom}
          initialDateTo={dateTo}
          onApply={applyDateRange}
        />
      )}

      {modal === "order_detail" && selectedOrderForView && (
        <ServiceOrderDetailModal
          order={selectedOrderForView}
          customers={customers}
          vehicles={vehicles}
          staff={staff}
          onRefresh={fetchData}
          onClose={() => { setModal(null); setSelectedOrderForView(null); }}
        />
      )}
    </div>
  );
}

function OrdersTableContent({
  navigate,
  orderStatusFilter,
  setOrderStatusFilter,
  orderStatusOptions,
  orderSortBy,
  setOrderSortBy,
  openDateRangePicker,
  dateFilterButtonLabel,
  canResetToolbarFilters,
  onResetToolbarFilters,
  filteredOrders,
  loading,
  pagedOrders,
  setSelectedOrderForView,
  setModal,
  getOrderCustomerName,
  getOrderVehiclePlate,
  statusPermitUpdate,
  handleCompleteOrder,
  handleCancelOrder,
  ordersPage,
  ordersPageSize,
  setOrdersPage,
  setOrdersPageSize,
  getStatusBadge,
}) {
  const orderFilterOptions = [toStatusOptionVi("ALL"), ...orderStatusOptions.map(toStatusOptionVi)];
  const orderSortOptions = [
    { value: "CREATED_DESC", label: "Mới nhất" },
    { value: "CREATED_ASC", label: "Cũ nhất" },
    { value: "SCHEDULE_DESC", label: "Lịch hẹn mới nhất" },
    { value: "CHECKIN_DESC", label: "Check-in mới nhất" },
    { value: "CHECKIN_ASC", label: "Check-in cũ nhất" },
  ];

  return (
    <>
      <div className="table-toolbar">
        <button className="btn-primary" onClick={() => navigate("/workshop/reception/create")}>+ Tiếp nhận xe mới</button>
        <div className="toolbar-controls">
          <FilterDropdown
            id="order-status-filter"
            label="Trạng thái"
            value={orderStatusFilter}
            options={orderFilterOptions}
            onChange={setOrderStatusFilter}
            minWidth={160}
          />
          <FilterDropdown
            id="order-sort"
            label="Sắp xếp"
            value={orderSortBy}
            options={orderSortOptions}
            onChange={setOrderSortBy}
            minWidth={220}
          />
          <button type="button" className="toolbar-action-btn toolbar-action-outline" onClick={openDateRangePicker}>
            {dateFilterButtonLabel || "Chọn khoảng ngày"}
          </button>
          <button type="button" className="toolbar-action-btn toolbar-action-ghost" onClick={onResetToolbarFilters} disabled={!canResetToolbarFilters}>
            Xóa lọc
          </button>
        </div>
        <span className="result-count">{filteredOrders.length} lệnh sửa chữa</span>
      </div>

      <div className="workshop-table-card">
        {loading ? (
          <div className="loading-state"><div className="spinner" /><p>Đang tải danh sách lệnh...</p></div>
        ) : (
          <table className="admin-table">
            <thead>
              <tr>
                <th>Mã lệnh</th>
                <th>Khách hàng</th>
                <th>Biển số</th>
                <th>Trạng thái</th>
                <th>Thời điểm vào</th>
                <th>Hẹn trả</th>
                <th className="th-center" style={{ width: "120px" }}>Thao tác</th>
              </tr>
            </thead>
            <tbody>
              {filteredOrders.length === 0 ? (
                <tr><td colSpan="7" style={{ textAlign: "center", padding: "40px", color: "#a3aed0" }}>Chưa có lệnh sửa chữa nào.</td></tr>
              ) : pagedOrders.map((order) => (
                <tr
                  key={order.id}
                  onClick={() => {
                    setSelectedOrderForView(order);
                    setModal("order_detail");
                  }}
                  className="clickable-row"
                  style={{ cursor: "pointer" }}
                >
                  <td className="td-id">{order.code}</td>
                  <td style={{ fontWeight: 600 }}>{getOrderCustomerName(order)}</td>
                  <td>{getOrderVehiclePlate(order)}</td>
                  <td>
                    <span className={`badge ${getStatusBadge(order.status)}`}>
                      {getStatusLabelVi(order.status, order.status)}
                    </span>
                  </td>
                  <td>{order.checkInTime ? new Date(order.checkInTime).toLocaleString([], { dateStyle: "short", timeStyle: "short" }) : "-"}</td>
                  <td>{order.estimatedHandover ? new Date(order.estimatedHandover).toLocaleString([], { dateStyle: "short", timeStyle: "short" }) : "Chưa hẹn"}</td>
                  <td className="td-center" onClick={(e) => e.stopPropagation()} style={{ width: "120px" }}>
                    <div style={{ display: "flex", gap: "8px", justifyContent: "center", alignItems: "center", width: "100%" }}>
                      {order.status === "WAITING_APPROVAL" && (
                        <button className="btn-primary btn-sm" onClick={() => navigate(`/workshop/reception/orders/${order.id}/quote`)}>✍ Báo giá</button>
                      )}
                      {order.status === "READY" && (
                        <button className="btn-success btn-sm" onClick={() => handleCompleteOrder(order)}>🚗 Bàn giao</button>
                      )}
                      {statusPermitUpdate(order.status) && (
                        <button
                          type="button"
                          style={{
                            color: "#ee5d50",
                            border: "none",
                            background: "transparent",
                            padding: "0 6px",
                            margin: 0,
                            height: 30,
                            display: "inline-flex",
                            alignItems: "center",
                            justifyContent: "center",
                            lineHeight: 1,
                            fontWeight: 700,
                            cursor: "pointer",
                          }}
                          title="Hủy lệnh"
                          onClick={() => handleCancelOrder(order)}
                        >
                          Hủy
                        </button>
                      )}
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>
      <TablePagination
        totalItems={filteredOrders.length}
        currentPage={ordersPage}
        pageSize={ordersPageSize}
        onPageChange={setOrdersPage}
        onPageSizeChange={(size) => {
          setOrdersPageSize(size);
          setOrdersPage(1);
        }}
      />
    </>
  );
}
