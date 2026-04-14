import { useEffect, useMemo, useState } from "react";
import { Link, useNavigate, useParams, useSearchParams } from "react-router-dom";
import Footer from "@shared/components/Footer";
import Header from "@shared/components/Header";
import { getCustomerVehicles, getVehicleHistory } from "@features/customerPortal/api/customerPortalApi";
import "./CustomerPortalShell.css";
import "./VehicleDetailPage.css";

function fmtDt(value) {
  if (!value) return "—";
  try {
    return new Date(value).toLocaleString("vi-VN", { dateStyle: "medium", timeStyle: "short" });
  } catch {
    return String(value);
  }
}

function sanitizeConditionWarnings(raw) {
  const value = String(raw || "").trim();
  const normalized = value.toUpperCase();
  if (!value || normalized === "PENDING" || normalized === "ASSIGNED" || normalized === "COMPLETED") {
    return "";
  }
  return value;
}

function summarizeDefects(raw) {
  if (!raw) return "";
  try {
    const parsed = JSON.parse(raw);
    const items = Array.isArray(parsed)
      ? parsed
      : Array.isArray(parsed?.defectItems)
        ? parsed.defectItems
        : [];

    const summaries = items
      .map((item) => {
        const part = String(item?.part || item?.label || "").trim();
        const issue = String(item?.issue || item?.note || "").trim();
        const imageCount = Array.isArray(item?.images) ? item.images.length : 0;
        const bits = [part, issue].filter(Boolean);
        if (imageCount > 0) bits.push(`${imageCount} ảnh`);
        return bits.join(": ");
      })
      .filter(Boolean);

    return summaries.join(" | ");
  } catch {
    return String(raw);
  }
}

function humanizeLogType(logType) {
  const value = String(logType || "").trim().toUpperCase();
  switch (value) {
    case "CHECK_IN":
      return "Biên bản tiếp nhận";
    case "CHECK_OUT":
      return "Biên bản trả xe";
    default:
      return logType || "Biên bản tình trạng";
  }
}

function maintenanceKindLabel(kind) {
  switch (kind) {
    case "LAST_SERVICE_AGE":
      return "Chu kỳ thời gian";
    case "ODO_DELTA":
      return "Chu kỳ quãng đường";
    case "AI_FORECAST":
      return "Dự báo AI";
    default:
      return kind || "Khuyến cáo";
  }
}

const TABS = [
  { id: "overview", label: "Tổng quan" },
  { id: "history", label: "Lịch sử sửa chữa" },
  { id: "invoices", label: "Hóa đơn" },
  { id: "maintenance", label: "Bảo trì & gợi ý" },
];

