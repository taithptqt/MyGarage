import React from "react";
import TablePagination from "@shared/components/TablePagination";

export default function ReceptionAppointmentsSection({
  setModal,
  apptStatusFilter,
  setApptStatusFilter,
  apptStatusOptions,
  apptSortBy,
  setApptSortBy,
  openDateRangePicker,
  dateFrom,
  dateTo,
  clearDateRange,
  filteredAppts,
  apptLoading,
  pagedAppts,
  formatApptDate,
  formatApptTime,
  getApptStatusBadge,
  handleApptAction,
  actingAppt,
  apptsPage,
  apptsPageSize,
  setApptsPage,
  setApptsPageSize,
}) {
  return (
    <>
      <div className="table-toolbar">
        <button className="btn-primary" onClick={() => setModal("create_appt")}>+ Tạo lịch hẹn</button>
        <div className="toolbar-controls">
          <label htmlFor="appt-status-filter" className="toolbar-label">Trạng thái:</label>
          <select id="appt-status-filter" className="toolbar-select" value={apptStatusFilter} onChange={(e) => setApptStatusFilter(e.target.value)} style={{ minWidth: "160px" }}>
            <option value="ALL">{"T\u1ea5t c\u1ea3"}</option>
            {apptStatusOptions.map((status) => (<option key={status} value={status}>{status}</option>))}
          </select>
          <label htmlFor="appt-sort" className="toolbar-label">Sắp xếp:</label>
          <select id="appt-sort" className="toolbar-select" value={apptSortBy} onChange={(e) => setApptSortBy(e.target.value)} style={{ minWidth: "190px" }}>
            <option value="NEWEST">Mơi nhất</option>
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
        <span className="result-count">{filteredAppts.length} lịch đặt trươc</span>
      </div>

      <div className="workshop-table-card">
        {apptLoading ? (
          <div className="loading-state"><div className="spinner" /><p>Đang tải lịch hẹn...</p></div>
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
              {filteredAppts.length === 0 ? (
                <tr><td colSpan="7" style={{ textAlign: "center", padding: "40px", color: "#a3aed0" }}>Không có lịch hẹn nào.</td></tr>
              ) : pagedAppts.map((appt) => (
                <tr key={appt.id}>
                  <td style={{ fontWeight: 600 }}>{appt.customerName}</td>
                  <td>{appt.customerPhone}</td>
                  <td>{formatApptDate(appt.startTime)}</td>
                  <td>{formatApptTime(appt.startTime)} - {formatApptTime(appt.endTime)}</td>
                  <td>{appt.vehicleLicensePlate}</td>
                  <td className="th-center">
                    <span className={`badge ${getApptStatusBadge(appt.status)}`}>
                      {appt.status}
                    </span>
                  </td>
                  <td className="col-actions">
                    <div style={{ display: "flex", justifyContent: "center" }}>
                      <button
                        className={`btn-icon btn-action-danger ${(appt.status !== "PENDING" && appt.status !== "CONFIRMED") ? "faded" : ""}`}
                        title="Hủy lịch hẹn"
                        onClick={() => handleApptAction(appt.id)}
                        disabled={(appt.status !== "PENDING" && appt.status !== "CONFIRMED") || actingAppt === appt.id}
                      >
                        Hủy
                      </button>
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>
      <TablePagination
        totalItems={filteredAppts.length}
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





