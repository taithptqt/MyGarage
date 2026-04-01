import React, { useCallback, useEffect, useState, useMemo } from "react";
import { useNavigate, useLocation } from "react-router-dom";
import Calendar from "react-calendar";
import "react-calendar/dist/Calendar.css";
import { serviceOrderApi } from "@features/workshop/api/serviceOrderApi";
import { getAllCustomers, getAllVehicles, getAllCustomerVehicles, createCustomer, createVehicle, createCustomerVehicle } from "@features/admin/api/customerApi";
import { getAllStaff, getAllRoles } from "@features/admin/api/staffApi";
import { Modal } from "@features/admin/components/AdminModals";
import { getStatusBadge } from "@features/admin/utils/adminUtils";
import { useSearch } from "@shared/core/context/SearchContext";
import { useBranch } from "@shared/core/context/BranchContext";
import { useAuth } from "@shared/core/context/AuthContext";
import TablePagination from "@shared/components/TablePagination";
import {
  getBranchAppointments,
  cancelAppointment,
  bookAppointment,
  getAllStandardServices,
} from "@features/appointments/api/appointmentApi";
import AppointmentForm from "@features/workshop/components/AppointmentForm";
import ServiceOrderDetailModal from "@features/workshop/components/ServiceOrderDetailModal";
import ReceptionAppointmentsSection from "@features/workshop/components/ReceptionAppointmentsSection";

