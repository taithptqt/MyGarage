import test from "node:test";
import assert from "node:assert/strict";
import { collectPages } from "./collectPages.js";

const response = (page, totalCount = 3, pageSize = 1, items = [page]) => {
  const totalPages = totalCount === 0 ? 0 : Math.ceil(totalCount / pageSize);
  return {
    data: {
      items,
      totalCount,
      total: totalCount,
      page,
      pageSize,
      totalPages,
      hasPreviousPage: page > 1,
      hasNextPage: page < totalPages,
    },
  };
};

test("legacy requests collect all pages and preserve filters", async () => {
  const seen = [];
  const data = await collectPages({ url: "/api/v1/items", params: { status: "OPEN" } }, response(1), async (config) => {
    seen.push(config.params);
    return response(config.params.page);
  });
  assert.deepEqual(data, [1, 2, 3]);
  assert.deepEqual(seen, [
    { status: "OPEN", page: 2, pageSize: 1 },
    { status: "OPEN", page: 3, pageSize: 1 },
  ]);
});

test("explicit pagination does not collect other pages and returns PagedEnvelope", async () => {
  const expectedEnvelope = {
    items: [1],
    totalCount: 3,
    total: 3,
    page: 1,
    pageSize: 1,
    totalPages: 3,
    hasPreviousPage: false,
    hasNextPage: true,
  };
  const data = await collectPages({ url: "/api/v1/items?page=1" }, response(1), () => {
    throw new Error("unexpected request");
  });
  assert.deepEqual(data, expectedEnvelope);
});

test("omitted pagination parameters still collect all pages", async () => {
  const data = await collectPages(
    { url: "/api/v1/items", params: { page: undefined, pageSize: null, status: "OPEN" } },
    response(1),
    async ({ params }) => {
      assert.equal(params.status, "OPEN");
      return response(params.page);
    }
  );
  assert.deepEqual(data, [1, 2, 3]);
});

test("URLSearchParams preserves repeated filters and detects explicit pagination", async () => {
  const params = new URLSearchParams([
    ["status", "OPEN"],
    ["status", "PENDING"],
  ]);
  const data = await collectPages({ url: "/api/v1/items", params }, response(1), async ({ params: next }) => {
    assert.deepEqual(next.getAll("status"), ["OPEN", "PENDING"]);
    return response(Number(next.get("page")));
  });
  assert.deepEqual(data, [1, 2, 3]);
  params.set("page", "1");
  assert.deepEqual(
    await collectPages({ url: "/api/v1/items", params }, response(1), () => {
      throw new Error("unexpected request");
    }),
    response(1).data
  );
});

test("inconsistent pages fail instead of silently returning partial data", async () => {
  await assert.rejects(
    () => collectPages({ url: "/api/v1/items" }, response(1), async () => response(2, 4)),
    /Invalid or changed paginated response\./
  );
});

test("rejects a short middle page even if later rows make the total appear complete", async () => {
  const first = response(1, 5, 2, [1, 2]);
  await assert.rejects(
    () =>
      collectPages({ url: "/api/v1/items" }, first, async ({ params }) =>
        response(params.page, 5, 2, params.page === 2 ? [3] : [4, 5])
      ),
    /Invalid or changed paginated response\./
  );
});

test("inconsistent or incomplete first-page metadata fails rather than returning partial data", async () => {
  const bad = response(1, 1);
  bad.data.totalCount = 2;
  bad.data.total = 2;
  await assert.rejects(
    () =>
      collectPages({ url: "/api/v1/items" }, bad, async () => {
        throw new Error("unexpected");
      }),
    /Invalid or changed paginated response\./
  );
  const missing = response(1, 2);
  delete missing.data.totalCount;
  delete missing.data.total;
  await assert.rejects(
    () =>
      collectPages({ url: "/api/v1/items" }, missing, async () => {
        throw new Error("unexpected");
      }),
    /Invalid or changed paginated response\./
  );
});

test("non-paginated arrays and empty paginated responses remain supported", async () => {
  assert.deepEqual(await collectPages({ url: "/api/v1/items" }, { data: [1] }, null), [1]);
  assert.deepEqual(await collectPages({ url: "/api/v1/items" }, response(1, 0, 50, []), null), []);
});
