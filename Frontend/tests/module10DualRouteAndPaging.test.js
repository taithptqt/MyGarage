import test from "node:test";
import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

import {
  buildBoundedPaginationParams,
  getCustomerDashboard,
  getCustomerVehicles,
  createCustomerVehicle,
  updateCustomerVehicle,
  getVehicleHistory,
  getCustomerNotifications,
  markNotificationRead,
  markAllNotificationsRead,
  getVehicleLookups,
  getServiceOrderLinkContext,
  requestWithDualRoute as customerPortalDualRoute,
} from "../src/features/customerPortal/api/customerPortalApi.js";

import {
  getQuotationById,
  applyCustomerQuotationDecision,
  sendCustomerQuotationDecisionOtpByToken,
  applyCustomerQuotationDecisionByToken,
  requestWithDualRoute as quotationDualRoute,
} from "../src/features/customerPortal/api/customerPortalQuotationApi.js";

import {
  getCustomerConversations,
  getCustomerConversation,
  getCustomerMessengerConversation,
  getCustomerMessages,
  getCustomerConversationOrders,
  createCustomerConversation,
  sendCustomerMessage,
  uploadCustomerChatAttachments,
  markCustomerRead,
  getReceptionConversations,
  getReceptionConversation,
  getReceptionMessages,
  getReceptionConversationOrders,
  sendReceptionMessage,
  uploadReceptionChatAttachments,
  updateReceptionConversationStatus,
  assignReceptionConversation,
  markReceptionRead,
  requestWithDualRoute as chatDualRoute,
} from "../src/features/chat/api/chatApi.js";

// Helper for capturing calls and creating mock responses
function createMockRequester(behavior = {}) {
  const calls = [];
  const requester = async (config) => {
    calls.push(config);
    if (behavior.shouldFail404 && behavior.shouldFail404(config.url)) {
      const err = new Error(`Not Found: ${config.url}`);
      err.status = 404;
      err.response = { status: 404 };
      throw err;
    }
    if (behavior.shouldFail500 && behavior.shouldFail500(config.url)) {
      const err = new Error(`Server Error: ${config.url}`);
      err.status = 500;
      err.response = { status: 500 };
      throw err;
    }
    if (typeof behavior.handle === "function") {
      return behavior.handle(config);
    }
    return { ok: true, data: [] };
  };
  return { calls, requester };
}

test("buildBoundedPaginationParams enforces boundary limits and clamps defaults", () => {
  const normal = buildBoundedPaginationParams({ page: 2, pageSize: 30, branchId: 5 });
  assert.equal(normal.page, 2);
  assert.equal(normal.pageSize, 30);
  assert.equal(normal.branchId, 5);

  const defaults = buildBoundedPaginationParams();
  assert.equal(defaults.page, 1);
  assert.equal(defaults.pageSize, 25);

  const clampedLower = buildBoundedPaginationParams({ page: -5, pageSize: 0 });
  assert.equal(clampedLower.page, 1);
  assert.equal(clampedLower.pageSize, 25);

  const clampedUpper = buildBoundedPaginationParams({ page: 1, pageSize: 5000 }, 25, 200);
  assert.equal(clampedUpper.page, 1);
  assert.equal(clampedUpper.pageSize, 200);

  const customLimits = buildBoundedPaginationParams({ page: 3, pageSize: 250 }, 10, 50);
  assert.equal(customLimits.page, 3);
  assert.equal(customLimits.pageSize, 50);
});

