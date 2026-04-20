import { useCallback, useEffect, useMemo, useState } from "react";
import { useNavigate } from "react-router-dom";
import Footer from "@shared/components/Footer";
import Header from "@shared/components/Header";
import {
  getCustomerNotifications,
  getServiceOrderLinkContext,
  markAllNotificationsRead,
  markNotificationRead,
} from "@features/customerPortal/api/customerPortalApi";
import {
  extractLegacySurveyServiceOrderId,
  isLegacySurveyNotificationLink,
  notificationCtaLabel,
  notificationTone,
  notificationTypeLabel,
  resolvePortalNotificationLink,
} from "@features/customerPortal/utils/notificationLinks";
import "./CustomerPortalShell.css";
import "./MyNotificationsPage.css";

function fmtDt(v) {
  if (!v) return "—";
  try {
    return new Date(v).toLocaleString("vi-VN", { dateStyle: "medium", timeStyle: "short" });
  } catch {
    return String(v);
  }
}

export default function MyNotificationsPage() {
  const navigate = useNavigate();
  const [filterUnread, setFilterUnread] = useState(false);
  const [unreadCount, setUnreadCount] = useState(0);
  const [items, setItems] = useState([]);
  const [resolvedLinks, setResolvedLinks] = useState({});
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [busyId, setBusyId] = useState(null);
  const [markingAll, setMarkingAll] = useState(false);

  const load = useCallback(async () => {
    setLoading(true);
    setError("");
    try {
      const res = await getCustomerNotifications({ unreadOnly: filterUnread });
      const nextItems = Array.isArray(res?.items) ? res.items : [];
      setUnreadCount(Number(res?.unreadCount) || 0);
      setItems(nextItems);

      const legacySurveyItems = nextItems.filter((item) =>
        isLegacySurveyNotificationLink(item.linkAction)
      );
      if (legacySurveyItems.length === 0) {
        setResolvedLinks({});
        return;
      }

      const entries = await Promise.all(
        legacySurveyItems.map(async (item) => {
          const serviceOrderId = extractLegacySurveyServiceOrderId(item.linkAction);
          if (!serviceOrderId) return [item.id, null];

          try {
            const context = await getServiceOrderLinkContext(serviceOrderId);
            return [item.id, context?.targetPath || null];
          } catch {
            return [item.id, null];
          }
        })
      );

      setResolvedLinks(
        Object.fromEntries(entries.filter(([, path]) => typeof path === "string" && path.length > 0))
      );
    } catch (e) {
      setError(e?.message || "Không t?i ??c th�ng b�o.");
    } finally {
      setLoading(false);
    }
  }, [filterUnread]);

  useEffect(() => {
    load();
  }, [load]);

  const totalCount = items.length;
  const actionableCount = useMemo(
    () =>
      items.filter((item) => {
        const path = resolvedLinks[item.id] || resolvePortalNotificationLink(item.linkAction);
        return Boolean(path);
      }).length,
    [items, resolvedLinks]
  );

  const onOpenLink = (item) => {
    const path = resolvedLinks[item.id] || resolvePortalNotificationLink(item.linkAction);
    if (path) navigate(path);
  };

  const onMarkOne = async (id) => {
    setBusyId(id);
    try {
      await markNotificationRead(id);
      await load();
    } catch (e) {
      setError(e?.message || "Không cập nhật được.");
    } finally {
      setBusyId(null);
    }
  };

  const onMarkAll = async () => {
    setMarkingAll(true);
    try {
      await markAllNotificationsRead();
      await load();
    } catch (e) {
      setError(e?.message || "Không cập nhật được.");
    } finally {
      setMarkingAll(false);
    }
  };

  return (
    <div className="cp-page">
      <Header />
      <div className="cp-shell">
        <div className="cp-inner">
          <section className="cp-card cp-card--highlight cp-notif-hero">
            <div className="cp-notif-hero-copy">
              <p className="cp-eyebrow">Garage của tôi</p>
              <h1 className="cp-title">Th�ng b�o</h1>
              <p className="cp-desc">
                Nhắc bảo dưỡng, khảo sát sau dịch vụ, bảo hiểm và cập nhật tiến độ từ xưởng
                đều được gom về một inbox dễ theo dõi.
              </p>
            </div>

            <div className="cp-notif-stats">
              <div className="cp-stat-card">
                <span className="cp-stat-label">Chưa đọc</span>
                <strong className="cp-stat-value">{unreadCount}</strong>
              </div>
              <div className="cp-stat-card">
                <span className="cp-stat-label">Đang hiển thị</span>
                <strong className="cp-stat-value">{totalCount}</strong>
              </div>
              <div className="cp-stat-card">
                <span className="cp-stat-label">Có CTA nội bộ</span>
                <strong className="cp-stat-value">{actionableCount}</strong>
              </div>
            </div>
          </section>

          <section className="cp-card cp-card--secondary cp-notif-toolbar-shell">
            <div className="cp-notif-toolbar">
              <div className="cp-notif-filters">
                <button
                  type="button"
                  className={`cp-tab ${!filterUnread ? "cp-tab--active" : ""}`}
                  onClick={() => setFilterUnread(false)}
                >
                  Tất cả
                </button>
                <button
                  type="button"
                  className={`cp-tab ${filterUnread ? "cp-tab--active" : ""}`}
                  onClick={() => setFilterUnread(true)}
                >
                  Chưa đọc
                </button>
              </div>
              <button
                type="button"
                className="cp-btn cp-btn-ghost"
                onClick={onMarkAll}
                disabled={markingAll || unreadCount === 0}
              >
                {markingAll ? "Đang x? lĐã" : "Đánh d?u d� d?c t?t c?"}
              </button>
            </div>
          </section>

          {error ? <div className="cp-error">{error}</div> : null}
          {loading ? <p className="cp-muted">Đang tải…</p> : null}

          {!loading && items.length === 0 ? (
            <section className="cp-card cp-empty-state">
              <h2 className="cp-empty-title">Inbox đang trống</h2>
              <p className="cp-muted">
                Khi có nhắc bảo dưỡng, bảo hiểm hay cập nhật từ xưởng, bạn sẽ thấy chúng ở đây.
              </p>
            </section>
          ) : null}

          <ul className="cp-notif-list">
            {!loading &&
              items.map((item) => {
                const resolvedPath = resolvedLinks[item.id] || resolvePortalNotificationLink(item.linkAction);
                const tone = notificationTone(item.derivedType);

                return (
                  <li
                    key={item.id}
                    className={`cp-card cp-notif-item cp-notif-item--${tone} ${
                      item.isRead ? "" : "cp-notif-item--unread"
                    }`}
                  >
                    <div className="cp-notif-item-head">
                      <div className="cp-notif-meta">
                        <span className={`cp-chip cp-chip--${tone}`}>
                          {notificationTypeLabel(item.derivedType)}
                        </span>
                        {!item.isRead ? <span className="cp-notif-unread-dot" /> : null}
                      </div>
                      <span className="cp-muted">{fmtDt(item.sentAt)}</span>
                    </div>

                    <div className="cp-notif-content">
                      <h2 className="cp-notif-title">{item.title}</h2>
                      <p className="cp-notif-body">{item.content}</p>
                    </div>

                    <div className="cp-notif-actions">
                      {resolvedPath ? (
                        <button
                          type="button"
                          className="cp-btn"
                          onClick={() => onOpenLink(item)}
                        >
                          {notificationCtaLabel(item.derivedType)}
                        </button>
                      ) : (
                        <span className="cp-muted cp-notif-hint">
                          Thông báo này hiện chưa có màn hình điều hướng trực tiếp.
                        </span>
                      )}

                      {!item.isRead ? (
                        <button
                          type="button"
                          className="cp-btn cp-btn-ghost"
                          disabled={busyId === item.id}
                          onClick={() => onMarkOne(item.id)}
                        >
                          {busyId === item.id ? "Đang c?p nh?t�" : "Đánh d?u d� d?c"}
                        </button>
                      ) : (
                        <span className="cp-notif-read-pill">Đã đọc</span>
                      )}
                    </div>
                  </li>
                );
              })}
          </ul>
        </div>
      </div>
      <Footer />
    </div>
  );
}


