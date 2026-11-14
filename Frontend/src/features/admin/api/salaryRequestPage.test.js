import test from "node:test";
import assert from "node:assert/strict";
import { parseSalaryRequestPage } from "./salaryRequestPage.js";

test("reads filtered totals independently of the visible page from JSON PagedEnvelope or header fallback", () => {
  const fromEnvelope = parseSalaryRequestPage({
    ok: true,
    data: {
      items: [{ id: 3 }],
      totalCount: 12,
      total: 12,
      pendingCount: 9,
      approvedCount: 2,
      page: 1,
      pageSize: 10,
      totalPages: 2,
    },
  });
  assert.deepEqual(fromEnvelope, { items: [{ id: 3 }], total: 12, pending: 9, approved: 2 });

  const fromHeaders = parseSalaryRequestPage({
    ok: true,
    data: [{ id: 3 }],
    headers: {
      "x-total-count": "12",
      "x-salary-pending-count": "9",
      "x-salary-approved-count": "2",
    },
  });
  assert.deepEqual(fromHeaders, { items: [{ id: 3 }], total: 12, pending: 9, approved: 2 });
});

test("rejects missing, inconsistent and unauthorized results", () => {
  assert.throws(() => parseSalaryRequestPage({ ok: true, data: { items: [], totalCount: 0 } }));
  assert.throws(() => parseSalaryRequestPage({ ok: true, data: [], headers: { "x-total-count": "0" } }));
  assert.throws(() =>
    parseSalaryRequestPage({
      ok: true,
      data: { items: [], totalCount: 1, total: 1, pendingCount: 1, approvedCount: 1 },
    })
  );
  assert.throws(() =>
    parseSalaryRequestPage({
      ok: true,
      data: [],
      headers: {
        "x-total-count": "1",
        "x-salary-pending-count": "1",
        "x-salary-approved-count": "1",
      },
    })
  );
  assert.throws(() => parseSalaryRequestPage({ ok: false, status: 403 }), (error) => error.response.status === 403);
});