test("customerPortalApi dual routes endpoints and falls back on 404", async () => {
  const { calls, requester } = createMockRequester({
    shouldFail404: (url) => url.startsWith("/api/v1/"),
    handle: (config) => ({ ok: true, url: config.url }),
  });

  // 1. getCustomerDashboard
  await getCustomerDashboard(requester);
  assert.equal(calls.length, 2);
  assert.equal(calls[0].url, "/api/v1/customers/me/dashboard");
  assert.equal(calls[1].url, "/api/customers/me/dashboard");

  // 2. getCustomerVehicles
  calls.length = 0;
  await getCustomerVehicles(requester);
  assert.equal(calls[0].url, "/api/v1/customers/me/vehicles");
  assert.equal(calls[1].url, "/api/customers/me/vehicles");

  // 3. createCustomerVehicle
  calls.length = 0;
  await createCustomerVehicle({ licensePlate: "51F-12345" }, requester);
  assert.equal(calls[0].url, "/api/v1/customers/me/vehicles");
  assert.equal(calls[1].url, "/api/customers/me/vehicles");
  assert.equal(calls[0].method, "POST");
  assert.equal(calls[0].data.licensePlate, "51F-12345");

  // 4. updateCustomerVehicle
  calls.length = 0;
  await updateCustomerVehicle(15, { color: "Đỏ" }, requester);
  assert.equal(calls[0].url, "/api/v1/customers/me/vehicles/15");
  assert.equal(calls[1].url, "/api/customers/me/vehicles/15");
  assert.equal(calls[0].method, "PUT");

  // 5. getVehicleHistory with bounded pagination
  calls.length = 0;
  await getVehicleHistory(15, { page: 2, pageSize: 10 }, requester);
  assert.equal(calls[0].url, "/api/v1/customers/me/vehicles/15/history");
  assert.equal(calls[1].url, "/api/customers/me/vehicles/15/history");
  assert.equal(calls[0].params.page, 2);
  assert.equal(calls[0].params.pageSize, 10);

  // 6. getCustomerNotifications with bounded pagination & unreadOnly
  calls.length = 0;
  await getCustomerNotifications({ unreadOnly: true, page: -1, pageSize: 999 }, requester);
  assert.equal(calls[0].url, "/api/v1/customers/me/notifications");
  assert.equal(calls[1].url, "/api/customers/me/notifications");
  assert.equal(calls[0].params.unreadOnly, true);
  assert.equal(calls[0].params.page, 1);
  assert.equal(calls[0].params.pageSize, 200);

  // 7. markNotificationRead
  calls.length = 0;
  await markNotificationRead(42, requester);
  assert.equal(calls[0].url, "/api/v1/customers/me/notifications/42/read");
  assert.equal(calls[1].url, "/api/customers/me/notifications/42/read");
  assert.equal(calls[0].method, "POST");

  // 8. markAllNotificationsRead
  calls.length = 0;
  await markAllNotificationsRead(requester);
  assert.equal(calls[0].url, "/api/v1/customers/me/notifications/read-all");
  assert.equal(calls[1].url, "/api/customers/me/notifications/read-all");
  assert.equal(calls[0].method, "POST");

  // 9. getVehicleLookups
  calls.length = 0;
  await getVehicleLookups(requester);
  assert.equal(calls[0].url, "/api/v1/customers/me/vehicle-lookups");
  assert.equal(calls[1].url, "/api/customers/me/vehicle-lookups");

  // 10. getServiceOrderLinkContext
  calls.length = 0;
  await getServiceOrderLinkContext(101, requester);
  assert.equal(calls[0].url, "/api/v1/customers/me/service-orders/101/link-context");
  assert.equal(calls[1].url, "/api/customers/me/service-orders/101/link-context");
});

