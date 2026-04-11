# Chatbot Orchestration

## Purpose

`ChatbotOrchestrator` coordinates deterministic intent flow, guardrail checks, retrieval, LLM gateway calls, optional tool execution, and persistence of conversation artifacts.

## Related docs

- `ChatbotScenarioMatrix.md` defines scenario coverage strategy, missing-field permutations, and recommended regression categories.

## Runtime orchestration overview

Execution order per turn:

1. Resolve owned session (`SessionId` or `SessionToken`) for authenticated customer.
2. Validate guardrails (`IGuardrailValidator`) and detect intent (`IIntentDetector`).
3. Attempt handoff (`IChatbotHandoffManager`) when needed.
4. Run deterministic self-service/flow handlers by intent:
   - self-service lookup for `INQUIRY` and `SERVICE_ORDER`
   - consultation flow
   - booking flow
   - retail purchase flow
   - service-order draft flow
5. If not resolved deterministically, build LLM prompt and call `IChatbotLlmGateway`.
6. Validate model-suggested tool calls against allowlisted registry.
7. Execute tools server-side through `IChatbotToolDefinitionRegistry`.
8. Persist messages to `ChatbotMessages` in sequence:
   - user message
   - assistant tool call message(s) (if any)
   - assistant tool result message(s) (if any)
   - assistant final response
9. Return `OrchestratedChatbotReplyDto` with state updates.

## Data and telemetry

- `ChatbotMessages` stores regular turns and tool call/result artifacts.
- `ChatbotActionLogs` stores LLM gateway telemetry and workflow action outcomes.
- LLM telemetry includes provider, model, latency, token counts, and failure codes.

## Intent state model

`IntentState` is JSON and contains orchestrator-level flow flags:

- `flow.intent`
- `flow.step`
- `flow.revision`
- `flow.updated_at`
- `flags.is_low_confidence`
- `flags.needs_handoff`
- `flags.violation_code`

Workflow services also store their own state blocks in `IntentState`:

- `booking`
- `retail_purchase`
- `service_order_draft`

## Booking workflow state machine

Service: `ChatbotBookingWorkflowService`

States:

- `IDENTIFY_OR_CREATE_VEHICLE`
- `SELECT_SERVICES`
- `SELECT_BRANCH`
- `COMPUTE_AVAILABLE_SLOTS`
- `FINALIZE_BOOKING`
- `COMPLETED`

Transitions:

- vehicle resolved/created -> `SELECT_SERVICES`
- services selected -> `SELECT_BRANCH`
- branch selected -> `COMPUTE_AVAILABLE_SLOTS`
- slot selected -> `FINALIZE_BOOKING`
- confirmation accepted and transaction succeeds -> `COMPLETED`
- slot conflict/race condition -> back to `COMPUTE_AVAILABLE_SLOTS`

## Retail purchase workflow state machine

Service: `ChatbotRetailPurchaseWorkflowService`

States:

- `SEARCH_PRODUCTS`
- `REVIEW_CART`
- `COMPLETED`

Transitions:

- valid product search/cart selection -> `REVIEW_CART`
- cart update/search override while reviewing -> `SEARCH_PRODUCTS`
- confirmation accepted and draft order succeeds -> `COMPLETED`
- create failure -> `SEARCH_PRODUCTS`

## Service-order draft workflow state machine

Service: `ChatbotServiceOrderDraftWorkflowService`

States:

- `COLLECT_ORDER_INPUTS`
- `REVIEW_ORDER_ESTIMATE`
- `COMPLETED`

Transitions:

- valid vehicle/branch/services with resolvable pricing -> `REVIEW_ORDER_ESTIMATE`
- input updates while reviewing -> `COLLECT_ORDER_INPUTS`
- confirmation accepted and draft creation succeeds -> `COMPLETED`
- create failure/invalidated plan -> `COLLECT_ORDER_INPUTS`

## Tool-routing security model

- Model can only suggest tool calls.
- Backend validates suggested tool names against allowlist.
- Backend executes tools with authenticated server context (`customerId`, `branchId`, `sessionId`).
- Backend persists tool call and tool result messages after execution.
- Tool arguments are treated as untrusted input.

## Assumptions

- Public chatbot controller exposes `POST /api/chatbot/orchestrate` for customer-facing orchestration.
- Booking/retail/service-order workflow states documented above are based on current step enums in service implementations.
- Provider-specific policy (for example hard circuit breaker) is expected at gateway/client infra layer if needed.

## Extension points

- Add new intent handlers in `ChatbotOrchestrator` before LLM fallback.
- Add new tools by extending `ChatbotToolDefinitionRegistry` and allowlist.
- Add new provider clients by implementing `IChatbotLlmProviderClient`.
- Extend prompt strategy via `IChatbotLlmPromptBuilder` and `IPromptBuilder`.
- Add richer observability/export sinks by extending `ChatbotActionLog` usage.
