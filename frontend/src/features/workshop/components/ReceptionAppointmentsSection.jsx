import React from "react";
import TablePagination from "@shared/components/TablePagination";
import FilterDropdown from "@shared/components/FilterDropdown";
import { getStatusLabelVi, toStatusOptionVi } from "@shared/core/utils/statusLabelUtils";

function buildRowInteraction(appt, onAppointmentClick) {
  if (!onAppointmentClick) return {};

  const openDetail = () => onAppointmentClick(appt);
  return {
    role: "button",
    tabIndex: 0,
    onClick: openDetail,
    onKeyDown: (event) => {
      if (event.key === "Enter" || event.key === " ") {
        event.preventDefault();
        openDetail();
      }
    },
  };
}

function canConfirmAppointment(status) {
  const normalized = String(status || "").trim().toUpperCase();
  return normalized === "PENDING" || normalized === "LATE";
}

function canCheckInAppointment(status) {
  const normalized = String(status || "").trim().toUpperCase();
  return normalized === "CONFIRMED" || normalized === "LATE";
}

function canCancelAppointment(status) {
  const normalized = String(status || "").trim().toUpperCase();
  return normalized === "PENDING" || normalized === "CONFIRMED" || normalized === "LATE";
}

function AppointmentActionButtons({ appointment, actingAppt, handleApptAction }) {
  const isActing = actingAppt === appointment.id;
  const canConfirm = canConfirmAppointment(appointment.status);
  const canCheckIn = canCheckInAppointment(appointment.status);
  const canCancel = canCancelAppointment(appointment.status);

  return (
    <div className="appt-action-row">
      <button
        className={`btn-icon btn-action-pill btn-action-confirm ${(!canConfirm || isActing) ? "is-disabled-action" : ""}`}
        title="Xác nhận"
        disabled={isActing || !canConfirm}
        onClick={(event) => {
          event.stopPropagation();
          handleApptAction(appointment, "confirm");
        }}
      >
        Xác nhận
      </button>
      <button
        className={`btn-icon btn-action-pill btn-action-checkin ${(!canCheckIn || isActing) ? "is-disabled-action" : ""}`}
        title="Check-in"
        disabled={isActing || !canCheckIn}
        onClick={(event) => {
          event.stopPropagation();
          handleApptAction(appointment, "checkin");
        }}
      >
        Check-in
      </button>
      <button
        className={`btn-icon btn-action-pill btn-action-cancel ${(!canCancel || isActing) ? "is-disabled-action" : ""}`}
        title="Hủy lịch hẹn"
        disabled={isActing || !canCancel}
        onClick={(event) => {
          event.stopPropagation();
          handleApptAction(appointment, "cancel");
        }}
      >
        Hủy
      </button>
    </div>
  );
}