test("customerPortalQuotationApi dual routes quotation endpoints and falls back on 404", async () => {
  const { calls, requester } = createMockRequester({
    shouldFail404: (url) => url.startsWith("/api/v1/"),
    handle: (config) => ({ ok: true, url: config.url }),
  });

  // 1. getQuotationById
  await getQuotationById(88, requester);
  assert.equal(calls.length, 2);
  assert.equal(calls[0].url, "/api/v1/quotations/88");
  assert.equal(calls[1].url, "/api/quotations/88");

  // 2. applyCustomerQuotationDecision
  calls.length = 0;
  await applyCustomerQuotationDecision(88, { decision: "APPROVE" }, requester);
  assert.equal(calls[0].url, "/api/v1/quotations/88/customer-decision");
  assert.equal(calls[1].url, "/api/quotations/88/customer-decision");
  assert.equal(calls[0].method, "POST");

  // 3. sendCustomerQuotationDecisionOtpByToken
  calls.length = 0;
  await sendCustomerQuotationDecisionOtpByToken("tok-xyz", { phone: "0901234567" }, requester);
  assert.equal(calls[0].url, "/api/v1/quotations/public/tok-xyz/otp/send");
  assert.equal(calls[1].url, "/api/quotations/public/tok-xyz/otp/send");
  assert.equal(calls[0].method, "POST");

  // 4. applyCustomerQuotationDecisionByToken
  calls.length = 0;
  await applyCustomerQuotationDecisionByToken("tok-xyz", { otp: "123456", decision: "APPROVE" }, requester);
  assert.equal(calls[0].url, "/api/v1/quotations/public/tok-xyz/decision");
  assert.equal(calls[1].url, "/api/quotations/public/tok-xyz/decision");
  assert.equal(calls[0].method, "POST");
});

