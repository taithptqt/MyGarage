import { useEffect, useMemo, useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import Footer from "@shared/components/Footer";
import Header from "@shared/components/Header";
import { useAuth } from "@shared/core/context/AuthContext";
import RetailImage from "@features/retail/components/RetailImage";
import { createPayosOrder } from "@features/payment/api/payosApi";
import {
  checkoutRetailOrder,
  getBranches,
  getCategories,
  getProductImages,
  getProductStockAvailability,
} from "@features/retail/api/retailApi";
import useRetailCart from "@features/retail/hooks/useRetailCart";
import { voucherApi } from "@features/voucher/api/voucherApi";
import {
  clampQuantityToAvailable,
  formatVnd,
  getBranchLabel,
  getCategoryLabel,
  getProductImageAlt,
  getProductName,
  getProductUnitLabel,
  normalizeAvailableQuantity,
  readStoredRetailBranchId,
  resolveProductImage,
  sortProductImages,
  toPositiveInt,
  writeStoredRetailBranchId,
} from "@features/retail/utils/retailFormat";
import {
  ShoppingCart,
  ShoppingBag,
  Package,
  CheckCircle2,
  AlertCircle,
  Trash2,
  CreditCard,
  Store,
  Truck,
  Tag,
  ArrowRight,
  X,
  MapPin,
  Receipt,
  FileText,
  ShieldCheck,
} from "lucide-react";
import "./RetailShared.css";
import "./RetailCartPage.css";

const BRANCH_SHORTCUT_ORDER = [
  { key: "ha-noi", label: "Hà Nội", aliases: ["ha noi", "hanoi", "hn"] },
  { key: "da-nang", label: "Đà Nẵng", aliases: ["da nang", "danang", "dn"] },
  {
    key: "tp-hcm",
    label: "TP HCM",
    aliases: ["tp hcm", "tphcm", "ho chi minh", "hcm", "sai gon", "saigon"],
  },
];

const FULFILLMENT_METHOD = {
  PICKUP: "PICKUP",
  DELIVERY: "DELIVERY",
};

const VOUCHER_UI = {
  LABEL: "Mã giảm giá / Voucher",
  PLACEHOLDER: "Nhập mã voucher (VD: MYGARAGE10)",
  APPLY_BTN: "Áp dụng",
  VALIDATING_BTN: "Đang kiểm tra...",
  REMOVE_BTN: "Gỡ bỏ",
  EMPTY_CODE_ERROR: "Vui lòng nhập mã voucher giảm giá.",
  EMPTY_CART_ERROR: "Vui lòng chọn ít nhất một sản phẩm trước khi áp dụng voucher.",
  DEFAULT_ERROR: "Mã voucher không hợp lệ hoặc đã hết hạn.",
  DISCOUNT_ROW_LABEL: (code) => `Giảm giá voucher (${code})`,
  AFTER_DISCOUNT_LABEL: "Tạm tính sau giảm",
  CONFIRM_DISCOUNT_LABEL: (code) => `Giảm giá voucher (${code})`,
  APPLIED_SUCCESS: (code, amount) =>
    amount > 0
      ? `Áp dụng thành công mã ${code}: giảm ${formatVnd(amount)}.`
      : `Áp dụng thành công mã ${code}.`,
  RECORDED_INFO: (code) =>
    `Mã ${code} đã được ghi nhận và sẽ được áp dụng khi tạo đơn.`,
};

function toSafeAmount(value) {
  const parsed = Number(value);
  if (Number.isFinite(parsed) && parsed > 0) return Math.floor(parsed);

  if (value == null) return 0;

  const normalizedDigits = String(value).replace(/[^\d]/g, "");
  const fallback = Number(normalizedDigits);
  return Number.isFinite(fallback) && fallback > 0 ? Math.floor(fallback) : 0;
}

function buildFrontendUrl(pathname) {
  if (typeof window === "undefined") return pathname;
  return `${window.location.origin}${pathname}`;
}

function openPayosPopupWindow() {
  if (typeof window === "undefined") return null;

  const popup = window.open("", "mygarage_payos_checkout", "width=980,height=760");
  if (!popup) return null;

  popup.document.write(
    "<!doctype html><html><head><meta charset='utf-8' /><title>MyGarage PayOS</title></head><body style='font-family:Segoe UI,Arial,sans-serif;padding:20px;color:#0f172a;background:#f8fafc'><h3 style='margin:0 0 8px'>MyGarage</h3><p style='margin:0'>Đang khởi tạo cổng thanh toán PayOS...</p></body></html>"
  );
  popup.document.close();
  return popup;
}

function normalizeBranchSearchText(value) {
  return String(value || "")
    .normalize("NFD")
    .replace(/[\u0300-\u036f]/g, "")
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, " ")
    .trim();
}

function resolvePreferredBranch(branches, user) {
  if (!Array.isArray(branches) || branches.length === 0) return null;

  const rawBranchId = Number(user?.branchId);
  if (Number.isFinite(rawBranchId) && rawBranchId > 0) {
    const byId = branches.find((branch) => Number(branch?.id) === rawBranchId);
    if (byId) return byId;
  }

  const normalizedProfileBranch = normalizeBranchSearchText(user?.branchName || "");
  if (!normalizedProfileBranch) return null;

  return (
    branches.find((branch) =>
      normalizeBranchSearchText(getBranchLabel(branch)).includes(normalizedProfileBranch)
    ) || null
  );
}

function buildFulfillmentNote({
  method,
  address,
  preferredBranch,
  selectedBranch,
  etaDays,
}) {
  const methodLabel =
    method === FULFILLMENT_METHOD.DELIVERY ? "Giao hàng tận nơi" : "Nhận tại cửa hàng";
  const preferredLabel = preferredBranch
    ? getBranchLabel(preferredBranch)
    : "Chưa xác định chi nhánh ưu tiên";
  const sourceLabel = selectedBranch
    ? getBranchLabel(selectedBranch)
    : "Chưa xác định chi nhánh đặt hàng";

  const lines = [
    `[FULFILLMENT]`,
    `HinhThuc=${methodLabel}`,
    `ChiNhanhUuTien=${preferredLabel}`,
    `ChiNhanhDatHang=${sourceLabel}`,
    `ThoiGianDuKien=${etaDays} ngay`,
  ];

  if (method === FULFILLMENT_METHOD.DELIVERY && address) {
    lines.push(`DiaChiGiao=${address}`);
  }

  return lines.join(" | ");
}

