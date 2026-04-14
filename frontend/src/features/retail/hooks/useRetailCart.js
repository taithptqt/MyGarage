import { useEffect, useMemo, useState } from "react";
import {
  clampQuantityToAvailable,
  normalizeAvailableQuantity,
  RETAIL_CART_STORAGE_KEY,
  toPositiveInt,
} from "@features/retail/utils/retailFormat";

function toNormalizedPrice(value) {
  const parsed = Number(value);
  return Number.isFinite(parsed) && parsed >= 0 ? parsed : 0;
}

function normalizeCartItem(item) {
  const id = Number(item?.id ?? item?.productId ?? item?.ProductId);
  if (!Number.isFinite(id) || id <= 0) {
    return null;
  }

  const qty = toPositiveInt(item?.qty ?? item?.quantity, 1);

  return {
    id,
    name: item?.name ?? item?.productName ?? item?.ProductName ?? `Phụ tùng #${id}`,
    sku: item?.sku ?? item?.productSku ?? item?.ProductSku ?? null,
    unit: item?.unit ?? item?.Unit ?? null,
    retailPrice: toNormalizedPrice(
      item?.retailPrice ?? item?.price ?? item?.unitPriceAtTime ?? item?.UnitPriceAtTime
    ),
    qty,
    selected: item?.selected !== false,
    categoryId: item?.categoryId ?? item?.CategoryId ?? null,
    imageUrl:
      item?.imageUrl ??
      item?.thumbnailUrl ??
      item?.imagePath ??
      item?.photoUrl ??
      item?.photo ??
      null,
  };
}

function readStoredCart() {
  try {
    const raw = localStorage.getItem(RETAIL_CART_STORAGE_KEY);
    if (!raw) return [];

    const parsed = JSON.parse(raw);
    if (!Array.isArray(parsed)) {
      return [];
    }

    return parsed.map(normalizeCartItem).filter(Boolean);
  } catch {
    return [];
  }
}

function getMaxQuantityOption(options = {}) {
  if (options?.maxQty == null || options.maxQty === "") {
    return null;
  }

  return normalizeAvailableQuantity(options.maxQty);
}

function buildCartMutationResult(overrides = {}) {
  return {
    ok: true,
    adjusted: false,
    addedQuantity: 0,
    quantity: null,
    requestedQuantity: null,
    maxQty: null,
    removedProductIds: [],
    updatedProductIds: [],
    reason: null,
    ...overrides,
  };
}

