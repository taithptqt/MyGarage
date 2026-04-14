# MyGarage AI Chatbot API

## Scope

This document describes the public HTTP API surface of the chatbot module under `/api/chatbot`.

## Authentication and authorization

- All endpoints require JWT authentication (`Authorization: Bearer <token>`).
- All endpoints are `Customer` role only.
- Customer identity is resolved from JWT claims on the server.
- Client input for `CustomerId`, `BranchId`, role escalation, tool execution, or pricing authority is not trusted.

## Endpoint summary

| Method | Route | Purpose |
|---|---|---|
| `POST` | `/api/chatbot/sessions` | Create a customer chatbot session |
| `POST` | `/api/chatbot/orchestrate` | Run one AI chatbot turn and receive assistant reply |
| `POST` | `/api/chatbot/messages` | Persist a user message in a session |
| `GET` | `/api/chatbot/sessions/{id}` | Read a session and message history |
| `POST` | `/api/chatbot/upload-image` | Upload image and persist AI vision analysis |

## Endpoint details and examples

### `POST /api/chatbot/sessions`

Create a new chatbot session.

Request:

```json
{
  "currentIntent": "BOOKING",
  "intentState": "{\"flow\":{\"step\":\"IDENTIFY_OR_CREATE_VEHICLE\"}}"
}
```

Success response (`201 Created`):

```json
{
  "id": 1201,
  "sessionToken": "cbs_6d2052649f834c9ebf4551f6a1f3202f",
  "customerId": 55,
  "branchId": 2,
  "status": "ACTIVE",
  "currentIntent": "BOOKING",
  "intentState": "{\"flow\":{\"step\":\"IDENTIFY_OR_CREATE_VEHICLE\"}}",
  "handoffConversationId": null,
  "handoffSummary": null,
  "lastActivityAt": "2026-04-08T09:14:33.0000000Z",
  "closedAt": null,
  "createdAt": "2026-04-08T09:14:33.0000000Z",
  "updatedAt": "2026-04-08T09:14:33.0000000Z",
  "messages": []
}
```

Possible errors:

- `400` invalid payload.
- `401` missing/invalid token.
- `403` non-customer role.
- `404` customer not found.

### `POST /api/chatbot/orchestrate`

Run one full AI turn (guardrails + intent + workflow/tool routing + assistant reply).

Request:

```json
{
  "sessionId": 1201,
  "userMessage": "Toi muon dat lich bao duong vao sang mai",
  "attachments": []
}
```

Success response (`200 OK`):

```json
{
  "sessionId": 1201,
  "sessionToken": "cbs_6d2052649f834c9ebf4551f6a1f3202f",
  "intent": "BOOKING",
  "confidence": 0.93,
  "strategy": "INTENT_ROUTER",
  "isRejected": false,
  "rejectionReason": null,
  "shouldHandoff": false,
  "assistantReply": "Minh da ghi nhan yeu cau dat lich. Vui long chon xe dang su dung.",
  "bookingWorkflow": {
    "step": "IDENTIFY_OR_CREATE_VEHICLE"
  },
  "updatedIntentState": "{\"flow\":{\"intent\":\"BOOKING\",\"step\":\"IDENTIFY_OR_CREATE_VEHICLE\"}}",
  "functionCalls": [],
  "orchestratedAt": "2026-04-09T09:18:21.0000000Z"
}
```

Notes:

- If `sessionId/sessionToken` is missing, backend auto-creates a session for authenticated customer.
- `allowSendToLockedSession` is ignored and forced to `false` on public endpoint.

Possible errors:

- `400` invalid payload or business validation failed.
- `401` missing/invalid token.
- `403` non-customer role.
- `404` session/customer not found.
- `409` locked/handed-off session conflict.
- `429` rate limit.

### `POST /api/chatbot/messages`

Persist a user message.

Server-authoritative behavior:

- Request role is forcibly normalized to `USER`.
- Client-provided `toolCall`, `toolResult`, and `sessionState` are ignored on this endpoint.
- `allowSendToLockedSession` is forced to `false`.

Request:

```json
{
  "sessionId": 1201,
  "role": "USER",
  "content": "I want to book maintenance for tomorrow morning",
  "attachments": []
}
```

Success response (`200 OK`):

