import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import {
  FileText,
  ImagePlus,
  MessageCircle,
  Minus,
  Paperclip,
  SendHorizontal,
  ShoppingBag,
  X,
} from "lucide-react";
import { useNavigate } from "react-router-dom";
import { useAuth } from "@shared/core/context/AuthContext";
import {
  getCustomerConversationOrders,
  getCustomerMessengerConversation,
  getCustomerMessages,
  markCustomerRead,
  sendCustomerMessage,
  uploadCustomerChatAttachments,
} from "@features/chat/api/chatApi";
import {
  CHAT_FILE_ACCEPT,
  CHAT_IMAGE_ACCEPT,
  CHAT_MAX_ATTACHMENT_COUNT,
  CHAT_MAX_ATTACHMENT_SIZE_BYTES,
  formatAttachmentSize,
  getAttachmentDisplayName,
  getExtensionFromPath,
  isImageAttachment,
  isImageFile,
  isSupportedAttachmentFile,
} from "@features/chat/utils/chatAttachment";
import {
  buildOrderCardMessagePayload,
  formatOrderAmount,
  getOrderCardFromMessage,
  getOrderTypeLabel,
} from "@features/chat/utils/chatOrderCard";
import { formatChatDividerTime, shouldShowChatTimeDivider } from "@features/chat/utils/chatTime";
import { createChatHubConnection } from "@features/chat/realtime/chatHubConnection";
import "./CustomerMessengerWidget.css";

function MessengerBadgeIcon(props) {
  return (
    <svg viewBox="0 0 24 24" fill="none" xmlns="http://www.w3.org/2000/svg" {...props}>
      <defs>
        <linearGradient id="messenger-widget-gradient" x1="0" y1="24" x2="24" y2="0" gradientUnits="userSpaceOnUse">
          <stop offset="0%" stopColor="#00a8ff" />
          <stop offset="50%" stopColor="#a948ff" />
          <stop offset="100%" stopColor="#ff5f7e" />
        </linearGradient>
      </defs>
      <path
        d="M12 0C5.373 0 0 4.974 0 11.111c0 3.498 1.744 6.614 4.469 8.654V24l4.088-2.242c1.092.3 2.246.464 3.443.464 6.627 0 12-4.975 12-11.11S18.627 0 12 0z"
        fill="url(#messenger-widget-gradient)"
      />
      <path
        d="M13.191 14.953l-3.308-3.529-6.442 3.529 7.034-7.465 3.308 3.528 6.442-3.528-7.034 7.465z"
        fill="#fff"
      />
    </svg>
  );
}

function getItems(response) {
  if (!response) return [];
  if (Array.isArray(response)) return response;
  if (Array.isArray(response.items)) return response.items;
  if (Array.isArray(response.Items)) return response.Items;
  return [];
}

function getId(item) {
  return item?.id ?? item?.Id ?? null;
}

function getAttachmentItems(message) {
  const raw = message?.attachments ?? message?.Attachments;
  if (Array.isArray(raw) && raw.length > 0) {
    return raw
      .map((item, index) => ({
        id: item?.id ?? item?.Id ?? `${getId(message) ?? "msg"}-att-${index}`,
        attachmentUrl: item?.attachmentUrl ?? item?.AttachmentUrl ?? "",
        contentType: item?.contentType ?? item?.ContentType ?? "",
        fileSize: item?.fileSize ?? item?.FileSize ?? null,
      }))
      .filter((item) => item.attachmentUrl);
  }

  const legacyUrl = message?.attachmentUrl ?? message?.AttachmentUrl ?? "";
  if (String(legacyUrl).trim()) {
    return [
      {
        id: `${getId(message) ?? "msg"}-legacy`,
        attachmentUrl: String(legacyUrl).trim(),
        contentType: "",
        fileSize: null,
      },
    ];
  }

  return [];
}

function getShortName(name) {
  const value = String(name || "").trim();
  if (!value) return "U";

  const parts = value.split(/\s+/).slice(0, 2);
  const initials = parts.map((part) => part[0]).join("");
  return initials.toUpperCase();
}