function GroupBlock({
  group,
  formatApptDate,
  formatApptTime,
  getApptStatusBadge,
  handleApptAction,
  handleGroupCancel,
  actingAppt,
  actingGroup,
  onAppointmentClick,
}) {
  return (
    <article
      style={{
        border: "2px solid #c3d6f2",
        borderRadius: 14,
        background: "#f5f9ff",
        boxShadow: "0 6px 20px rgba(23, 60, 115, 0.08)",
        padding: 14,
      }}
    >
      <header
        style={{
          display: "flex",
          justifyContent: "space-between",
          alignItems: "center",
          gap: 12,
          marginBottom: 12,
          paddingBottom: 10,
          borderBottom: "1px solid #d9e6fb",
          flexWrap: "wrap",
        }}
      >
        <div>
          <h4 style={{ margin: 0, color: "#17325b" }}>{group.groupCode || `BG-${group.id}`}</h4>
          <p style={{ margin: "4px 0 0", fontSize: 13, color: "#5b6f8c" }}>
            {group.customerName || "Khách hàng"} - {group.customerPhone || "--"} - {group.appointments.length} lịch
          </p>
        </div>
        <div style={{ display: "flex", alignItems: "center", gap: 10, marginRight: 10 }}>
          <span
            className={`badge ${getApptStatusBadge(group.status)}`}
            style={{ display: "inline-flex", alignItems: "center", justifyContent: "center", height: 30 }}
          >
            {getStatusLabelVi(group.status || "PENDING", "Chờ xác nhận")}
          </span>
          <button
            className={`btn-icon btn-action-pill btn-action-cancel ${(actingGroup === group.id || !group.hasCancelableAppointments) ? "is-disabled-action" : ""}`}
            title="Hủy nhóm"
            onClick={(event) => {
              event.stopPropagation();
              handleGroupCancel(group);
            }}
            disabled={actingGroup === group.id || !group.hasCancelableAppointments}
          >
            Hủy nhóm
          </button>
        </div>
      </header>

      <div
        style={{
          overflowX: "auto",
          border: "1px solid #dbe7f8",
          borderRadius: 12,
          background: "#ffffff",
        }}
      >
        <table className="admin-table">
          <thead>
            <tr>
              <th>Ngày</th>
              <th>Giờ</th>
              <th>Xe</th>
              <th className="th-center">Trạng thái</th>
              <th className="col-actions col-actions-wide th-center">Thao tác</th>
            </tr>
          </thead>
          <tbody>
            {group.appointments.map((appt) => {
              const rowInteraction = buildRowInteraction(appt, onAppointmentClick);
              return (
                <tr key={appt.id} className="appointment-click-row" {...rowInteraction}>
                  <td>{formatApptDate(appt.startTime)}</td>
                  <td>{formatApptTime(appt.startTime)} - {formatApptTime(appt.endTime)}</td>
                  <td>{appt.vehicleLicensePlate || "--"}</td>
                  <td className="th-center">
                    <span className={`badge ${getApptStatusBadge(appt.status)}`}>{getStatusLabelVi(appt.status, appt.status)}</span>
                  </td>
                  <td className="col-actions col-actions-wide th-center">
                    <AppointmentActionButtons appointment={appt} actingAppt={actingAppt} handleApptAction={handleApptAction} />
                  </td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>
    </article>
  );
}

export default function ReceptionAppointmentsSection({
  onCreateAppointment,
  apptStatusFilter,
  setApptStatusFilter,
  apptStatusOptions,
  apptSortBy,
  setApptSortBy,
  openDateRangePicker,
  dateFilterButtonLabel,
  canResetToolbarFilters,
  onResetToolbarFilters,
  apptViewMode,
  setApptViewMode,
  filteredSingleAppts,
  groupedAppts,
  apptLoading,
  pagedSingleAppts,
  pagedGroupedAppts,
  formatApptDate,
  formatApptTime,
  getApptStatusBadge,
  handleApptAction,
  handleGroupCancel,
  actingAppt,
  actingGroup,
  onAppointmentClick,
  apptsPage,
  apptsPageSize,
  apptsTotalItems,
  setApptsPage,
  setApptsPageSize,
}) {
  const isGroupMode = apptViewMode === "GROUP";
  const totalSingle = filteredSingleAppts.length;
  const totalGroups = groupedAppts.length;
  const apptFilterOptions = [toStatusOptionVi("ALL"), ...apptStatusOptions.map(toStatusOptionVi)];
  const apptSortOptions = [
    { value: "NEWEST", label: "Mới nhất" },
    { value: "OLDEST", label: "Cũ nhất" },
    { value: "NAME_ASC", label: "Tên khách A-Z" },
    { value: "NAME_DESC", label: "Tên khách Z-A" },
  ];

  return (
    <>
      <div className="table-toolbar">
        <button className="btn-primary" onClick={onCreateAppointment}>+ Tạo lịch hẹn</button>
        <div className="toolbar-controls">
          <FilterDropdown
            id="appt-status-filter"
            label="Trạng thái"
            value={apptStatusFilter}
            options={apptFilterOptions}
            onChange={setApptStatusFilter}
            minWidth={160}
          />

          <FilterDropdown
            id="appt-sort"
            label="Sắp xếp"
            value={apptSortBy}
            options={apptSortOptions}
            onChange={setApptSortBy}
            minWidth={190}
          />

          <button type="button" className="toolbar-action-btn toolbar-action-outline" onClick={openDateRangePicker}>
            {dateFilterButtonLabel || "Chọn khoảng ngày"}
          </button>
          <button type="button" className="toolbar-action-btn toolbar-action-ghost" onClick={onResetToolbarFilters} disabled={!canResetToolbarFilters}>
            Xóa lọc
          </button>
        </div>
        <span className="result-count">
          {isGroupMode ? `${totalGroups} nhóm lịch` : `${totalSingle} lịch thường`}
        </span>
      </div>

      <p className="appointment-row-hint">Nhấn vào một dòng để xem thông tin lịch hẹn chi tiết.</p>

      <div
        style={{
          display: "inline-grid",
          gridTemplateColumns: "repeat(2, minmax(0, 1fr))",
          gap: 8,
          background: "#edf3fb",
          border: "1px solid #d3e0f1",
          borderRadius: 12,
          padding: 6,
          marginBottom: 14,
        }}
      >
        <button
          type="button"
          className="toolbar-action-btn"
          style={{
            minWidth: 170,
            borderRadius: 10,
            border: apptViewMode === "SINGLE" ? "1px solid #c3d4ed" : "1px solid transparent",
            background: apptViewMode === "SINGLE" ? "#ffffff" : "transparent",
            color: apptViewMode === "SINGLE" ? "#173c73" : "#446286",
            boxShadow: apptViewMode === "SINGLE" ? "0 3px 10px rgba(23, 60, 115, 0.12)" : "none",
          }}
          onClick={() => setApptViewMode("SINGLE")}
        >
          Lịch thường ({totalSingle})
        </button>
        <button
          type="button"
          className="toolbar-action-btn"
          style={{
            minWidth: 170,
            borderRadius: 10,
            border: apptViewMode === "GROUP" ? "1px solid #c3d4ed" : "1px solid transparent",
            background: apptViewMode === "GROUP" ? "#ffffff" : "transparent",
            color: apptViewMode === "GROUP" ? "#173c73" : "#446286",
            boxShadow: apptViewMode === "GROUP" ? "0 3px 10px rgba(23, 60, 115, 0.12)" : "none",
          }}
          onClick={() => setApptViewMode("GROUP")}
        >
          Lịch nhóm ({totalGroups})
        </button>
      </div>

      <div className="workshop-table-card">
        {apptLoading ? (
          <div className="loading-state"><div className="spinner" /><p>Đang tải lịch hẹn...</p></div>
        ) : isGroupMode ? (
          totalGroups === 0 ? (
            <div style={{ textAlign: "center", padding: 40, color: "#a3aed0" }}>Không có lịch hẹn nhóm nào.</div>
          ) : (
            <div style={{ display: "grid", gap: 16, padding: 14 }}>
              {pagedGroupedAppts.map((group) => (
                <GroupBlock
                  key={group.id}
                  group={group}
                  formatApptDate={formatApptDate}
                  formatApptTime={formatApptTime}
                  getApptStatusBadge={getApptStatusBadge}
                  handleApptAction={handleApptAction}
                  handleGroupCancel={handleGroupCancel}
                  actingAppt={actingAppt}
                  actingGroup={actingGroup}
                  onAppointmentClick={onAppointmentClick}
                />
              ))}
            </div>
          )
        ) : (
          <table className="admin-table">
            <thead>
              <tr>
                <th>Khách hàng</th>
                <th>SĐT</th>
                <th>Ngày</th>
                <th>Giờ</th>
                <th>Xe</th>
                <th className="th-center">Trạng thái</th>
                <th className="col-actions col-actions-wide th-center">Thao tác</th>
              </tr>
            </thead>
            <tbody>
              {totalSingle === 0 ? (
                <tr>
                  <td colSpan="7" style={{ textAlign: "center", padding: "40px", color: "#a3aed0" }}>
                    Không có lịch hẹn thường nào.
                  </td>
                </tr>
              ) : (
                pagedSingleAppts.map((appt) => {
                  const rowInteraction = buildRowInteraction(appt, onAppointmentClick);
                  return (
                    <tr key={appt.id} className="appointment-click-row" {...rowInteraction}>
                      <td style={{ fontWeight: 600 }}>{appt.customerName}</td>
                      <td>{appt.customerPhone}</td>
                      <td>{formatApptDate(appt.startTime)}</td>
                      <td>{formatApptTime(appt.startTime)} - {formatApptTime(appt.endTime)}</td>
                      <td>{appt.vehicleLicensePlate}</td>
                      <td className="th-center">
                        <span className={`badge ${getApptStatusBadge(appt.status)}`}>{getStatusLabelVi(appt.status, appt.status)}</span>
                      </td>
                      <td className="col-actions col-actions-wide th-center">
                        <AppointmentActionButtons appointment={appt} actingAppt={actingAppt} handleApptAction={handleApptAction} />
                      </td>
                    </tr>
                  );
                })
              )}
            </tbody>
          </table>
        )}
      </div>

      <TablePagination
        totalItems={apptsTotalItems}
        currentPage={apptsPage}
        pageSize={apptsPageSize}
        onPageChange={setApptsPage}
        onPageSizeChange={(size) => {
          setApptsPageSize(size);
          setApptsPage(1);
        }}
      />
    </>
  );
}


