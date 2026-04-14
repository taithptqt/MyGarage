import { useEffect, useMemo, useState } from "react";
import { Link, useSearchParams } from "react-router-dom";
import Footer from "@shared/components/Footer";
import Header from "@shared/components/Header";
import { getCustomerDashboard } from "@features/customerPortal/api/customerPortalApi";
import "./CustomerPortalShell.css";
import "./GarageDashboardPage.css";

function fmtDt(v) {
  if (!v) return "—";
  try {
    return new Date(v).toLocaleString("vi-VN", { dateStyle: "medium", timeStyle: "short" });
  } catch {
    return String(v);
  }
}

export default function GarageDashboardPage() {
  const [searchParams] = useSearchParams();
  const highlightSo = searchParams.get("serviceOrderId");
  const [data, setData] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");

  useEffect(() => {
    let cancelled = false;
    (async () => {
      setLoading(true);
      setError("");
      try {
        const nextData = await getCustomerDashboard();
        if (!cancelled) setData(nextData);
      } catch (e) {
        if (!cancelled) setError(e?.message || "Không tải được dữ liệu.");
      } finally {
        if (!cancelled) setLoading(false);
      }
    })();
    return () => {
      cancelled = true;
    };
  }, []);

  const tracks = data?.activeTracks || [];
  const appointments = data?.upcomingAppointments || [];
  const last = data?.lastCompletedService;

  const stats = useMemo(
    () => [
      { label: "Xe đang theo dõi", value: tracks.length },
      { label: "Lịch hẹn sắp tới", value: appointments.length },
      { label: "Lần hoàn tất gần nhất", value: last?.orderCode || "—" },
    ],
    [appointments.length, last?.orderCode, tracks.length]
  );

  const fallbackSummary = useMemo(() => {
    if (tracks.length) return null;
    if (appointments.length) {
      const nextAppointment = appointments[0];
      return {
        title: "Chưa có xe đang ở xưởng",
        body: `Lịch hẹn gần nhất của bạn là ${fmtDt(nextAppointment.startTime)} tại ${
          nextAppointment.branchName || "chi nhánh đã chọn"
        }.`,
      };
    }

    if (last) {
      return {
        title: "Không có lệnh sửa chữa đang active",
        body: `Lần hoàn tất gần nhất là ${last.orderCode || "đơn gần nhất"} ${
          last.completedAt ? `vào ${fmtDt(last.completedAt)}` : ""
        }.`,
      };
    }

    return {
      title: "Garage đang sẵn sàng cho xe của bạn",
      body: "Khi có lệnh sửa chữa hoặc lịch hẹn sắp tới, toàn bộ tiến trình sẽ hiện ở đây theo thời gian thực.",
    };
  }, [appointments, last, tracks.length]);

  return (
    <div className="cp-page">
      <Header />
      <div className="cp-shell">
        <div className="cp-inner">
          <section className="cp-card cp-card--highlight cp-dashboard-hero">
            <div className="cp-dashboard-hero-copy">
              <p className="cp-eyebrow">Garage của tôi</p>
              <h1 className="cp-title">Theo dõi xe đang sửa</h1>
              <p className="cp-desc">
                Xem xe đang ở giai đoạn nào, được xử lý tại đâu, ai đang phụ trách và khi nào
                dự kiến bàn giao.
              </p>

              <div className="cp-dashboard-actions">
                <Link className="cp-btn" to="/my-vehicles">
                  Đội xe của tôi
                </Link>
                <Link className="cp-btn cp-btn-ghost" to="/my-notifications">
                  Thông báo
                </Link>
                <Link className="cp-btn cp-btn-ghost" to="/my-appointments">
                  Lịch hẹn
                </Link>
              </div>
            </div>

            <div className="cp-dashboard-stats">
              {stats.map((stat) => (
                <div key={stat.label} className="cp-stat-card">
                  <span className="cp-stat-label">{stat.label}</span>
                  <strong className="cp-stat-value">{stat.value}</strong>
                </div>
              ))}
            </div>
          </section>

          {error ? <div className="cp-error">{error}</div> : null}
          {loading ? <p className="cp-muted">Đang tải…</p> : null}

          {!loading && !tracks.length && fallbackSummary ? (
            <section className="cp-card cp-empty-state cp-dashboard-empty">
              <h2 className="cp-empty-title">{fallbackSummary.title}</h2>
              <p className="cp-muted">{fallbackSummary.body}</p>
              <div className="cp-dashboard-actions">
                <Link className="cp-btn" to="/my-appointments">
                  Xem lịch hẹn
                </Link>
                <Link className="cp-btn cp-btn-ghost" to="/my-vehicles">
                  Xem đội xe
                </Link>
              </div>
            </section>
          ) : null}

          {!loading &&
            tracks.map((track) => {
              const isHighlighted =
                highlightSo && String(track.serviceOrderId) === String(highlightSo);
              const assignedNames = Array.isArray(track.assignedTechnicianNames)
                ? track.assignedTechnicianNames.filter(Boolean)
                : [];
              const hasActiveTechnician = assignedNames.length > 0;

              return (
                <article
                  key={track.serviceOrderId}
                  className={`cp-card cp-track-card ${
                    isHighlighted ? "cp-track-card--hi" : ""
                  }`}
                >
                  <div className="cp-track-shell">
                    <div className="cp-track-overview">
                      <div className="cp-track-head">
                        <div>
                          <h2 className="cp-track-plate">{track.licensePlate || "Xe của bạn"}</h2>
                          <p className="cp-track-model">{track.modelLabel || "Đang cập nhật model"}</p>
                        </div>

                        <div className="cp-track-statusbox">
                          <span className="cp-stage-pill">{track.customerStageLabel}</span>
                          <strong className="cp-track-ordercode">{track.orderCode || "—"}</strong>
                          <span className="cp-muted cp-track-statusraw">
                            Trạng thái hệ thống: {track.lifecycleStatus || "—"}
                          </span>
                        </div>
                      </div>

                      <div className="cp-timeline" aria-label="Tiến trình sửa chữa">
                        {(track.timeline || []).map((step, index) => (
                          <div
                            key={`${track.serviceOrderId}-${step.code}-${index}`}
                            className={`cp-timeline-step cp-timeline-step--${step.state || "upcoming"}`}
                          >
                            <span className="cp-timeline-rail" />
                            <span className="cp-timeline-dot" />
                            <span className="cp-timeline-label">{step.label}</span>
                          </div>
                        ))}
                      </div>

                      <dl className="cp-track-meta">
                        <div className="cp-track-meta-card">
                          <dt>Chi nhánh xử lý</dt>
                          <dd>{track.branchName || "—"}</dd>
                        </div>
                        <div className="cp-track-meta-card">
                          <dt>Người phụ trách</dt>
                          <dd>
                            {hasActiveTechnician ? (
                              <span className="cp-person-list">
                                {assignedNames.map((name) => (
                                  <span key={name} className="cp-chip cp-chip--staff">
                                    {name}
                                  </span>
                                ))}
                              </span>
                            ) : (
                              track.serviceAdvisorName || "—"
                            )}
                          </dd>
                        </div>
                        <div className="cp-track-meta-card">
                          <dt>Cố vấn dịch vụ</dt>
                          <dd>{track.serviceAdvisorName || "—"}</dd>
                        </div>
                        <div className="cp-track-meta-card">
                          <dt>Bay / khu vực</dt>
                          <dd>
                            {track.bayCode || track.bayName
                              ? [track.bayCode, track.bayName].filter(Boolean).join(" · ")
                              : "—"}
                          </dd>
                        </div>
                        <div className="cp-track-meta-card">
                          <dt>Dự kiến bàn giao</dt>
                          <dd>{fmtDt(track.estimatedHandover || track.promisedHandoverTime)}</dd>
                        </div>
                      </dl>

                      <div className="cp-track-actions">
                        <Link
                          className="cp-btn"
                          to={`/my-vehicles/${track.vehicleId}?tab=history&serviceOrderId=${track.serviceOrderId}`}
                        >
                          Xem lịch sử lệnh này
                        </Link>
                        <Link className="cp-btn cp-btn-ghost" to={`/my-vehicles/${track.vehicleId}`}>
                          Chi tiết xe
                        </Link>
                      </div>
                    </div>
                  </div>
                </article>
              );
            })}

          {!loading && appointments.length > 0 ? (
            <section className="cp-section">
              <div className="cp-section-head">
                <div>
                  <p className="cp-eyebrow">Lịch hẹn</p>
                  <h2 className="cp-section-title">Lịch hẹn sắp tới</h2>
                </div>
                <Link className="cp-btn cp-btn-ghost" to="/my-appointments">
                  Xem tất cả
                </Link>
              </div>

              <div className="cp-grid-2">
                {appointments.map((appointment) => (
                  <article key={appointment.id} className="cp-card cp-card--secondary cp-appt-card">
                    <strong className="cp-appt-branch">{appointment.branchName || "Chi nhánh"}</strong>
                    <p className="cp-muted">{fmtDt(appointment.startTime)}</p>
                    <p className="cp-appt-plate">Biển số: {appointment.vehiclePlate || "—"}</p>
                    <span className="cp-chip cp-chip--soft">{appointment.status || "—"}</span>
                  </article>
                ))}
              </div>
            </section>
          ) : null}

          {!loading && last && !tracks.length ? (
            <section className="cp-section">
              <div className="cp-section-head">
                <div>
                  <p className="cp-eyebrow">Hoàn tất gần nhất</p>
                  <h2 className="cp-section-title">Lần sửa chữa gần nhất</h2>
                </div>
              </div>
              <article className="cp-card cp-card--secondary cp-last-service-card">
                <div>
                  <strong className="cp-last-service-code">{last.orderCode}</strong>
                  <p className="cp-muted">
                    {last.licensePlate || "—"} · {fmtDt(last.completedAt)} · {last.branchName || "—"}
                  </p>
                </div>
                {last.vehicleId ? (
                  <Link className="cp-btn cp-btn-ghost" to={`/my-vehicles/${last.vehicleId}`}>
                    Mở chi tiết xe
                  </Link>
                ) : null}
              </article>
            </section>
          ) : null}
        </div>
      </div>
      <Footer />
    </div>
  );
}
