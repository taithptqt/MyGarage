import test from "node:test";
import assert from "node:assert/strict";
import { parseInvoiceTaxSummary } from "./invoiceTaxSummary.js";

test("accepts a valid total and bounded invoice sample", () => {
  const summary = { totalTax: -5, taxedCount: 2, items: [{ id: 1 }] };
  assert.equal(parseInvoiceTaxSummary(summary), summary);
});

test("rejects absent or inconsistent totals instead of treating them as zero", () => {
  for (const value of [null, { totalTax: null, taxedCount: 0, items: [] },
    { totalTax: 0, taxedCount: 0, items: [{ id: 1 }] },
    { totalTax: 0, taxedCount: 31, items: Array(31).fill({}) }]) {
    assert.throws(() => parseInvoiceTaxSummary(value));
  }
});
