import test from "node:test";
import assert from "node:assert/strict";
import { collectPages } from "../../../app/collectPages.js";
import { getReceptionCustomerVehicleReferences, getReceptionOrderReferences } from "./customerApi.js";

test("reception references collect every page and scope customers to the selected branch", async () => {
  const calls = [];
  const requester = async (config) => {
    calls.push(config);
    const first = { data: { items: [{ id: 1 }], page: 1, pageSize: 1, totalCount: 2, totalPages: 2 } };
    return collectPages(config, first, async (next) => {
      calls.push(next);
      return { data: { items: [{ id: 2 }], page: 2, pageSize: 1, totalCount: 2, totalPages: 2 } };
    });
  };

  const [customers, vehicles, links] = await getReceptionCustomerVehicleReferences(7, requester);
  assert.deepEqual(customers, [{ id: 1 }, { id: 2 }]);
  assert.equal(vehicles.length, 2);
  assert.equal(links.length, 2);
  assert.equal(calls.find((call) => call.url === "/api/v1/customers").params.branchId, 7);
});

test("order edit keeps customer lookup scoped and tolerates an unavailable vehicle list", async () => {
  const calls = [];
  const [customers, vehicles] = await getReceptionOrderReferences(3, async (config) => {
    calls.push(config);
    if (config.url === "/api/v1/vehicles") throw new Error("unavailable");
    return [{ id: 42 }];
  });
  assert.deepEqual(customers, [{ id: 42 }]);
  assert.deepEqual(vehicles, []);
  assert.equal(calls[0].params.branchId, 3);
});
