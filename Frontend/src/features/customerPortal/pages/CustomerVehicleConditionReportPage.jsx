import React, { useEffect, useMemo, useState, useCallback } from "react";
import { Link, useNavigate, useParams, useSearchParams } from "react-router-dom";
import Footer from "@shared/components/Footer";
import Header from "@shared/components/Header";
import {
  getCustomerVehicles,
  getVehicleHistory,
  getServiceOrderLinkContext,
} from "@features/customerPortal/api/customerPortalApi";
import {
  Camera,
  FileText,
  CheckCircle2,
  AlertTriangle,
  Fuel,
  Gauge,
  Shield,
  Printer,
  ArrowLeft,
  Car,
  Calendar,
  MapPin,
  Maximize2,
  X,
  ChevronLeft,
  ChevronRight,
  Eye,
} from "lucide-react";
import "./CustomerPortalShell.css";
import "./CustomerVehicleConditionReportPage.css";

function fmtDt(val) {
  if (!val) return "—";
  try {
    return new Date(val).toLocaleString("vi-VN", {
      dateStyle: "medium",
      timeStyle: "short",
    });
  } catch {
    return String(val);
  }
}

function parseDefectsData(raw) {
  if (!raw) return [];
  try {
    const parsed = JSON.parse(raw);
    if (Array.isArray(parsed)) return parsed;
    if (Array.isArray(parsed?.defectItems)) return parsed.defectItems;
    return [];
  } catch {
    return [];
  }
}

function sanitizeWarningLights(raw) {
  const val = String(raw || "").trim();
  const up = val.toUpperCase();
  if (!val || up === "PENDING" || up === "ASSIGNED" || up === "COMPLETED") return [];
  return val
    .split(",")
    .map((s) => s.trim())
    .filter(Boolean);
}

function humanizePhase(type) {
  const norm = String(type || "").trim().toUpperCase();
  switch (norm) {
    case "CHECK_IN":
      return "Tiếp nhận xe vào xưởng";
    case "CHECK_OUT":
    case "HANDOVER":
      return "Bàn giao xe xuất xưởng";
    default:
      return "Biên bản hiện trạng";
  }
}

