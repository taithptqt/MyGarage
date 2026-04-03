import React from "react";
import TablePagination from "@shared/components/TablePagination";

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

function GroupBlock({
  group,
  formatApptDate,
  formatApptTime,
  getApptStatusBadge,
  handleApptAction,
  actingAppt,
  onAppointmentClick,
}) {
  return (
    <article
      style={{
        border: "1px solid #d8e3f3",
        borderRadius: 14,
        background: "#f7fbff",
        boxShadow: "0 2px 16px rgba(13, 33, 61, 0.05)",
        padding: 12,
      }}
    >
      <header
        style={{
          display: "flex",
          justifyContent: "space-between",
          alignItems: "center",
          gap: 12,
          marginBottom: 12,
          flexWrap: "wrap",
        }}
      >
        <div>
          <h4 style={{ margin: 0, color: "#17325b" }}>{group.groupCode || `BG-${group.id}`}</h4>
          <p style={{ margin: "4px 0 0", fontSize: 13, color: "#5b6f8c" }}>
            {group.customerName || "Khách hàng"} - {group.customerPhone || "--"} - {group.appointments.length} lịch
          </p>
        </div>
        <span className={`badge ${getApptStatusBadge(group.status)}`}>{group.status || "PENDING"}</span>
      </header>

      <div style={{ overflowX: "auto" }}>
        <table className="admin-table">
          <thead>
            <tr>
              <th>Ngày</th>
              <th>Giờ</th>
              <th>Xe</th>
              <th className="th-center">Trạng thái</th>
              <th className="col-actions">Thao tác</th>
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
                    <span className={`badge ${getApptStatusBadge(appt.status)}`}>{appt.status}</span>
                  </td>
                  <td className="col-actions">
                    <div style={{ display: "flex", justifyContent: "center" }}>
                      <button
                        className={`btn-icon btn-action-danger ${(appt.status !== "PENDING" && appt.status !== "CONFIRMED") ? "faded" : ""}`}
                        title="Hủy lịch hẹn"
                        onClick={(event) => {
                          event.stopPropagation();
                          handleApptAction(appt.id);
                        }}
                        disabled={(appt.status !== "PENDING" && appt.status !== "CONFIRMED") || actingAppt === appt.id}
                      >
                        Hủy
                      </button>
                    </div>
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
  dateFrom,
  dateTo,
  clearDateRange,
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
  actingAppt,
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

  return (
    <>
      <div className="table-toolbar">
        <button className="btn-primary" onClick={onCreateAppointment}>+ Tạo lịch hẹn</button>
        <div className="toolbar-controls">
          <label htmlFor="appt-status-filter" className="toolbar-label">Trạng thái:</label>
          <select
            id="appt-status-filter"
            className="toolbar-select"
            value={apptStatusFilter}
            onChange={(e) => setApptStatusFilter(e.target.value)}
            style={{ minWidth: "160px" }}
          >
            <option value="ALL">Tất cả</option>
            {apptStatusOptions.map((status) => (
              <option key={status} value={status}>{status}</option>
            ))}
          </select>

          <label htmlFor="appt-sort" className="toolbar-label">Sắp xếp:</label>
          <select
            id="appt-sort"
            className="toolbar-select"
            value={apptSortBy}
            onChange={(e) => setApptSortBy(e.target.value)}
            style={{ minWidth: "190px" }}
          >
            <option value="NEWEST">Mới nhất</option>
            <option value="OLDEST">Cũ nhất</option>
            <option value="NAME_ASC">Tên khách A-Z</option>
            <option value="NAME_DESC">Tên khách Z-A</option>
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
            <div style={{ display: "grid", gap: 12 }}>
              {pagedGroupedAppts.map((group) => (
                <GroupBlock
                  key={group.id}
                  group={group}
                  formatApptDate={formatApptDate}
                  formatApptTime={formatApptTime}
                  getApptStatusBadge={getApptStatusBadge}
                  handleApptAction={handleApptAction}
                  actingAppt={actingAppt}
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
                <th className="col-actions">Thao tác</th>
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
                        <span className={`badge ${getApptStatusBadge(appt.status)}`}>{appt.status}</span>
                      </td>
                      <td className="col-actions">
                        <div style={{ display: "flex", justifyContent: "center" }}>
                          <button
                            className={`btn-icon btn-action-danger ${(appt.status !== "PENDING" && appt.status !== "CONFIRMED") ? "faded" : ""}`}
                            title="Hủy lịch hẹn"
                            onClick={(event) => {
                              event.stopPropagation();
                              handleApptAction(appt.id);
                            }}
                            disabled={(appt.status !== "PENDING" && appt.status !== "CONFIRMED") || actingAppt === appt.id}
                          >
                            Hủy
                          </button>
                        </div>
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

