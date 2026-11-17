import { useEffect, useMemo, useState } from "react";
import { Link, useSearchParams } from "react-router-dom";
import Footer from "@shared/components/Footer";
import Header from "@shared/components/Header";
import { getCustomerDashboard } from "@features/customerPortal/api/customerPortalApi";
import {
  Car,
  Wrench,
  MapPin,
  Clock,
  UserCheck,
  FileText,
  Layers,
  Check,
  Sparkles,
  Search,
  ClipboardCheck,
  ArrowRight,
  Calendar,
  Box,
  AlertCircle,
  Camera,
} from "lucide-react";
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

function renderStepIcon(code, state, index) {
  if (state === "past") {
    return <Check size={16} strokeWidth={2.8} />;
  }
  switch (code) {
    case "RECEIVED":
      return <ClipboardCheck size={16} />;
    case "INSPECTING":
      return <Search size={16} />;
    case "QUOTE":
      return <FileText size={16} />;
    case "REPAIRING":
    case "WAITING_PARTS":
      return <Wrench size={16} />;
    case "READY":
      return <Sparkles size={16} />;
    case "DELIVERED":
      return <Car size={16} />;
    default:
      return <span>{index + 1}</span>;
  }
}

function getStageBadgeClass(stageCode, lifecycleStatus) {
  const code = String(stageCode || lifecycleStatus || "").toUpperCase();
  if (code.includes("READY") || code.includes("DELIVERED") || code.includes("COMPLETED")) {
    return "cp-stage-badge--success";
  }
  if (code.includes("REPAIRING") || code.includes("IN_PROGRESS") || code.includes("INSPECTING")) {
    return "cp-stage-badge--active";
  }
  if (code.includes("WAIT") || code.includes("QUOTE")) {
    return "cp-stage-badge--warning";
  }
  if (code.includes("CANCEL")) {
    return "cp-stage-badge--danger";
  }
  return "cp-stage-badge--neutral";
}

