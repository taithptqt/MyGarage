import { useEffect, useState } from "react";
import { Link, useNavigate, useSearchParams } from "react-router-dom";
import {
  MdEventNote,
  MdAccessTime,
  MdCheckCircle,
  MdCancel,
  MdStore,
  MdDirectionsCar,
  MdBuild,
  MdSearch,
  MdAdd,
  MdOutlineNotes,
  MdCalendarToday,
  MdRefresh,
} from "react-icons/md";
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
import { formatAppointmentCancelReason } from "@shared/core/utils/appointmentCancelReasonUtils";

function AppointmentCard({ item, cancelling, openCancelPopup, inGroup = false }) {
  const status = STATUS_META[item.status] || { label: item.status || "Không rõ", tone: "default" };
  const canCancel =
    item.status === APPOINTMENT_STATUS.PENDING ||
    item.status === APPOINTMENT_STATUS.CONFIRMED;
  const displayCancelReason = formatAppointmentCancelReason(item.cancelReason);

  return (
    <article className={`my-appt-card ${inGroup ? "my-appt-card-in-group" : ""}`}>
      <div className="my-appt-card-main">
        <div className="my-appt-card-head">
          <div className="my-appt-branch-info">
            <div className="my-appt-branch-eyebrow">
              <MdStore className="my-appt-branch-icon" />
              <span>CHI NHÁNH TIẾP NHẬN</span>
            </div>
            <h3 className="my-appt-branch-name">{item.branchName || "Chi nhánh chưa xác định"}</h3>
          </div>
          <span className={`my-appt-status my-appt-status--${status.tone}`}>{status.label}</span>
        </div>

        <div className="my-appt-card-grid">
          <div className="my-appt-grid-tile">
            <span className="my-appt-tile-label">
              <MdCalendarToday className="my-appt-tile-icon" /> NGÀY TIẾP NHẬN
            </span>
            <p className="my-appt-tile-val">{formatDate(item.startTime)}</p>
          </div>

          <div className="my-appt-grid-tile">
            <span className="my-appt-tile-label">
              <MdAccessTime className="my-appt-tile-icon" /> KHUNG GIỜ
            </span>
            <p className="my-appt-tile-val">{`${formatTime(item.startTime)} - ${formatTime(item.endTime)}`}</p>
          </div>

          <div className="my-appt-grid-tile">
            <span className="my-appt-tile-label">
              <MdDirectionsCar className="my-appt-tile-icon" /> BIỂN SỐ XE
            </span>
            {item.vehicleLicensePlate ? (
              <div className="my-appt-plate-badge">
                <span className="my-appt-plate-screw" />
                <span className="my-appt-plate-number">{item.vehicleLicensePlate}</span>
                <span className="my-appt-plate-screw" />
              </div>
            ) : (
              <p className="my-appt-tile-val">—</p>
            )}
          </div>

          <div className="my-appt-grid-tile">
            <span className="my-appt-tile-label">
              <MdDirectionsCar className="my-appt-tile-icon" /> DÒNG XE
            </span>
            <p className="my-appt-tile-val">{item.vehicleModelName || "—"}</p>
          </div>
        </div>

        {Array.isArray(item.serviceNames) && item.serviceNames.length > 0 && (
          <div className="my-appt-services-block">
            <span className="my-appt-services-label">
              <MdBuild className="my-appt-services-icon" /> Dịch vụ đăng ký:
            </span>
            <div className="my-appt-service-tags">
              {item.serviceNames.map((serviceName) => (
                <span key={`${item.id}-${serviceName}`} className="my-appt-service-tag">
                  {serviceName}
                </span>
              ))}
            </div>
          </div>
        )}

        {item.customerNotes && (
          <div className="my-appt-callout my-appt-callout-note">
            <MdOutlineNotes className="my-appt-callout-icon" />
            <div className="my-appt-callout-content">
              <strong>Ghi chú:</strong> <span>{item.customerNotes}</span>
            </div>
          </div>
        )}

        {displayCancelReason && (
          <div className="my-appt-callout my-appt-callout-cancel">
            <MdCancel className="my-appt-callout-icon" />
            <div className="my-appt-callout-content">
              <strong>Lý do hủy:</strong> <span>{displayCancelReason}</span>
            </div>
          </div>
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
            <MdCancel className="my-appt-btn-icon" />
            {cancelling === item.id ? "Đang hủy..." : "Hủy lịch hẹn"}
          </button>
        )}
      </div>
    </article>
  );
}

