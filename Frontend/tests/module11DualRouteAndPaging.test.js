import test from "node:test";
import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

import {
  buildBoundedPaginationParams as buildChatbotPaginationParams,
  createChatbotSession,
  orchestrateChatbot,
  getChatbotSession,
  saveChatbotMessages,
  uploadChatbotImage,
  requestWithDualRoute as chatbotDualRoute,
} from "../src/features/chatbot/api/chatbotApi.js";

import {
  buildBoundedPaginationParams as buildAiAdminPaginationParams,
  getAiForecasts,
  getAiKnowledgePage,
  createAiKnowledgeEntry,
  updateAiKnowledgeEntry,
  deleteAiKnowledgeEntry,
  getAiModelConfigPage,
  createAiModelConfig,
  updateAiModelConfig,
  getAiSmartDispatchings,
  recommendBayForOrder,
  requestWithDualRoute as aiAdminDualRoute,
} from "../src/features/admin/api/aiAdminApi.js";

// Helper for capturing calls and creating mock responses
function createMockRequester(behavior = {}) {
  const calls = [];
  const requester = async (config) => {
    calls.push(config);
    if (behavior.shouldFail404 && behavior.shouldFail404(config.url, calls.length)) {
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

test("buildBoundedPaginationParams in chatbotApi and aiAdminApi enforces boundary limits and clamps defaults", () => {
  for (const buildFn of [buildChatbotPaginationParams, buildAiAdminPaginationParams]) {
    const normal = buildFn({ page: 2, pageSize: 30, branchId: 5 });
    assert.equal(normal.page, 2);
    assert.equal(normal.pageSize, 30);
    assert.equal(normal.branchId, 5);

    const defaults = buildFn();
    assert.equal(defaults.page, 1);
    assert.equal(defaults.pageSize, 20);

    const clampedLower = buildFn({ page: -5, pageSize: 0 });
    assert.equal(clampedLower.page, 1);
    assert.equal(clampedLower.pageSize, 20);

    const clampedUpper = buildFn({ page: 1, pageSize: 5000 }, 20, 100);
    assert.equal(clampedUpper.page, 1);
    assert.equal(clampedUpper.pageSize, 100);

    const customLimits = buildFn({ page: 3, pageSize: 250 }, 15, 50);
    assert.equal(customLimits.page, 3);
    assert.equal(customLimits.pageSize, 50);
  }
});

test("chatbotApi dual routes endpoints to /api/v1/chatbot/... and falls back to /api/chatbot/... on 404", async () => {
  const { calls, requester } = createMockRequester({
    shouldFail404: (url, callCount) => callCount === 1 && url.startsWith("/api/v1/"),
    handle: (config) => ({ ok: true, url: config.url }),
  });

  // 1. createChatbotSession
  await createChatbotSession({ channel: "WEB" }, requester);
  assert.equal(calls.length, 2);
  assert.equal(calls[0].url, "/api/v1/chatbot/sessions");
  assert.equal(calls[1].url, "/api/v1/chatbot/sessions");
  assert.equal(calls[0].method, "POST");
  assert.equal(calls[0].data.channel, "WEB");

  // 2. orchestrateChatbot
  calls.length = 0;
  await orchestrateChatbot({ sessionId: 12, userMessage: "Bảo dưỡng định kỳ" }, requester);
  assert.equal(calls.length, 2);
  assert.equal(calls[0].url, "/api/v1/chatbot/orchestrations");
  assert.equal(calls[1].url, "/api/v1/chatbot/orchestrations");
  assert.equal(calls[0].method, "POST");
  assert.equal(calls[0].data.sessionId, 12);

  // 3. getChatbotSession
  calls.length = 0;
  await getChatbotSession(12, requester);
  assert.equal(calls.length, 2);
  assert.equal(calls[0].url, "/api/v1/chatbot/sessions/12");
  assert.equal(calls[1].url, "/api/v1/chatbot/sessions/12");
  assert.equal(calls[0].method, "GET");

  // 4. saveChatbotMessages
  calls.length = 0;
  await saveChatbotMessages({ sessionId: 12, content: "Xin chào" }, requester);
  assert.equal(calls.length, 2);
  assert.equal(calls[0].url, "/api/v1/chatbot/messages");
  assert.equal(calls[1].url, "/api/v1/chatbot/messages");
  assert.equal(calls[0].method, "POST");
  assert.equal(calls[0].data.content, "Xin chào");

  // 5. uploadChatbotImage
  calls.length = 0;
  await uploadChatbotImage({ sessionId: 12, prompt: "Kiểm tra vết xước" }, requester);
  assert.equal(calls.length, 2);
  assert.equal(calls[0].url, "/api/v1/chatbot/images");
  assert.equal(calls[1].url, "/api/v1/chatbot/images");
  assert.equal(calls[0].method, "POST");
  assert.equal(calls[0].headers["Content-Type"], "multipart/form-data");
});

test("aiAdminApi dual routes all AI admin endpoints and falls back on 404 with bounded pagination", async () => {
  const { calls, requester } = createMockRequester({
    shouldFail404: (url, callCount) => callCount === 1 && url.startsWith("/api/v1/"),
    handle: (config) => ({ ok: true, url: config.url }),
  });

  // 1. getAiForecasts
  await getAiForecasts({ branchId: 1, page: -2, pageSize: 500 }, requester);
  assert.equal(calls.length, 2);
  assert.equal(calls[0].url, "/api/v1/ai-forecasts");
  assert.equal(calls[1].url, "/api/v1/ai-forecasts");
  assert.equal(calls[0].method, "GET");
  assert.equal(calls[0].params.branchId, 1);
  assert.equal(calls[0].params.page, 1);
  assert.equal(calls[0].params.pageSize, 100);

  // 2. getAiKnowledgePage
  calls.length = 0;
  await getAiKnowledgePage({ search: "phanh", page: 2, pageSize: 25 }, requester);
  assert.equal(calls.length, 2);
  assert.equal(calls[0].url, "/api/v1/ai-knowledge-bases/page");
  assert.equal(calls[1].url, "/api/v1/ai-knowledge-bases/page");
  assert.equal(calls[0].method, "GET");
  assert.equal(calls[0].params.search, "phanh");
  assert.equal(calls[0].params.page, 2);
  assert.equal(calls[0].params.pageSize, 25);

  // 3. createAiKnowledgeEntry
  calls.length = 0;
  await createAiKnowledgeEntry({ title: "Quy trình thay dầu", category: "MAINTENANCE" }, requester);
  assert.equal(calls.length, 2);
  assert.equal(calls[0].url, "/api/v1/ai-knowledge-bases");
  assert.equal(calls[1].url, "/api/v1/ai-knowledge-bases");
  assert.equal(calls[0].method, "POST");
  assert.equal(calls[0].data.title, "Quy trình thay dầu");

  // 4. updateAiKnowledgeEntry
  calls.length = 0;
  await updateAiKnowledgeEntry(33, { title: "Quy trình thay dầu máy" }, requester);
  assert.equal(calls.length, 2);
  assert.equal(calls[0].url, "/api/v1/ai-knowledge-bases/33");
  assert.equal(calls[1].url, "/api/v1/ai-knowledge-bases/33");
  assert.equal(calls[0].method, "PUT");

  // 5. deleteAiKnowledgeEntry
  calls.length = 0;
  await deleteAiKnowledgeEntry(33, requester);
  assert.equal(calls.length, 2);
  assert.equal(calls[0].url, "/api/v1/ai-knowledge-bases/33");
  assert.equal(calls[1].url, "/api/v1/ai-knowledge-bases/33");
  assert.equal(calls[0].method, "DELETE");

  // 6. getAiModelConfigPage
  calls.length = 0;
  await getAiModelConfigPage({}, requester);
  assert.equal(calls.length, 2);
  assert.equal(calls[0].url, "/api/v1/ai-model-configs/page");
  assert.equal(calls[1].url, "/api/v1/ai-model-configs/page");
  assert.equal(calls[0].params.page, 1);
  assert.equal(calls[0].params.pageSize, 20);

  // 7. createAiModelConfig
  calls.length = 0;
  await createAiModelConfig({ modelName: "gemini-2.5-flash", temperature: 0.2 }, requester);
  assert.equal(calls.length, 2);
  assert.equal(calls[0].url, "/api/v1/ai-model-configs");
  assert.equal(calls[1].url, "/api/v1/ai-model-configs");
  assert.equal(calls[0].method, "POST");

  // 8. updateAiModelConfig
  calls.length = 0;
  await updateAiModelConfig(9, { temperature: 0.1 }, requester);
  assert.equal(calls.length, 2);
  assert.equal(calls[0].url, "/api/v1/ai-model-configs/9");
  assert.equal(calls[1].url, "/api/v1/ai-model-configs/9");
  assert.equal(calls[0].method, "PUT");

  // 9. getAiSmartDispatchings
  calls.length = 0;
  await getAiSmartDispatchings({ branchId: 2, pageSize: 300 }, requester);
  assert.equal(calls.length, 2);
  assert.equal(calls[0].url, "/api/v1/ai-smart-dispatchings");
  assert.equal(calls[1].url, "/api/v1/ai-smart-dispatchings");
  assert.equal(calls[0].params.branchId, 2);
  assert.equal(calls[0].params.page, 1);
  assert.equal(calls[0].params.pageSize, 100);

  // 10. recommendBayForOrder
  calls.length = 0;
  await recommendBayForOrder(501, requester);
  assert.equal(calls.length, 2);
  assert.equal(calls[0].url, "/api/v1/ai-smart-dispatchings/recommend-bay/501");
  assert.equal(calls[1].url, "/api/v1/ai-smart-dispatchings/recommend-bay/501");
  assert.equal(calls[0].method, "GET");
});

test("Module 11 dual routing propagates 500 server error without fallback", async () => {
  const { calls, requester } = createMockRequester({
    shouldFail500: (url) => url.startsWith("/api/v1/"),
  });

  await assert.rejects(
    async () => {
      await orchestrateChatbot({ sessionId: 1, userMessage: "Test" }, requester);
    },
    (err) => err.status === 500
  );
  assert.equal(calls.length, 1);

  calls.length = 0;
  await assert.rejects(
    async () => {
      await getAiForecasts({ branchId: 1 }, requester);
    },
    (err) => err.status === 500
  );
  assert.equal(calls.length, 1);

  calls.length = 0;
  await assert.rejects(
    async () => {
      await recommendBayForOrder(99, requester);
    },
    (err) => err.status === 500
  );
  assert.equal(calls.length, 1);
});

test("UTF-8 strictness & Vietnamese diacritics on Module 11 files", () => {
  const __dirname = path.dirname(fileURLToPath(import.meta.url));
  const filesToCheck = [
    "../src/features/chatbot/api/chatbotApi.js",
    "../src/features/admin/api/aiAdminApi.js",
    "../src/features/chatbot/components/CustomerChatbotWidget.jsx",
    "../src/features/admin/pages/DispatchCenterPage.jsx",
    "../src/features/home/pages/HomePage.jsx",
    "../src/features/news/pages/NewsJournalPage.jsx",
    "../src/features/contact/pages/ContactPage.jsx",
  ];

  for (const relPath of filesToCheck) {
    const fullPath = path.resolve(__dirname, relPath);
    const content = fs.readFileSync(fullPath, "utf8");

    // 0 replacement characters
    assert.ok(!content.includes("\uFFFD"), `File ${relPath} contains \\uFFFD replacement char`);

    // 0 mojibake patterns
    assert.ok(!content.includes("ðŸ"), `File ${relPath} contains mojibake ðŸ`);
    assert.ok(!content.includes("\u00c3\u0192"), `File ${relPath} contains mojibake \u00c3\u0192`);
    assert.ok(!content.includes("Kh?ng"), `File ${relPath} contains corrupted Kh?ng`);
    assert.ok(!content.includes("T?t c?"), `File ${relPath} contains corrupted T?t c?`);
    assert.ok(!content.includes("C?p nh?t"), `File ${relPath} contains corrupted C?p nh?t`);
  }

  // Verify Vietnamese text in CustomerChatbotWidget.jsx
  const chatbotWidgetPath = path.resolve(__dirname, "../src/features/chatbot/components/CustomerChatbotWidget.jsx");
  const chatbotWidgetContent = fs.readFileSync(chatbotWidgetPath, "utf8");
  assert.ok(chatbotWidgetContent.includes("Trợ lý AI MyGarage"), "CustomerChatbotWidget.jsx must contain 'Trợ lý AI MyGarage'");
  assert.ok(chatbotWidgetContent.includes('aria-label="Đóng gợi ý chatbot"'), "CustomerChatbotWidget.jsx must contain proper Vietnamese aria-label");

  // Verify Vietnamese text in DispatchCenterPage.jsx
  const dispatchPath = path.resolve(__dirname, "../src/features/admin/pages/DispatchCenterPage.jsx");
  const dispatchContent = fs.readFileSync(dispatchPath, "utf8");
  assert.ok(dispatchContent.includes("Giám sát khoang theo thời gian thực"), "DispatchCenterPage.jsx must preserve Vietnamese heading");
});

test("Module 11 routes and widgets are mounted in App.jsx", () => {
  const __dirname = path.dirname(fileURLToPath(import.meta.url));
  const appFile = path.resolve(__dirname, "../src/app/App.jsx");
  const appContent = fs.readFileSync(appFile, "utf8");

  assert.ok(appContent.includes('path="/home"'), "App.jsx mounts /home");
  assert.ok(appContent.includes("<HomePage />"), "App.jsx mounts HomePage");
  assert.ok(appContent.includes('path="/news"'), "App.jsx mounts /news");
  assert.ok(appContent.includes("<NewsJournalPage />"), "App.jsx mounts NewsJournalPage");
  assert.ok(appContent.includes('path="/contact"'), "App.jsx mounts /contact");
  assert.ok(appContent.includes("<ContactPage />"), "App.jsx mounts ContactPage");
  assert.ok(appContent.includes('path="/dispatch-center"'), "App.jsx mounts /dispatch-center");
  assert.ok(appContent.includes("<DispatchCenterPage />"), "App.jsx mounts DispatchCenterPage");
  assert.ok(appContent.includes("<CustomerChatbotWidget />"), "App.jsx mounts CustomerChatbotWidget");
});
