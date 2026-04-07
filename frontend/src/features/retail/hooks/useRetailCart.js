import { useEffect, useMemo, useState } from "react";
import { RETAIL_CART_STORAGE_KEY, toPositiveInt } from "@features/retail/utils/retailFormat";

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

  function addToCart(product, rawQty = 1) {
    const qty = toPositiveInt(rawQty, 1);

    setCart((previous) => {
      const existing = previous.find((item) => Number(item.id) === Number(product.id));
      if (existing) {
        return previous.map((item) =>
          Number(item.id) === Number(product.id)
            ? { ...item, qty: item.qty + qty, selected: true }
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
        qty,
        selected: true,
        categoryId: product?.categoryId ?? null,
      });

      return nextItem ? [...previous, nextItem] : previous;
    });
  }

  function setItemQuantity(productId, rawQty) {
    const qty = toPositiveInt(rawQty, 1);
    setCart((previous) =>
      previous.map((item) =>
        Number(item.id) === Number(productId) ? { ...item, qty } : item
      )
    );
  }

  function updateCartQty(productId, delta) {
    setCart((previous) =>
      previous.map((item) =>
        Number(item.id) === Number(productId)
          ? { ...item, qty: Math.max(1, Number(item.qty || 1) + Number(delta || 0)) }
          : item
      )
    );
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

  return {
    cart,
    subtotal,
    totalItems,
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
    toggleItemSelected,
    toggleSelectAllCartItems,
    updateCartQty,
  };
}