export default function ReceptionOrdersSection() {
  const navigate = useNavigate();
  const location = useLocation();
  const [orders, setOrders] = useState([]);
  const [customers, setCustomers] = useState([]);
  const [vehicles, setVehicles] = useState([]);
  const [customerVehicles, setCustomerVehicles] = useState([]);
  const [staff, setStaff] = useState([]);
  const [loading, setLoading] = useState(false);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState("");
  const [modal, setModal] = useState(null); // 'create', 'detail'
  const [appointments, setAppointments] = useState([]);
  const [apptLoading, setApptLoading] = useState(false);
  const [actingAppt, setActingAppt] = useState(null);
  const [services, setServices] = useState([]); // New services state
  const [selectedOrderForView, setSelectedOrderForView] = useState(null);
  const [successAppt, setSuccessAppt] = useState(null);
  const [orderStatusFilter, setOrderStatusFilter] = useState("ALL");
  const [orderSortBy, setOrderSortBy] = useState("CREATED_DESC");
  const [dateFrom, setDateFrom] = useState("");
  const [dateTo, setDateTo] = useState("");
  const [draftDateFrom, setDraftDateFrom] = useState("");
  const [draftDateTo, setDraftDateTo] = useState("");
  const [apptStatusFilter, setApptStatusFilter] = useState("ALL");
  const [apptSortBy, setApptSortBy] = useState("NEWEST");
  const [ordersPage, setOrdersPage] = useState(1);
  const [ordersPageSize, setOrdersPageSize] = useState(10);
  const [apptsPage, setApptsPage] = useState(1);
  const [apptsPageSize, setApptsPageSize] = useState(10);

  const { searchQuery } = useSearch();
  const { selectedBranchId } = useBranch();
  const { user } = useAuth();
  const effectiveBranchId = Number(selectedBranchId || user?.branchId || 0);
  const activeTab = useMemo(() => {
    const tab = new URLSearchParams(location.search).get("tab");
    return tab === "appointments" ? "appointments" : "orders";
  }, [location.search]);

  const getApptStatusBadge = (status) => {
    const s = String(status || "").toUpperCase();
    if (s === 'PENDING') return 'badge-warning';
    if (s === 'CONFIRMED') return 'badge-active';
    if (s === 'ARRIVED' || s === 'CHECKED_IN') return 'badge-success';
    if (s === 'CANCELED' || s === 'CANCELLED') return 'badge-danger';
    if (s === 'COMPLETED') return 'badge-completed';
    return 'badge-inactive';
  };

  const normalizeStatus = (status) => String(status || "").trim().toUpperCase();
  const toDateText = useCallback((dateObj) => {
    if (!(dateObj instanceof Date) || Number.isNaN(dateObj.getTime())) return "";
    const y = dateObj.getFullYear();
    const m = String(dateObj.getMonth() + 1).padStart(2, "0");
    const d = String(dateObj.getDate()).padStart(2, "0");
    return `${y}-${m}-${d}`;
  }, []);
  const toDisplayDate = useCallback((dateText) => {
    if (!dateText) return "--/--/----";
    const d = new Date(`${dateText}T00:00:00`);
    if (Number.isNaN(d.getTime())) return "--/--/----";
    return d.toLocaleDateString("vi-VN");
  }, []);

  const toStartOfDay = useCallback((dateText) => {
    if (!dateText) return null;
    const d = new Date(`${dateText}T00:00:00`);
    return Number.isNaN(d.getTime()) ? null : d;
  }, []);

  const toEndOfDay = useCallback((dateText) => {
    if (!dateText) return null;
    const d = new Date(`${dateText}T23:59:59.999`);
    return Number.isNaN(d.getTime()) ? null : d;
  }, []);

  const formatApptDate = useCallback((value) => {
    if (!value) return "--/--/----";
    const d = new Date(value);
    return Number.isNaN(d.getTime()) ? "--/--/----" : d.toLocaleDateString("vi-VN");
  }, []);

  const formatApptTime = useCallback((value) => {
    if (!value) return "--:--";
    const d = new Date(value);
    return Number.isNaN(d.getTime()) ? "--:--" : d.toLocaleTimeString("vi-VN", { hour: "2-digit", minute: "2-digit" });
  }, []);

  const fetchData = useCallback(async () => {
    setLoading(true);
    setError("");
    try {
      const [oRes, cRes, vRes, cvRes, sRes, rRes, servicesRes] = await Promise.all([
        serviceOrderApi.getAll(),
        getAllCustomers(),
        getAllVehicles(),
        getAllCustomerVehicles(),
        getAllStaff(),
        getAllRoles(),
        getAllStandardServices() // Fetch services
      ]);
      setOrders(oRes || []);
      setCustomers(cRes || []);
      setVehicles(vRes || []);
      setCustomerVehicles(cvRes || []);
      setServices(servicesRes || []);
      
      // 1. Filter for RECEPTIONIST for selection (Service Advisor)
      const filteredStaff = sRes.filter(s => {
        const role = rRes.find(r => r.id === s.roleId);
        const isMatchedRole = role && role.roleCode === "RECEPTIONIST";
        const isMatchedBranch = !effectiveBranchId || s.branchId === effectiveBranchId;
        return isMatchedRole && isMatchedBranch;
      });
      setStaff(filteredStaff);
    } catch (err) {
      setError(err.message || "Failed to load reception data.");
    } finally {
      setLoading(false);
    }
  }, [effectiveBranchId]);

  useEffect(() => {
    fetchData();
  }, [fetchData]);

  const fetchAppointments = useCallback(async () => {
    if (!effectiveBranchId) return;
    setApptLoading(true);
    try {
      const data = await getBranchAppointments(effectiveBranchId);
      setAppointments(Array.isArray(data) ? data : []);
    } catch (err) {
      setError(err.message || "Không thể tải dữ liệu lịch hẹn.");
    } finally {
      setApptLoading(false);
    }
  }, [effectiveBranchId]);

  const fetchAppointmentsSilent = useCallback(async () => {
    if (!effectiveBranchId) return;
    try {
      const data = await getBranchAppointments(effectiveBranchId);
      setAppointments(Array.isArray(data) ? data : []);
    } catch (err) {
      setError(err.message || "Không thể tải dữ liệu lịch hẹn.");
    }
  }, [effectiveBranchId]);

  useEffect(() => {
    if (activeTab === "appointments") {
      fetchAppointments();
    }
  }, [activeTab, fetchAppointments]);

  const getOrderCustomerName = useCallback((order) => {
    if (order.customerName) return order.customerName;
    const customer = customers.find((c) => Number(c.id) === Number(order.customerId));
    if (customer?.fullName) return customer.fullName;
    return "-";
  }, [customers]);

  const getOrderVehiclePlate = useCallback((order) => {
    if (order.vehicleLicensePlate) return order.vehicleLicensePlate;
    const vehicle = vehicles.find((v) => Number(v.id) === Number(order.vehicleId));
    return vehicle?.licensePlate || "N/A";
  }, [vehicles]);

  const orderStatusOptions = useMemo(() => {
    const statuses = Array.from(new Set((orders || []).map((o) => normalizeStatus(o.status)).filter(Boolean)));
    return statuses.sort((a, b) => a.localeCompare(b));
  }, [orders]);

  const filteredOrders = useMemo(() => {
    const query = String(searchQuery || "").trim().toLowerCase();
    const queryCompact = query.replace(/[\s.-]/g, "");
    const startDate = toStartOfDay(dateFrom);
    const endDate = toEndOfDay(dateTo);
    const list = (orders || []).filter((o) => {
      const byStatus = orderStatusFilter === "ALL" || normalizeStatus(o.status) === orderStatusFilter;
      const createdAtTime = new Date(o.createdAt || 0).getTime();
      const byDate =
        (!startDate || createdAtTime >= startDate.getTime()) &&
        (!endDate || createdAtTime <= endDate.getTime());
      const orderPlate = String(getOrderVehiclePlate(o) || "").toLowerCase();
      const orderPlateCompact = orderPlate.replace(/[\s.-]/g, "");
      const byQuery =
        !query ||
        (o.code || "").toLowerCase().includes(query) ||
        getOrderCustomerName(o).toLowerCase().includes(query) ||
        (o.status || "").toLowerCase().includes(query) ||
        orderPlate.includes(query) ||
        (queryCompact && orderPlateCompact.includes(queryCompact));
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
      } else {
        if (createdB !== createdA) return createdB - createdA;
      }

      return Number(b.id || 0) - Number(a.id || 0);
    });
  }, [orders, searchQuery, getOrderCustomerName, orderStatusFilter, orderSortBy, dateFrom, dateTo, normalizeStatus, toStartOfDay, toEndOfDay]);
  const pagedOrders = useMemo(() => {
    const start = (ordersPage - 1) * ordersPageSize;
    return filteredOrders.slice(start, start + ordersPageSize);
  }, [filteredOrders, ordersPage, ordersPageSize]);

  const activeOrdersCount = useMemo(() => orders.filter(o => !["COMPLETED", "CANCELLED"].includes(o.status)).length, [orders]);

  const handleSaveOrder = async (formData) => {
    setSaving(true);
    setError("");
    try {
      let finalCustomerId = formData.customerId;
      let finalVehicleId = formData.vehicleId;

      // Handle Quick Order (Guest/Walk-in)
      if (formData.isQuickOrder) {
        // 1. Find or Create Customer
        let customer = customers.find(c => c.phone === formData.guestPhone);
        if (!customer) {
          customer = await createCustomer({
            fullName: formData.guestName,
            phone: formData.guestPhone,
            isRegistered: false,
            status: "active"
          });
        }
        finalCustomerId = customer.id;

        // 2. Find or Create Vehicle
        let vehicle = vehicles.find(v => v.licensePlate === formData.guestVehiclePlate);
        if (!vehicle) {
          vehicle = await createVehicle({
            licensePlate: formData.guestVehiclePlate,
            modelId: 1, // Fallback placeholder to satisfy DB constraint
            status: "active"
          });
          await createCustomerVehicle({
            customerId: finalCustomerId,
            vehicleId: vehicle.id,
            isCurrentOwner: true,
            ownershipStartDate: new Date().toISOString().split('T')[0]
          });
        }
        finalVehicleId = vehicle.id;
      }

      let finalCustomerNotes = formData.customerNotes || "Không có ghi chú";

      const payload = {
        ...formData,
        customerId: finalCustomerId ? Number(finalCustomerId) : null,
        vehicleId: finalVehicleId ? Number(finalVehicleId) : null,
        serviceAdvisorId: formData.serviceAdvisorId ? Number(formData.serviceAdvisorId) : null,
        estimatedHandover: formData.estimatedHandover || null,
        checkInTime: formData.checkInTime || new Date().toISOString(),
        customerNotes: finalCustomerNotes,
        code: serviceOrderApi.generateCode(),
        status: "NEW",
        originBranchId: selectedBranchId ? Number(selectedBranchId) : 1,
        executeBranchId: selectedBranchId ? Number(selectedBranchId) : 1,
        CreatedBy: user?.id
      };
      
      // Remove temporary frontend-only flags
      delete payload.isQuickOrder;
      delete payload.guestVehiclePlate;
      delete payload.serviceIds;

      const createdOrder = await serviceOrderApi.create(payload);

      // Create ServiceOrderDetails 
      if (formData.serviceIds && formData.serviceIds.length > 0 && createdOrder && createdOrder.id) {
        const uniqueServiceIds = Array.from(new Set(formData.serviceIds.map(Number).filter((x) => x > 0)));
        const detailsPromises = uniqueServiceIds.map((sid) => {
          const service = services.find((s) => Number(s.id) === Number(sid));
          if (!service) return Promise.resolve();
          return serviceOrderApi.createDetail({
            serviceOrderId: createdOrder.id,
            standardServiceId: sid,
            itemType: "LABOR",
            itemName: service.Name || service.name,
            description: "Dịch vụ khách yêu cầu trước",
            quantity: 1,
            unit: "Lần",
            unitPriceAtTime: service.BasePrice || service.basePrice || 0,
            subTotal: service.BasePrice || service.basePrice || 0,
            taxRate: service.TaxRate || service.taxRate || 0,
            finalAmount: service.BasePrice || service.basePrice || 0,
            status: "APPROVED"
          });
        });
        await Promise.allSettled(detailsPromises);
      }

      setModal(null);
      await fetchData();
    } catch (err) {
      setError(err.message || "Failed to create service order.");
    } finally {
      setSaving(false);
    }
  };
  const handleCompleteOrder = async (order) => {
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
  };

  const handleCancelOrder = async (order) => {
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
  };

  const statusPermitUpdate = (status) => !["COMPLETED", "CANCELLED"].includes(status);

  const handleApptAction = async (id) => {
    setActingAppt(id);
    try {
      const reason = window.prompt("Lý do hủy lịch:", "Nhân viên hủy");
      if (reason === null) return;
      await cancelAppointment(id, reason);
      setAppointments(prev => prev.map(a => a.id === id ? { ...a, status: 'CANCELED' } : a));
    } catch (err) {
      setError("Thao tác lịch hẹn thất bại: " + err.message);
    } finally {
      setActingAppt(null);
    }
  };
  const apptStatusOptions = useMemo(() => {
    const statuses = Array.from(new Set((appointments || []).map((a) => normalizeStatus(a.status)).filter(Boolean)));
    return statuses.sort((a, b) => a.localeCompare(b));
  }, [appointments]);

  const filteredAppts = useMemo(() => {
    const query = String(searchQuery || "").trim().toLowerCase();
    const queryCompact = query.replace(/[\s.-]/g, "");
    const startDate = toStartOfDay(dateFrom);
    const endDate = toEndOfDay(dateTo);
    const list = (appointments || []).filter((a) => {
      const byStatus = apptStatusFilter === "ALL" || normalizeStatus(a.status) === apptStatusFilter;
      const scheduledTime = new Date(a.startTime || 0).getTime();
      const byDate =
        (!startDate || scheduledTime >= startDate.getTime()) &&
        (!endDate || scheduledTime <= endDate.getTime());
      const apptPlate = String(a.vehicleLicensePlate || a.licensePlate || "").toLowerCase();
      const apptPlateCompact = apptPlate.replace(/[\s.-]/g, "");
      const byQuery =
        !query ||
        (a.customerName || "").toLowerCase().includes(query) ||
        (a.customerPhone || "").toLowerCase().includes(query) ||
        apptPlate.includes(query) ||
        (queryCompact && apptPlateCompact.includes(queryCompact));
      return byStatus && byDate && byQuery;
    });

    return list.sort((a, b) => {
      const aTime = new Date(a.startTime || 0).getTime();
      const bTime = new Date(b.startTime || 0).getTime();
      const aName = String(a.customerName || "");
      const bName = String(b.customerName || "");

      if (apptSortBy === "OLDEST") return aTime - bTime;
      if (apptSortBy === "NAME_ASC") return aName.localeCompare(bName, "vi");
      if (apptSortBy === "NAME_DESC") return bName.localeCompare(aName, "vi");
      return bTime - aTime;
    });
  }, [appointments, searchQuery, apptStatusFilter, apptSortBy, dateFrom, dateTo, toStartOfDay, toEndOfDay]);
  const pagedAppts = useMemo(() => {
    const start = (apptsPage - 1) * apptsPageSize;
    return filteredAppts.slice(start, start + apptsPageSize);
  }, [filteredAppts, apptsPage, apptsPageSize]);

  useEffect(() => {
    setOrdersPage(1);
  }, [searchQuery, orderStatusFilter, orderSortBy, dateFrom, dateTo, orders.length]);

  useEffect(() => {
    setApptsPage(1);
  }, [searchQuery, apptStatusFilter, apptSortBy, dateFrom, dateTo, appointments.length]);

  useEffect(() => {
    const params = new URLSearchParams(location.search);
    const viewOrderId = Number(params.get("viewOrderId") || 0);
    if (!viewOrderId || !Array.isArray(orders) || orders.length === 0) return;
    const found = orders.find((o) => Number(o.id) === viewOrderId);
    if (!found) return;

    setSelectedOrderForView(found);
    setModal("order_detail");

    params.delete("viewOrderId");
    const nextSearch = params.toString();
    navigate(
      { pathname: location.pathname, search: nextSearch ? `?${nextSearch}` : "" },
      { replace: true }
    );
  }, [location.pathname, location.search, orders, navigate]);

  const openDateRangePicker = () => {
    setDraftDateFrom(dateFrom);
    setDraftDateTo(dateTo);
    setModal("date_range");
  };

  const applyDateRange = () => {
    setDateFrom(draftDateFrom || "");
    setDateTo(draftDateTo || "");
    setModal(null);
  };

  const clearDateRange = () => {
    setDateFrom("");
    setDateTo("");
    setDraftDateFrom("");
    setDraftDateTo("");
  };

  const quickSetDateRange = (days) => {
    const now = new Date();
    const to = now.toISOString().slice(0, 10);
    const fromDate = new Date(now);
    fromDate.setDate(now.getDate() - (days - 1));
    const from = fromDate.toISOString().slice(0, 10);
    setDraftDateFrom(from);
    setDraftDateTo(to);
  };

  const handleRangeDayClick = (dateObj) => {
    const picked = toDateText(dateObj);
    if (!picked) return;

    if (!draftDateFrom || (draftDateFrom && draftDateTo)) {
      setDraftDateFrom(picked);
      setDraftDateTo("");
      return;
    }

    if (picked < draftDateFrom) {
      return;
    }

    setDraftDateTo(picked);
  };

  const tileClassName = ({ date, view }) => {
    if (view !== "month") return "";
    const text = toDateText(date);
    if (!text || !draftDateFrom) return "";
    if (text === draftDateFrom) return "range-start";
    if (draftDateTo && text === draftDateTo) return "range-end";
    if (draftDateTo && text > draftDateFrom && text < draftDateTo) return "range-middle";
    return "";
  };

  return (
    <div className="reception-dashboard">
      <div className="stats-bar">
        <div className="stat-card">
          <div className="stat-value">{activeOrdersCount}</div>
          <div className="stat-label">Xe đang ở xưởng</div>
        </div>
        <div className="stat-card">
          <div className="stat-value">{orders.filter(o => ["NEW", "RECEPTION"].includes(o.status)).length}</div>
          <div className="stat-label">Mới tiếp nhận</div>
        </div>
        <div className="stat-card">
          <div className="stat-value" style={{ color: '#ffb547' }}>{orders.filter(o => o.status === "WAITING_APPROVAL").length}</div>
          <div className="stat-label">Chờ báo giá</div>
        </div>
        <div className="stat-card">
          <div className="stat-value" style={{ color: '#05cd99' }}>{orders.filter(o => o.status === "READY").length}</div>
          <div className="stat-label">Sẵn sàng bàn giao</div>
        </div>
      </div>
      {error && (
        <div style={{ padding: '12px', background: '#fee', color: '#e6522c', borderRadius: '8px', marginBottom: '16px', fontWeight: 600 }}>
          ? {error}
        </div>
      )}

      {activeTab === "orders" ? (
        <OrdersTableContent
          navigate={navigate}
          orderStatusFilter={orderStatusFilter}
          setOrderStatusFilter={setOrderStatusFilter}
          orderStatusOptions={orderStatusOptions}
          orderSortBy={orderSortBy}
          setOrderSortBy={setOrderSortBy}
          openDateRangePicker={openDateRangePicker}
          dateFrom={dateFrom}
          dateTo={dateTo}
          clearDateRange={clearDateRange}
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
      ) : (
        <ReceptionAppointmentsSection
          setModal={setModal}
          apptStatusFilter={apptStatusFilter}
          setApptStatusFilter={setApptStatusFilter}
          apptStatusOptions={apptStatusOptions}
          apptSortBy={apptSortBy}
          setApptSortBy={setApptSortBy}
          openDateRangePicker={openDateRangePicker}
          dateFrom={dateFrom}
          dateTo={dateTo}
          clearDateRange={clearDateRange}
          filteredAppts={filteredAppts}
          apptLoading={apptLoading}
          pagedAppts={pagedAppts}
          formatApptDate={formatApptDate}
          formatApptTime={formatApptTime}
          getApptStatusBadge={getApptStatusBadge}
          handleApptAction={handleApptAction}
          actingAppt={actingAppt}
          apptsPage={apptsPage}
          apptsPageSize={apptsPageSize}
          setApptsPage={setApptsPage}
          setApptsPageSize={setApptsPageSize}
        />
      )}

      {modal === "create_appt" && (
        <Modal title="Tạo lịch hẹn mới (Lễ tân)" onClose={() => setModal(null)} className="modal-lg">
           {error && <div style={{ padding: '12px', background: '#fee', color: '#e6522c', borderRadius: '8px', marginBottom: '16px', fontWeight: 600 }}>? {error}</div>}
           <AppointmentForm 
             customers={customers} 
            vehicles={vehicles}
            customerVehicles={customerVehicles}
            services={services}
            branchId={selectedBranchId}
             onSubmit={handleSaveAppointment} 
             onCancel={() => setModal(null)} 
             loading={saving} 
           />
        </Modal>
      )}

      {modal === "appt_success" && successAppt && (
        <Modal title="Đặt lịch thành công" onClose={() => { setModal(null); setSuccessAppt(null); }}>
          <div className="success-summary" style={{ padding: '20px', textAlign: 'center' }}>
            <div style={{ fontSize: '48px', color: '#05cd99', marginBottom: '16px' }}>?</div>
            <h3 style={{ color: '#2b3674', marginTop: 0 }}>Đã xác nhận lịch hẹn!</h3>
            <div style={{ background: '#f4f7fe', padding: '16px', borderRadius: '12px', textAlign: 'left', marginTop: '20px', lineHeight: '1.8' }}>
              <p style={{margin: 0}}><strong>Khách hàng:</strong> {successAppt.customerName} - {successAppt.customerPhone}</p>
              <p style={{margin: 0}}><strong>Biển số xe:</strong> <span style={{ textTransform: 'uppercase', fontWeight: 'bold' }}>{successAppt.vehicleLicensePlate}</span></p>
              <p style={{margin: 0}}><strong>Thời gian nhận xe:</strong> <span style={{ color: '#4318ff', fontWeight: 'bold' }}>{formatApptTime(successAppt.startTime)}</span> ngày {formatApptDate(successAppt.startTime)}</p>
              <p style={{margin: 0}}><strong>Dịch vụ yêu cầu:</strong> {successAppt.services.length > 0 ? successAppt.services.map(s => s.Name || s.name).join(', ') : 'Chưa định trước'}</p>
            </div>
            <button className="btn-primary" style={{ marginTop: '24px', width: '100%', padding: '12px' }} onClick={() => { setModal(null); setSuccessAppt(null); }}>
              Đóng và Quay lại
            </button>
          </div>
        </Modal>
      )}

      {modal === "date_range" && (
        <Modal title="Lọc lịch theo khoảng ngày" onClose={() => setModal(null)} className="date-range-modal">
          <div className="date-range-picker-shell">
            <div className="date-range-summary">
              <div className="date-pill">
                <span className="date-pill-label">Bắt đầu</span>
                <strong>{toDisplayDate(draftDateFrom)}</strong>
              </div>
              <div className="date-pill-arrow">?</div>
              <div className="date-pill">
                <span className="date-pill-label">Kết thúc</span>
                <strong>{toDisplayDate(draftDateTo)}</strong>
              </div>
            </div>

            <div className="date-range-hint">
              {!draftDateFrom && "Chọn ngày bắt đầu"}
              {draftDateFrom && !draftDateTo && "Chọn ngày kết thúc (không nhỏ hơn ngày bắt đầu)"}
              {draftDateFrom && draftDateTo && "Đã chọn xong khoảng ngày"}
            </div>

            <div className="date-range-calendar-wrap">
              <Calendar
                locale="vi-VN"
                className="date-range-calendar"
                onClickDay={handleRangeDayClick}
                tileClassName={tileClassName}
                tileDisabled={({ date, view }) =>
                  view === "month" &&
                  Boolean(draftDateFrom && !draftDateTo && toDateText(date) < draftDateFrom)
                }
                value={draftDateTo ? [new Date(`${draftDateFrom}T00:00:00`), new Date(`${draftDateTo}T00:00:00`)] : (draftDateFrom ? new Date(`${draftDateFrom}T00:00:00`) : new Date())}
              />
            </div>

            <div className="date-range-quick-actions">
              <button type="button" className="date-quick-btn" onClick={() => quickSetDateRange(7)}>7 ngày gần nhất</button>
              <button type="button" className="date-quick-btn" onClick={() => quickSetDateRange(30)}>30 ngày gần nhất</button>
              <button
                type="button"
                className="date-quick-btn"
                onClick={() => {
                  const now = new Date();
                  const from = new Date(now.getFullYear(), now.getMonth(), 1).toISOString().slice(0, 10);
                  const to = new Date(now.getFullYear(), now.getMonth() + 1, 0).toISOString().slice(0, 10);
                  setDraftDateFrom(from);
                  setDraftDateTo(to);
                }}
              >
                Tháng này
              </button>
            </div>

            <div className="date-range-footer">
              <button type="button" className="btn-secondary-outline" style={{ height: "42px", minWidth: "90px", borderRadius: "10px", padding: "0 16px" }} onClick={() => setModal(null)}>
                ??ng
              </button>
              <button
                type="button"
                className="btn-primary"
                style={{ height: "42px", minWidth: "110px", borderRadius: "10px", padding: "0 18px" }}
                onClick={applyDateRange}
                disabled={!draftDateFrom || !draftDateTo}
              >
                Áp dụng
              </button>
            </div>
          </div>
        </Modal>
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
  dateFrom,
  dateTo,
  clearDateRange,
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
  return (
    <>
      <div className="table-toolbar">
        <button className="btn-primary" onClick={() => navigate("/workshop/reception/create")}>+ Tiếp nhận xe mới</button>
        <div className="toolbar-controls">
          <label htmlFor="order-status-filter" className="toolbar-label">Trạng thái:</label>
          <select id="order-status-filter" className="toolbar-select" value={orderStatusFilter} onChange={(e) => setOrderStatusFilter(e.target.value)} style={{ minWidth: "160px" }}>
            <option value="ALL">{"T\u1ea5t c\u1ea3"}</option>
            {orderStatusOptions.map((status) => (<option key={status} value={status}>{status}</option>))}
          </select>
          <label htmlFor="order-sort" className="toolbar-label">Sắp xếp:</label>
          <select id="order-sort" className="toolbar-select" value={orderSortBy} onChange={(e) => setOrderSortBy(e.target.value)} style={{ minWidth: "220px" }}>
            <option value="CREATED_DESC">Mới nhất</option>
            <option value="CREATED_ASC">Cũ nhất</option>
            <option value="SCHEDULE_DESC">Lịch hẹn mới nhất</option>
            <option value="CHECKIN_DESC">Check-in mới nhất</option>
            <option value="CHECKIN_ASC">Check-in cũ nhất</option>
          </select>
          <button type="button" className="toolbar-action-btn toolbar-action-outline" onClick={openDateRangePicker}>
            Chọn khoảng ngày
          </button>
          {(dateFrom || dateTo) && (
            <button type="button" className="toolbar-action-btn toolbar-action-ghost" onClick={clearDateRange}>
              Xóa lọc
            </button>
          )}
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
                      {order.status}
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
                          className="btn-icon"
                          style={{ color: "#ee5d50" }}
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