export default function VehicleDetailPage() {
  const { vehicleId } = useParams();
  const navigate = useNavigate();
  const [searchParams, setSearchParams] = useSearchParams();
  const idNum = Number(vehicleId);
  const requestedTab = searchParams.get("tab");
  const focusedOrderId = searchParams.get("serviceOrderId");
  const initialTab = TABS.some((tab) => tab.id === requestedTab) ? requestedTab : "overview";
  const [tab, setTab] = useState(initialTab);
  const [vehicles, setVehicles] = useState([]);
  const [history, setHistory] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");

  const vehicle = useMemo(
    () => vehicles.find((item) => item.vehicleId === idNum) || null,
    [idNum, vehicles]
  );

  useEffect(() => {
    setTab(TABS.some((item) => item.id === requestedTab) ? requestedTab : "overview");
  }, [requestedTab]);

  useEffect(() => {
    if (!Number.isFinite(idNum) || idNum <= 0) {
      setError("Mã xe không hợp lệ.");
      setLoading(false);
      return;
    }

    let cancelled = false;
    (async () => {
      setLoading(true);
      setError("");
      try {
        const [vehicleList, nextHistory] = await Promise.all([
          getCustomerVehicles(),
          getVehicleHistory(idNum),
        ]);
        if (cancelled) return;
        setVehicles(Array.isArray(vehicleList) ? vehicleList : []);
        setHistory(nextHistory);
      } catch (e) {
        if (!cancelled) setError(e?.message || "Không tải được dữ liệu.");
      } finally {
        if (!cancelled) setLoading(false);
      }
    })();

    return () => {
      cancelled = true;
    };
  }, [idNum]);

  const items = history?.items || [];
  const hints = history?.maintenanceHints || [];
  const invoiceRows = useMemo(() => items.filter((row) => row.invoiceId || row.invoiceUrl), [items]);
  const conditionLogCount = useMemo(
    () => items.reduce((sum, row) => sum + (Array.isArray(row.conditionLogs) ? row.conditionLogs.length : 0), 0),
    [items]
  );

  const summaryStats = [
    { label: "Lệnh sửa chữa", value: items.length },
    { label: "Hóa đơn liên kết", value: invoiceRows.length },
    { label: "Biên bản tình trạng", value: conditionLogCount },
  ];

  const updateTab = (nextTab) => {
    setTab(nextTab);
    const nextParams = new URLSearchParams(searchParams);
    nextParams.set("tab", nextTab);
    setSearchParams(nextParams, { replace: true });
  };

  return (
    <div className="cp-page">
      <Header />
      <div className="cp-shell">
        <div className="cp-inner">
          <section className="cp-card cp-card--highlight cp-detail-hero">
            <div className="cp-detail-hero-main">
              <div className="cp-detail-head">
                <div>
                  <p className="cp-eyebrow">Garage của tôi</p>
                  <h1 className="cp-title">{vehicle?.licensePlate || "Chi tiết xe"}</h1>
                  <p className="cp-desc">
                    {[vehicle?.brandName, vehicle?.modelName].filter(Boolean).join(" ") ||
                      "Thông tin xe và lịch sử dịch vụ"}
                  </p>
                </div>

                <div className="cp-detail-head-actions">
                  <Link className="cp-btn cp-btn-ghost" to="/my-vehicles">
                    Về danh sách xe
                  </Link>
                  <Link className="cp-btn" to="/my-garage-dashboard">
                    Xem xe đang sửa
                  </Link>
                </div>
              </div>

              <div className="cp-detail-summary">
                {summaryStats.map((stat) => (
                  <div key={stat.label} className="cp-stat-card">
                    <span className="cp-stat-label">{stat.label}</span>
                    <strong className="cp-stat-value">{stat.value}</strong>
                  </div>
                ))}
              </div>
            </div>
          </section>

          {error ? <div className="cp-error">{error}</div> : null}
          {loading ? <p className="cp-muted">Đang tải…</p> : null}

          {!loading && history && !error ? (
            <>
              <div className="cp-tabs cp-detail-tabs" role="tablist">
                {TABS.map((tabItem) => (
                  <button
                    key={tabItem.id}
                    type="button"
                    role="tab"
                    aria-selected={tab === tabItem.id}
                    className={`cp-tab ${tab === tabItem.id ? "cp-tab--active" : ""}`}
                    onClick={() => updateTab(tabItem.id)}
                  >
                    {tabItem.label}
                  </button>
                ))}
              </div>

              {tab === "overview" && (
                <section className="cp-card cp-card--primary cp-detail-overview">
                  <div className="cp-overview-grid">
                    <div className="cp-overview-item">
                      <dt>Biển số</dt>
                      <dd>{vehicle?.licensePlate || "—"}</dd>
                    </div>
                    <div className="cp-overview-item">
                      <dt>VIN</dt>
                      <dd>{vehicle?.vin || "—"}</dd>
                    </div>
                    <div className="cp-overview-item">
                      <dt>Dòng xe</dt>
                      <dd>{[vehicle?.brandName, vehicle?.modelName].filter(Boolean).join(" ") || "—"}</dd>
                    </div>
                    <div className="cp-overview-item">
                      <dt>Màu xe / Năm</dt>
                      <dd>
                        {[vehicle?.color, vehicle?.manufactureYear]
                          .filter((value) => value != null && value !== "")
                          .join(" · ") || "—"}
                      </dd>
                    </div>
                    <div className="cp-overview-item">
                      <dt>ODO hiện tại</dt>
                      <dd>{vehicle?.odo != null ? `${vehicle.odo.toLocaleString("vi-VN")} km` : "—"}</dd>
                    </div>
                    <div className="cp-overview-item">
                      <dt>Chủ sở hữu hiện tại</dt>
                      <dd>{vehicle?.isCurrentOwner ? "Có" : "Không"}</dd>
                    </div>
                  </div>

                  <div className="cp-detail-inline-actions">
                    <button
                      type="button"
                      className="cp-btn cp-btn-ghost"
                      onClick={() => navigate("/my-vehicles")}
                    >
                      Cập nhật thông tin xe
                    </button>
                    <button type="button" className="cp-btn" onClick={() => updateTab("maintenance")}>
                      Xem gợi ý bảo trì
                    </button>
                  </div>
                </section>
              )}

              {tab === "history" && (
                <div className="cp-history-list">
                  {items.length === 0 ? (
                    <section className="cp-card cp-empty-state">
                      <h2 className="cp-empty-title">Chưa có lịch sử sửa chữa</h2>
                      <p className="cp-muted">
                        Lịch sử lệnh sửa chữa, dịch vụ và biên bản tiếp nhận/trả xe sẽ hiện ở đây khi phát sinh.
                      </p>
                    </section>
                  ) : (
                    items.map((row) => {
                      const isFocused =
                        focusedOrderId && String(row.serviceOrderId) === String(focusedOrderId);
                      return (
                        <article
                          key={row.serviceOrderId}
                          className={`cp-card cp-history-card ${
                            isFocused ? "cp-history-card--hi" : ""
                          }`}
                        >
                          <div className="cp-history-card-head">
                            <div>
                              <strong className="cp-history-code">{row.orderCode}</strong>
                              <p className="cp-muted cp-history-branch">
                                {row.branchName || "—"} · Vào xưởng: {fmtDt(row.checkInTime)}
                              </p>
                            </div>
                            <div className="cp-history-card-side">
                              <span className="cp-stage-pill">{row.customerStageLabel}</span>
                              <span className="cp-history-completed">
                                Hoàn tất: {fmtDt(row.completedAt)}
                              </span>
                            </div>
                          </div>

                          {row.lineSummaries?.length ? (
                            <ul className="cp-history-lines">
                              {row.lineSummaries.map((line) => (
                                <li key={line}>{line}</li>
                              ))}
                            </ul>
                          ) : null}

                          <div className="cp-history-finance">
                            <div className="cp-history-amount">
                              <span className="cp-muted">Tổng tiền</span>
                              <strong>
                                {row.totalAmount != null
                                  ? `${Number(row.totalAmount).toLocaleString("vi-VN")} ₫`
                                  : "—"}
                              </strong>
                            </div>
                            <span className="cp-chip cp-chip--soft">
                              Thanh toán: {row.paymentStatus || "—"}
                            </span>
                            {row.invoiceUrl ? (
                              <a
                                className="cp-btn cp-btn-ghost"
                                href={row.invoiceUrl}
                                target="_blank"
                                rel="noopener noreferrer"
                              >
                                Mở hóa đơn
                              </a>
                            ) : null}
                          </div>

                          {row.conditionLogs?.length > 0 ? (
                            <details className="cp-condition-logs" open={isFocused}>
                              <summary className="cp-condition-logs-summary">
                                Biên bản tiếp nhận / trả xe ({row.conditionLogs.length})
                              </summary>
                              <ul className="cp-condition-log-list">
                                {row.conditionLogs.map((log) => (
                                  <li key={log.id} className="cp-condition-log-item">
                                    <div className="cp-condition-log-head">
                                      <div>
                                        <span className="cp-chip cp-chip--soft">
                                          {humanizeLogType(log.logType)}
                                        </span>
                                        <p className="cp-muted cp-condition-log-time">
                                          {fmtDt(log.createdAt)}
                                        </p>
                                      </div>
                                      {log.customerSignatureUrl ? (
                                        <a
                                          className="cp-btn cp-btn-ghost cp-condition-log-link"
                                          href={log.customerSignatureUrl}
                                          target="_blank"
                                          rel="noopener noreferrer"
                                        >
                                          Xem chữ ký xác nhận
                                        </a>
                                      ) : null}
                                    </div>

                                    <div className="cp-condition-log-meta">
                                      {log.odoReading != null ? (
                                        <span className="cp-chip cp-chip--soft">ODO: {log.odoReading} km</span>
                                      ) : null}
                                      {log.fuelLevel != null ? (
                                        <span className="cp-chip cp-chip--soft">Nhiên liệu: {log.fuelLevel}%</span>
                                      ) : null}
                                      {sanitizeConditionWarnings(log.dashboardWarningLights) ? (
                                        <span className="cp-chip cp-chip--soft">
                                          Đèn cảnh báo: {sanitizeConditionWarnings(log.dashboardWarningLights)}
                                        </span>
                                      ) : null}
                                    </div>

                                    {log.valuables ? (
                                      <p className="cp-condition-log-text">
                                        Tài sản trên xe: {log.valuables}
                                      </p>
                                    ) : null}

                                    {log.defectsAndImages ? (
                                      <p className="cp-condition-log-text">
                                        Hư hỏng / ảnh check-in: {summarizeDefects(log.defectsAndImages)}
                                      </p>
                                    ) : null}
                                  </li>
                                ))}
                              </ul>
                            </details>
                          ) : null}
                        </article>
                      );
                    })
                  )}
                </div>
              )}

              {tab === "invoices" && (
                <div className="cp-history-list">
                  {invoiceRows.length === 0 ? (
                    <section className="cp-card cp-empty-state">
                      <h2 className="cp-empty-title">Chưa có hóa đơn liên kết</h2>
                      <p className="cp-muted">
                        Hóa đơn và trạng thái thanh toán của từng lệnh sẽ xuất hiện khi xưởng phát hành.
                      </p>
                    </section>
                  ) : (
                    invoiceRows.map((row) => (
                      <article key={`inv-${row.serviceOrderId}`} className="cp-card cp-card--secondary cp-invoice-card">
                        <div>
                          <strong className="cp-history-code">{row.orderCode}</strong>
                          <p className="cp-muted">
                            {row.branchName || "—"} · {fmtDt(row.completedAt)}
                          </p>
                        </div>
                        <div className="cp-history-finance">
                          <span className="cp-chip cp-chip--soft">
                            Thanh toán: {row.paymentStatus || "—"}
                          </span>
                          {row.totalAmount != null ? (
                            <strong className="cp-history-invoice-amount">
                              {Number(row.totalAmount).toLocaleString("vi-VN")} ₫
                            </strong>
                          ) : null}
                          {row.invoiceUrl ? (
                            <a
                              className="cp-btn"
                              href={row.invoiceUrl}
                              target="_blank"
                              rel="noopener noreferrer"
                            >
                              Mở hóa đơn
                            </a>
                          ) : (
                            <span className="cp-muted">Chưa có liên kết file</span>
                          )}
                        </div>
                      </article>
                    ))
                  )}
                </div>
              )}

              {tab === "maintenance" && (
                <section className="cp-maintenance-panel">
                  {hints.length === 0 ? (
                    <article className="cp-card cp-empty-state">
                      <h2 className="cp-empty-title">Chưa có khuyến cáo tự động</h2>
                      <p className="cp-muted">
                        Khi đạt ngưỡng thời gian, ODO hoặc có forecast AI sắp tới, hệ thống sẽ hiển thị gợi ý bảo trì ở đây.
                      </p>
                    </article>
                  ) : (
                    hints.map((hint, index) => (
                      <article key={`${hint.kind}-${index}`} className="cp-card cp-card--secondary cp-hint-card">
                        <div className="cp-hint-card-head">
                          <span className="cp-chip cp-chip--maintenance">
                            {maintenanceKindLabel(hint.kind)}
                          </span>
                          {hint.predictedDate ? (
                            <span className="cp-muted">Mốc gợi ý: {fmtDt(hint.predictedDate)}</span>
                          ) : null}
                        </div>
                        <p className="cp-hint-message">{hint.message}</p>
                      </article>
                    ))
                  )}
                </section>
              )}
            </>
          ) : null}
        </div>
      </div>
      <Footer />
    </div>
  );
}
