import { useEffect, useMemo, useState } from "react";
import { RETAIL_CART_STORAGE_KEY, toPositiveInt } from "@features/retail/utils/retailFormat";

function readStoredCart() {
  try {
    const raw = localStorage.getItem(RETAIL_CART_STORAGE_KEY);
    if (!raw) return [];

    const parsed = JSON.parse(raw);
    return Array.isArray(parsed) ? parsed : [];
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

  function addToCart(product, rawQty = 1) {
    const qty = toPositiveInt(rawQty, 1);

    setCart((previous) => {
      const existing = previous.find((item) => Number(item.id) === Number(product.id));
      if (existing) {
        return previous.map((item) =>
          Number(item.id) === Number(product.id)
            ? { ...item, qty: item.qty + qty }
            : item
        );
      }

      return [
        ...previous,
        {
          id: product.id,
          name: product.name,
          sku: product.sku || null,
          retailPrice: Number(product.retailPrice || 0),
          qty,
        },
      ];
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

  return {
    cart,
    subtotal,
    totalItems,
    addToCart,
    clearCart,
    removeFromCart,
    setItemQuantity,
    updateCartQty,
  };
}