export default function RetailCartPage() {
  const navigate = useNavigate();
  const { user } = useAuth();
  const [categories, setCategories] = useState([]);
  const [productImages, setProductImages] = useState([]);
  const [branches, setBranches] = useState([]);
  const [stockByProductId, setStockByProductId] = useState({});
  const [loadingMeta, setLoadingMeta] = useState(true);
  const [loadingStock, setLoadingStock] = useState(false);
  const [error, setError] = useState("");
  const [bannerMessage, setBannerMessage] = useState("");
  const [branchId, setBranchId] = useState("");
  const [notes, setNotes] = useState("");
  const [fulfillmentMethod, setFulfillmentMethod] = useState(FULFILLMENT_METHOD.PICKUP);
  const [deliveryAddress, setDeliveryAddress] = useState("");
  const [showDeliveryAddressModal, setShowDeliveryAddressModal] = useState(false);
  const [pendingCheckoutIntent, setPendingCheckoutIntent] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [payingOrder, setPayingOrder] = useState(false);
  const [pendingOrder, setPendingOrder] = useState(null);
  const [voucherCodeInput, setVoucherCodeInput] = useState("");
  const [appliedVoucher, setAppliedVoucher] = useState(null);
  const [voucherValidating, setVoucherValidating] = useState(false);
  const [voucherStatus, setVoucherStatus] = useState({ error: "", success: "" });
  const {
    cart,
    totalLineItems,
    subtotal,
    selectedItems,
    selectedLineCount,
    selectedQuantity,
    selectedSubtotal,
    areAllItemsSelected,
    removeFromCart,
    removeSelectedItems,
    setItemQuantity,
    setItemSelected,
    syncCartWithAvailability,
    toggleSelectAllCartItems,
    updateCartQty,
  } = useRetailCart();

  const voucherDiscount = appliedVoucher
    ? Math.min(toSafeAmount(appliedVoucher.discountAmount), selectedSubtotal)
    : 0;
  const selectedSubtotalAfterDiscount = Math.max(0, selectedSubtotal - voucherDiscount);

  useEffect(() => {
    let ignore = false;

    async function loadMeta() {
      setLoadingMeta(true);

      try {
        const [categoryData, imageData, branchData] = await Promise.all([
          getCategories(),
          getProductImages(),
          getBranches(),
        ]);

        if (ignore) return;

        const nextCategories = Array.isArray(categoryData)
          ? categoryData.filter((item) => item?.isDeleted !== true)
          : [];
        const nextProductImages = Array.isArray(imageData) ? imageData : [];
        const nextBranches = Array.isArray(branchData)
          ? branchData.filter((item) => item?.isDeleted !== true)
          : [];

        setCategories(nextCategories);
        setProductImages(nextProductImages);
        setBranches(nextBranches);
      } catch (loadError) {
        if (!ignore) {
          setError(loadError?.message || "Không thể tải dữ liệu giỏ hàng retail lúc này.");
          setCategories([]);
          setProductImages([]);
          setBranches([]);
        }
      } finally {
        if (!ignore) {
          setLoadingMeta(false);
        }
      }
    }

    loadMeta();

    return () => {
      ignore = true;
    };
  }, []);

  useEffect(() => {
    if (!branches.length) {
      return;
    }

    setBranchId((current) => {
      if (current && branches.some((item) => String(item.id) === String(current))) {
        return String(current);
      }

      const stored = readStoredRetailBranchId();
      if (stored && branches.some((item) => String(item.id) === stored)) {
        return stored;
      }

      return String(branches[0].id);
    });
  }, [branches]);

  useEffect(() => {
    if (!branchId || !branches.length) {
      return;
    }

    if (!branches.some((item) => String(item.id) === String(branchId))) {
      return;
    }

    writeStoredRetailBranchId(branchId);
  }, [branchId, branches]);

  useEffect(() => {
    function handlePaymentMessage(event) {
      if (event.origin !== window.location.origin) return;

      const data = event.data;
      if (!data || data.type !== "MYGARAGE_RETAIL_PAYMENT_COMPLETED") return;

      const paidOrderId = Number(data.orderId);
      setBannerMessage("Thanh toán thành công. Đang chuyển đến đơn phụ tùng.");
      navigate(
        Number.isFinite(paidOrderId) && paidOrderId > 0
          ? `/my-retail-orders?orderId=${paidOrderId}`
          : "/my-retail-orders"
      );
    }

    window.addEventListener("message", handlePaymentMessage);
    return () => window.removeEventListener("message", handlePaymentMessage);
  }, [navigate]);

  const categoriesById = useMemo(
    () => new Map(categories.map((item) => [String(item.id), item])),
    [categories]
  );

  const productImagesByProductId = useMemo(() => {
    const groupedImages = new Map();

    productImages.forEach((item) => {
      const key = String(item?.productId ?? "");
      if (!key) return;

      if (!groupedImages.has(key)) {
        groupedImages.set(key, []);
      }

      groupedImages.get(key).push(item);
    });

    groupedImages.forEach((items, key) => {
      groupedImages.set(key, sortProductImages(items));
    });

    return groupedImages;
  }, [productImages]);

  const selectedBranch = useMemo(
    () => branches.find((item) => String(item.id) === String(branchId)) || null,
    [branchId, branches]
  );

  const preferredBranch = useMemo(
    () => resolvePreferredBranch(branches, user),
    [branches, user]
  );

  const normalizedDeliveryAddress = useMemo(() => deliveryAddress.trim(), [deliveryAddress]);

  const leadTimeDays = useMemo(() => {
    const preferredId = Number(preferredBranch?.id || 0);
    const selectedId = Number(selectedBranch?.id || 0);
    const isCrossBranch =
      preferredId > 0 && selectedId > 0 && preferredId !== selectedId;

    if (fulfillmentMethod === FULFILLMENT_METHOD.DELIVERY) {
      return isCrossBranch ? 30 : 7;
    }

    return isCrossBranch ? 15 : 3;
  }, [fulfillmentMethod, preferredBranch, selectedBranch]);

  const fulfillmentPreviewText = useMemo(() => {
    const preferredLabel = preferredBranch
      ? getBranchLabel(preferredBranch)
      : "chi nhánh ưu tiên trong hồ sơ";

    if (fulfillmentMethod === FULFILLMENT_METHOD.DELIVERY) {
      if (!normalizedDeliveryAddress) {
        return `Giao hàng tận nơi. Vui lòng nhập địa chỉ trước khi checkout. Dự kiến ${leadTimeDays} ngày.`;
      }
      return `Giao hàng đến địa chỉ đăng ký, dự kiến ${leadTimeDays} ngày.`;
    }

    return `Nhận hàng tại cửa hàng ở ${preferredLabel}, dự kiến ${leadTimeDays} ngày.`;
  }, [fulfillmentMethod, normalizedDeliveryAddress, preferredBranch, leadTimeDays]);

  const branchShortcutOptions = useMemo(() => {
    const usedIds = new Set();
    const mappedShortcuts = BRANCH_SHORTCUT_ORDER.map((shortcut) => {
      const matchedBranch = branches.find((branch) => {
        if (!branch || usedIds.has(branch.id)) return false;
        const normalized = normalizeBranchSearchText(getBranchLabel(branch));
        return shortcut.aliases.some((alias) => normalized.includes(alias));
      });

      if (!matchedBranch) return null;

      usedIds.add(matchedBranch.id);
      return {
        id: String(matchedBranch.id),
        label: shortcut.label,
        fullLabel: getBranchLabel(matchedBranch),
      };
    }).filter(Boolean);

    if (mappedShortcuts.length) {
      return mappedShortcuts;
    }

    return branches.slice(0, 3).map((branch) => ({
      id: String(branch.id),
      label: getBranchLabel(branch),
      fullLabel: getBranchLabel(branch),
    }));
  }, [branches]);

  const cartProductIds = useMemo(
    () => cart.map((item) => Number(item.id)).filter((value) => Number.isFinite(value) && value > 0),
    [cart]
  );

  useEffect(() => {
    if (!cartProductIds.length) {
      setStockByProductId({});
      return undefined;
    }

    let ignore = false;

    async function loadStockByBranch() {
      setLoadingStock(true);

      try {
        const entries = await getProductStockAvailability({
          productIds: cartProductIds,
          branchId: branchId || undefined,
        });

        if (ignore) return;

        const nextStockByProductId = Array.isArray(entries)
          ? Object.fromEntries(
              entries.map((entry) => [
                String(entry?.productId),
                normalizeAvailableQuantity(entry?.availableQuantity),
              ])
            )
          : {};

        setStockByProductId(nextStockByProductId);

        const syncResult = syncCartWithAvailability(nextStockByProductId, {
          removeUnavailable: false,
        });
        if (syncResult?.adjusted) {
          const adjustments = [
            syncResult.updatedProductIds?.length
              ? `${syncResult.updatedProductIds.length} mặt hàng đã được giảm về mức tồn kho hợp lệ`
              : null,
            syncResult.removedProductIds?.length
              ? `${syncResult.removedProductIds.length} mặt hàng tạm hết hàng đã được giữ lại trong giỏ`
              : null,
          ]
            .filter(Boolean)
            .join(". ");

          if (adjustments) {
            setBannerMessage(adjustments);
          }
        }
      } catch (loadError) {
        if (!ignore) {
          setError(loadError?.message || "Không thể đồng bộ tồn kho retail lúc này.");
          setStockByProductId({});
        }
      } finally {
        if (!ignore) {
          setLoadingStock(false);
        }
      }
    }

    loadStockByBranch();

    return () => {
      ignore = true;
    };
  }, [branchId, cartProductIds.join(",")]);

  function getAvailableQuantity(productId) {
    const key = String(productId);
    return Object.prototype.hasOwnProperty.call(stockByProductId, key)
      ? normalizeAvailableQuantity(stockByProductId[key])
      : null;
  }

  function formatStockHint(availableQuantity) {
    if (availableQuantity == null) {
      return "Đang kiểm tra tồn kho khả dụng...";
    }

    if (availableQuantity <= 0) {
      return "Hết hàng tại chi nhánh đã chọn";
    }

    return `Còn ${availableQuantity} sản phẩm tại chi nhánh đã chọn`;
  }

  function handleCartQuantityChange(product, rawValue) {
    const availableQuantity = getAvailableQuantity(product.id);
    const mutation = setItemQuantity(product.id, rawValue, {
      maxQty: availableQuantity ?? undefined,
    });

    if (!mutation?.ok && availableQuantity != null && availableQuantity <= 0) {
      setError(`${getProductName(product)} hiện đã hết hàng tại chi nhánh đã chọn.`);
      return;
    }

    if (mutation?.adjusted && availableQuantity != null) {
      setBannerMessage(
        `${getProductName(product)} chỉ còn tối đa ${availableQuantity} sản phẩm tại chi nhánh đã chọn.`
      );
    }
  }

  function handleCartQuantityStep(product, delta) {
    const availableQuantity = getAvailableQuantity(product.id);
    const mutation = updateCartQty(product.id, delta, {
      maxQty: availableQuantity ?? undefined,
    });

    if (!mutation?.ok && availableQuantity != null && availableQuantity <= 0) {
      setError(`${getProductName(product)} hiện đã hết hàng tại chi nhánh đã chọn.`);
      return;
    }

    if (mutation?.adjusted && Number(delta) > 0 && availableQuantity != null) {
      setBannerMessage(
        `${getProductName(product)} chỉ còn tối đa ${availableQuantity} sản phẩm tại chi nhánh đã chọn.`
      );
    }
  }

  async function handleApplyVoucher() {
    const code = voucherCodeInput.trim().toUpperCase();
    if (!code) {
      setVoucherStatus({ error: VOUCHER_UI.EMPTY_CODE_ERROR, success: "" });
      return;
    }
    if (!selectedItems.length) {
      setVoucherStatus({ error: VOUCHER_UI.EMPTY_CART_ERROR, success: "" });
      return;
    }

    setVoucherValidating(true);
    setVoucherStatus({ error: "", success: "" });

    try {
      const response = await voucherApi.checkVoucher({
        voucherCode: code,
        branchId: branchId ? Number(branchId) : undefined,
        eligibleAmount: selectedSubtotal,
      });

      if (response?.ok && response?.data) {
        const data = response.data;
        if (data.isEligible === false) {
          setVoucherStatus({
            error: data.reason || data.message || VOUCHER_UI.DEFAULT_ERROR,
            success: "",
          });
          return;
        }

        const discountAmount = toSafeAmount(data.discountAmount || data.calculatedDiscount || 0);
        setAppliedVoucher({
          code,
          discountAmount,
          description: data.description || data.voucherName || `Mã ${code}`,
        });
        setVoucherStatus({
          error: "",
          success: VOUCHER_UI.APPLIED_SUCCESS(code, discountAmount),
        });
      } else if (response?.status === 400 && response?.data?.isEligible === false) {
        setVoucherStatus({
          error: response.data.reason || response.data.message || VOUCHER_UI.DEFAULT_ERROR,
          success: "",
        });
      } else {
        setAppliedVoucher({
          code,
          discountAmount: 0,
          description: `Mã ${code}`,
        });
        setVoucherStatus({
          error: "",
          success: VOUCHER_UI.RECORDED_INFO(code),
        });
      }
    } catch {
      setAppliedVoucher({
        code,
        discountAmount: 0,
        description: `Mã ${code}`,
      });
      setVoucherStatus({
        error: "",
        success: VOUCHER_UI.RECORDED_INFO(code),
      });
    } finally {
      setVoucherValidating(false);
    }
  }

  function handleRemoveVoucher() {
    setAppliedVoucher(null);
    setVoucherCodeInput("");
    setVoucherStatus({ error: "", success: "" });
  }

  async function performCheckout() {
    setBannerMessage("");
    setError("");

    if (!branchId) {
      setError("Vui lòng chọn chi nhánh tiếp nhận đơn phụ tùng.");
      return;
    }

    if (!selectedItems.length) {
      setError("Hãy chọn ít nhất một phụ tùng để checkout.");
      return;
    }

    setSubmitting(true);

    try {
      const latestStockEntries = await getProductStockAvailability({
        productIds: selectedItems.map((item) => item.id),
        branchId,
      });
      const latestStockByProductId = Array.isArray(latestStockEntries)
        ? Object.fromEntries(
            latestStockEntries.map((entry) => [
              String(entry?.productId),
              normalizeAvailableQuantity(entry?.availableQuantity),
            ])
          )
        : {};

      setStockByProductId(latestStockByProductId);

      const stockIssues = selectedItems
        .map((item) => {
          const availableQuantity = normalizeAvailableQuantity(latestStockByProductId[String(item.id)]);
          return {
            id: Number(item.id),
            name: getProductName(item),
            requestedQuantity: toPositiveInt(item.qty, 1),
            availableQuantity,
          };
        })
        .filter((item) => item.requestedQuantity > item.availableQuantity);

      if (stockIssues.length) {
        syncCartWithAvailability(latestStockByProductId, {
          removeUnavailable: false,
        });
        setError(
          `Không đủ tồn kho tại chi nhánh đã chọn cho: ${stockIssues
            .map((item) => `${item.name} (còn ${item.availableQuantity})`)
            .join(", ")}. Giỏ hàng đã được đồng bộ lại theo tồn kho mới nhất.`
        );
        return;
      }

      const fulfillmentNote = buildFulfillmentNote({
        method: fulfillmentMethod,
        address:
          fulfillmentMethod === FULFILLMENT_METHOD.DELIVERY
            ? normalizedDeliveryAddress
            : "",
        preferredBranch,
        selectedBranch,
        etaDays: leadTimeDays,
      });

      const mergedCustomerNotes = [notes.trim(), fulfillmentNote]
        .filter(Boolean)
        .join(" || ");

      const payload = {
        branchId: Number(branchId),
        customerNotes: mergedCustomerNotes || null,
        items: selectedItems.map((item) => ({
          productId: Number(item.id),
          quantity: toPositiveInt(item.qty, 1),
        })),
        ...(appliedVoucher?.code ? { voucherCode: appliedVoucher.code } : {}),
      };

      const createdOrder = await checkoutRetailOrder(payload);
      const retailOrderId = Number(createdOrder?.id);

      if (!Number.isFinite(retailOrderId) || retailOrderId <= 0) {
        throw new Error("Không tạo được đơn bán lẻ hợp lệ.");
      }

      const orderCode = createdOrder?.orderCode || `RO-${retailOrderId}`;
      const subTotalAmount =
        toSafeAmount(createdOrder?.subTotal) ||
        toSafeAmount(createdOrder?.subTotalAmount) ||
        toSafeAmount(selectedSubtotal);
      const taxAmount = toSafeAmount(createdOrder?.taxAmount);
      const finalAmount =
        toSafeAmount(createdOrder?.finalAmount) || toSafeAmount(subTotalAmount + taxAmount);
      const branchLabel = selectedBranch
        ? getBranchLabel(selectedBranch)
        : `Chi nhánh #${branchId}`;

      if (finalAmount <= 0) {
        throw new Error("Không xác định được tổng thanh toán cho đơn hàng.");
      }

      removeSelectedItems();
      setNotes("");
      const resolvedVoucherDiscount =
        toSafeAmount(createdOrder?.voucherDiscountAmount) || voucherDiscount;
      setPendingOrder({
        id: retailOrderId,
        orderCode,
        branchLabel,
        customerNotes: createdOrder?.customerNotes || mergedCustomerNotes || "",
        fulfillmentMethod,
        deliveryAddress:
          fulfillmentMethod === FULFILLMENT_METHOD.DELIVERY
            ? normalizedDeliveryAddress
            : "",
        preferredBranchLabel: preferredBranch
          ? getBranchLabel(preferredBranch)
          : "Chưa xác định",
        leadTimeDays,
        subTotal: subTotalAmount,
        voucherDiscountAmount: resolvedVoucherDiscount,
        appliedVoucherCode: appliedVoucher?.code || null,
        taxAmount,
        finalAmount,
      });
      setAppliedVoucher(null);
      setVoucherCodeInput("");
      setVoucherStatus({ error: "", success: "" });
      setBannerMessage(`Đã tạo đơn ${orderCode} từ các sản phẩm đã chọn trong giỏ hàng.`);
    } catch (checkoutError) {
      setError(checkoutError?.message || "Không thể tạo đơn phụ tùng lúc này.");
    } finally {
      setSubmitting(false);
    }
  }

  async function handleCheckout() {
    if (fulfillmentMethod === FULFILLMENT_METHOD.DELIVERY && !normalizedDeliveryAddress) {
      setShowDeliveryAddressModal(true);
      setPendingCheckoutIntent(true);
      return;
    }

    await performCheckout();
  }

  async function handleConfirmDeliveryAddress() {
    if (!normalizedDeliveryAddress) {
      setError("Vui lòng nhập địa chỉ giao hàng trước khi checkout.");
      return;
    }

    setShowDeliveryAddressModal(false);

    if (pendingCheckoutIntent) {
      setPendingCheckoutIntent(false);
      await performCheckout();
    }
  }

  async function handleConfirmPendingOrderPayment() {
    if (!pendingOrder) return;

    setError("");
    setPayingOrder(true);
    let popupWindow = null;

    try {
      if (pendingOrder.finalAmount < 1000) {
        throw new Error("Tổng thanh toán tối thiểu là 1,000đ để tạo liên kết PayOS.");
      }

      popupWindow = openPayosPopupWindow();
      if (!popupWindow) {
        throw new Error("Trình duyệt đang chặn popup. Hãy cho phép popup và thử lại.");
      }

      const payosDescription = `TT ${String(pendingOrder.orderCode).slice(0, 22)}`.slice(0, 25);
      const paymentResult = await createPayosOrder({
        totalAmount: pendingOrder.finalAmount,
        description: payosDescription,
        payerType: "CUSTOMER",
        payerId: user?.id || null,
        retailOrderId: pendingOrder.id,
        paymentType: "PAYMENT",
        returnUrl: buildFrontendUrl("/payment/success"),
        cancelUrl: buildFrontendUrl("/payment/cancel"),
        items: [
          {
            name: "Đơn phụ tùng MyGarage",
            quantity: 1,
            price: pendingOrder.finalAmount,
          },
        ],
      });

      if (!paymentResult?.ok) {
        throw new Error(paymentResult?.data?.message || "Không tạo được link thanh toán PayOS.");
      }

      const paymentOrder = paymentResult.data;
      if (!paymentOrder?.checkoutUrl) {
        throw new Error("Không nhận được checkoutUrl từ PayOS.");
      }

      const retryParams = new URLSearchParams({
        amount: String(pendingOrder.finalAmount),
        description: payosDescription,
        retailOrderId: String(pendingOrder.id),
      });

      localStorage.setItem("payos_last_payment_search", `?${retryParams.toString()}`);
      localStorage.setItem("payos_last_checkout_kind", "RETAIL");
      localStorage.setItem("payos_last_retail_order_id", String(pendingOrder.id));

      if (paymentOrder?.paymentRecordId) {
        localStorage.setItem("payos_last_payment_id", String(paymentOrder.paymentRecordId));
      } else {
        localStorage.removeItem("payos_last_payment_id");
      }

      if (paymentOrder?.id) {
        localStorage.setItem("payos_last_order_id", String(paymentOrder.id));
      }

      popupWindow.location.href = paymentOrder.checkoutUrl;
      popupWindow.focus();
      setBannerMessage(`Đã mở popup PayOS cho đơn ${pendingOrder.orderCode}.`);
      setPendingOrder(null);
    } catch (paymentError) {
      if (popupWindow && !popupWindow.closed) {
        popupWindow.close();
      }
      setError(paymentError?.message || "Không thể tạo liên kết thanh toán PayOS.");
    } finally {
      setPayingOrder(false);
    }
  }

  return (
    <div className="parts-shop-page retail-cart-page">
      <section className="parts-shop-hero retail-cart-hero">
        <img
          className="parts-shop-hero-image"
          src="https://images.unsplash.com/photo-1778757949657-615fe09f063b?auto=format&fit=crop&w=2400&q=90"
          alt="Khu vực phụ tùng và hậu mãi MyGarage"
          loading="eager"
        />
        <div className="parts-shop-hero-overlay retail-cart-hero-overlay" />
        <div className="parts-shop-hero-fade" />
        <Header overlay />
        <div className="parts-shop-hero-content retail-cart-hero-content">
          <div className="retail-cart-hero-left">
            <div className="retail-cart-hero-badge">
              <span className="retail-cart-hero-badge-dot" />
              <span>MYGARAGE RETAIL CART</span>
            </div>
            <h1 className="retail-cart-hero-title">Giỏ Phụ Tùng Của Bạn</h1>
            <p className="retail-cart-hero-desc">
              Chọn đúng sản phẩm cần checkout, chỉnh số lượng nhanh, rồi chốt chi nhánh và ghi chú trong một flow retail rõ ràng hơn.
            </p>
            <div className="retail-cart-hero-actions">
              <Link to="/parts-shop" className="parts-shop-btn parts-shop-btn-primary">
                <ShoppingBag size={17} />
                Tiếp tục mua phụ tùng
              </Link>
              <Link to="/my-retail-orders" className="parts-shop-btn parts-shop-btn-ghost">
                <Package size={17} />
                Xem đơn phụ tùng
              </Link>
            </div>
          </div>

          <div className="retail-cart-hero-right">
            <div className="retail-cart-dashboard-card">
              <div className="retail-dash-header">
                <div className="retail-dash-title">
                  <ShoppingCart size={18} color="var(--parts-gold, #c5a059)" />
                  <span>Tổng Quan Giỏ Hàng</span>
                </div>
                <span className="retail-dash-badge">
                  <span className="retail-dash-dot" />
                  {selectedLineCount > 0 ? "Sẵn sàng checkout" : "Đang duyệt giỏ"}
                </span>
              </div>

              <div className="retail-dash-grid">
                <div className="retail-dash-item">
                  <div className="retail-dash-icon-wrap" style={{ background: "rgba(197, 160, 89, 0.12)", color: "#dfbe7d" }}>
                    <Package size={20} />
                  </div>
                  <div className="retail-dash-info">
                    <span className="retail-dash-label">Mặt hàng trong giỏ</span>
                    <strong className="retail-dash-value">{totalLineItems}</strong>
                  </div>
                </div>

                <div className="retail-dash-item">
                  <div className="retail-dash-icon-wrap" style={{ background: "rgba(50, 213, 131, 0.12)", color: "#4ee09b" }}>
                    <CheckCircle2 size={20} />
                  </div>
                  <div className="retail-dash-info">
                    <span className="retail-dash-label">Đã chọn checkout</span>
                    <strong className="retail-dash-value">{selectedQuantity}</strong>
                  </div>
                </div>
              </div>

              <div className="retail-dash-summary-row">
                <div className="retail-dash-summary-label">
                  <span>Tạm tính đã chọn</span>
                  <small>{selectedLineCount} sản phẩm được đánh dấu</small>
                </div>
                <div className="retail-dash-summary-price">
                  {formatVnd(selectedSubtotal)}
                </div>
              </div>
            </div>
          </div>
        </div>
      </section>

      <div className="parts-shop retail-cart-container">
        <section className="parts-shop-header retail-cart-main-header">
          <div className="retail-cart-header-meta">
            <span className="parts-shop-section-kicker">Chi tiết giỏ hàng</span>
            <h2>Danh sách sản phẩm trong giỏ</h2>
          </div>
          <p>
            Chọn đúng sản phẩm cần checkout, chỉnh số lượng nhanh, rồi chốt chi nhánh và ghi chú trong một flow retail rõ ràng hơn.
          </p>
        </section>

        {error ? (
          <div className="parts-shop-card parts-shop-alert parts-shop-alert-error">{error}</div>
        ) : null}
        {bannerMessage ? (
          <div className="parts-shop-card parts-shop-alert parts-shop-alert-success">
            {bannerMessage}
          </div>
        ) : null}

        {cart.length === 0 ? (
          <section className="parts-shop-card retail-cart-empty">
            <div className="retail-cart-empty-icon">
              <ShoppingCart size={44} strokeWidth={1.5} />
            </div>
            <span className="parts-shop-section-kicker">Cart trống</span>
            <h3>Giỏ phụ tùng hiện chưa có sản phẩm nào.</h3>
            <p>Quay lại Part Shop để thêm phụ tùng rồi checkout khi bạn đã sẵn sàng.</p>
            <div className="retail-cart-empty-actions">
              <Link to="/parts-shop" className="parts-shop-btn parts-shop-btn-primary">
                <ShoppingBag size={17} />
                Mở Part Shop
              </Link>
              <Link to="/my-retail-orders" className="parts-shop-btn parts-shop-btn-ghost">
                <Package size={17} />
                Xem đơn phụ tùng
              </Link>
            </div>
          </section>
        ) : (
          <div className="retail-cart-layout">
            <section className="parts-shop-card retail-cart-panel">
              <div className="retail-cart-panel-head">
                <div>
                  <span className="parts-shop-section-kicker">Cart items</span>
                  <h3>Phụ tùng đang có trong giỏ</h3>
                  <p>{totalLineItems} mặt hàng trong giỏ, {selectedLineCount} mặt hàng đang được chọn để checkout.</p>
                </div>
                <div className="retail-cart-toolbar">
                  <label className="retail-cart-select-all">
                    <input
                      type="checkbox"
                      checked={areAllItemsSelected}
                      onChange={(event) => toggleSelectAllCartItems(event.target.checked)}
                    />
                    <span>Chọn tất cả</span>
                  </label>
                  <button
                    type="button"
                    className="parts-shop-btn parts-shop-btn-ghost retail-cart-btn-danger"
                    onClick={removeSelectedItems}
                    disabled={!selectedItems.length}
                  >
                    <Trash2 size={15} />
                    <span>Xóa đã chọn</span>
                  </button>
                </div>
              </div>

              <div className="retail-cart-list">
                {cart.map((item) => {
                  const relatedImages = productImagesByProductId.get(String(item.id)) || [];
                  const category = categoriesById.get(String(item.categoryId));
                  const availableQuantity = getAvailableQuantity(item.id);
                  const isOutOfStock = availableQuantity != null && availableQuantity <= 0;
                  const isAtStockLimit =
                    availableQuantity != null && availableQuantity > 0 && Number(item.qty || 0) >= availableQuantity;
                  const categoryLabel =
                    category || item?.categoryId
                      ? getCategoryLabel(category || { id: item.categoryId })
                      : "Danh mục chưa cập nhật";
                  const lineTotal = Number(item?.retailPrice || 0) * Number(item?.qty || 0);

                  return (
                    <article
                      key={item.id}
                      className={`retail-cart-item ${item.selected !== false ? "is-selected" : ""}`}
                    >
                      <button
                        type="button"
                        className="retail-cart-item-remove-btn"
                        onClick={() => removeFromCart(item.id)}
                        title="Xóa phụ tùng khỏi giỏ hàng"
                        aria-label={`Xóa ${getProductName(item)} khỏi giỏ hàng`}
                      >
                        <Trash2 size={16} />
                      </button>

                      <label className="retail-cart-item-check" title={`Chọn phụ tùng ${getProductName(item)}`}>
                        <input
                          type="checkbox"
                          checked={item.selected !== false}
                          onChange={(event) => setItemSelected(item.id, event.target.checked)}
                          aria-label={`Chọn phụ tùng ${getProductName(item)}`}
                        />
                        <span className="sr-only">
                          Chọn phụ tùng {getProductName(item)}
                        </span>
                      </label>

                      <Link
                        to={`/parts-shop/products/${item.id}`}
                        className="retail-cart-item-media"
                        aria-label={`Xem chi tiết phụ tùng ${getProductName(item)}`}
                      >
                        <RetailImage
                          src={resolveProductImage(item, relatedImages)}
                          alt={getProductImageAlt(item, relatedImages[0])}
                          className="retail-cart-item-image"
                          fallbackClassName="retail-cart-item-image-fallback"
                          fallbackLabel="Ảnh phụ tùng đang cập nhật"
                        />
                      </Link>

                      <div className="retail-cart-item-main">
                        <div className="retail-cart-item-copy">
                          <Link to={`/parts-shop/products/${item.id}`} className="retail-cart-item-name">
                            {getProductName(item)}
                          </Link>
                          <div className="retail-cart-item-meta">
                            <span>{item.sku ? `SKU ${item.sku}` : `ID #${item.id}`}</span>
                            <span>{categoryLabel}</span>
                            <span>{getProductUnitLabel(item)}</span>
                          </div>
                          <div className={`retail-cart-item-stock ${isOutOfStock ? "is-out" : ""}`}>
                            {formatStockHint(availableQuantity)}
                          </div>
                        </div>

                        <div className="retail-cart-item-price">
                          <span>Đơn giá</span>
                          <strong>{formatVnd(item.retailPrice)}</strong>
                        </div>

                        <div className="retail-cart-item-qty">
                          <span>Số lượng</span>
                          <div className="retail-cart-qty-stepper">
                            <button
                              type="button"
                              className="retail-cart-qty-btn"
                              onClick={() => handleCartQuantityStep(item, -1)}
                              aria-label={`Giảm số lượng ${getProductName(item)}`}
                              disabled={isOutOfStock}
                            >
                              -
                            </button>
                            <input
                              type="number"
                              min="1"
                              max={Math.max(availableQuantity ?? Number(item.qty || 1), 1)}
                              className="retail-cart-qty-input"
                              value={item.qty}
                              onChange={(event) => handleCartQuantityChange(item, event.target.value)}
                              aria-label={`Số lượng ${getProductName(item)}`}
                              disabled={isOutOfStock}
                            />
                            <button
                              type="button"
                              className="retail-cart-qty-btn"
                              onClick={() => handleCartQuantityStep(item, 1)}
                              aria-label={`Tăng số lượng ${getProductName(item)}`}
                              disabled={isOutOfStock || isAtStockLimit}
                            >
                              +
                            </button>
                          </div>
                        </div>

                        <div className="retail-cart-item-total">
                          <span>Thành tiền</span>
                          <strong>{formatVnd(lineTotal)}</strong>
                        </div>
                      </div>
                    </article>
                  );
                })}
              </div>
            </section>

            <aside className="parts-shop-card retail-cart-summary-card">
              <div className="retail-cart-summary-head">
                <span className="parts-shop-section-kicker">Checkout summary</span>
                <h3>Tóm tắt checkout</h3>
                <p>Chỉ các item đang được chọn mới được đưa vào đơn bán lẻ.</p>
              </div>

              <div className="retail-cart-summary-stats">
                <div className="parts-shop-summary-row">
                  <span>Tổng trong cart</span>
                  <strong>{formatVnd(subtotal)}</strong>
                </div>
                <div className="parts-shop-summary-row">
                  <span>Dòng đã chọn</span>
                  <strong>{selectedLineCount}</strong>
                </div>
                <div className="parts-shop-summary-row">
                  <span>Số lượng đã chọn</span>
                  <strong>{selectedQuantity}</strong>
                </div>
                <div className="parts-shop-summary-row">
                  <span>Tạm tính đã chọn</span>
                  <strong>{formatVnd(selectedSubtotal)}</strong>
                </div>
                {voucherDiscount > 0 ? (
                  <div className="parts-shop-summary-row">
                    <span>{VOUCHER_UI.DISCOUNT_ROW_LABEL(appliedVoucher?.code)}</span>
                    <strong style={{ color: "var(--parts-emerald, #059669)" }}>
                      -{formatVnd(voucherDiscount)}
                    </strong>
                  </div>
                ) : null}
                {voucherDiscount > 0 ? (
                  <div className="parts-shop-summary-row">
                    <span>{VOUCHER_UI.AFTER_DISCOUNT_LABEL}</span>
                    <strong>{formatVnd(selectedSubtotalAfterDiscount)}</strong>
                  </div>
                ) : null}
              </div>

              <div className="retail-cart-form">
                <div className="retail-cart-field retail-cart-voucher-field">
                  <span>{VOUCHER_UI.LABEL}</span>
                  <div className="retail-cart-voucher-input-group">
                    <input
                      type="text"
                      value={voucherCodeInput}
                      onChange={(event) => {
                        setVoucherCodeInput(event.target.value.toUpperCase());
                        if (voucherStatus.error) setVoucherStatus((prev) => ({ ...prev, error: "" }));
                      }}
                      placeholder={VOUCHER_UI.PLACEHOLDER}
                      disabled={voucherValidating || Boolean(appliedVoucher)}
                      className="retail-cart-voucher-input"
                    />
                    {appliedVoucher ? (
                      <button
                        type="button"
                        className="parts-shop-btn parts-shop-btn-ghost retail-cart-voucher-btn"
                        onClick={handleRemoveVoucher}
                      >
                        {VOUCHER_UI.REMOVE_BTN}
                      </button>
                    ) : (
                      <button
                        type="button"
                        className="parts-shop-btn parts-shop-btn-primary retail-cart-voucher-btn"
                        onClick={handleApplyVoucher}
                        disabled={voucherValidating || !voucherCodeInput.trim() || !selectedItems.length}
                      >
                        {voucherValidating ? VOUCHER_UI.VALIDATING_BTN : VOUCHER_UI.APPLY_BTN}
                      </button>
                    )}
                  </div>
                  {voucherStatus.error ? (
                    <div className="retail-cart-voucher-msg is-error" role="alert">
                      {voucherStatus.error}
                    </div>
                  ) : null}
                  {voucherStatus.success ? (
                    <div className="retail-cart-voucher-msg is-success">
                      {voucherStatus.success}
                    </div>
                  ) : null}
                </div>
                <div className="retail-cart-field">
                  <span>Chi nhánh tiếp nhận</span>
                  <div className="retail-cart-branch-fixed">
                    <Store size={15} />
                    <span className="retail-cart-branch-fixed-name">
                      {selectedBranch ? getBranchLabel(selectedBranch) : "Chưa chọn chi nhánh"}
                    </span>
                  </div>
                </div>

                <div className="retail-cart-field">
                  <span>Hình thức nhận hàng</span>
                  <div className="retail-cart-fulfillment-options" role="radiogroup" aria-label="Hình thức nhận hàng">
                    <button
                      type="button"
                      role="radio"
                      aria-checked={fulfillmentMethod === FULFILLMENT_METHOD.PICKUP}
                      className={`retail-cart-fulfillment-option${fulfillmentMethod === FULFILLMENT_METHOD.PICKUP ? " is-active" : ""}`}
                      onClick={() => setFulfillmentMethod(FULFILLMENT_METHOD.PICKUP)}
                    >
                      <Store size={15} />
                      <span>Lấy tại cửa hàng</span>
                    </button>
                    <button
                      type="button"
                      role="radio"
                      aria-checked={fulfillmentMethod === FULFILLMENT_METHOD.DELIVERY}
                      className={`retail-cart-fulfillment-option${fulfillmentMethod === FULFILLMENT_METHOD.DELIVERY ? " is-active" : ""}`}
                      onClick={() => setFulfillmentMethod(FULFILLMENT_METHOD.DELIVERY)}
                    >
                      <Truck size={15} />
                      <span>Giao hàng tận nơi</span>
                    </button>
                  </div>
                  <div className="retail-cart-fulfillment-hint">{fulfillmentPreviewText}</div>
                  {fulfillmentMethod === FULFILLMENT_METHOD.DELIVERY ? (
                    <button
                      type="button"
                      className="parts-shop-btn parts-shop-btn-ghost retail-cart-delivery-address-btn"
                      onClick={() => setShowDeliveryAddressModal(true)}
                    >
                      {normalizedDeliveryAddress ? "Sửa địa chỉ giao hàng" : "Nhập địa chỉ giao hàng"}
                    </button>
                  ) : null}
                </div>

                <label className="retail-cart-field">
                  <span>Ghi chú cho đơn bán lẻ</span>
                  <textarea
                    value={notes}
                    onChange={(event) => setNotes(event.target.value)}
                    rows={4}
                    placeholder="Ví dụ: gọi trước khi xác nhận, nhận tại quầy, ưu tiên đúng đời xe..."
                  />
                </label>
              </div>

              <div className="retail-cart-summary-note">
                <strong>{user?.fullName || "Khách hàng MyGarage"}</strong>
                <p>
                  Sau khi tạo đơn thành công, chỉ các item đã chọn mới bị gỡ khỏi cart.
                  Những item chưa chọn sẽ được giữ lại cho lần mua tiếp theo.
                </p>
              </div>

              <div className="retail-cart-summary-actions">
                <button
                  type="button"
                  className="parts-shop-btn parts-shop-btn-primary retail-cart-checkout-btn"
                  disabled={submitting || payingOrder || !selectedItems.length || !branchId}
                  onClick={handleCheckout}
                >
                  <CreditCard size={18} />
                  <span>
                    {submitting
                      ? "Đang tạo đơn..."
                      : payingOrder
                        ? "Đang mở PayOS..."
                        : "Checkout sản phẩm đã chọn"}
                  </span>
                </button>
                <Link to="/parts-shop" className="parts-shop-btn parts-shop-btn-ghost retail-cart-checkout-btn">
                  <ArrowRight size={16} />
                  <span>Quay lại mua tiếp</span>
                </Link>
              </div>

              {loadingMeta ? (
                <div className="retail-cart-summary-footnote">Đang đồng bộ danh mục, ảnh và chi nhánh...</div>
              ) : null}
              {loadingStock ? (
                <div className="retail-cart-summary-footnote">Đang kiểm tra tồn kho khả dụng theo chi nhánh đã chọn...</div>
              ) : null}
            </aside>
          </div>
        )}
      </div>

      {pendingOrder ? (
        <div
          className="parts-shop-modal-backdrop"
          role="presentation"
          onClick={(event) => {
            if (event.target !== event.currentTarget || payingOrder) return;
            setPendingOrder(null);
          }}
        >
          <section
            className="parts-shop-modal retail-cart-payos-modal"
            role="dialog"
            aria-modal="true"
            aria-labelledby="parts-shop-modal-title"
          >
            {/* Modal Header (Fixed at top) */}
            <div className="retail-cart-modal-header">
              <div className="retail-cart-modal-header-info">
                <div className="retail-cart-modal-badge">
                  <span className="retail-cart-modal-badge-dot"></span>
                  <span>Đơn hàng đã khởi tạo</span>
                </div>
                <h3 id="parts-shop-modal-title" className="retail-cart-modal-title">
                  Xác nhận &amp; Thanh toán
                </h3>
              </div>
              <button
                type="button"
                className="retail-cart-modal-close"
                onClick={() => setPendingOrder(null)}
                disabled={payingOrder}
                aria-label="Đóng popup"
              >
                <X size={18} />
              </button>
            </div>

            {/* Modal Body (Scrollable!) */}
            <div className="retail-cart-modal-body">
              {/* Order Code Callout Banner */}
              <div className="retail-cart-modal-order-banner">
                <div className="retail-cart-modal-order-meta">
                  <span className="retail-cart-modal-order-label">MÃ ĐƠN HÀNG</span>
                  <strong className="retail-cart-modal-order-code">{pendingOrder.orderCode}</strong>
                </div>
                <div className="retail-cart-modal-order-status">
                  <CheckCircle2 size={16} />
                  <span>Sẵn sàng thanh toán</span>
                </div>
              </div>

              {/* Group 1: Fulfillment / Delivery Info */}
              <div className="retail-cart-modal-section">
                <h4 className="retail-cart-modal-section-title">
                  <Store size={15} />
                  <span>Thông tin nhận hàng</span>
                </h4>
                <div className="retail-cart-modal-grid">
                  <div className="retail-cart-modal-info-item">
                    <span className="retail-cart-modal-info-label">Chi nhánh xuất kho</span>
                    <strong className="retail-cart-modal-info-val">{pendingOrder.branchLabel}</strong>
                  </div>
                  <div className="retail-cart-modal-info-item">
                    <span className="retail-cart-modal-info-label">Hình thức</span>
                    <strong className="retail-cart-modal-info-val">
                      {pendingOrder.fulfillmentMethod === FULFILLMENT_METHOD.DELIVERY
                        ? "Giao tận nơi"
                        : "Lấy tại cửa hàng"}
                    </strong>
                  </div>
                  <div className="retail-cart-modal-info-item">
                    <span className="retail-cart-modal-info-label">Chi nhánh ưu tiên</span>
                    <strong className="retail-cart-modal-info-val">{pendingOrder.preferredBranchLabel}</strong>
                  </div>
                  <div className="retail-cart-modal-info-item">
                    <span className="retail-cart-modal-info-label">Dự kiến hoàn thành</span>
                    <strong className="retail-cart-modal-info-val retail-highlight-amber">
                      {pendingOrder.leadTimeDays} ngày
                    </strong>
                  </div>
                </div>

                {pendingOrder.fulfillmentMethod === FULFILLMENT_METHOD.DELIVERY && pendingOrder.deliveryAddress ? (
                  <div className="retail-cart-modal-address-box">
                    <MapPin size={15} />
                    <div>
                      <span>Địa chỉ nhận hàng</span>
                      <p>{pendingOrder.deliveryAddress}</p>
                    </div>
                  </div>
                ) : null}
              </div>

              {/* Group 2: Payment Breakdown */}
              <div className="retail-cart-modal-section">
                <h4 className="retail-cart-modal-section-title">
                  <Receipt size={15} />
                  <span>Chi tiết thanh toán</span>
                </h4>
                <div className="retail-cart-modal-breakdown">
                  <div className="retail-cart-modal-line">
                    <span>Tạm tính</span>
                    <strong>{formatVnd(pendingOrder.subTotal)}</strong>
                  </div>

                  {pendingOrder.voucherDiscountAmount > 0 ? (
                    <div className="retail-cart-modal-line is-discount">
                      <span>{VOUCHER_UI.CONFIRM_DISCOUNT_LABEL(pendingOrder.appliedVoucherCode || "VOUCHER")}</span>
                      <strong>-{formatVnd(pendingOrder.voucherDiscountAmount)}</strong>
                    </div>
                  ) : null}

                  <div className="retail-cart-modal-line">
                    <span>Thuế VAT (10%)</span>
                    <strong>{formatVnd(pendingOrder.taxAmount)}</strong>
                  </div>

                  <div className="retail-cart-modal-line is-total">
                    <div>
                      <span>Tổng thanh toán</span>
                      <small>Đã bao gồm thuế và chiết khấu</small>
                    </div>
                    <strong>{formatVnd(pendingOrder.finalAmount)}</strong>
                  </div>
                </div>
              </div>

              {/* Group 3: Customer Personal Notes (Suppress internal [FULFILLMENT] serialization) */}
              {(() => {
                const customerNotesText = pendingOrder.customerNotes || "";
                const cleanNote = customerNotesText
                  .split(" || ")
                  .filter((p) => !p.trim().startsWith("[FULFILLMENT]"))
                  .join(" || ")
                  .trim();
                return cleanNote ? (
                  <div className="retail-cart-modal-section">
                    <h4 className="retail-cart-modal-section-title">
                      <FileText size={15} />
                      <span>Ghi chú của bạn</span>
                    </h4>
                    <div className="retail-cart-modal-note-box">
                      <p>{cleanNote}</p>
                    </div>
                  </div>
                ) : null;
              })()}

              {/* Gateway Assurance */}
              <div className="retail-cart-modal-gateway-info">
                <ShieldCheck size={16} />
                <span>
                  Thanh toán bảo mật qua cổng <strong>PayOS</strong> (Hỗ trợ quét mã VietQR, thẻ ATM &amp; Internet Banking).
                </span>
              </div>
            </div>

            {/* Modal Footer (Fixed at bottom) */}
            <div className="retail-cart-modal-footer">
              <button
                type="button"
                className="parts-shop-btn parts-shop-btn-primary retail-cart-modal-btn-pay"
                onClick={handleConfirmPendingOrderPayment}
                disabled={payingOrder}
              >
                <CreditCard size={18} />
                <span>{payingOrder ? "Đang tạo liên kết PayOS..." : "Xác nhận và thanh toán"}</span>
              </button>

              <div className="retail-cart-modal-footer-secondary">
                <Link
                  to="/my-retail-orders"
                  className="parts-shop-btn parts-shop-btn-ghost"
                  onClick={() => setPendingOrder(null)}
                >
                  <ArrowRight size={15} />
                  <span>Xem đơn của tôi</span>
                </Link>
                <button
                  type="button"
                  className="parts-shop-btn parts-shop-btn-ghost"
                  onClick={() => setPendingOrder(null)}
                  disabled={payingOrder}
                >
                  <span>Đóng</span>
                </button>
              </div>
            </div>
          </section>
        </div>
      ) : null}

      {showDeliveryAddressModal ? (
        <div
          className="parts-shop-modal-backdrop"
          role="presentation"
          onClick={(event) => {
            if (event.target !== event.currentTarget || submitting) return;
            setShowDeliveryAddressModal(false);
            setPendingCheckoutIntent(false);
          }}
        >
          <section
            className="parts-shop-modal retail-cart-delivery-modal"
            role="dialog"
            aria-modal="true"
            aria-labelledby="retail-delivery-address-title"
          >
            <div className="parts-shop-modal-head">
              <span className="parts-shop-section-kicker">Giao hàng tận nơi</span>
              <h3 id="retail-delivery-address-title">Nhập địa chỉ giao hàng</h3>
              <p>
                Nếu đặt từ chi nhánh khác chi nhánh ưu tiên, thời gian giao đến địa chỉ sẽ khoảng 30 ngày.
              </p>
            </div>

            <label className="retail-cart-field">
              <span>Địa chỉ nhận hàng</span>
              <textarea
                value={deliveryAddress}
                onChange={(event) => setDeliveryAddress(event.target.value)}
                rows={4}
                placeholder="Số nhà, đường, phường/xã, quận/huyện, tỉnh/thành..."
              />
            </label>

            <div className="parts-shop-modal-actions">
              <button
                type="button"
                className="parts-shop-btn parts-shop-btn-primary"
                onClick={handleConfirmDeliveryAddress}
              >
                Xác nhận địa chỉ
              </button>
              <button
                type="button"
                className="parts-shop-btn parts-shop-btn-ghost"
                onClick={() => {
                  setShowDeliveryAddressModal(false);
                  setPendingCheckoutIntent(false);
                }}
              >
                Đóng
              </button>
            </div>
          </section>
        </div>
      ) : null}

      <Footer />
    </div>
  );
}