```json
{
  "session": {
    "id": 1201,
    "sessionToken": "cbs_6d2052649f834c9ebf4551f6a1f3202f",
    "customerId": 55,
    "branchId": 2,
    "status": "ACTIVE",
    "currentIntent": "BOOKING",
    "intentState": "{\"flow\":{\"step\":\"IDENTIFY_OR_CREATE_VEHICLE\"}}",
    "handoffConversationId": null,
    "handoffSummary": null,
    "lastActivityAt": "2026-04-08T09:18:21.0000000Z",
    "closedAt": null,
    "createdAt": "2026-04-08T09:14:33.0000000Z",
    "updatedAt": "2026-04-08T09:18:21.0000000Z",
    "messages": []
  },
  "persistedMessages": [
    {
      "id": 90110,
      "sessionId": 1201,
      "role": "USER",
      "content": "I want to book maintenance for tomorrow morning",
      "attachmentUrl": null,
      "intentName": null,
      "sentimentScore": null,
      "toolName": null,
      "toolArgs": null,
      "toolResult": null,
      "createdAt": "2026-04-08T09:18:21.0000000Z"
    }
  ]
}
```

Possible errors:

- `400` invalid payload.
- `401` missing/invalid token.
- `403` non-customer role.
- `404` session not found.
- `409` session locked.
- `429` rate limit.

### `GET /api/chatbot/sessions/{id}`

Get session state and message history.

Success response (`200 OK`): same shape as `session` object above.

Possible errors:

- `401` missing/invalid token.
- `403` non-customer role.
- `404` session not found or not owned by current customer.

### `POST /api/chatbot/upload-image`

Upload and analyze an image for triage.

Request: `multipart/form-data`

- `sessionId` or `sessionToken` (one is required).
- `note` (optional).
- `file` (required, JPEG/PNG/WEBP, max ~8 MB).

Success response (`200 OK`):

```json
{
  "sessionId": 1201,
  "attachmentUrl": "https://storage.example.com/chatbot/sessions/1201/20260408092101_abcd.webp",
  "userMessageId": 90115,
  "assistantMessageId": 90116,
  "analysis": {
    "imageSummary": "Front bumper scratch and minor paint peel detected.",
    "suspectedCategory": "BODY_DAMAGE",
    "suggestedServiceCategories": [
      "Body Repair",
      "Paint Work"
    ],
    "disclaimer": "Preliminary AI assessment only. Please confirm with an in-person inspection by a MyGarage technician."
  }
}
```

Possible errors:

- `400` unsupported file type/signature, oversize file, invalid payload.
- `401` missing/invalid token.
- `403` non-customer role.
- `404` session/customer not found.

## HTTP status model

- `200` successful read/write.
- `201` session created.
- `400` validation/business rule failure.
- `401` unauthenticated.
- `403` unauthorized role.
- `404` not found/ownership mismatch.
- `409` locked-session conflict.
- `429` rate limit.

## Internal orchestration note

- `IChatbotOrchestrator` is implemented in service layer and handles intent/guardrail/tool routing.
- Public orchestration endpoint is available at `POST /api/chatbot/orchestrate`.

## Security notes

- Write operations are scoped to authenticated customer ownership at service layer.
- Tool calls and tool results persisted into `ChatbotMessages` are server-generated.
- Session lock statuses (`HANDED_OFF`, `EXPIRED`, `CLOSED`, `ARCHIVED`) enforce write restrictions.
- Image uploads enforce extension/content-type/signature validation and file security scan before persistence.
- Pricing and stock for chatbot tool execution are resolved from authoritative backend records, not model output.

## Operational troubleshooting

| Symptom | Probable cause | Check | Resolution |
|---|---|---|---|
| `401 Unauthorized` | JWT missing/invalid | token issuer/audience/signature | refresh token, check JWT config |
| `403 Forbidden` | role not `Customer` | JWT role claim | use customer token |
| `404 Session not found` | session does not exist or ownership mismatch | session id/token and customer ownership | use valid owned session |
| `409 Session is locked` | session in locked state | `ChatbotSessions.Status` | create new session or unlock by workflow |
| `429 Too many chatbot requests` | rate limiter triggered | rate-limit hook logs | retry with backoff |
| image upload blocked | MIME/signature scan failure | upload action logs + scanner result | upload supported clean file |