function getAttachmentMetaText(attachment) {
  const extension = getExtensionFromPath(attachment?.attachmentUrl).replace(".", "").toUpperCase();
  const sizeText = formatAttachmentSize(attachment?.fileSize);
  if (extension && sizeText) {
    return `${extension} | ${sizeText}`;
  }

  if (extension) {
    return extension;
  }

  if (sizeText) {
    return sizeText;
  }

  return "T?p d? t?i xu?ng";
}

function formatOrderCardDate(value) {
  if (!value) return "";
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return "";
  return date.toLocaleString("vi-VN", {
    hour12: false,
    hour: "2-digit",
    minute: "2-digit",
    day: "2-digit",
    month: "2-digit",
    year: "numeric",
    timeZone: "Asia/Ho_Chi_Minh",
  });
}

function MessengerOrderCard({ orderCard, onClick }) {
  if (!orderCard) return null;

  const amountText = formatOrderAmount(orderCard.totalAmount);
  const orderTime = formatOrderCardDate(orderCard.createdAt);
  const typeLabel = getOrderTypeLabel(orderCard.orderType);
  const fallbackThumbSrc = "/assets/booking/showcase-2.jpg";
  const [thumbSrc, setThumbSrc] = useState(orderCard.imageUrl || fallbackThumbSrc);
  const [usedFallback, setUsedFallback] = useState(!orderCard.imageUrl);

  useEffect(() => {
    const nextSrc = orderCard.imageUrl || fallbackThumbSrc;
    setThumbSrc(nextSrc);
    setUsedFallback(!orderCard.imageUrl);
  }, [orderCard.imageUrl]);

  return (
    <button
      type="button"
      className="messenger-order-card"
      onClick={() => onClick(orderCard)}
      title="M? chi ti?t don h�ng"
    >
      <div className="messenger-order-card-head">B?n dang h?i thông tin v? don h�ng sau:</div>
      <div className="messenger-order-card-body">
        <div className="messenger-order-card-thumb">
          {thumbSrc ? (
            <img
              src={thumbSrc}
              alt={orderCard.title}
              loading="lazy"
              onError={() => {
                if (!usedFallback) {
                  setThumbSrc(fallbackThumbSrc);
                  setUsedFallback(true);
                  return;
                }
                setThumbSrc("");
              }}
            />
          ) : (
            <span>{typeLabel}</span>
          )}
        </div>
        <div className="messenger-order-card-main">
          <strong>{orderCard.title}</strong>
          <p>{orderCard.subtitle}</p>
          <div className="messenger-order-card-line">
            <span className="messenger-order-card-status">{orderCard.statusLabel || "Đang x? l�"}</span>
            {amountText ? <span className="messenger-order-card-amount">{amountText}</span> : null}
          </div>
        </div>
      </div>
      <div className="messenger-order-card-meta">
        <span>M� don: {orderCard.orderCode}</span>
        {orderTime ? <span>{orderTime}</span> : null}
      </div>
    </button>
  );
}