test("chatApi dual routes customer and reception chat endpoints and falls back on 404", async () => {
  const { calls, requester } = createMockRequester({
    shouldFail404: (url) => url.startsWith("/api/v1/"),
    handle: (config) => ({ ok: true, url: config.url }),
  });

  // Customer chat endpoints
  await getCustomerConversations({ status: "OPEN" }, requester);
  assert.equal(calls[0].url, "/api/v1/chat/my-conversations?status=OPEN");
  assert.equal(calls[1].url, "/api/chat/my-conversations?status=OPEN");

  calls.length = 0;
  await getCustomerConversation(7, requester);
  assert.equal(calls[0].url, "/api/v1/chat/conversations/7");
  assert.equal(calls[1].url, "/api/chat/conversations/7");

  calls.length = 0;
  await getCustomerMessengerConversation(requester);
  assert.equal(calls[0].url, "/api/v1/chat/messenger/conversation");
  assert.equal(calls[1].url, "/api/chat/messenger/conversation");

  calls.length = 0;
  await getCustomerMessages(7, { take: 50 }, requester);
  assert.equal(calls[0].url, "/api/v1/chat/conversations/7/messages?take=50");
  assert.equal(calls[1].url, "/api/chat/conversations/7/messages?take=50");

  calls.length = 0;
  await getCustomerConversationOrders(7, requester);
  assert.equal(calls[0].url, "/api/v1/chat/conversations/7/orders");
  assert.equal(calls[1].url, "/api/chat/conversations/7/orders");

  calls.length = 0;
  await createCustomerConversation({ branchId: 1 }, requester);
  assert.equal(calls[0].url, "/api/v1/chat/conversations");
  assert.equal(calls[1].url, "/api/chat/conversations");
  assert.equal(calls[0].method, "POST");

  calls.length = 0;
  await sendCustomerMessage(7, { content: "Xin chào" }, requester);
  assert.equal(calls[0].url, "/api/v1/chat/conversations/7/messages");
  assert.equal(calls[1].url, "/api/chat/conversations/7/messages");
  assert.equal(calls[0].method, "POST");

  calls.length = 0;
  await uploadCustomerChatAttachments(7, [], requester);
  assert.equal(calls[0].url, "/api/v1/chat/conversations/7/attachments");
  assert.equal(calls[1].url, "/api/chat/conversations/7/attachments");
  assert.equal(calls[0].method, "POST");

  calls.length = 0;
  await markCustomerRead(7, { lastReadMessageId: 100 }, requester);
  assert.equal(calls[0].url, "/api/v1/chat/conversations/7/read-state");
  assert.equal(calls[1].url, "/api/chat/conversations/7/read-state");
  assert.equal(calls[0].method, "PUT");

  // Reception chat endpoints
  calls.length = 0;
  await getReceptionConversations({ branchId: 2 }, requester);
  assert.equal(calls[0].url, "/api/v1/reception/chat/conversations?branchId=2");
  assert.equal(calls[1].url, "/api/reception/chat/conversations?branchId=2");

  calls.length = 0;
  await getReceptionConversation(9, requester);
  assert.equal(calls[0].url, "/api/v1/reception/chat/conversations/9");
  assert.equal(calls[1].url, "/api/reception/chat/conversations/9");

  calls.length = 0;
  await getReceptionMessages(9, { take: 100 }, requester);
  assert.equal(calls[0].url, "/api/v1/reception/chat/conversations/9/messages?take=100");
  assert.equal(calls[1].url, "/api/reception/chat/conversations/9/messages?take=100");

  calls.length = 0;
  await getReceptionConversationOrders(9, requester);
  assert.equal(calls[0].url, "/api/v1/reception/chat/conversations/9/orders");
  assert.equal(calls[1].url, "/api/reception/chat/conversations/9/orders");

  calls.length = 0;
  await sendReceptionMessage(9, { content: "Dạ vâng" }, requester);
  assert.equal(calls[0].url, "/api/v1/reception/chat/conversations/9/messages");
  assert.equal(calls[1].url, "/api/reception/chat/conversations/9/messages");
  assert.equal(calls[0].method, "POST");

  calls.length = 0;
  await uploadReceptionChatAttachments(9, [], requester);
  assert.equal(calls[0].url, "/api/v1/reception/chat/conversations/9/attachments");
  assert.equal(calls[1].url, "/api/reception/chat/conversations/9/attachments");
  assert.equal(calls[0].method, "POST");

  calls.length = 0;
  await updateReceptionConversationStatus(9, { status: "CLOSED" }, requester);
  assert.equal(calls[0].url, "/api/v1/reception/chat/conversations/9/status");
  assert.equal(calls[1].url, "/api/reception/chat/conversations/9/status");
  assert.equal(calls[0].method, "PUT");

  calls.length = 0;
  await assignReceptionConversation(9, { staffId: 3 }, requester);
  assert.equal(calls[0].url, "/api/v1/reception/chat/conversations/9/assign");
  assert.equal(calls[1].url, "/api/reception/chat/conversations/9/assign");
  assert.equal(calls[0].method, "PUT");

  calls.length = 0;
  await markReceptionRead(9, { lastReadMessageId: 200 }, requester);
  assert.equal(calls[0].url, "/api/v1/reception/chat/conversations/9/read-state");
  assert.equal(calls[1].url, "/api/reception/chat/conversations/9/read-state");
  assert.equal(calls[0].method, "PUT");
});

test("Module 10 dual routing propagates 500 server error without fallback", async () => {
  const { requester } = createMockRequester({
    shouldFail500: (url) => url.startsWith("/api/v1/"),
  });

  await assert.rejects(
    async () => {
      await getCustomerDashboard(requester);
    },
    (err) => err.status === 500
  );

  await assert.rejects(
    async () => {
      await getQuotationById(12, requester);
    },
    (err) => err.status === 500
  );

  await assert.rejects(
    async () => {
      await getCustomerConversation(5, requester);
    },
    (err) => err.status === 500
  );
});