export default function GarageDashboardPage() {
  const [searchParams] = useSearchParams();
  const highlightSo = searchParams.get("serviceOrderId");
  const [data, setData] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");

  useEffect(() => {
    let cancelled = false;
    let busy = false;
    const load = async () => {
      if (busy) return;
      busy = true;
      try {
        const nextData = await getCustomerDashboard();
        if (!cancelled) {
          setData(nextData);
          setError("");
        }
      } catch (e) {
        if (!cancelled) setError(e?.message || "Không tải được dữ liệu.");
      } finally {
        if (!cancelled) setLoading(false);
        busy = false;
      }
    };
    void load();
    const refresh = () => { if (!document.hidden) void load(); };
    const timer = setInterval(refresh, 15000);
    window.addEventListener("focus", refresh);
    return () => {
      cancelled = true;
      clearInterval(timer);
      window.removeEventListener("focus", refresh);
    };
  }, []);

  const tracks = data?.activeTracks || [];
  const appointments = data?.upcomingAppointments || [];
  const last = data?.lastCompletedService;

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
      title: "Garage đang sẵn sàng tiếp nhận xe của bạn",
      body: "Khi có lệnh sửa chữa hoặc lịch hẹn tới xưởng, toàn bộ quy trình và tiến độ xử lý xe sẽ được cập nhật tại đây theo thời gian thực.",
    };
  }, [appointments, last, tracks.length]);

  return (
    <div className="cp-page cp-dashboard-page">
      {/* CINEMATIC HERO SECTION FOR MAIN TAB */}
      <section className="dashboard-track-hero">
        <img
          className="dashboard-track-hero-img"
          src="https://images.unsplash.com/photo-1625047509168-a7026f36de04?auto=format&fit=crop&w=2400&q=85"
          alt="Kỹ thuật viên thao tác bảo dưỡng và sửa chữa xe tại xưởng MyGarage"
          loading="eager"
        />
        <div className="dashboard-track-hero-overlay" />
        <div className="dashboard-track-hero-fade" />
        <Header overlay />

        <div className="dashboard-track-hero-container">
          <div className="track-hero-copy">
            <h1 className="track-hero-title">
              Theo Dõi <span>Tiến Độ Sửa Xe</span> Trực Tuyến
            </h1>
            <p className="track-hero-desc">
              Cập nhật trực tiếp quy trình kiểm tra, khoang kỹ thuật thao tác, KTV phụ trách và thời điểm bàn giao xe theo thời gian thực.
            </p>

            <div className="track-hero-nav-pills">
              <Link className="track-nav-pill active" to="/my-garage-dashboard">
                <Wrench size={14} />
                <span>Tiến độ sửa xe</span>
              </Link>
              <Link className="track-nav-pill" to="/my-vehicles">
                <Car size={14} />
                <span>Đội xe của tôi</span>
              </Link>
              <Link className="track-nav-pill" to="/my-appointments">
                <Calendar size={14} />
                <span>Lịch hẹn ({appointments.length})</span>
              </Link>
              <Link className="track-nav-pill" to="/my-garage-3d">
                <Box size={14} />
                <span>Vị trí xe 3D</span>
              </Link>
            </div>
          </div>

          <div className="track-hero-summary-box">
            <div className="track-hero-stat-item">
              <span className="track-hero-stat-num">{tracks.length}</span>
              <span className="track-hero-stat-text">Xe đang ở xưởng</span>
            </div>
            <div className="track-hero-stat-divider" />
            <div className="track-hero-stat-item">
              <span className="track-hero-stat-num">{appointments.length}</span>
              <span className="track-hero-stat-text">Lịch hẹn sắp tới</span>
            </div>
          </div>
        </div>
      </section>

      {/* MAIN CONTENT AREA */}
      <div className="dashboard-track-content">
        <div className="cp-inner">
          {/* Error Banner */}
          {error ? (
            <div className="cp-error-card">
              <AlertCircle size={20} />
              <span>{error}</span>
            </div>
          ) : null}

          {/* Loading Indicator */}
          {loading ? (
            <div className="cp-loading-card">
              <div className="cp-spinner" />
              <span>Đang đồng bộ dữ liệu tiến độ xe từ xưởng...</span>
            </div>
          ) : null}

          {/* Empty State when no active vehicles */}
          {!loading && !tracks.length && fallbackSummary ? (
            <section className="cp-card cp-empty-state cp-dashboard-empty">
              <div className="cp-empty-icon-circle">
                <Wrench size={32} />
              </div>
              <h2 className="cp-empty-title">{fallbackSummary.title}</h2>
              <p className="cp-empty-desc">{fallbackSummary.body}</p>
              <div className="cp-empty-actions">
                <Link className="cp-btn cp-btn-accent" to="/booking">
                  <Calendar size={15} />
                  <span>Đặt lịch bảo dưỡng ngay</span>
                </Link>
                <Link className="cp-btn cp-btn-ghost" to="/my-vehicles">
                  <Car size={15} />
                  <span>Xem đội xe của tôi</span>
                </Link>
              </div>
            </section>
          ) : null}

          {/* Active Vehicles List */}
          {!loading &&
            tracks.map((track) => {
              const isHighlighted =
                highlightSo && String(track.serviceOrderId) === String(highlightSo);
              const assignedNames = Array.isArray(track.assignedTechnicianNames)
                ? track.assignedTechnicianNames.filter(Boolean)
                : [];
              const hasActiveTechnician = assignedNames.length > 0;
              const stageBadgeClass = getStageBadgeClass(
                track.customerStageCode,
                track.lifecycleStatus
              );

              return (
                <article
                  key={track.serviceOrderId}
                  className={`cp-card cp-track-card ${
                    isHighlighted ? "cp-track-card--hi" : ""
                  }`}
                >
                  <div className="cp-track-shell">
                    {/* Header: License plate, Model, Order code & Status */}
                    <div className="cp-track-head">
                      <div className="cp-veh-head-left">
                        <div className="cp-license-plate-badge">
                          <span className="cp-plate-screw" />
                          <span className="cp-plate-number">
                            {track.licensePlate || "Xe của bạn"}
                          </span>
                          <span className="cp-plate-screw" />
                        </div>
                        <h2 className="cp-veh-model">
                          {track.modelLabel || "Đang cập nhật model"}
                        </h2>
                      </div>

                      <div className="cp-veh-head-right">
                        <span className="cp-track-ordercode-badge" title="Mã lệnh dịch vụ">
                          <FileText size={14} />
                          <span>{track.orderCode || "—"}</span>
                        </span>
                        <div className={`cp-stage-pill ${stageBadgeClass}`}>
                          <span className="cp-live-dot" />
                          <span>{track.customerStageLabel || "Đang xử lý"}</span>
                        </div>
                      </div>
                    </div>

                    {/* Connected Stepper Progress */}
                    <div className="cp-stepper-wrap" aria-label="Tiến trình sửa chữa">
                      <div className="cp-stepper-track">
                        {(track.timeline || []).map((step, index) => (
                          <div
                            key={`${track.serviceOrderId}-${step.code}-${index}`}
                            className={`cp-stepper-step cp-stepper-step--${step.state || "upcoming"}`}
                          >
                            <div className="cp-step-node">
                              {renderStepIcon(step.code, step.state, index)}
                            </div>
                            <div className="cp-step-info">
                              <span className="cp-step-label">{step.label}</span>
                            </div>
                          </div>
                        ))}
                      </div>
                    </div>

                    {/* 5-Column Telemetry Grid */}
                    <div className="cp-telemetry-grid">
                      <div className="cp-telemetry-tile">
                        <div className="cp-telemetry-label">
                          <MapPin size={13} className="cp-tile-icon" />
                          <span>Chi nhánh xử lý</span>
                        </div>
                        <div className="cp-telemetry-value">{track.branchName || "—"}</div>
                      </div>

                      <div className="cp-telemetry-tile">
                        <div className="cp-telemetry-label">
                          <Wrench size={13} className="cp-tile-icon" />
                          <span>Kỹ thuật viên</span>
                        </div>
                        <div className="cp-telemetry-value">
                          {hasActiveTechnician ? (
                            <div className="cp-tech-chips">
                              {assignedNames.map((name) => (
                                <span key={name} className="cp-tech-badge">
                                  <UserCheck size={11} />
                                  <span>{name}</span>
                                </span>
                              ))}
                            </div>
                          ) : (
                            <span className="cp-val-fallback">
                              {track.serviceAdvisorName || "Đang phân bổ"}
                            </span>
                          )}
                        </div>
                      </div>

                      <div className="cp-telemetry-tile">
                        <div className="cp-telemetry-label">
                          <UserCheck size={13} className="cp-tile-icon" />
                          <span>Cố vấn dịch vụ</span>
                        </div>
                        <div className="cp-telemetry-value">
                          {track.serviceAdvisorName || "Đang phân bổ"}
                        </div>
                      </div>

                      <div className="cp-telemetry-tile">
                        <div className="cp-telemetry-label">
                          <Layers size={13} className="cp-tile-icon" />
                          <span>Khoang / Vị trí xe</span>
                        </div>
                        <div className="cp-telemetry-value">
                          {track.bayName || track.bayCode ? (
                            <span className="cp-bay-badge">
                              <Box size={12} />
                              <span>{[track.serviceArea, track.bayCode, track.bayName].filter(Boolean).join(" · ")}</span>
                            </span>
                          ) : (
                            <span className="cp-val-fallback">Chưa xác nhận vị trí</span>
                          )}
                          {track.locationReportedAt ? <small>{fmtDt(track.locationReportedAt)}{track.locationReportedBy ? ` · ${track.locationReportedBy}` : ""}</small> : null}
                          {track.locationNote ? <small>{track.locationNote}</small> : null}
                        </div>
                      </div>

                      <div className="cp-telemetry-tile cp-telemetry-tile--highlight">
                        <div className="cp-telemetry-label">
                          <Clock size={13} className="cp-tile-icon" />
                          <span>Dự kiến bàn giao</span>
                        </div>
                        <div className="cp-telemetry-value cp-val-time">
                          {fmtDt(track.estimatedHandover || track.promisedHandoverTime)}
                        </div>
                      </div>
                    </div>

                    {/* Card Actions Footer */}
                    <div className="cp-track-actions">
                      <Link
                        className="cp-btn cp-btn-primary"
                        to={`/my-vehicles/${track.vehicleId}?tab=history&serviceOrderId=${track.serviceOrderId}`}
                      >
                        <FileText size={15} />
                        <span>Xem chi tiết & Lịch sử lệnh</span>
                        <ArrowRight size={14} />
                      </Link>
                      <Link
                        className="cp-btn cp-btn-secondary"
                        to={`/my-vehicles/${track.vehicleId}/condition-report?serviceOrderId=${track.serviceOrderId}`}
                      >
                        <Camera size={15} />
                        <span>Ảnh & Biên bản tiếp nhận</span>
                      </Link>
                      <Link className="cp-btn cp-btn-ghost" to={`/my-vehicles/${track.vehicleId}`}>
                        <Car size={15} />
                        <span>Chi tiết xe</span>
                      </Link>
                        <Link className="cp-btn cp-btn-ghost" to="/my-garage-3d">
                          <Box size={15} />
                          <span>Xem vị trí xe 3D</span>
                        </Link>
                    </div>
                  </div>
                </article>
              );
            })}

          {/* Upcoming Appointments Section */}
          {!loading && appointments.length > 0 ? (
            <section className="cp-section">
              <div className="cp-section-head">
                <div>
                  <h2 className="cp-section-title">Lịch hẹn sắp tới</h2>
                  <p className="cp-section-subtitle">Các lịch hẹn dịch vụ của bạn tại các chi nhánh MyGarage</p>
                </div>
                <Link className="cp-btn cp-btn-ghost cp-btn-sm" to="/my-appointments">
                  <span>Xem tất cả ({appointments.length})</span>
                  <ArrowRight size={14} />
                </Link>
              </div>

              <div className="cp-grid-2">
                {appointments.map((appointment) => (
                  <article key={appointment.id} className="cp-card cp-appt-card">
                    <div className="cp-appt-card-head">
                      {appointment.vehiclePlate ? (
                        <div className="cp-license-plate-badge cp-license-plate-badge--sm">
                          <span className="cp-plate-screw" />
                          <span className="cp-plate-number">{appointment.vehiclePlate}</span>
                          <span className="cp-plate-screw" />
                        </div>
                      ) : (
                        <span className="cp-appt-plate-fallback">Xe hẹn mới</span>
                      )}
                      <span className="cp-chip cp-chip--soft">{appointment.status || "Chờ tiếp nhận"}</span>
                    </div>
                    <div className="cp-appt-body">
                      <div className="cp-appt-row">
                        <MapPin size={14} className="cp-tile-icon" />
                        <strong>{appointment.branchName || "Chi nhánh MyGarage"}</strong>
                      </div>
                      <div className="cp-appt-row">
                        <Clock size={14} className="cp-tile-icon" />
                        <span>{fmtDt(appointment.startTime)}</span>
                      </div>
                    </div>
                  </article>
                ))}
              </div>
            </section>
          ) : null}

          {/* Last Completed Service */}
          {!loading && last && !tracks.length ? (
            <section className="cp-section">
              <div className="cp-section-head">
                <div>
                  <h2 className="cp-section-title">Lần sửa chữa gần nhất</h2>
                  <p className="cp-section-subtitle">Lệnh dịch vụ đã hoàn thành và bàn giao xe thành công</p>
                </div>
              </div>
              <article className="cp-card cp-last-service-card">
                <div className="cp-last-service-left">
                  <div className="cp-last-check-icon">
                    <Check size={20} strokeWidth={2.5} />
                  </div>
                  <div>
                    <div className="cp-last-code-row">
                      <span className="cp-track-ordercode-badge">{last.orderCode}</span>
                      {last.licensePlate ? (
                        <span className="cp-plate-tag">{last.licensePlate}</span>
                      ) : null}
                    </div>
                    <p className="cp-last-meta">
                      Hoàn tất lúc: {fmtDt(last.completedAt)} · {last.branchName || "Chi nhánh MyGarage"}
                    </p>
                  </div>
                </div>
                {last.vehicleId ? (
                  <Link className="cp-btn cp-btn-ghost cp-btn-sm" to={`/my-vehicles/${last.vehicleId}`}>
                    <span>Mở hồ sơ xe</span>
                    <ArrowRight size={14} />
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
