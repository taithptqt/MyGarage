# Chat V2 API Contract

## Scope

- Customer can chat only with receptionist.
- Staff-side chat APIs are for receptionist role only.
- Branch scope is enforced in service and SignalR hub.

## Base Routes

- Customer: `/api/chat`
- Receptionist: `/api/reception/chat`
- SignalR hub: `/hubs/chat`

## Auth & Role

- Customer APIs require authenticated `Customer`.
- Receptionist APIs require authenticated `Staff` with receptionist role code/name.
- Hub group join validates role + ownership/branch:
  - `Conversation_{id}`:
    - Customer: must own conversation.
    - Receptionist: same branch and receptionist role.
  - `BranchInbox_{id}`:
    - Receptionist: same branch and receptionist role.
  - `User_{id}`:
    - only same user id.

## Endpoints

### Customer

- `GET /api/chat/my-conversations`
- `GET /api/chat/conversations/{id}`
- `GET /api/chat/conversations/{id}/messages?beforeMessageId=&take=`
- `POST /api/chat/conversations`
- `POST /api/chat/conversations/{id}/messages`
- `PUT /api/chat/conversations/{id}/read-state`

### Receptionist

- `GET /api/reception/chat/conversations?branchId=&status=&isUnread=&page=&pageSize=`
- `GET /api/reception/chat/conversations/{id}`
- `GET /api/reception/chat/conversations/{id}/messages?beforeMessageId=&take=`
- `POST /api/reception/chat/conversations/{id}/messages`
- `PUT /api/reception/chat/conversations/{id}/status`
- `PUT /api/reception/chat/conversations/{id}/read-state`
- `PUT /api/reception/chat/conversations/{id}/assign`

## Response Codes

- `200`: success
- `201`: created conversation
- `204`: mark-read success
- `400`: invalid input payload
- `401`: not authenticated
- `403`: forbidden (role/branch/ownership)
- `404`: conversation or entity not found
- `409`: business conflict (`ChatConflictException`)

## State Machine

### States

- `OPEN`
- `WAITING_FOR_STAFF`
- `WAITING_FOR_CUSTOMER`
- `CLOSED`

### Message-driven transitions

- Customer send:
  - `OPEN` -> `WAITING_FOR_STAFF`
  - `WAITING_FOR_STAFF` -> `WAITING_FOR_STAFF`
  - `WAITING_FOR_CUSTOMER` -> `WAITING_FOR_STAFF`
  - `CLOSED` -> `WAITING_FOR_STAFF` (auto reopen)
- Receptionist send:
  - `OPEN` -> `WAITING_FOR_CUSTOMER`
  - `WAITING_FOR_STAFF` -> `WAITING_FOR_CUSTOMER`
  - `WAITING_FOR_CUSTOMER` -> `WAITING_FOR_CUSTOMER`
  - `CLOSED` -> conflict `409`

### Manual status update (receptionist)

- Allowed:
  - `OPEN` -> `WAITING_FOR_STAFF|WAITING_FOR_CUSTOMER|CLOSED`
  - `WAITING_FOR_STAFF` -> `WAITING_FOR_CUSTOMER|CLOSED`
  - `WAITING_FOR_CUSTOMER` -> `WAITING_FOR_STAFF|CLOSED`
- Not allowed:
  - any transition from `CLOSED` (except customer message auto reopen)

## Idempotency

- `ClientMessageId` is unique (filtered unique index).
- Duplicate `ClientMessageId` in same conversation returns existing message.
- Duplicate `ClientMessageId` in another conversation returns `409`.

## Read-state

- Customer read-state: `ConversationCustomerReads`.
- Receptionist read-state: `ConversationStaffReads`.
- Monotonic behavior: only move `LastReadMessageId` forward.

## Realtime Events

- `ReceiveMessage`
- `ConversationUpdated`
- `ReadStateUpdated`
