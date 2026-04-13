import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { Bot, Expand, Minimize2, Minus, SendHorizontal } from "lucide-react";
import { useAuth } from "@shared/core/context/AuthContext";
import {
  createChatbotSession,
  getChatbotSession,
  orchestrateChatbot,
} from "@features/chatbot/api/chatbotApi";
import "./CustomerChatbotWidget.css";

const STORAGE_KEY = "mygarage_chatbot_session_v1";
const WELCOME_HINT_SESSION_KEY = "mygarage_chatbot_welcome_hint_v1";
const GREETING_TEXT =
  "Xin chào. Mình là trợ lý AI MyGarage. Bạn có thể hỏi về bảo dưỡng, đặt lịch, báo giá, đơn sửa chữa và ưu đãi.";
const WELCOME_HINT_TEXT =
  "Xin chào, mình là trợ lý AI MyGarage. Mình có thể hỗ trợ đặt lịch, báo giá nhanh, tra cứu đơn sửa chữa và gợi ý dịch vụ phù hợp cho xe của bạn.";
const LOCKED_SESSION_STATUSES = new Set(["HANDED_OFF", "EXPIRED", "CLOSED", "ARCHIVED"]);

function normalizeRole(role) {
  return String(role || "").trim().toUpperCase();
}

function normalizeMessages(rawMessages) {
  const list = Array.isArray(rawMessages) ? rawMessages : [];
  return list
    .map((item) => ({
      id: item?.id ?? item?.Id ?? `msg-${Date.now()}-${Math.random().toString(36).slice(2, 7)}`,
      role: normalizeRole(item?.role ?? item?.Role),
      content: String(item?.content ?? item?.Content ?? "").trim(),
      createdAt: item?.createdAt ?? item?.CreatedAt ?? null,
      quickReplies: [],
      slotHoldExpiresAtUtc: null,
      bookingStep: null,
      slotHoldSecondsRemaining: null,
    }))
    .filter((item) => (item.role === "USER" || item.role === "ASSISTANT") && item.content.length > 0);
}

function normalizeStatus(status) {
  return String(status || "").trim().toUpperCase();
}

function isLockedSessionStatus(status) {
  return LOCKED_SESSION_STATUSES.has(normalizeStatus(status));
}

function isLockedSessionErrorMessage(message) {
  const normalized = String(message || "").trim().toLowerCase();
  return normalized.includes("handed off") || normalized.includes("session is locked");
}

function readStoredSession() {
  if (typeof window === "undefined") return null;
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    if (!raw) return null;
    const parsed = JSON.parse(raw);
    if (!parsed || Number(parsed.id) <= 0) return null;
    return {
      id: Number(parsed.id),
      token: typeof parsed.token === "string" ? parsed.token : "",
    };
  } catch {
    return null;
  }
}

function writeStoredSession(session) {
  if (typeof window === "undefined") return;
  if (!session || Number(session.id) <= 0) {
    localStorage.removeItem(STORAGE_KEY);
    return;
  }
  localStorage.setItem(
    STORAGE_KEY,
    JSON.stringify({
      id: Number(session.id),
      token: String(session.token || ""),
    })
  );
}

function toQuickReplyList(rawQuickReplies) {
  const list = Array.isArray(rawQuickReplies) ? rawQuickReplies : [];
  return list
    .map((item) => ({
      label: String(item?.label ?? item?.Label ?? "").trim(),
      value: String(item?.value ?? item?.Value ?? "").trim(),
    }))
    .filter((item) => item.label.length > 0 && item.value.length > 0);
}