export default function useRetailCart() {
  const [cart, setCart] = useState(readStoredCart);

  useEffect(() => {
    if (!cart.length) {
      localStorage.removeItem(RETAIL_CART_STORAGE_KEY);
      return;
    }

    localStorage.setItem(RETAIL_CART_STORAGE_KEY, JSON.stringify(cart));
  }, [cart]);

  const totalItems = useMemo(
    () => cart.reduce((sum, item) => sum + Number(item?.qty || 0), 0),
    [cart]
  );

  const totalLineItems = useMemo(() => cart.length, [cart]);

  const subtotal = useMemo(
    () =>
      cart.reduce(
        (sum, item) => sum + Number(item?.retailPrice || 0) * Number(item?.qty || 0),
        0
      ),
    [cart]
  );

  const selectedItems = useMemo(
    () => cart.filter((item) => item?.selected !== false),
    [cart]
  );

  const selectedLineCount = useMemo(() => selectedItems.length, [selectedItems]);

  const selectedQuantity = useMemo(
    () => selectedItems.reduce((sum, item) => sum + Number(item?.qty || 0), 0),
    [selectedItems]
  );

  const selectedSubtotal = useMemo(
    () =>
      selectedItems.reduce(
        (sum, item) => sum + Number(item?.retailPrice || 0) * Number(item?.qty || 0),
        0
      ),
    [selectedItems]
  );

  const areAllItemsSelected = useMemo(
    () => cart.length > 0 && cart.every((item) => item?.selected !== false),
    [cart]
  );

  function addToCart(product, rawQty = 1, options = {}) {
    const qty = toPositiveInt(rawQty, 1);
    const maxQty = getMaxQuantityOption(options);
    let mutationResult = buildCartMutationResult({
      requestedQuantity: qty,
      maxQty,
    });

    setCart((previous) => {
      const existing = previous.find((item) => Number(item.id) === Number(product.id));
      const currentQty = Number(existing?.qty || 0);
      const requestedNextQty = currentQty + qty;

      if (maxQty != null && maxQty <= 0) {
        mutationResult = buildCartMutationResult({
          ok: false,
          requestedQuantity: qty,
          maxQty,
          reason: "out_of_stock",
        });
        return previous;
      }

      const nextQty =
        maxQty != null
          ? clampQuantityToAvailable(requestedNextQty, maxQty, requestedNextQty)
          : requestedNextQty;

      const addedQuantity = Math.max(0, nextQty - currentQty);

      if (addedQuantity <= 0 && existing) {
        mutationResult = buildCartMutationResult({
          ok: false,
          adjusted: maxQty != null && requestedNextQty > nextQty,
          requestedQuantity: qty,
          quantity: currentQty,
          maxQty,
          reason: "max_reached",
        });
        return previous;
      }

      if (existing) {
        mutationResult = buildCartMutationResult({
          ok: true,
          adjusted: requestedNextQty !== nextQty,
          addedQuantity,
          requestedQuantity: qty,
          quantity: nextQty,
          maxQty,
          updatedProductIds: [Number(product.id)],
        });

        return previous.map((item) =>
          Number(item.id) === Number(product.id)
            ? { ...item, qty: nextQty, selected: true }
            : item
        );
      }

      const nextItem = normalizeCartItem({
        ...product,
        id: product?.id,
        name: product?.name,
        sku: product?.sku,
        unit: product?.unit,
        retailPrice: product?.retailPrice,
        qty: maxQty != null ? clampQuantityToAvailable(qty, maxQty, qty) : qty,
        selected: true,
        categoryId: product?.categoryId ?? null,
      });

      if (!nextItem || Number(nextItem.qty || 0) <= 0) {
        mutationResult = buildCartMutationResult({
          ok: false,
          requestedQuantity: qty,
          maxQty,
          reason: "out_of_stock",
        });
        return previous;
      }

      mutationResult = buildCartMutationResult({
        ok: true,
        adjusted: qty !== nextItem.qty,
        addedQuantity: nextItem.qty,
        requestedQuantity: qty,
        quantity: nextItem.qty,
        maxQty,
        updatedProductIds: [Number(product.id)],
      });

      return nextItem ? [...previous, nextItem] : previous;
    });

    return mutationResult;
  }

  function setItemQuantity(productId, rawQty, options = {}) {
    const requestedQty = toPositiveInt(rawQty, 1);
    const maxQty = getMaxQuantityOption(options);
    let mutationResult = buildCartMutationResult({
      requestedQuantity: requestedQty,
      maxQty,
      updatedProductIds: [Number(productId)],
    });

    setCart((previous) =>
      previous.map((item) => {
        if (Number(item.id) !== Number(productId)) {
          return item;
        }

        if (maxQty != null && maxQty <= 0) {
          mutationResult = buildCartMutationResult({
            ok: false,
            requestedQuantity: requestedQty,
            maxQty,
            updatedProductIds: [Number(productId)],
            reason: "out_of_stock",
            quantity: Number(item.qty || 1),
          });
          return item;
        }

        const nextQty =
          maxQty != null
            ? clampQuantityToAvailable(requestedQty, maxQty, requestedQty)
            : requestedQty;

        mutationResult = buildCartMutationResult({
          ok: true,
          adjusted: nextQty !== requestedQty,
          requestedQuantity: requestedQty,
          maxQty,
          updatedProductIds: [Number(productId)],
          quantity: nextQty,
        });

        return { ...item, qty: nextQty };
      })
    );

    return mutationResult;
  }

  function updateCartQty(productId, delta, options = {}) {
    const maxQty = getMaxQuantityOption(options);
    const normalizedDelta = Number(delta || 0);
    let mutationResult = buildCartMutationResult({
      maxQty,
      updatedProductIds: [Number(productId)],
    });

    setCart((previous) =>
      previous.map((item) => {
        if (Number(item.id) !== Number(productId)) {
          return item;
        }

        if (maxQty != null && maxQty <= 0) {
          mutationResult = buildCartMutationResult({
            ok: false,
            maxQty,
            updatedProductIds: [Number(productId)],
            reason: "out_of_stock",
            quantity: Number(item.qty || 1),
            requestedQuantity: Number(item.qty || 1),
          });
          return item;
        }

        const requestedQty = Math.max(1, Number(item.qty || 1) + normalizedDelta);
        const nextQty =
          maxQty != null
            ? clampQuantityToAvailable(requestedQty, maxQty, requestedQty)
            : requestedQty;

        mutationResult = buildCartMutationResult({
          ok: true,
          adjusted: nextQty !== requestedQty,
          maxQty,
          updatedProductIds: [Number(productId)],
          quantity: nextQty,
          requestedQuantity: requestedQty,
        });

        return { ...item, qty: nextQty };
      })
    );

    return mutationResult;
  }

  function removeFromCart(productId) {
    setCart((previous) =>
      previous.filter((item) => Number(item.id) !== Number(productId))
    );
  }

  function clearCart() {
    setCart([]);
  }

  function setItemSelected(productId, selected) {
    setCart((previous) =>
      previous.map((item) =>
        Number(item.id) === Number(productId)
          ? { ...item, selected: selected !== false }
          : item
      )
    );
  }

  function toggleItemSelected(productId) {
    setCart((previous) =>
      previous.map((item) =>
        Number(item.id) === Number(productId)
          ? { ...item, selected: item?.selected === false }
          : item
      )
    );
  }

  function selectAllCartItems() {
    setCart((previous) => previous.map((item) => ({ ...item, selected: true })));
  }

  function deselectAllCartItems() {
    setCart((previous) => previous.map((item) => ({ ...item, selected: false })));
  }

  function toggleSelectAllCartItems(forceSelected) {
    const shouldSelect =
      typeof forceSelected === "boolean" ? forceSelected : !areAllItemsSelected;

    setCart((previous) =>
      previous.map((item) => ({ ...item, selected: shouldSelect }))
    );
  }

  function removeSelectedItems() {
    setCart((previous) => previous.filter((item) => item?.selected === false));
  }

  function syncCartWithAvailability(availabilityMap = {}, options = {}) {
    const removeUnavailable = options?.removeUnavailable !== false;
    const normalizedAvailabilityMap = Object.fromEntries(
      Object.entries(availabilityMap || {}).map(([key, value]) => [
        String(key),
        normalizeAvailableQuantity(value),
      ])
    );

    let mutationResult = buildCartMutationResult({
      adjusted: false,
      removedProductIds: [],
      updatedProductIds: [],
    });

    setCart((previous) => {
      const nextCart = [];

      previous.forEach((item) => {
        const productId = Number(item.id);
        const mapKey = String(productId);
        if (!(mapKey in normalizedAvailabilityMap)) {
          nextCart.push(item);
          return;
        }

        const availableQuantity = normalizedAvailabilityMap[mapKey];
        if (availableQuantity <= 0) {
          mutationResult.removedProductIds.push(productId);
          mutationResult.adjusted = true;

          if (!removeUnavailable) {
            nextCart.push({ ...item, qty: 1, selected: false });
          }

          return;
        }

        const nextQty = Math.min(Number(item.qty || 1), availableQuantity);
        if (nextQty !== Number(item.qty || 1)) {
          mutationResult.adjusted = true;
          mutationResult.updatedProductIds.push(productId);
        }

        nextCart.push({
          ...item,
          qty: nextQty,
        });
      });

      mutationResult.ok = true;
      return nextCart;
    });

    return mutationResult;
  }

  return {
    cart,
    subtotal,
    totalItems,
    totalLineItems,
    selectedItems,
    selectedLineCount,
    selectedQuantity,
    selectedSubtotal,
    areAllItemsSelected,
    addToCart,
    clearCart,
    deselectAllCartItems,
    removeFromCart,
    removeSelectedItems,
    selectAllCartItems,
    setItemSelected,
    setItemQuantity,
    syncCartWithAvailability,
    toggleItemSelected,
    toggleSelectAllCartItems,
    updateCartQty,
  };
}
