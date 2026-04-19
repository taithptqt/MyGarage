import { useEffect, useMemo, useState } from "react";
import { Link, useSearchParams } from "react-router-dom";
import Footer from "@shared/components/Footer";
import Header from "@shared/components/Header";
import { getCustomerDashboard } from "@features/customerPortal/api/customerPortalApi";
import "./CustomerPortalShell.css";
import "./GarageDashboardPage.css";

function fmtDt(v) {
  if (!v) return "�";
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
        if (!cancelled) setError(e?.message || "Không t?i du?c dữ liệu.");
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
      { label: "Xe dang theo d�i", value: tracks.length },
      { label: "L?ch h?n s?p t?i", value: appointments.length },
      { label: "L?n hoàn tất g?n nh?t", value: last?.orderCode || "�" },
    ],
    [appointments.length, last?.orderCode, tracks.length]
  );

  const fallbackSummary = useMemo(() => {
    if (tracks.length) return null;
    if (appointments.length) {
      const nextAppointment = appointments[0];
      return {
        title: "Chua c� xe dang ? xu?ng",
        body: `L?ch h?n g?n nh?t c?a b?n l� ${fmtDt(nextAppointment.startTime)} t?i ${
          nextAppointment.branchName || "chi nh?nh d? chọn"
        }.`,
      };
    }

    if (last) {
      return {
        title: "Không c� l?nh s?a ch?a dang active",
        body: `L?n hoàn tất g?n nh?t l� ${last.orderCode || "don g?n nh?t"} ${
          last.completedAt ? `v�o ${fmtDt(last.completedAt)}` : ""
        }.`,
      };
    }

    return {
      title: "Garage dang s?n s�ng cho xe c?a b?n",
      body: "Khi c? l?nh s?a ch?a ho?c l?ch h?n s?p t?i, to?n b? ti?n tr?nh s? hi?n ? d?y theo thời gian th?c.",
    };
  }, [appointments, last, tracks.length]);

  return (
    <div className="cp-page">
      <Header />
      <div className="cp-shell">
        <div className="cp-inner">
          <section className="cp-card cp-card--highlight cp-dashboard-hero">
            <div className="cp-dashboard-hero-copy">
              <p className="cp-eyebrow">Garage c?a t�i</p>
              <h1 className="cp-title">Theo d�i xe dang s?a</h1>
              <p className="cp-desc">
                Xem xe dang ? giai do?n n?o, du?c x? l? t?i d?u, ai dang ph? tr?ch v? khi n?o
                d? ki?n b�n giao.
              </p>

              <div className="cp-dashboard-actions">
                <Link className="cp-btn" to="/my-vehicles">
                  Đội xe c?a t�i
                </Link>
                <Link className="cp-btn cp-btn-ghost" to="/my-notifications">
                  Th?ng b?o
                </Link>
                <Link className="cp-btn cp-btn-ghost" to="/my-appointments">
                  L?ch h?n
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
          {loading ? <p className="cp-muted">Đang t?i�</p> : null}

          {!loading && !tracks.length && fallbackSummary ? (
            <section className="cp-card cp-empty-state cp-dashboard-empty">
              <h2 className="cp-empty-title">{fallbackSummary.title}</h2>
              <p className="cp-muted">{fallbackSummary.body}</p>
              <div className="cp-dashboard-actions">
                <Link className="cp-btn" to="/my-appointments">
                  Xem l?ch h?n
                </Link>
                <Link className="cp-btn cp-btn-ghost" to="/my-vehicles">
                  Xem d?i xe
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
                          <h2 className="cp-track-plate">{track.licensePlate || "Xe c?a b?n"}</h2>
                          <p className="cp-track-model">{track.modelLabel || "Đang c?p nh?t model"}</p>
                        </div>

                        <div className="cp-track-statusbox">
                          <span className="cp-stage-pill">{track.customerStageLabel}</span>
                          <strong className="cp-track-ordercode">{track.orderCode || "�"}</strong>
                          <span className="cp-muted cp-track-statusraw">
                            Tr?ng th?i hệ thống: {track.lifecycleStatus || "?"}
                          </span>
                        </div>
                      </div>

                      <div className="cp-timeline" aria-label="Ti?n tr�nh s?a ch?a">
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
                          <dt>Chi nh?nh x? l?</dt>
                          <dd>{track.branchName || "�"}</dd>
                        </div>
                        <div className="cp-track-meta-card">
                          <dt>Ngu?i ph? tr?ch</dt>
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
                              track.serviceAdvisorName || "�"
                            )}
                          </dd>
                        </div>
                        <div className="cp-track-meta-card">
                          <dt>C? v?n dịch vụ</dt>
                          <dd>{track.serviceAdvisorName || "�"}</dd>
                        </div>
                        <div className="cp-track-meta-card">
                          <dt>Bay / khu v?c</dt>
                          <dd>
                            {track.bayCode || track.bayName
                              ? [track.bayCode, track.bayName].filter(Boolean).join(" � ")
                              : "�"}
                          </dd>
                        </div>
                        <div className="cp-track-meta-card">
                          <dt>D? ki?n b�n giao</dt>
                          <dd>{fmtDt(track.estimatedHandover || track.promisedHandoverTime)}</dd>
                        </div>
                      </dl>

                      <div className="cp-track-actions">
                        <Link
                          className="cp-btn"
                          to={`/my-vehicles/${track.vehicleId}?tab=history&serviceOrderId=${track.serviceOrderId}`}
                        >
                          Xem l?ch s? l?nh n�y
                        </Link>
                        <Link className="cp-btn cp-btn-ghost" to={`/my-vehicles/${track.vehicleId}`}>
                          Chi ti?t xe
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
                  <p className="cp-eyebrow">L?ch h?n</p>
                  <h2 className="cp-section-title">L?ch h?n s?p t?i</h2>
                </div>
                <Link className="cp-btn cp-btn-ghost" to="/my-appointments">
                  Xem t?t c?
                </Link>
              </div>

              <div className="cp-grid-2">
                {appointments.map((appointment) => (
                  <article key={appointment.id} className="cp-card cp-card--secondary cp-appt-card">
                    <strong className="cp-appt-branch">{appointment.branchName || "Chi nhánh"}</strong>
                    <p className="cp-muted">{fmtDt(appointment.startTime)}</p>
                    <p className="cp-appt-plate">Bi?n s?: {appointment.vehiclePlate || "�"}</p>
                    <span className="cp-chip cp-chip--soft">{appointment.status || "�"}</span>
                  </article>
                ))}
              </div>
            </section>
          ) : null}

          {!loading && last && !tracks.length ? (
            <section className="cp-section">
              <div className="cp-section-head">
                <div>
                  <p className="cp-eyebrow">Hoàn tất g?n nh?t</p>
                  <h2 className="cp-section-title">L?n s?a ch?a g?n nh?t</h2>
                </div>
              </div>
              <article className="cp-card cp-card--secondary cp-last-service-card">
                <div>
                  <strong className="cp-last-service-code">{last.orderCode}</strong>
                  <p className="cp-muted">
                    {last.licensePlate || "�"} � {fmtDt(last.completedAt)} � {last.branchName || "�"}
                  </p>
                </div>
                {last.vehicleId ? (
                  <Link className="cp-btn cp-btn-ghost" to={`/my-vehicles/${last.vehicleId}`}>
                    M? chi ti?t xe
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