export default function MyAppointmentsPage() {
  const navigate = useNavigate();
  const [searchParams, setSearchParams] = useSearchParams();
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
    GroupSessions,
    filteredAppointments,
    filteredGroupSessions,
    setFilter,
    setSearchTerm,
    setCancelReason,
    loadAppointments,
    openCancelPopup,
    closeCancelPopup,
    handleConfirmCancel,
  } = useMyAppointments();
  const resolveTabFromQuery = (tabParam) => (
    tabParam === APPOINTMENTS_VIEW_TABS.GROUP
      ? APPOINTMENTS_VIEW_TABS.GROUP
      : APPOINTMENTS_VIEW_TABS.SINGLE
  );

  const [activeTab, setActiveTab] = useState(() => resolveTabFromQuery(searchParams.get("tab")));

  useEffect(() => {
    const nextTab = resolveTabFromQuery(searchParams.get("tab"));
    setActiveTab((prev) => (prev === nextTab ? prev : nextTab));
  }, [searchParams]);

  useEffect(() => {
    document.title = "Lịch hẹn của tôi | MyGarage";
  }, []);

  const handleChangeTab = (nextTab) => {
    setActiveTab(nextTab);
    const nextParams = new URLSearchParams(searchParams);
    if (nextTab === APPOINTMENTS_VIEW_TABS.SINGLE) {
      nextParams.delete("tab");
    } else {
      nextParams.set("tab", nextTab);
    }
    setSearchParams(nextParams, { replace: true });
  };

  useEffect(() => {
    if (!isAuthenticated) {
      navigate("/login", { replace: true });
      return;
    }
    loadAppointments();
  }, [isAuthenticated, navigate, loadAppointments]);

  const isGroupTab = activeTab === APPOINTMENTS_VIEW_TABS.GROUP;
  const hasResults = isGroupTab
    ? filteredGroupSessions.length > 0
    : filteredAppointments.length > 0;

  return (
    <div className="my-appt-page">
      <Header />
      <div className="my-appt-shell">
        <main className="my-appt-content">
          <section className="my-appt-hero-card">
            <div className="my-appt-head">
              <div className="my-appt-head-copy">
                <div className="my-appt-eyebrow-badge">
                  <span className="my-appt-eyebrow-dot" />
                  <span>TRUNG TÂM LỊCH HẸN</span>
                </div>
                <h1>Lịch hẹn của tôi</h1>
                <p className="my-appt-description">
                  Theo dõi trạng thái lịch hẹn, thông tin xe và khung giờ xử lý trong một màn hình.
                </p>
              </div>
              <div className="my-appt-head-actions">
                <Link to="/booking/form" className="my-appt-primary-btn">
                  <MdAdd className="my-appt-btn-icon" /> Đặt lịch mới
                </Link>
              </div>
            </div>

            <section className="my-appt-stats" aria-label="Thống kê lịch hẹn">
              <article className="my-appt-stat-card">
                <div className="my-appt-stat-head">
                  <span>Tổng lịch hẹn</span>
                  <MdEventNote className="my-appt-stat-icon" />
                </div>
                <strong>{stats.total}</strong>
              </article>
              <article className="my-appt-stat-card">
                <div className="my-appt-stat-head">
                  <span>Sắp tới</span>
                  <MdAccessTime className="my-appt-stat-icon my-appt-stat-icon--gold" />
                </div>
                <strong>{stats.upcoming}</strong>
              </article>
              <article className="my-appt-stat-card">
                <div className="my-appt-stat-head">
                  <span>Đã xác nhận</span>
                  <MdCheckCircle className="my-appt-stat-icon my-appt-stat-icon--success" />
                </div>
                <strong>{stats.confirmed}</strong>
              </article>
              <article className="my-appt-stat-card">
                <div className="my-appt-stat-head">
                  <span>Đã hủy</span>
                  <MdCancel className="my-appt-stat-icon my-appt-stat-icon--danger" />
                </div>
                <strong>{stats.canceled}</strong>
              </article>
            </section>
          </section>

          <section className="my-appt-view-tabs" aria-label="Loại lịch hẹn">
            <button
              type="button"
              className={activeTab === APPOINTMENTS_VIEW_TABS.SINGLE ? "active" : ""}
              onClick={() => handleChangeTab(APPOINTMENTS_VIEW_TABS.SINGLE)}
            >
              <MdCalendarToday className="my-appt-tab-icon" /> Lịch hẹn thường ({filteredAppointments.length})
            </button>
            <button
              type="button"
              className={activeTab === APPOINTMENTS_VIEW_TABS.GROUP ? "active" : ""}
              onClick={() => handleChangeTab(APPOINTMENTS_VIEW_TABS.GROUP)}
            >
              <MdDirectionsCar className="my-appt-tab-icon" /> Lịch hẹn nhóm ({GroupSessions.length})
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
            <div className="my-appt-search-wrap">
              <MdSearch className="my-appt-search-icon" />
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
            </div>
          </section>

          {error && <div className="my-appt-alert my-appt-alert-error">{error}</div>}

          {loading ? (
            <div className="my-appt-spinner-wrap">
              <div className="my-appt-spinner" />
              <p>Đang tải thông tin lịch hẹn...</p>
            </div>
          ) : !hasResults ? (
            <section className="my-appt-empty">
              <div className="my-appt-empty-icon-wrap">
                <MdCalendarToday className="my-appt-empty-icon" />
              </div>
              <h3>Chưa có lịch hẹn phù hợp</h3>
              <p>
                {filter || searchTerm
                  ? "Không tìm thấy lịch hẹn theo bộ lọc hiện tại."
                  : isGroupTab
                    ? "Bạn chưa có lịch hẹn nhóm nào."
                    : "Bạn chưa có lịch hẹn thường nào. Hãy đặt lịch để gara chuẩn bị tiếp nhận xe."}
              </p>
              <div className="my-appt-empty-actions">
                <Link to="/booking/form" className="my-appt-primary-btn">
                  <MdAdd className="my-appt-btn-icon" /> Đặt lịch ngay
                </Link>
                <button type="button" className="my-appt-ghost-btn" onClick={loadAppointments}>
                  <MdRefresh className="my-appt-btn-icon" /> Tải lại
                </button>
              </div>
            </section>
          ) : isGroupTab ? (
            <section className="my-appt-group-list">
              {filteredGroupSessions.map((group) => {
                const groupStatus =
                  GROUP_STATUS_META[group.status] || { label: group.status || "Không rõ", tone: "default" };
                const visibleCount = Array.isArray(group.appointments) ? group.appointments.length : 0;
                const totalCount = Number(group.totalAppointments || 0);

                return (
                  <article key={group.id} className="my-appt-group-block">
                    <header className="my-appt-group-block-head">
                      <div>
                        <div className="my-appt-group-eyebrow">MÃ NHÓM LỊCH HẸN</div>
                        <h3>{group.groupCode || "Nhóm lịch hẹn"}</h3>
                        <p className="my-appt-group-meta">
                          {visibleCount}
                          {totalCount > 0 ? `/${totalCount}` : ""} lịch hẹn đang hiển thị
                        </p>
                      </div>
                      <span className={`my-appt-status my-appt-status--${groupStatus.tone}`}>{groupStatus.label}</span>
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
        </main>

        <Footer />
      </div>

      {cancelTarget && (
        <div className="my-appt-cancel-overlay" onClick={closeCancelPopup}>
          <div className="my-appt-cancel-popup" onClick={(event) => event.stopPropagation()}>
            <div className="my-appt-cancel-popup-head">
              <div className="my-appt-cancel-icon-wrap">
                <MdCancel className="my-appt-cancel-icon" />
              </div>
              <div>
                <h3>Xác nhận hủy lịch hẹn</h3>
                <p className="my-appt-cancel-desc">
                  Bạn có chắc muốn hủy lịch tại <strong>{cancelTarget.branchName}</strong> vào{" "}
                  <strong>{formatDate(cancelTarget.startTime)}</strong> (
                  <strong>{formatTime(cancelTarget.startTime)} - {formatTime(cancelTarget.endTime)}</strong>)?
                </p>
              </div>
            </div>

            <div className="my-appt-cancel-form">
              <label htmlFor="cancel-reason">Lý do hủy (tùy chọn)</label>
              <textarea
                id="cancel-reason"
                className="my-appt-cancel-reason-input"
                placeholder="Ví dụ: thay đổi kế hoạch cá nhân..."
                value={cancelReason}
                onChange={(event) => setCancelReason(event.target.value)}
                rows={3}
              />
            </div>

            {error && <div className="my-appt-alert my-appt-alert-error">{error}</div>}

            <div className="my-appt-popup-actions">
              <button type="button" className="my-appt-btn-popup-secondary" onClick={closeCancelPopup} disabled={cancelling}>
                Quay lại
              </button>
              <button type="button" className="my-appt-btn-popup-danger" onClick={handleConfirmCancel} disabled={cancelling}>
                {cancelling ? "Đang hủy..." : "Xác nhận hủy"}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