test("UTF-8 strictness & Vietnamese diacritic repairs in Module 10 files", () => {
  const __dirname = path.dirname(fileURLToPath(import.meta.url));
  const filesToCheck = [
    "../src/features/customerPortal/api/customerPortalApi.js",
    "../src/features/customerPortal/api/customerPortalQuotationApi.js",
    "../src/features/chat/api/chatApi.js",
    "../src/features/chat/pages/CustomerChatPage.jsx",
    "../src/features/chat/pages/ReceptionChatPage.jsx",
    "../src/features/chat/components/CustomerMessengerWidget.jsx",
    "../src/features/chat/utils/chatOrderCard.js",
    "../src/features/customerPortal/pages/GarageDashboardPage.jsx",
    "../src/features/customerPortal/pages/MyVehiclesPage.jsx",
    "../src/features/customerPortal/pages/VehicleDetailPage.jsx",
    "../src/features/customerPortal/pages/MyNotificationsPage.jsx",
    "../src/features/customerPortal/pages/QuotationDetailPage.jsx",
    "../src/features/customerPortal/pages/PublicQuotationAccessPage.jsx",
  ];

  for (const relPath of filesToCheck) {
    const fullPath = path.resolve(__dirname, relPath);
    const content = fs.readFileSync(fullPath, "utf8");

    // 0 replacement characters
    assert.ok(!content.includes("\uFFFD"), `File ${relPath} contains \\uFFFD replacement char`);

    // 0 mojibake patterns
    assert.ok(!content.includes("ðŸ‘¤"), `File ${relPath} contains mojibake ðŸ‘¤`);
    assert.ok(!content.includes("Kh?ng"), `File ${relPath} contains corrupted Kh?ng`);
    assert.ok(!content.includes("T?t c?"), `File ${relPath} contains corrupted T?t c?`);
    assert.ok(!content.includes("C?p nh?t"), `File ${relPath} contains corrupted C?p nh?t`);
  }

  // Verify Vietnamese diacritic fix in CustomerChatPage.jsx:12
  const customerChatFile = path.resolve(__dirname, "../src/features/chat/pages/CustomerChatPage.jsx");
  const customerChatContent = fs.readFileSync(customerChatFile, "utf8");
  assert.ok(customerChatContent.includes('label: "Tất cả"'), 'CustomerChatPage.jsx must contain label: "Tất cả"');
  assert.ok(!customerChatContent.includes('label: "Tat ca"'), 'CustomerChatPage.jsx must not contain label: "Tat ca"');

  // Verify currency symbol repair in chatOrderCard.js:127
  const chatOrderFile = path.resolve(__dirname, "../src/features/chat/utils/chatOrderCard.js");
  const chatOrderContent = fs.readFileSync(chatOrderFile, "utf8");
  assert.ok(
    chatOrderContent.includes("return `${moneyFormatter.format(Math.round(amount))}₫`;"),
    "chatOrderCard.js must use ₫ symbol"
  );
  assert.ok(
    !chatOrderContent.includes("return `${moneyFormatter.format(Math.round(amount))}d`;"),
    "chatOrderCard.js must not use ASCII 'd'"
  );
});

test("Module 10 customer portal and chat routes are mounted in application router", () => {
  const __dirname = path.dirname(fileURLToPath(import.meta.url));
  const appFile = path.resolve(__dirname, "../src/app/App.jsx");
  const appContent = fs.readFileSync(appFile, "utf8");

  assert.ok(appContent.includes('path="/my-garage-dashboard"'), "App.jsx mounts /my-garage-dashboard");
  assert.ok(appContent.includes('path="/my-vehicles"'), "App.jsx mounts /my-vehicles");
  assert.ok(appContent.includes('path="/my-vehicles/:vehicleId"'), "App.jsx mounts /my-vehicles/:vehicleId");
  assert.ok(appContent.includes('path="/my-notifications"'), "App.jsx mounts /my-notifications");
  assert.ok(appContent.includes('path="/my-quotations/:quotationId"'), "App.jsx mounts /my-quotations/:quotationId");
  assert.ok(appContent.includes('path="/quote-access/:token"'), "App.jsx mounts /quote-access/:token");
  assert.ok(appContent.includes('path="reception/chat"'), "App.jsx mounts reception/chat");
  assert.ok(appContent.includes("<CustomerMessengerWidget />"), "App.jsx mounts CustomerMessengerWidget");
});
