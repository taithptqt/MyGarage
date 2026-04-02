import { useEffect, useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import { useAuth } from "@shared/core/context/AuthContext";
import Footer from "@shared/components/Footer";
import Header from "@shared/components/Header";
import { useMyAppointments } from "@features/appointments/hooks/useMyAppointments";
import { APPOINTMENT_STATUS } from "@features/appointments/constants/appointmentStatuses";
import {
  APPOINTMENTS_VIEW_TABS,
  FILTERS,
  GROUP_STATUS_META,
  STATUS_META,
  formatDate,
  formatTime,
} from "@features/appointments/utils/myAppointmentsData";
import "./MyAppointmentsPage.css";

function AppointmentCard({ item, cancelling, openCancelPopup, inGroup = false }) {
  const status = STATUS_META[item.status] || { label: item.status || "Không rõ", tone: "default" };
  const canCancel =
    item.status === APPOINTMENT_STATUS.PENDING ||
    item.status === APPOINTMENT_STATUS.CONFIRMED;

  return (
    <article className={`my-appt-card ${inGroup ? "my-appt-card-in-group" : ""}`}>
      <div className="my-appt-card-main">
        <div className="my-appt-card-head">
          <h3>{item.branchName || "Chi nhánh chưa xác định"}</h3>
          <span className={`my-appt-status ${status.tone}`}>{status.label}</span>
        </div>
        <div className="my-appt-card-grid">
          <div>
            <label>Ngày tiếp nhận</label>
            <p>{formatDate(item.startTime)}</p>
          </div>
          <div>
            <label>Khung giờ</label>
            <p>{`${formatTime(item.startTime)} - ${formatTime(item.endTime)}`}</p>
          </div>
          <div>
            <label>Biển số</label>
            <p>{item.vehicleLicensePlate || "--"}</p>
          </div>
          <div>
            <label>Dòng xe</label>
            <p>{item.vehicleModelName || "--"}</p>
          </div>
        </div>
        {Array.isArray(item.serviceNames) && item.serviceNames.length > 0 && (
          <div className="my-appt-service-list">
            {item.serviceNames.map((serviceName) => (
              <span key={`${item.id}-${serviceName}`}>{serviceName}</span>
            ))}
          </div>
        )}
        <p className="my-appt-note">
          <strong>Ghi chú:</strong> {item.customerNotes || ""}
        </p>
        {item.cancelReason && (
          <p className="my-appt-note cancel">
            <strong>Lý do hủy:</strong> {item.cancelReason}
          </p>
        )}
      </div>
      <div className="my-appt-card-actions">
        {canCancel && (
          <button
            type="button"
            className="my-appt-cancel-btn"
            onClick={() => openCancelPopup(item)}
            disabled={cancelling === item.id}
          >
            {cancelling === item.id ? "Đang hủy..." : "Hủy lịch"}
          </button>
        )}
      </div>
    </article>
  );
}

export default function MyAppointmentsPage() {
  const navigate = useNavigate();
  const { isAuthenticated } = useAuth();
  const {
    loading,
    filter,
    searchTerm,
    cancelling,
    error,
    cancelTarget,
    cancelReason,
    stats,
    bookingGroups,
    filteredAppointments,
    filteredBookingGroups,
    setFilter,
    setSearchTerm,
    setCancelReason,
    loadAppointments,
    openCancelPopup,
    closeCancelPopup,
    handleConfirmCancel,
  } = useMyAppointments();
  const [activeTab, setActiveTab] = useState(APPOINTMENTS_VIEW_TABS.SINGLE);

  useEffect(() => {
    if (!isAuthenticated) {
      navigate("/login", { replace: true });
      return;
    }
    loadAppointments();
  }, [isAuthenticated, navigate, loadAppointments]);

  const isGroupTab = activeTab === APPOINTMENTS_VIEW_TABS.GROUP;
  const hasResults = isGroupTab
    ? filteredBookingGroups.length > 0
    : filteredAppointments.length > 0;

  return (
    <div className="my-appt-page">
      <div className="my-appt-shell">
        <Header />

        <section className="my-appt-content">
          <header className="my-appt-head">
            <div>
              <p className="my-appt-eyebrow">Trung tâm lịch hẹn</p>
              <h1>Lịch hẹn của tôi</h1>
              <p className="my-appt-description">
                Theo dõi trạng thái lịch hẹn, thông tin xe và khung giờ xử lý trong một màn hình.
              </p>
            </div>
            <Link to="/booking/form" className="my-appt-primary-btn">
              Đặt lịch mới
            </Link>
          </header>

          <section className="my-appt-stats">
            <article className="my-appt-stat-card">
              <span>Tổng lịch hẹn</span>
              <strong>{stats.total}</strong>
            </article>
            <article className="my-appt-stat-card">
              <span>Sắp tới</span>
              <strong>{stats.upcoming}</strong>
            </article>
            <article className="my-appt-stat-card">
              <span>Đã xác nhận</span>
              <strong>{stats.confirmed}</strong>
            </article>
            <article className="my-appt-stat-card">
              <span>Đã hủy</span>
              <strong>{stats.canceled}</strong>
            </article>
          </section>

          <section className="my-appt-view-tabs" aria-label="Loại lịch hẹn">
            <button
              type="button"
              className={activeTab === APPOINTMENTS_VIEW_TABS.SINGLE ? "active" : ""}
              onClick={() => setActiveTab(APPOINTMENTS_VIEW_TABS.SINGLE)}
            >
              Lịch hẹn thường ({filteredAppointments.length})
            </button>
            <button
              type="button"
              className={activeTab === APPOINTMENTS_VIEW_TABS.GROUP ? "active" : ""}
              onClick={() => setActiveTab(APPOINTMENTS_VIEW_TABS.GROUP)}
            >
              Lịch hẹn nhóm ({bookingGroups.length})
            </button>
          </section>

          <section className="my-appt-toolbar">
            <div className="my-appt-filter-list">
              {FILTERS.map((item) => (
                <button
                  key={item.key}
                  type="button"
                  className={filter === item.key ? "active" : ""}
                  onClick={() => setFilter(item.key)}
                >
                  {item.label}
                </button>
              ))}
            </div>
            <input
              type="text"
              className="my-appt-search"
              placeholder={
                isGroupTab
                  ? "Tìm theo mã nhóm, chi nhánh, biển số, dịch vụ..."
                  : "Tìm theo chi nhánh, biển số, dịch vụ..."
              }
              value={searchTerm}
              onChange={(event) => setSearchTerm(event.target.value)}
            />
          </section>

          {error && <div className="my-appt-alert my-appt-alert-error">{error}</div>}

          {loading ? (
            <div className="spinner-container">
              <div className="spinner" />
            </div>
          ) : !hasResults ? (
            <section className="my-appt-empty">
              <h3>Chưa có lịch hẹn phù hợp</h3>
              <p>
                {filter || searchTerm
                  ? "Không tìm thấy lịch hẹn theo bộ lọc hiện tại."
                  : isGroupTab
                    ? "Bạn chưa có lịch hẹn nhóm nào."
                    : "Bạn chưa có lịch hẹn thường nào. Hãy đặt lịch để gara chuẩn bị tiếp nhận xe."}
              </p>
              <div style={{ display: "flex", gap: 10, justifyContent: "center", flexWrap: "wrap" }}>
                <Link to="/booking/form" className="my-appt-secondary-btn">
                  Đặt lịch ngay
                </Link>
                <button type="button" className="my-appt-secondary-btn" onClick={loadAppointments}>
                  Tải lại
                </button>
              </div>
            </section>
          ) : isGroupTab ? (
            <section className="my-appt-group-list">
              {filteredBookingGroups.map((group) => {
                const groupStatus =
                  GROUP_STATUS_META[group.status] || { label: group.status || "Không rõ", tone: "default" };
                const visibleCount = Array.isArray(group.appointments) ? group.appointments.length : 0;
                const totalCount = Number(group.totalAppointments || 0);

                return (
                  <article key={group.id} className="my-appt-group-block">
                    <header className="my-appt-group-block-head">
                      <div>
                        <h3>{group.groupCode || "Nhóm lịch hẹn"}</h3>
                        <p>
                          {visibleCount}
                          {totalCount > 0 ? `/${totalCount}` : ""} lịch hẹn đang hiển thị
                        </p>
                      </div>
                      <span className={`my-appt-status ${groupStatus.tone}`}>{groupStatus.label}</span>
                    </header>

                    <div className="my-appt-group-appointments">
                      {group.appointments.map((item) => (
                        <AppointmentCard
                          key={`${group.id}-${item.id}`}
                          item={item}
                          cancelling={cancelling}
                          openCancelPopup={openCancelPopup}
                          inGroup
                        />
                      ))}
                    </div>
                  </article>
                );
              })}
            </section>
          ) : (
            <section className="my-appt-list">
              {filteredAppointments.map((item) => (
                <AppointmentCard
                  key={item.id}
                  item={item}
                  cancelling={cancelling}
                  openCancelPopup={openCancelPopup}
                />
              ))}
            </section>
          )}
        </section>

        <Footer />
      </div>

      {cancelTarget && (
        <div className="cancel-overlay" onClick={closeCancelPopup}>
          <div className="cancel-popup" onClick={(event) => event.stopPropagation()}>
            <h3>Xác nhận hủy lịch hẹn</h3>
            <p className="my-appt-cancel-desc">
              Bạn có chắc muốn hủy lịch tại <strong>{cancelTarget.branchName}</strong> vào{" "}
              <strong>{formatDate(cancelTarget.startTime)}</strong>,{" "}
              <strong>{formatTime(cancelTarget.startTime)} - {formatTime(cancelTarget.endTime)}</strong>?
            </p>
            <div className="my-appt-cancel-form">
              <label htmlFor="cancel-reason">Lý do hủy (tùy chọn)</label>
              <textarea
                id="cancel-reason"
                className="cancel-reason-input"
                placeholder="Ví dụ: thay đổi kế hoạch cá nhân"
                value={cancelReason}
                onChange={(event) => setCancelReason(event.target.value)}
                rows={3}
              />
            </div>
            {error && <div className="my-appt-alert my-appt-alert-error">{error}</div>}
            <div className="my-appt-popup-actions">
              <button type="button" className="btn-popup-secondary" onClick={closeCancelPopup} disabled={cancelling}>
                Quay lại
              </button>
              <button type="button" className="btn-popup-danger" onClick={handleConfirmCancel} disabled={cancelling}>
                {cancelling ? "Đang hủy..." : "Xác nhận hủy"}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
