import test from "node:test";
import assert from "node:assert/strict";
import { parsePaymentHistoryPage } from "./paymentHistoryPage.js";

test("parses a bounded history response and its server count", () => {
  assert.deepEqual(
    parsePaymentHistoryPage({ ok: true, data: { items: [{ id: 7 }], totalCount: 41, total: 41 } }),
    { items: [{ id: 7 }], total: 41 }
  );
  assert.deepEqual(parsePaymentHistoryPage({ ok: true, data: [{ id: 7 }], headers: { "x-total-count": "41" } }),
    { items: [{ id: 7 }], total: 41 });
});

test("rejects missing or malformed counts and failed responses", () => {
  assert.throws(() => parsePaymentHistoryPage({ ok: true, data: [], headers: {} }));
  assert.throws(() => parsePaymentHistoryPage({ ok: true, data: [], headers: { "x-total-count": "-1" } }));
  assert.throws(() => parsePaymentHistoryPage({ ok: true, data: [], headers: { "x-total-count": "" } }));
  assert.throws(() => parsePaymentHistoryPage({ ok: true, data: {}, headers: { "x-total-count": "0" } }));
  assert.throws(() => parsePaymentHistoryPage({ ok: true, data: [{ id: 1 }], headers: { "x-total-count": "0" } }));
  assert.throws(() => parsePaymentHistoryPage({ ok: false, data: { message: "denied" } }), /denied/);
});
