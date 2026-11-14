import test from "node:test";
import assert from "node:assert/strict";
import { parseSalaryProfilePage } from "./salaryProfilePage.js";

test("reads the server count and the current page from JSON PagedEnvelope or header fallback", () => {
  assert.deepEqual(
    parseSalaryProfilePage({
      ok: true,
      data: { items: [{ id: 3 }], totalCount: 21, total: 21, page: 1, pageSize: 10, totalPages: 3 },
    }),
    { items: [{ id: 3 }], total: 21 }
  );
  assert.deepEqual(
    parseSalaryProfilePage({ ok: true, data: [{ id: 3 }], headers: { "x-total-count": "21" } }),
    { items: [{ id: 3 }], total: 21 }
  );
});

test("does not silently treat missing counts or authorization errors as empty data", () => {
  assert.throws(() => parseSalaryProfilePage({ ok: true, data: [], headers: {} }));
  assert.throws(() => parseSalaryProfilePage({ ok: true, data: { items: [] } }));
  assert.throws(() => parseSalaryProfilePage({ ok: true, data: [], headers: { "x-total-count": "" } }));
  assert.throws(() => parseSalaryProfilePage({ ok: true, data: { items: [{ id: 1 }], totalCount: 0, total: 0 } }));
  assert.throws(() => parseSalaryProfilePage({ ok: true, data: [{ id: 1 }], headers: { "x-total-count": "0" } }));
  assert.throws(
    () => parseSalaryProfilePage({ ok: false, status: 403, data: {} }),
    (error) => error.response.status === 403
  );
});
