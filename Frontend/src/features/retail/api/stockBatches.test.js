import test from "node:test";
import assert from "node:assert/strict";
import { loadStockBatches } from "./stockBatches.js";

test("stock requests are bounded, deduplicated, and combined", async () => {
  const sizes = [];
  const ids = Array.from({ length: 121 }, (_, index) => index + 1);
  const result = await loadStockBatches([...ids, 1, -1], async (batch) => {
    sizes.push(batch.length);
    return { ok: true, data: batch.map((productId) => ({ productId })) };
  });
  assert.deepEqual(sizes, [50, 50, 21]);
  assert.deepEqual(result.data.map((item) => item.productId), ids);
});

test("batch failure returns the error instead of incomplete stock", async () => {
  const result = await loadStockBatches(Array.from({ length: 51 }, (_, index) => index + 1), async (batch) =>
    batch[0] === 51 ? { ok: false, status: 503, data: { message: "unavailable" } } : { ok: true, data: [] });
  assert.equal(result.ok, false);
  assert.equal(result.status, 503);
});