export default function CustomerMessengerWidget() {
  const { isAuthenticated, user } = useAuth();
  const navigate = useNavigate();
  const roleCode = String(user?.roleCode || user?.RoleCode || "").toUpperCase();
  const isCustomer = isAuthenticated && roleCode === "CUSTOMER";
  const customerName = String(user?.fullName || user?.FullName || "B?n");

  const [isOpen, setIsOpen] = useState(false);
  const [loadingConversation, setLoadingConversation] = useState(false);
  const [loadingMessages, setLoadingMessages] = useState(false);
  const [sending, setSending] = useState(false);
  const [error, setError] = useState("");
  const [conversation, setConversation] = useState(null);
  const [messages, setMessages] = useState([]);
  const [composeText, setComposeText] = useState("");
  const [selectedFiles, setSelectedFiles] = useState([]);
  const [orderPickerOpen, setOrderPickerOpen] = useState(false);
  const [loadingOrders, setLoadingOrders] = useState(false);
  const [orderPickerError, setOrderPickerError] = useState("");
  const [conversationOrders, setConversationOrders] = useState([]);
  const [orderPreview, setOrderPreview] = useState(null);
  const connectionRef = useRef(null);
  const messageListRef = useRef(null);
  const conversationIdRef = useRef(null);
  const imageInputRef = useRef(null);
  const fileInputRef = useRef(null);

  const conversationId = useMemo(() => getId(conversation), [conversation]);
  const filePreviews = useMemo(
    () =>
      selectedFiles.map((file) => {
        const image = isImageFile(file);
        return {
          id: `${file.name}-${file.lastModified}-${file.size}`,
          file,
          isImage: image,
          url: image ? URL.createObjectURL(file) : "",
        };
      }),
    [selectedFiles]
  );

  useEffect(() => {
    return () => {
      filePreviews.forEach((item) => {
        if (item.url) {
          URL.revokeObjectURL(item.url);
        }
      });
    };
  }, [filePreviews]);

  useEffect(() => {
    conversationIdRef.current = conversationId;
  }, [conversationId]);

  useEffect(() => {
    setSelectedFiles([]);
    setOrderPickerOpen(false);
    setOrderPickerError("");
    setConversationOrders([]);
    if (imageInputRef.current) {
      imageInputRef.current.value = "";
    }
    if (fileInputRef.current) {
      fileInputRef.current.value = "";
    }
  }, [conversationId, isOpen]);

  useEffect(() => {
    if (!isOpen) return;
    const list = messageListRef.current;
    if (!list) return;

    window.requestAnimationFrame(() => {
      list.scrollTop = list.scrollHeight;
    });
  }, [conversationId, isOpen, messages.length]);

  const appendIncomingMessage = useCallback((incomingMessage) => {
    const activeConversationId = conversationIdRef.current;
    if (!activeConversationId) return;

    const incomingConversationId = incomingMessage?.conversationId ?? incomingMessage?.ConversationId ?? null;
    if (!incomingConversationId || Number(incomingConversationId) !== Number(activeConversationId)) {
      return;
    }

    const incomingId = getId(incomingMessage);
    setMessages((prev) => {
      if (incomingId && prev.some((item) => getId(item) === incomingId)) {
        return prev;
      }
      return [...prev, incomingMessage];
    });

    if (incomingId) {
      markCustomerRead(activeConversationId, { lastReadMessageId: incomingId }).catch(() => {});
    }
  }, []);

  const loadConversation = useCallback(async ({ silent = false } = {}) => {
    if (!silent) {
      setLoadingConversation(true);
    }

    try {
      const detail = await getCustomerMessengerConversation();
      setConversation(detail || null);

      const initialMessages = getItems(detail?.messages ?? detail?.Messages);
      setMessages(initialMessages);

      const lastMessage = initialMessages[initialMessages.length - 1];
      const lastMessageId = getId(lastMessage);
      const currentConversationId = getId(detail);
      if (currentConversationId && lastMessageId) {
        markCustomerRead(currentConversationId, { lastReadMessageId: lastMessageId }).catch(() => {});
      }
    } catch (err) {
      if (!silent) {
      setError(err.message || "Không t?i du?c h?i tho?i chat.");
      }
    } finally {
      if (!silent) {
        setLoadingConversation(false);
      }
    }
  }, []);

  const loadMessages = useCallback(async (id, { silent = false } = {}) => {
    if (!id) return;
    if (!silent) {
      setLoadingMessages(true);
    }

    try {
      const data = await getCustomerMessages(id, { take: 200 });
      const nextMessages = getItems(data);
      setMessages(nextMessages);

      const lastMessage = nextMessages[nextMessages.length - 1];
      const lastMessageId = getId(lastMessage);
      if (lastMessageId) {
        markCustomerRead(id, { lastReadMessageId: lastMessageId }).catch(() => {});
      }
    } catch (err) {
      if (!silent) {
      setError(err.message || "Không t?i du?c tin nh?n.");
      }
    } finally {
      if (!silent) {
        setLoadingMessages(false);
      }
    }
  }, []);

  const loadConversationOrders = useCallback(async () => {
    if (!conversationId) return;

    setLoadingOrders(true);
    setOrderPickerError("");
    try {
      const data = await getCustomerConversationOrders(conversationId);
      const items = getItems(data).map((item) => getOrderCardFromMessage({ orderCard: item })).filter(Boolean);
      setConversationOrders(items);
    } catch (err) {
      setOrderPickerError(err.message || "Kh?ng t?i du?c danh s?ch don h?ng.");
      setConversationOrders([]);
    } finally {
      setLoadingOrders(false);
    }
  }, [conversationId]);

  useEffect(() => {
    if (!isCustomer || !isOpen) return;
    setError("");
    loadConversation();
  }, [isCustomer, isOpen, loadConversation]);

  useEffect(() => {
    if (!isCustomer || !isOpen || !conversationId) return;

    const timer = window.setInterval(() => {
      loadMessages(conversationId, { silent: true });
    }, 12000);

    return () => window.clearInterval(timer);
  }, [conversationId, isCustomer, isOpen, loadMessages]);

  useEffect(() => {
    if (!isCustomer || !isOpen || !conversationId) {
      return undefined;
    }

    let disposed = false;
    const connection = createChatHubConnection();
    connectionRef.current = connection;

    connection.on("ReceiveMessage", appendIncomingMessage);
    connection.on("ConversationUpdated", (payload) => {
      const activeConversationId = conversationIdRef.current;
      if (!activeConversationId) return;

      const updatedId = payload?.conversationId ?? payload?.ConversationId ?? null;
      if (updatedId && Number(updatedId) !== Number(activeConversationId)) return;

      loadMessages(activeConversationId, { silent: true });
    });

    connection.onreconnected(async () => {
      const activeConversationId = conversationIdRef.current;
      if (!activeConversationId) return;

      try {
        await connection.invoke("JoinConversationGroup", Number(activeConversationId));
      } catch (err) {
        console.error("ChatHub rejoin conversation failed:", err);
      }
    });

    const startRealtime = async () => {
      try {
        await connection.start();
        if (disposed) return;
        await connection.invoke("JoinConversationGroup", Number(conversationId));
      } catch (err) {
        console.error("ChatHub customer connection failed:", err);
      }
    };

    startRealtime();

    return () => {
      disposed = true;
      connection.off("ReceiveMessage");
      connection.off("ConversationUpdated");
      connectionRef.current = null;
      connection.stop().catch(() => {});
    };
  }, [appendIncomingMessage, conversationId, isCustomer, isOpen, loadMessages]);

  async function handleSendMessage(event) {
    event.preventDefault();
    if (!conversationId) return;

    const content = composeText.trim();
    if (!content && selectedFiles.length === 0) return;

    setSending(true);
    setError("");
    try {
      const hasOnlyImageAttachments =
        selectedFiles.length > 0 && selectedFiles.every((file) => isImageFile(file));
      let attachmentUrls = [];
      if (selectedFiles.length > 0) {
        const uploaded = await uploadCustomerChatAttachments(conversationId, selectedFiles);
        const uploadedItems = getItems(uploaded);
        attachmentUrls = uploadedItems
          .map((item) => item?.attachmentUrl ?? item?.AttachmentUrl ?? "")
          .filter((url) => String(url).trim());
      }

      await sendCustomerMessage(conversationId, {
        content,
        messageType: attachmentUrls.length > 0 && hasOnlyImageAttachments ? "IMAGE" : "TEXT",
        attachmentUrls,
      });
      setComposeText("");
      setSelectedFiles([]);
      setOrderPickerOpen(false);
      if (imageInputRef.current) {
        imageInputRef.current.value = "";
      }
      if (fileInputRef.current) {
        fileInputRef.current.value = "";
      }
      await loadMessages(conversationId, { silent: true });
    } catch (err) {
      setError(err.message || "G?i tin nh?n th?t b?i.");
    } finally {
      setSending(false);
    }
  }

  async function handleSendOrderCard(orderCard) {
    if (!conversationId) return;
    const payload = buildOrderCardMessagePayload(orderCard);
    if (!payload) {
      setOrderPickerError("Không t?o du?c n?i dung don h�ng.");
      return;
    }

    setSending(true);
    setError("");
    setOrderPickerError("");
    try {
      await sendCustomerMessage(conversationId, payload);
      setOrderPickerOpen(false);
      await loadMessages(conversationId, { silent: true });
    } catch (err) {
      setOrderPickerError(err.message || "G?i don h�ng th?t b?i.");
    } finally {
      setSending(false);
    }
  }

  function handleOpenOrderCard(orderCard) {
    if (!orderCard) return;
    if (orderCard.detailUrl) {
      setIsOpen(false);
      navigate(orderCard.detailUrl);
      return;
    }

    setOrderPreview(orderCard);
  }

  async function toggleOrderPicker() {
    if (!conversationId || sending) return;

    const nextOpen = !orderPickerOpen;
    setOrderPickerOpen(nextOpen);
    setOrderPickerError("");

    if (nextOpen) {
      await loadConversationOrders();
    }
  }

  function handleFileSelect(event, pickerType = "all") {
    const picked = Array.from(event.target.files || []);
    if (picked.length === 0) return;

    setError("");
    const next = [...selectedFiles];
    const onlyImages = pickerType === "image";
    const onlyFiles = pickerType === "file";
    for (const file of picked) {
      if (!isSupportedAttachmentFile(file)) {
        setError(`T?p ${file.name} không du?c h? tr?.`);
        continue;
      }

      const fileIsImage = isImageFile(file);
      if (onlyImages && !fileIsImage) {
        setError("N�t ?nh ch? nh?n t?p ?nh.");
        continue;
      }

      if (onlyFiles && fileIsImage) {
        setError("N�t file ch? nh?n t�i li?u.");
        continue;
      }

      if (file.size > CHAT_MAX_ATTACHMENT_SIZE_BYTES) {
        setError(`T?p ${file.name} vu?t qu? ${CHAT_MAX_ATTACHMENT_SIZE_BYTES / (1024 * 1024)}MB.`);
        continue;
      }

      if (next.length >= CHAT_MAX_ATTACHMENT_COUNT) {
        setError(`T?i da ${CHAT_MAX_ATTACHMENT_COUNT} t?p m?i l?n g?i.`);
        break;
      }

      const duplicated = next.some(
        (item) =>
          item.name === file.name &&
          item.size === file.size &&
          item.lastModified === file.lastModified
      );
      if (duplicated) {
        continue;
      }

      next.push(file);
    }

    setSelectedFiles(next);
    event.target.value = "";
  }

  function removeSelectedFile(targetId) {
    setSelectedFiles((prev) =>
      prev.filter((file) => `${file.name}-${file.lastModified}-${file.size}` !== targetId)
    );
  }

  if (!isCustomer) return null;

  return (
    <>
      {isOpen ? (
        <section className="messenger-widget">
          <header className="messenger-widget-header">
            <div className="messenger-header-profile">
              <span className="messenger-header-avatar">LT</span>
              <div>
                <strong>L? t?n MyGarage</strong>
                <small>Đang ho?t d?ng</small>
              </div>
            </div>

            <div className="messenger-header-actions">
              <button
                type="button"
                className="messenger-icon-btn messenger-icon-btn-minimize"
                onClick={() => setIsOpen(false)}
                aria-label="Thu nh? khung chat"
                title="Thu nh?"
              >
                <Minus size={20} strokeWidth={2.6} />
              </button>
            </div>
          </header>

          {error ? <div className="messenger-error">{error}</div> : null}

          <div ref={messageListRef} className="messenger-message-list">
            {loadingConversation || loadingMessages ? (
              <p className="messenger-empty">Đang t?i...</p>
            ) : null}
            {!loadingConversation && !loadingMessages && messages.length === 0 ? (
              <p className="messenger-empty">Xin ch?o, d? l?i l?i nh?n v? l? t?n s? ph?n h?i ngay.</p>
            ) : null}

            {messages.map((message, index) => {
              const id = getId(message);
              const senderType = String(message?.senderType ?? message?.SenderType ?? "").toUpperCase();
              const mine = senderType === "CUSTOMER";
              const senderAvatar = mine ? getShortName(customerName) : "LT";
              const content = message?.content ?? message?.Content ?? "";
              const orderCard = getOrderCardFromMessage(message);
              const attachments = getAttachmentItems(message);
              const imageAttachments = attachments.filter((attachment) => isImageAttachment(attachment));
              const fileAttachments = attachments.filter((attachment) => !isImageAttachment(attachment));
              const createdAt = message?.createdAt ?? message?.CreatedAt;
              const previous = index > 0 ? messages[index - 1] : null;
              const previousCreatedAt = previous?.createdAt ?? previous?.CreatedAt ?? null;
              const showDivider = shouldShowChatTimeDivider(previousCreatedAt, createdAt, 120);
              const dividerText = formatChatDividerTime(createdAt);

              return (
                <div key={id ?? `message-${index}`} className="messenger-message-block">
                  {showDivider && dividerText ? (
                    <div className="messenger-time-divider">{dividerText}</div>
                  ) : null}
                  <article className={`messenger-message-row ${mine ? "mine" : "other"}`}>
                    {!mine ? <span className="messenger-message-avatar">{senderAvatar}</span> : null}
                    <div className="messenger-message-content">
                      <div className={`messenger-message-bubble ${orderCard ? "order-card-only" : ""}`}>
                        {orderCard ? (
                          <MessengerOrderCard orderCard={orderCard} onClick={handleOpenOrderCard} />
                        ) : (
                          <>
                            {imageAttachments.length > 0 ? (
                              <div className={`messenger-attachment-grid count-${Math.min(imageAttachments.length, 4)}`}>
                                {imageAttachments.map((attachment) => (
                                  <a
                                    key={attachment.id}
                                    href={attachment.attachmentUrl}
                                    target="_blank"
                                    rel="noreferrer"
                                    className="messenger-attachment-item"
                                  >
                                    <img src={attachment.attachmentUrl} alt="attachment" loading="lazy" />
                                  </a>
                                ))}
                              </div>
                            ) : null}
                            {fileAttachments.length > 0 ? (
                              <div className="messenger-file-list">
                                {fileAttachments.map((attachment, fileIndex) => (
                                  <a
                                    key={attachment.id}
                                    href={attachment.attachmentUrl}
                                    target="_blank"
                                    rel="noreferrer"
                                    className="messenger-file-item"
                                  >
                                    <span className="messenger-file-icon">
                                      <FileText size={14} />
                                    </span>
                                    <span className="messenger-file-meta">
                                      <span className="messenger-file-name">
                                        {getAttachmentDisplayName(
                                          attachment.attachmentUrl,
                                          `tep-dinh-kem-${fileIndex + 1}`
                                        )}
                                      </span>
                                      <small>{getAttachmentMetaText(attachment)}</small>
                                    </span>
                                  </a>
                                ))}
                              </div>
                            ) : null}
                            {content ? <p>{content}</p> : null}
                            {!content && attachments.length === 0 ? <p>(Attachment)</p> : null}
                          </>
                        )}
                      </div>
                    </div>
                  </article>
                </div>
              );
            })}
          </div>

          {filePreviews.length > 0 ? (
            <div className="messenger-compose-preview">
              {filePreviews.map((item) => (
                <div
                  key={item.id}
                  className={`messenger-compose-preview-item ${item.isImage ? "image" : "file"}`}
                >
                  {item.isImage ? (
                    <img src={item.url} alt={item.file.name} />
                  ) : (
                    <div className="messenger-compose-preview-file">
                      <FileText size={16} />
                      <div>
                        <strong>{item.file.name}</strong>
                        <small>{formatAttachmentSize(item.file.size) || "t?p"}</small>
                      </div>
                    </div>
                  )}
                  <button
                    type="button"
                    className="messenger-preview-remove-btn"
                    onClick={() => removeSelectedFile(item.id)}
                    aria-label="X�a ?nh"
                  >
                    <X size={12} />
                  </button>
                </div>
              ))}
            </div>
          ) : null}

          {orderPickerOpen ? (
            <div className="messenger-order-picker">
              <div className="messenger-order-picker-head">
                <strong>Chọn don h�ng d? g?i</strong>
                <button
                  type="button"
                  className="messenger-icon-btn"
                  onClick={() => setOrderPickerOpen(false)}
                  aria-label="??ng danh s?ch don h?ng"
                >
                  <X size={12} />
                </button>
              </div>
              {orderPickerError ? <div className="messenger-error">{orderPickerError}</div> : null}
              {loadingOrders ? <p className="messenger-empty">Đang t?i don h�ng...</p> : null}
              {!loadingOrders && conversationOrders.length === 0 ? (
                <p className="messenger-empty">Không c� don h�ng n�o ph� h?p d? g?i.</p>
              ) : null}
              {!loadingOrders && conversationOrders.length > 0 ? (
                <div className="messenger-order-picker-list">
                  {conversationOrders.map((order) => (
                    <button
                      key={`${order.orderType}-${order.orderId}`}
                      type="button"
                      className="messenger-order-picker-item"
                      onClick={() => handleSendOrderCard(order)}
                      disabled={sending}
                    >
                      <div className="messenger-order-picker-row">
                        <span>{order.title}</span>
                        <span>{formatOrderAmount(order.totalAmount)}</span>
                      </div>
                      <small>
                        {order.subtitle} - {order.statusLabel}
                      </small>
                    </button>
                  ))}
                </div>
              ) : null}
            </div>
          ) : null}

          <form className="messenger-compose-form" onSubmit={handleSendMessage}>
            <input
              ref={imageInputRef}
              type="file"
              accept={CHAT_IMAGE_ACCEPT}
              multiple
              className="messenger-file-input"
              onChange={(event) => handleFileSelect(event, "image")}
              disabled={!conversationId || sending}
            />
            <input
              ref={fileInputRef}
              type="file"
              accept={CHAT_FILE_ACCEPT}
              multiple
              className="messenger-file-input"
              onChange={(event) => handleFileSelect(event, "file")}
              disabled={!conversationId || sending}
            />
            <button
              type="button"
              className="messenger-attach-btn image"
              onClick={() => imageInputRef.current?.click()}
              disabled={!conversationId || sending}
              aria-label="Chọn ?nh"
            >
              <ImagePlus size={16} />
            </button>
            <button
              type="button"
              className="messenger-attach-btn file"
              onClick={() => fileInputRef.current?.click()}
              disabled={!conversationId || sending}
              aria-label="Chọn t?p d�nh k�m"
            >
              <Paperclip size={16} />
            </button>
            <button
              type="button"
              className="messenger-attach-btn order"
              onClick={toggleOrderPicker}
              disabled={!conversationId || sending}
              aria-label="G?i th? don h�ng"
            >
              <ShoppingBag size={16} />
            </button>
            <input
              type="text"
              className="messenger-compose-text-input"
              placeholder={conversationId ? "Nh?p tin nh?n..." : "?ang k?t n?i l? t?n..."}
              value={composeText}
              onChange={(e) => setComposeText(e.target.value)}
              disabled={!conversationId || sending}
            />
            <button
              type="submit"
              className="messenger-send-btn"
              disabled={!conversationId || sending || (!composeText.trim() && selectedFiles.length === 0)}
              aria-label="G?i tin nh?n"
            >
              <SendHorizontal size={16} />
            </button>
          </form>
        </section>
      ) : null}

      {orderPreview ? (
        <div className="messenger-order-modal-backdrop" onClick={() => setOrderPreview(null)}>
          <div className="messenger-order-modal" onClick={(event) => event.stopPropagation()}>
            <header>
              <strong>{orderPreview.title}</strong>
              <button
                type="button"
                className="messenger-icon-btn"
                onClick={() => setOrderPreview(null)}
                aria-label="Đồng chi ti?t don h�ng"
              >
                <X size={12} />
              </button>
            </header>
            <p>{orderPreview.subtitle}</p>
            <div className="messenger-order-modal-grid">
              <span>Lo?i</span>
              <strong>{getOrderTypeLabel(orderPreview.orderType)}</strong>
              <span>M� don</span>
              <strong>{orderPreview.orderCode}</strong>
              <span>Tr?ng th?i</span>
              <strong>{orderPreview.statusLabel}</strong>
              <span>Tổng ti?n</span>
              <strong>{formatOrderAmount(orderPreview.totalAmount) || "--"}</strong>
              <span>Chi nhánh</span>
              <strong>{orderPreview.branchName || "--"}</strong>
            </div>
            {orderPreview.detailUrl ? (
              <button
                type="button"
                className="messenger-order-modal-link"
                onClick={() => {
                  const target = orderPreview.detailUrl;
                  setOrderPreview(null);
                  setIsOpen(false);
                  navigate(target);
                }}
              >
                M? trang chi ti?t
              </button>
            ) : null}
          </div>
        </div>
      ) : null}

      <button
        type="button"
        className="messenger-floating-btn"
        onClick={() => setIsOpen(true)}
        aria-label="M? chat v?i l? t?n"
      >
        <MessengerBadgeIcon className="messenger-floating-icon" aria-hidden="true" />
      </button>
    </>
  );
}