function extractBookingWorkflowMeta(reply) {
  const bookingWorkflow = reply?.bookingWorkflow ?? reply?.BookingWorkflow ?? null;
  if (!bookingWorkflow) {
    return {
      quickReplies: [],
      slotHoldExpiresAtUtc: null,
      bookingStep: null,
      slotHoldSecondsRemaining: null,
      bookingPrompt: "",
    };
  }

  return {
    quickReplies: toQuickReplyList(bookingWorkflow?.quickReplies ?? bookingWorkflow?.QuickReplies),
    slotHoldExpiresAtUtc:
      bookingWorkflow?.slotHoldExpiresAtUtc ?? bookingWorkflow?.SlotHoldExpiresAtUtc ?? null,
    bookingStep: bookingWorkflow?.step ?? bookingWorkflow?.Step ?? null,
    slotHoldSecondsRemaining:
      bookingWorkflow?.slotHoldSecondsRemaining ?? bookingWorkflow?.SlotHoldSecondsRemaining ?? null,
    bookingPrompt: String(bookingWorkflow?.prompt ?? bookingWorkflow?.Prompt ?? "").trim(),
  };
}

function createLocalMessage(role, content, extra = {}) {
  return {
    id: `local-${Date.now()}-${Math.random().toString(36).slice(2, 8)}`,
    role,
    content: String(content || ""),
    createdAt: new Date().toISOString(),
    quickReplies: extra.quickReplies ?? [],
    slotHoldExpiresAtUtc: extra.slotHoldExpiresAtUtc ?? null,
    bookingStep: extra.bookingStep ?? null,
    slotHoldSecondsRemaining: extra.slotHoldSecondsRemaining ?? null,
  };
}

function formatMessageTime(value) {
  if (!value) return "";
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return "";
  return date.toLocaleTimeString("vi-VN", {
    hour12: false,
    hour: "2-digit",
    minute: "2-digit",
    timeZone: "Asia/Ho_Chi_Minh",
  });
}

function formatHoldCountdown(expiresAt, nowTick) {
  if (!expiresAt) return "";
  const expireMs = new Date(expiresAt).getTime();
  if (Number.isNaN(expireMs)) return "";
  const diffSeconds = Math.max(0, Math.floor((expireMs - nowTick) / 1000));
  const minutes = String(Math.floor(diffSeconds / 60)).padStart(2, "0");
  const seconds = String(diffSeconds % 60).padStart(2, "0");
  return diffSeconds > 0 ? `Giữ chỗ còn ${minutes}:${seconds}` : "Giữ chỗ đã hết hạn";
}