export default function CustomerVehicleConditionReportPage() {
  const { vehicleId, serviceOrderId: routeOrderId } = useParams();
  const [searchParams] = useSearchParams();
  const queryOrderId = searchParams.get("serviceOrderId") || routeOrderId;
  const navigate = useNavigate();

  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [vehicles, setVehicles] = useState([]);
  const [activeVehicleId, setActiveVehicleId] = useState(Number(vehicleId) || 0);
  const [history, setHistory] = useState(null);
  const [selectedOrderId, setSelectedOrderId] = useState(
    queryOrderId ? Number(queryOrderId) : null
  );
  const [selectedLogType, setSelectedLogType] = useState("CHECK_IN");

  // Lightbox state
  const [lightbox, setLightbox] = useState({
    isOpen: false,
    images: [],
    currentIndex: 0,
    part: "",
    issue: "",
  });

  // Resolve active vehicleId if came from serviceOrderId without vehicleId
  useEffect(() => {
    let cancelled = false;

    async function init() {
      setLoading(true);
      setError("");

      try {
        let currentVehId = Number(vehicleId);

        if ((!currentVehId || currentVehId <= 0) && queryOrderId) {
          const linkContext = await getServiceOrderLinkContext(Number(queryOrderId)).catch(() => null);
          if (linkContext?.vehicleId) {
            currentVehId = Number(linkContext.vehicleId);
            setActiveVehicleId(currentVehId);
          }
        }

        const vehicleList = await getCustomerVehicles().catch(() => []);
        if (cancelled) return;

        setVehicles(Array.isArray(vehicleList) ? vehicleList : []);

        if (!currentVehId || currentVehId <= 0) {
          if (vehicleList?.length > 0) {
            currentVehId = Number(vehicleList[0].vehicleId);
            setActiveVehicleId(currentVehId);
          } else {
            setError("Bạn chưa có xe nào đã lưu trên hệ thống.");
            setLoading(false);
            return;
          }
        } else {
          setActiveVehicleId(currentVehId);
        }

        // Fetch vehicle history which contains condition logs
        const nextHistory = await getVehicleHistory(currentVehId);
        if (cancelled) return;

        setHistory(nextHistory);

        // Auto-select order
        const orders = nextHistory?.items || [];
        const ordersWithLogs = orders.filter(
          (o) => Array.isArray(o.conditionLogs) && o.conditionLogs.length > 0
        );

        if (queryOrderId) {
          setSelectedOrderId(Number(queryOrderId));
        } else if (ordersWithLogs.length > 0) {
          setSelectedOrderId(Number(ordersWithLogs[0].serviceOrderId));
        } else if (orders.length > 0) {
          setSelectedOrderId(Number(orders[0].serviceOrderId));
        }
      } catch (err) {
        if (!cancelled) {
          setError(err?.message || "Không thể tải hồ sơ hiện trạng xe.");
        }
      } finally {
        if (!cancelled) setLoading(false);
      }
    }

    void init();

    return () => {
      cancelled = true;
    };
  }, [vehicleId, queryOrderId]);

  // Current active vehicle object
  const currentVehicle = useMemo(() => {
    return vehicles.find((v) => Number(v.vehicleId) === Number(activeVehicleId)) || null;
  }, [vehicles, activeVehicleId]);

  // Orders list
  const historyItems = useMemo(() => history?.items || [], [history]);

  // Current selected order
  const currentOrder = useMemo(() => {
    if (!selectedOrderId) return historyItems[0] || null;
    return (
      historyItems.find((o) => Number(o.serviceOrderId) === Number(selectedOrderId)) ||
      historyItems[0] ||
      null
    );
  }, [historyItems, selectedOrderId]);

  // Condition logs of the current order
  const logs = useMemo(() => {
    return Array.isArray(currentOrder?.conditionLogs) ? currentOrder.conditionLogs : [];
  }, [currentOrder]);

  // Determine available phases
  const availableLogTypes = useMemo(() => {
    const types = new Set(logs.map((l) => String(l.logType || "").toUpperCase()));
    return Array.from(types);
  }, [logs]);

  // Active condition log (Check-in or Check-out)
  const activeLog = useMemo(() => {
    if (logs.length === 0) return null;
    const match = logs.find(
      (l) => String(l.logType || "").toUpperCase() === String(selectedLogType).toUpperCase()
    );
    return match || logs[0];
  }, [logs, selectedLogType]);

  // Parsed defect items
  const defectItems = useMemo(() => {
    return parseDefectsData(activeLog?.defectsAndImages);
  }, [activeLog]);

  // Warning lights list
  const warningList = useMemo(() => {
    return sanitizeWarningLights(activeLog?.dashboardWarningLights);
  }, [activeLog]);

  // Lightbox handlers
  const openLightbox = (images, index, part, issue) => {
    setLightbox({
      isOpen: true,
      images,
      currentIndex: index,
      part,
      issue,
    });
  };

  const closeLightbox = () => {
    setLightbox((prev) => ({ ...prev, isOpen: false }));
  };

  const prevImage = useCallback(() => {
    setLightbox((prev) => ({
      ...prev,
      currentIndex:
        prev.currentIndex > 0 ? prev.currentIndex - 1 : prev.images.length - 1,
    }));
  }, []);

  const nextImage = useCallback(() => {
    setLightbox((prev) => ({
      ...prev,
      currentIndex:
        prev.currentIndex < prev.images.length - 1 ? prev.currentIndex + 1 : 0,
    }));
  }, []);

  // Keyboard navigation for lightbox
  useEffect(() => {
    if (!lightbox.isOpen) return;
    const onKeyDown = (e) => {
      if (e.key === "Escape") closeLightbox();
      if (e.key === "ArrowLeft") prevImage();
      if (e.key === "ArrowRight") nextImage();
    };
    window.addEventListener("keydown", onKeyDown);
    return () => window.removeEventListener("keydown", onKeyDown);
  }, [lightbox.isOpen, prevImage, nextImage]);

  const handlePrint = () => {
    window.print();
  };

  return (
    <div className="cr-page">
      <Header />
      <div className="cr-shell">
        <div className="cr-inner">
          {/* Top Navigation */}
          <div className="cr-top-nav">
            <Link to="/my-garage-dashboard" className="cr-back-link">
              <ArrowLeft size={16} /> Quay lại tiến độ xe
            </Link>
            <div className="cr-top-actions">
              {activeVehicleId ? (
                <Link
                  to={`/my-vehicles/${activeVehicleId}?tab=history`}
                  className="cr-btn cr-btn-ghost"
                >
                  <Car size={15} /> Chi tiết xe & Lịch sử
                </Link>
              ) : null}
              <button
                type="button"
                className="cr-btn cr-btn-primary"
                onClick={handlePrint}
                title="In biên bản hoặc lưu dưới dạng file PDF"
              >
                <Printer size={15} /> In biên bản tiếp nhận
              </button>
            </div>
          </div>

          {loading ? (
            <div className="cr-empty-card">
              <div className="cr-empty-desc">Đang tải hồ sơ hiện trạng xe...</div>
            </div>
          ) : error ? (
            <div className="cr-empty-card">
              <AlertTriangle className="cr-empty-icon" style={{ color: "#ef4444" }} />
              <h2 className="cr-empty-title">Thông báo</h2>
              <p className="cr-empty-desc">{error}</p>
              <Link to="/my-garage-dashboard" className="cr-btn cr-btn-primary">
                Về trang tiến độ xe
              </Link>
            </div>
          ) : (
            <>
              {/* Hero & Vehicle Ribbon */}
              <section className="cr-hero-card">
                <h1 className="cr-hero-title">Biên bản hiện trạng xe</h1>

                <div className="cr-vehicle-ribbon">
                  <div className="cr-plate-badge">
                    <span className="cr-plate-text">
                      {currentVehicle?.licensePlate || "XE DỊCH VỤ"}
                    </span>
                  </div>

                  <div className="cr-vehicle-meta">
                    <span className="cr-vehicle-name">
                      {[currentVehicle?.brandName, currentVehicle?.modelName]
                        .filter(Boolean)
                        .join(" ") || "Thông tin phương tiện"}
                    </span>
                    <div className="cr-vehicle-sub">
                      {currentVehicle?.color ? (
                        <span>Màu: {currentVehicle.color}</span>
                      ) : null}
                      {currentOrder?.branchName ? (
                        <span>Chi nhánh: {currentOrder.branchName}</span>
                      ) : null}
                      {currentOrder?.checkInTime ? (
                        <span>Vào xưởng: {fmtDt(currentOrder.checkInTime)}</span>
                      ) : null}
                    </div>
                  </div>

                  <div className="cr-order-code-pill">
                    <small>Mã lệnh sửa chữa</small>
                    <strong>{currentOrder?.orderCode || "—"}</strong>
                  </div>
                </div>
              </section>

              {/* Selector Toolbar (Khi có nhiều đơn hoặc nhiều đợt check-in) */}
              <div className="cr-selector-toolbar">
                <div className="cr-order-select-group">
                  <label htmlFor="cr-order-picker" className="cr-order-select-label">
                    Chọn lần vào xưởng:
                  </label>
                  <select
                    id="cr-order-picker"
                    className="cr-order-select"
                    value={selectedOrderId || ""}
                    onChange={(e) => setSelectedOrderId(Number(e.target.value))}
                  >
                    {historyItems.map((o) => {
                      const timeStr = fmtDt(o.checkInTime || o.completedAt);
                      const stageStr = o.customerStageLabel || o.status;
                      const label = stageStr ? `${timeStr} (${stageStr})` : timeStr;
                      return (
                        <option key={o.serviceOrderId} value={o.serviceOrderId}>
                          {label}
                        </option>
                      );
                    })}
                  </select>
                </div>

                {availableLogTypes.length > 1 ? (
                  <div className="cr-phase-tabs">
                    {availableLogTypes.map((type) => (
                      <button
                        key={type}
                        type="button"
                        className={`cr-phase-tab ${
                          selectedLogType === type ? "active" : ""
                        }`}
                        onClick={() => setSelectedLogType(type)}
                      >
                        {humanizePhase(type)}
                      </button>
                    ))}
                  </div>
                ) : null}
              </div>

              {!activeLog ? (
                <div className="cr-empty-card">
                  <FileText className="cr-empty-icon" />
                  <h2 className="cr-empty-title">Chưa có biên bản hiện trạng cho lần này</h2>
                  <p className="cr-empty-desc">
                    Cố vấn dịch vụ đang trong quá trình lập biên bản và chụp ảnh tiếp nhận tại xưởng.
                    Thông tin sẽ tự động hiển thị tại đây ngay khi hoàn tất.
                  </p>
                </div>
              ) : (
                <>
                  {/* Vehicle Vitals Grid */}
                  <div className="cr-vitals-grid">
                    {/* ODO Reading */}
                    <div className="cr-vital-card">
                      <div className="cr-vital-head">
                        <Gauge size={16} /> Số ODO tiếp nhận
                      </div>
                      <div className="cr-vital-value">
                        {activeLog.odoReading != null
                          ? `${Number(activeLog.odoReading).toLocaleString("vi-VN")} km`
                          : "—"}
                      </div>
                    </div>

                    {/* Fuel Level */}
                    <div className="cr-vital-card">
                      <div className="cr-vital-head">
                        <Fuel size={16} /> Mức nhiên liệu
                      </div>
                      <div className="cr-vital-value">
                        {activeLog.fuelLevel != null ? `${activeLog.fuelLevel}%` : "50%"}
                      </div>
                      <div className="cr-fuel-meter">
                        <div className="cr-fuel-bar">
                          <div
                            className="cr-fuel-fill"
                            style={{
                              width: `${Math.min(100, Math.max(0, activeLog.fuelLevel != null ? activeLog.fuelLevel : 50))}%`,
                            }}
                          />
                        </div>
                        <div className="cr-fuel-markers">
                          <span>E (Hết)</span>
                          <span>1/2</span>
                          <span>F (Đầy)</span>
                        </div>
                      </div>
                    </div>

                    {/* Dashboard Warnings */}
                    <div className="cr-vital-card">
                      <div className="cr-vital-head">
                        <AlertTriangle size={16} /> Đèn cảnh báo Taplo
                      </div>
                      <div className="cr-vital-value" style={{ fontSize: 16 }}>
                        {warningList.length > 0 ? (
                          <div style={{ display: "flex", flexWrap: "wrap", gap: 6 }}>
                            {warningList.map((w) => (
                              <span
                                key={w}
                                className="cr-status-badge cr-status-badge--warning"
                                style={{
                                  background: "#fef2f2",
                                  color: "#b91c1c",
                                  border: "1px solid #fecaca",
                                  padding: "3px 8px",
                                  borderRadius: 6,
                                  fontSize: 12,
                                  fontWeight: 700,
                                }}
                              >
                                {w}
                              </span>
                            ))}
                          </div>
                        ) : (
                          <span
                            style={{
                              color: "#15803d",
                              display: "inline-flex",
                              alignItems: "center",
                              gap: 6,
                              fontSize: 14,
                            }}
                          >
                            <CheckCircle2 size={16} /> Không có đèn báo lỗi
                          </span>
                        )}
                      </div>
                    </div>

                    {/* Valuables / Belongings */}
                    <div className="cr-vital-card">
                      <div className="cr-vital-head">
                        <Shield size={16} /> Tài sản trên xe
                      </div>
                      <div
                        className="cr-vital-value"
                        style={{ fontSize: 14, fontWeight: 600, color: "#334155" }}
                      >
                        {activeLog.valuables && String(activeLog.valuables).trim()
                          ? activeLog.valuables
                          : "Không có đồ giá trị để lại"}
                      </div>
                    </div>
                  </div>

                  {/* Walkaround Gallery Section */}
                  <section className="cr-gallery-section">
                    <div className="cr-section-header">
                      <div>
                        <h2 className="cr-section-title">
                          <Camera size={18} color="#1d4ed8" /> Hình ảnh 4 góc xe & thân vỏ
                        </h2>
                      </div>
                    </div>

                    {defectItems.length === 0 ? (
                      <div className="cr-no-photo-box">
                        Chưa có danh mục ảnh kiểm tra thân vỏ chi tiết cho biên bản này.
                      </div>
                    ) : (
                      <div className="cr-defect-cards-grid">
                        {defectItems.map((item, idx) => {
                          const hasIssue = Boolean(String(item.issue || "").trim());
                          const images = Array.isArray(item.images) ? item.images : [];

                          return (
                            <div
                              key={item.id || idx}
                              className={`cr-defect-card ${hasIssue ? "has-issue" : "clean"}`}
                            >
                              <div className="cr-defect-head">
                                <div>
                                  <span className="cr-defect-part">
                                    {item.part || `Vị trí #${idx + 1}`}
                                  </span>
                                  {hasIssue ? (
                                    <div className="cr-defect-issue-text">
                                      <AlertTriangle size={14} /> Có ghi nhận: {item.issue}
                                    </div>
                                  ) : (
                                    <div className="cr-defect-clean-text">
                                      <CheckCircle2 size={14} /> Bình thường / Không trầy xước
                                    </div>
                                  )}
                                </div>
                                <span className="cr-hero-badge" style={{ margin: 0, padding: "4px 10px" }}>
                                  <Camera size={12} /> {images.length} ảnh
                                </span>
                              </div>

                              {images.length > 0 ? (
                                <div className="cr-photo-grid">
                                  {images.map((imgUrl, imgIdx) => (
                                    <button
                                      key={imgIdx}
                                      type="button"
                                      className="cr-photo-thumb"
                                      onClick={() =>
                                        openLightbox(
                                          images,
                                          imgIdx,
                                          item.part || `Vị trí #${idx + 1}`,
                                          item.issue
                                        )
                                      }
                                      title="Nhấp để phóng to"
                                    >
                                      <img
                                        src={imgUrl}
                                        alt={`${item.part || "Góc xe"} - ${imgIdx + 1}`}
                                        loading="lazy"
                                      />
                                      <div className="cr-photo-overlay">
                                        <Maximize2 size={20} />
                                      </div>
                                    </button>
                                  ))}
                                </div>
                              ) : (
                                <div className="cr-no-photo-box">
                                  Không có vết trầy xước phát sinh ở góc này
                                </div>
                              )}
                            </div>
                          );
                        })}
                      </div>
                    )}
                  </section>

                  {/* Signature & Transparency Cards */}
                  <div className="cr-footer-cards">
                    {/* Customer Signature Card */}
                    <div className="cr-signature-card">
                      <div className="cr-vital-head">
                        <FileText size={16} /> Chữ ký xác nhận bàn giao
                      </div>
                      <div className="cr-sig-box">
                        {activeLog.customerSignatureUrl ? (
                          <>
                            <img
                              src={activeLog.customerSignatureUrl}
                              alt="Chữ ký xác nhận của khách hàng"
                              className="cr-sig-img"
                            />
                            <span className="cr-sig-time">
                              Ký nhận điện tử vào: {fmtDt(activeLog.createdAt)}
                            </span>
                          </>
                        ) : (
                          <div style={{ color: "#94a3b8", fontSize: 13, textAlign: "center" }}>
                            Chữ ký xác nhận trực tiếp bằng biên bản giấy tại quầy lễ tân
                          </div>
                        )}
                      </div>
                    </div>
                  </div>
                </>
              )}
            </>
          )}
        </div>
      </div>

      {/* Lightbox Modal */}
      {lightbox.isOpen && (
        <div className="cr-lightbox-backdrop" onClick={closeLightbox}>
          <div className="cr-lightbox-content" onClick={(e) => e.stopPropagation()}>
            <button
              type="button"
              className="cr-lightbox-close"
              onClick={closeLightbox}
              title="Đóng (Phím Esc)"
            >
              <X size={28} />
            </button>

            <img
              src={lightbox.images[lightbox.currentIndex]}
              alt={lightbox.part}
              className="cr-lightbox-img"
            />

            {lightbox.images.length > 1 && (
              <>
                <button
                  type="button"
                  className="cr-lightbox-nav cr-lightbox-nav--prev"
                  onClick={prevImage}
                  title="Ảnh trước (Mũi tên trái)"
                >
                  <ChevronLeft size={28} />
                </button>
                <button
                  type="button"
                  className="cr-lightbox-nav cr-lightbox-nav--next"
                  onClick={nextImage}
                  title="Ảnh kế tiếp (Mũi tên phải)"
                >
                  <ChevronRight size={28} />
                </button>
              </>
            )}

            <div className="cr-lightbox-caption">
              <strong>
                {lightbox.part} ({lightbox.currentIndex + 1} / {lightbox.images.length})
              </strong>
              {lightbox.issue ? <p>Mô tả vết trầy xước: {lightbox.issue}</p> : <p>Tình trạng: Bình thường</p>}
            </div>
          </div>
        </div>
      )}

      <Footer />
    </div>
  );
}
