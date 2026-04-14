const IMAGE_EXTENSIONS = new Set([
  ".jpg",
  ".jpeg",
  ".png",
  ".webp",
  ".gif",
  ".bmp",
]);

const SUPPORTED_ATTACHMENT_EXTENSIONS = new Set([
  ...IMAGE_EXTENSIONS,
  ".pdf",
  ".doc",
  ".docx",
  ".xls",
  ".xlsx",
  ".txt",
  ".csv",
  ".zip",
  ".rar",
]);

const EXTENSION_FROM_CONTENT_TYPE = {
  "image/jpeg": ".jpg",
  "image/png": ".png",
  "image/webp": ".webp",
  "image/gif": ".gif",
  "image/bmp": ".bmp",
  "application/pdf": ".pdf",
  "application/msword": ".doc",
  "application/vnd.openxmlformats-officedocument.wordprocessingml.document": ".docx",
  "application/vnd.ms-excel": ".xls",
  "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet": ".xlsx",
  "text/plain": ".txt",
  "text/csv": ".csv",
  "application/zip": ".zip",
  "application/x-zip-compressed": ".zip",
  "application/vnd.rar": ".rar",
  "application/x-rar-compressed": ".rar",
};

export const CHAT_MAX_ATTACHMENT_COUNT = 6;
export const CHAT_MAX_ATTACHMENT_SIZE_BYTES = 10 * 1024 * 1024;
export const CHAT_IMAGE_ACCEPT = ".jpg,.jpeg,.png,.webp,.gif,.bmp,image/*";
export const CHAT_FILE_ACCEPT =
  ".pdf,.doc,.docx,.xls,.xlsx,.txt,.csv,.zip,.rar,application/pdf,text/plain,application/zip,application/x-zip-compressed,application/vnd.rar,application/x-rar-compressed,application/msword,application/vnd.openxmlformats-officedocument.wordprocessingml.document,application/vnd.ms-excel,application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
export const CHAT_ATTACHMENT_ACCEPT = `${CHAT_IMAGE_ACCEPT},${CHAT_FILE_ACCEPT}`;

function normalizeExtension(value) {
  if (!value) return "";
  const normalized = String(value).trim().toLowerCase();
  if (!normalized) return "";
  return normalized.startsWith(".") ? normalized : `.${normalized}`;
}

export function getExtensionFromPath(pathLike) {
  const raw = String(pathLike || "").trim();
  if (!raw) return "";

  const noQuery = raw.split("?")[0];
  const noHash = noQuery.split("#")[0];
  const slashIndex = Math.max(noHash.lastIndexOf("/"), noHash.lastIndexOf("\\"));
  const filename = slashIndex >= 0 ? noHash.slice(slashIndex + 1) : noHash;
  const dotIndex = filename.lastIndexOf(".");
  if (dotIndex < 0) return "";
  return normalizeExtension(filename.slice(dotIndex));
}

export function isImageContentType(contentType) {
  return String(contentType || "").toLowerCase().startsWith("image/");
}

export function isImageFile(file) {
  if (!file) return false;
  if (isImageContentType(file.type)) return true;

  const extension = getExtensionFromPath(file.name);
  return IMAGE_EXTENSIONS.has(extension);
}

export function isSupportedAttachmentFile(file) {
  if (!file) return false;

  const extension = getExtensionFromPath(file.name);
  if (extension && SUPPORTED_ATTACHMENT_EXTENSIONS.has(extension)) {
    return true;
  }

  const inferred = EXTENSION_FROM_CONTENT_TYPE[String(file.type || "").toLowerCase()] || "";
  return Boolean(inferred && SUPPORTED_ATTACHMENT_EXTENSIONS.has(inferred));
}

export function isImageAttachment(attachment) {
  if (!attachment) return false;
  const contentType = attachment.contentType ?? attachment.ContentType ?? "";
  if (isImageContentType(contentType)) {
    return true;
  }

  const url = attachment.attachmentUrl ?? attachment.AttachmentUrl ?? "";
  const extension = getExtensionFromPath(url);
  return IMAGE_EXTENSIONS.has(extension);
}

export function formatAttachmentSize(size) {
  const bytes = Number(size ?? 0);
  if (!Number.isFinite(bytes) || bytes <= 0) return "";

  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
}

export function getAttachmentDisplayName(attachmentUrl, fallbackName = "") {
  const fallback = String(fallbackName || "").trim();
  const raw = String(attachmentUrl || "").trim();
  if (!raw) {
    return fallback || "tep-dinh-kem";
  }

  const noQuery = raw.split("?")[0];
  const noHash = noQuery.split("#")[0];
  const slashIndex = Math.max(noHash.lastIndexOf("/"), noHash.lastIndexOf("\\"));
  const filename = slashIndex >= 0 ? noHash.slice(slashIndex + 1) : noHash;
  try {
    return decodeURIComponent(filename || fallback || "tep-dinh-kem");
  } catch {
    return filename || fallback || "tep-dinh-kem";
  }
}