export default function CustomerChatbotWidget() {
  const { isAuthenticated, user } = useAuth();
  const roleCode = String(user?.roleCode || user?.RoleCode || "").toUpperCase();
  const isCustomer = isAuthenticated && roleCode === "CUSTOMER";
  const userId = String(user?.id ?? user?.Id ?? user?.userId ?? user?.UserId ?? "guest");
  const messageListRef = useRef(null);

  const [isOpen, setIsOpen] = useState(false);
  const [isExpanded, setIsExpanded] = useState(false);
  const [showWelcomeHint, setShowWelcomeHint] = useState(false);
  const [initializing, setInitializing] = useState(false);
  const [sending, setSending] = useState(false);
  const [error, setError] = useState("");
  const [session, setSession] = useState(null);
  const [resumeCandidate, setResumeCandidate] = useState(null);
  const [messages, setMessages] = useState([]);
  const [composeText, setComposeText] = useState("");
  const [nowTick, setNowTick] = useState(Date.now());

  const canSend = useMemo(
    () => !initializing && !sending && !resumeCandidate && composeText.trim().length > 0,
    [composeText, initializing, resumeCandidate, sending]
  );

  const latestQuickReplyMessageId = useMemo(() => {
    for (let i = messages.length - 1; i >= 0; i -= 1) {
      const message = messages[i];
      if (normalizeRole(message?.role) === "ASSISTANT" && (message?.quickReplies?.length ?? 0) > 0) {
        return message.id;
      }
    }
    return null;
  }, [messages]);

  const createNewSession = useCallback(async () => {
    const created = await createChatbotSession({});
    const nextSession = {
      id: created?.id ?? created?.Id,
      token: created?.sessionToken ?? created?.SessionToken ?? "",
    };
    setSession(nextSession);
    setResumeCandidate(null);
    setMessages([createLocalMessage("ASSISTANT", GREETING_TEXT)]);
    writeStoredSession(nextSession);
    return nextSession;
  }, []);

  const prepareSessionOnOpen = useCallback(async () => {
    if (session?.id > 0 || resumeCandidate?.session?.id > 0) {
      return;
    }

    const stored = readStoredSession();
    if (stored?.id > 0) {
      try {
        const detail = await getChatbotSession(stored.id);
        const status = normalizeStatus(detail?.status ?? detail?.Status);
        if (isLockedSessionStatus(status)) {
          writeStoredSession(null);
          await createNewSession();
          return;
        }

        const loadedMessages = normalizeMessages(detail?.messages ?? detail?.Messages);
        const candidate = {
          session: {
            id: detail?.id ?? detail?.Id ?? stored.id,
            token: detail?.sessionToken ?? detail?.SessionToken ?? stored.token,
          },
          messages:
            loadedMessages.length > 0
              ? loadedMessages
              : [createLocalMessage("ASSISTANT", GREETING_TEXT)],
        };
        setResumeCandidate(candidate);
        return;
      } catch {
        writeStoredSession(null);
      }
    }

    await createNewSession();
  }, [createNewSession, resumeCandidate?.session?.id, session?.id]);

  const ensureSessionReady = useCallback(async () => {
    if (session?.id > 0) {
      return session;
    }

    if (resumeCandidate?.session?.id > 0) {
      throw new Error("Vui lòng chọn tiếp tục phiên trước hoặc bắt đầu phiên mới.");
    }

    return createNewSession();
  }, [createNewSession, resumeCandidate?.session?.id, session]);

  const handleContinuePreviousSession = useCallback(() => {
    if (!resumeCandidate?.session?.id) {
      return;
    }

    setSession(resumeCandidate.session);
    setMessages(resumeCandidate.messages);
    writeStoredSession(resumeCandidate.session);
    setResumeCandidate(null);
    setError("");
  }, [resumeCandidate]);

  const handleStartNewSession = useCallback(async () => {
    setInitializing(true);
    setError("");
    try {
      writeStoredSession(null);
      setResumeCandidate(null);
      await createNewSession();
    } catch (err) {
      setError(err?.message || "Không thể tạo phiên mới.");
    } finally {
      setInitializing(false);
    }
  }, [createNewSession]);

  useEffect(() => {
    if (!isOpen || !isCustomer) return;
    let cancelled = false;
    setInitializing(true);
    setError("");

    prepareSessionOnOpen()
      .catch((err) => {
        if (!cancelled) {
          setError(err.message || "Không thể khởi tạo chatbot.");
        }
      })
      .finally(() => {
        if (!cancelled) {
          setInitializing(false);
        }
      });

    return () => {
      cancelled = true;
    };
  }, [isCustomer, isOpen, prepareSessionOnOpen]);

  useEffect(() => {
    if (!isOpen) return;
    const node = messageListRef.current;
    if (!node) return;
    window.requestAnimationFrame(() => {
      node.scrollTop = node.scrollHeight;
    });
  }, [isOpen, messages]);

  useEffect(() => {
    if (!isOpen) return;
    const timer = window.setInterval(() => setNowTick(Date.now()), 1000);
    return () => window.clearInterval(timer);
  }, [isOpen]);

  useEffect(() => {
    if (!isOpen || typeof window === "undefined") return undefined;

    const handleKeyDown = (event) => {
      if (event.key === "Escape") {
        setIsExpanded(false);
      }
    };

    window.addEventListener("keydown", handleKeyDown);
    return () => window.removeEventListener("keydown", handleKeyDown);
  }, [isOpen]);

  useEffect(() => {
    if (typeof window === "undefined") return undefined;
    if (!isCustomer || isOpen) {
      setShowWelcomeHint(false);
      return undefined;
    }

    const sessionKey = `${WELCOME_HINT_SESSION_KEY}:${userId}`;
    if (window.sessionStorage.getItem(sessionKey) === "shown") {
      return undefined;
    }

    window.sessionStorage.setItem(sessionKey, "shown");
    setShowWelcomeHint(true);

    const timer = window.setTimeout(() => {
      setShowWelcomeHint(false);
    }, 9000);

    return () => window.clearTimeout(timer);
  }, [isCustomer, isOpen, userId]);

  const sendMessageContent = useCallback(
    async (content) => {
      const trimmed = String(content || "").trim();
      if (!trimmed) return;

      setSending(true);
      setError("");
      setComposeText("");

      try {
        const readySession = await ensureSessionReady();
        setMessages((prev) => [...prev, createLocalMessage("USER", trimmed)]);

        const reply = await orchestrateChatbot({
          sessionId: readySession.id,
          sessionToken: readySession.token,
          userMessage: trimmed,
          attachments: [],
          allowSendToLockedSession: false,
        });

        const nextSession = {
          id: reply?.sessionId ?? reply?.SessionId ?? readySession.id,
          token: reply?.sessionToken ?? reply?.SessionToken ?? readySession.token,
        };
        setSession(nextSession);
        writeStoredSession(nextSession);

        const bookingMeta = extractBookingWorkflowMeta(reply);
        const assistantText = String(reply?.assistantReply ?? reply?.AssistantReply ?? bookingMeta.bookingPrompt).trim();
        const fallbackText = "Mình đã nhận được yêu cầu và đang xử lý.";

        setMessages((prev) => [
          ...prev,
          createLocalMessage("ASSISTANT", assistantText.length > 0 ? assistantText : fallbackText, bookingMeta),
        ]);
      } catch (err) {
        const errorMessage = err?.message || "Gửi tin nhắn thất bại.";
        if (isLockedSessionErrorMessage(errorMessage)) {
          writeStoredSession(null);
          setSession(null);
          try {
            await createNewSession();
            setError(
              "Phiên chat trước đã được chuyển cho tư vấn viên. Hệ thống đã mở phiên mới, vui lòng gửi lại tin nhắn."
            );
          } catch {
            setError(errorMessage);
          }
        } else {
          setError(errorMessage);
          setMessages((prev) => [
            ...prev,
            createLocalMessage("ASSISTANT", "Có lỗi xảy ra. Vui lòng thử lại sau."),
          ]);
        }
      } finally {
        setSending(false);
      }
    },
    [createNewSession, ensureSessionReady]
  );

  function handleSendMessage(event) {
    event.preventDefault();
    void sendMessageContent(composeText);
  }

  function handleQuickReplyClick(value) {
    if (sending || initializing) return;
    void sendMessageContent(value);
  }

  if (!isCustomer) {
    return null;
  }

  return (
    <>
      {isOpen && isExpanded ? (
        <div
          className="chatbot-ai-expanded-backdrop"
          onClick={() => setIsExpanded(false)}
          aria-hidden="true"
        />
      ) : null}
      {isOpen ? (
        <section className={`chatbot-ai-widget ${isExpanded ? "is-expanded" : ""}`} aria-label="MyGarage AI Chatbot">
          <header className="chatbot-ai-header">
            <div className="chatbot-ai-profile">
              <span className="chatbot-ai-avatar" aria-hidden="true">
                <Bot size={18} strokeWidth={2.1} />
              </span>
              <div>
                <strong>MyGarage AI</strong>
                <small>{initializing ? "Đang kết nối..." : "Đang hoạt động"}</small>
              </div>
            </div>
            <div className="chatbot-ai-actions">
              <button
                type="button"
                className="chatbot-ai-icon-btn"
                onClick={() => setIsExpanded((prev) => !prev)}
                title={isExpanded ? "Thu nhỏ khung chat" : "Mở rộng chat"}
              >
                {isExpanded ? <Minimize2 size={18} /> : <Expand size={18} />}
              </button>
              <button
                type="button"
                className="chatbot-ai-icon-btn chatbot-ai-icon-btn-minimize"
                onClick={() => {
                  setIsExpanded(false);
                  setIsOpen(false);
                }}
                title="Thu nhỏ chatbot"
              >
                <Minus size={20} strokeWidth={2.5} />
              </button>
            </div>
          </header>

          {error ? <p className="chatbot-ai-error">{error}</p> : null}
          {resumeCandidate ? (
            <div className="chatbot-ai-resume-card">
              <p className="chatbot-ai-resume-text">
                Đã tìm thấy phiên chat trước. Bạn muốn tiếp tục phiên cũ hay bắt đầu phiên mới?
              </p>
              <div className="chatbot-ai-resume-actions">
                <button
                  type="button"
                  className="chatbot-ai-resume-btn primary"
                  onClick={handleContinuePreviousSession}
                  disabled={initializing || sending}
                >
                  Tiếp tục phiên trước
                </button>
                <button
                  type="button"
                  className="chatbot-ai-resume-btn secondary"
                  onClick={() => {
                    void handleStartNewSession();
                  }}
                  disabled={initializing || sending}
                >
                  Bắt đầu phiên mới
                </button>
              </div>
            </div>
          ) : null}

          <div ref={messageListRef} className="chatbot-ai-message-list">
            {messages.length === 0 ? (
              <p className="chatbot-ai-empty">Đang khởi tạo chatbot...</p>
            ) : (
              messages.map((message) => {
                const mine = normalizeRole(message.role) === "USER";
                const messageTime = formatMessageTime(message.createdAt);
                const showQuickReplies = !mine && message.id === latestQuickReplyMessageId;
                const holdCountdown = !mine
                  ? formatHoldCountdown(message.slotHoldExpiresAtUtc, nowTick)
                  : "";

                return (
                  <div key={message.id} className="chatbot-ai-message-block">
                    <article className={`chatbot-ai-message-row ${mine ? "mine" : "other"}`}>
                      {!mine ? (
                        <span className="chatbot-ai-message-avatar" aria-hidden="true">
                          <Bot size={15} strokeWidth={2.1} />
                        </span>
                      ) : null}
                      <div className="chatbot-ai-message-content">
                        <div className="chatbot-ai-bubble">
                          <p>{message.content}</p>
                        </div>
                        {holdCountdown ? <small className="chatbot-ai-hold">{holdCountdown}</small> : null}
                        {showQuickReplies && message.quickReplies?.length > 0 ? (
                          <div className="chatbot-ai-quick-replies">
                            {message.quickReplies.map((item) => (
                              <button
                                key={`${message.id}-${item.value}`}
                                type="button"
                                className="chatbot-ai-quick-reply-btn"
                                onClick={() => handleQuickReplyClick(item.value)}
                                disabled={sending || initializing}
                              >
                                {item.label}
                              </button>
                            ))}
                          </div>
                        ) : null}
                        {messageTime ? <small className="chatbot-ai-message-time">{messageTime}</small> : null}
                      </div>
                    </article>
                  </div>
                );
              })
            )}
          </div>

          <form className="chatbot-ai-compose" onSubmit={handleSendMessage}>
            <input
              type="text"
              className="chatbot-ai-compose-input"
              value={composeText}
              onChange={(event) => setComposeText(event.target.value)}
              placeholder={initializing ? "Đang kết nối AI..." : "Nhập tin nhắn..."}
              disabled={initializing || sending || Boolean(resumeCandidate)}
            />
            <button type="submit" className="chatbot-ai-send-btn" disabled={!canSend} title="Gửi tin nhắn">
              <SendHorizontal size={15} />
            </button>
          </form>
        </section>
      ) : null}

      <button
        type="button"
        className="chatbot-ai-floating-btn"
        onClick={() => {
          setShowWelcomeHint(false);
          setIsOpen(true);
        }}
        aria-label="Mở MyGarage AI chatbot"
        title="Mở MyGarage AI chatbot"
      >
        <Bot size={24} strokeWidth={2.1} />
      </button>
      {showWelcomeHint ? (
        <div className="chatbot-ai-welcome-hint" role="status" aria-live="polite">
          <button
            type="button"
            className="chatbot-ai-welcome-close"
            onClick={() => setShowWelcomeHint(false)}
            aria-label="Dong goi y chatbot"
          >
            ×
          </button>
          <div className="chatbot-ai-welcome-eyebrow">Tro ly AI MyGarage</div>
          <p>{WELCOME_HINT_TEXT}</p>
        </div>
      ) : null}
    </>
  );
}
