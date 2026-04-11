# Chatbot Scenario Matrix

## Purpose

This document defines how MyGarage chatbot should achieve high situation coverage without trying to memorize every possible user sentence.

The correct target is not "know every utterance". The correct target is:

- identify the correct intent
- know which business fields are already known
- know which required fields are still missing
- ask exactly one useful next question
- use backend-authoritative data for every business decision
- hand off to a human when the flow is unsafe or repeatedly failing

## Core rule

Coverage must be modeled as:

`intent x workflow state x known data x missing data x backend truth x expected next action`

Not as:

`one prompt per possible sentence`

## Backend-authoritative truth map

The chatbot must never trust chat text alone for the following fields:

| Field | Source of truth |
|---|---|
| customerId | authenticated backend context |
| owned vehicles | customer/vehicle tables |
| branch availability | branches + active/bookable checks |
| service catalog | active standard services + categories |
| slot availability | appointment service / slot computation |
| warranty | backend warranty lookup |
| vouchers | backend voucher resolver |
| loyalty/member status | backend loyalty lookup |
| stock | backend inventory lookup |
| prices | backend pricing services |
| order/service history | backend persisted history |

## Intent inventory

Primary customer-facing intents that require scenario coverage:

1. General garage Q and A
2. Symptom consultation
3. Booking appointment
4. Service-order draft
5. Retail-order draft
6. Customer-specific lookup
7. Human handoff

## Coverage strategy

For every intent, define:

- required fields
- optional fields
- backend validations
- stop conditions
- handoff triggers
- exact next-question policy

The next-question policy must follow this rule:

- ask only for the highest-priority missing field
- never ask a field already confirmed in state
- never ask two business questions in one turn unless the UI is explicitly designed for multi-select

---

## Booking workflow matrix

### Required fields

- vehicle
- service category
- detailed service selection
- branch
- booking date
- slot
- note
- explicit final confirmation

### Optional fields

- symptom note
- voucher code suggestion request
- preferred time wording before slot selection

### Booking scenario matrix

| ID | User example | Known state | Missing fields | Expected backend action | Expected chatbot action |
|---|---|---|---|---|---|
| B001 | dat lich bao duong vao ngay mai luc 9h | many vehicles | vehicle, detailed service, branch, note | load owned vehicles only | ask vehicle first |
| B002 | xe 1 | date/time already inferred, generic maintenance intent | detailed service, branch, note | keep prior booking context | ask detailed service, not summary |
| B003 | bao duong cap 1 | vehicle known | branch, date/time if not parsed, note | resolve exact service from catalog | ask branch if branch missing |
| B004 | chi nhanh quan 7 | vehicle and service known | date/time, note | validate active/bookable branch | ask date or slot next |
| B005 | ngay mai luc 9h | vehicle, service, branch known | slot, note | compute slots from backend only | show slot options or exact matched slot |
| B006 | dat lich luc 9h | vehicle, service, branch known, no date | date, slot, note | do not infer final slot without date | ask date |
| B007 | bao duong | one vehicle only | detailed service, branch, date, slot, note | auto-bind single owned vehicle | ask detailed service |
| B008 | bao duong cap 1 cho xe 51A12345 | multiple vehicles | branch, date, slot, note | match vehicle ownership in backend | ask branch |
| B009 | tai chi nhanh A sang mai | vehicle unknown, service unknown | vehicle, service detail, note | validate branch only after service/vehicle flow permits | ask vehicle first if multiple vehicles |
| B010 | slot 2 | branch/date known | note, final confirmation | reserve/check chosen slot | ask note if missing |
| B011 | khong co ghi chu | slot known | final confirmation | persist note as empty/none if allowed | show summary and ask confirm |
| B012 | xac nhan dat lich | summary shown | none | re-check slot and create appointment in transaction | create only now |
| B013 | xac nhan dat lich | summary not shown | summary, explicit confirmation state | reject unsafe shortcut | show summary first |
| B014 | doi xe | mid-booking | vehicle, maybe service revalidation | clear dependent fields | return to vehicle selection |
| B015 | doi dich vu | vehicle known | service, branch, slot, note | clear dependent fields | ask service again |
| B016 | doi ngay | branch/service known | date, slot | clear slot hold | ask date |
| B017 | branch dong cua | branch selected | branch | validate against active/bookable rules | ask another branch |
| B018 | het slot sau khi chon | slot selected | slot | re-check on finalize | return to slot selection honestly |
| B019 | xe khong thuoc khach | user typed plate | vehicle | ownership validation | refuse and ask owned vehicle |
| B020 | tao xe moi bien 51C99999 | no matching vehicle | vehicle confirmation | create only after explicit confirmation | ask confirm vehicle creation |

### Booking invariants

- generic maintenance is not enough when the selected category expands into multiple detailed services
- date/time text is not allowed to act as slot confirmation unless a real backend slot is selected
- branch must not be inferred from session defaults when the public booking request did not specify a branch and the flow is still collecting required fields
- booking note must be collected before final confirmation, even if empty note is allowed
- final booking creation must happen only after summary + explicit user confirmation

---

## Service-order draft matrix

### Required fields

- vehicle
- branch
- service items
- draft-safe pricing inputs from backend
- symptom/complaint note when required by business rules
- explicit create-draft confirmation

| ID | User example | Missing fields | Expected chatbot action |
|---|---|---|---|
| S001 | tao nhap lenh sua cho xe toi | vehicle, branch, service items, symptom note | ask vehicle |
| S002 | xe 2 | branch, service items, symptom note | ask complaint or service items |
| S003 | den bao duong cap 2 | branch, symptom note | ask branch |
| S004 | chi nhanh trung tam | symptom note | ask symptom/note if required |
| S005 | tao nhap lenh | final confirmation | show draft summary and ask confirm |
| S006 | xac nhan tao nhap lenh | none | create real draft, not preview-only object |

### Service-order invariants

- service-order draft must persist a real draft if API/docs claim draft behavior
- draft creation must not depend on LLM tool-calling correctness
- branch and vehicle must be validated server-side before draft creation

---

## Retail-order draft matrix

### Required fields

- customer context
- branch
- product(s)
- quantity
- stock validation
- pricing from backend
- explicit create-draft confirmation

| ID | User example | Missing fields | Expected chatbot action |
|---|---|---|---|
| R001 | toi muon mua dau nhot | branch, exact product, quantity | ask branch or product variant depending on catalog ambiguity |
| R002 | lay 2 chai | exact product, branch | ask exact product |
| R003 | tai chi nhanh quan 7 | exact product, quantity | ask product |
| R004 | dong y tao don tam | none | create retail draft only after stock/pricing validation |

### Retail invariants

- stock availability must come from backend only
- quantity alone is never enough without exact product identity
- chatbot must not fabricate part compatibility or stock levels

---

## Customer-specific lookup matrix

| ID | Intent | Example | Required backend lookup | Expected response pattern |
|---|---|---|---|---|
| L001 | voucher | toi con voucher nao khong | voucher resolver | answer only with real active vouchers |
| L002 | loyalty | hang thanh vien cua toi | loyalty lookup | return tier/benefits from backend |
| L003 | warranty | xe toi con bao hanh khong | warranty lookup | answer honestly if unavailable |
| L004 | history | lich su bao duong xe 51A12345 | service history by owned vehicle | ask vehicle if ambiguous |
| L005 | invoice/order status | don hang cua toi den dau roi | order lookup | return current persisted status only |

### Lookup invariants

- if data is unavailable, say so and give the next best action
- never fill missing history/warranty/payment status from model inference

---

## Symptom consultation matrix

| ID | User example | Backend dependency | Expected chatbot action |
|---|---|---|---|
| C001 | xe bi keu khi phanh | none required initially | ask 1 follow-up symptom question |
| C002 | den check engine sang | none required initially | ask severity/safety follow-up |
| C003 | xe khong de no duoc | none required initially | advise caution, suggest booking/handoff based on severity |
| C004 | toi muon gap ky thuat vien | none | trigger handoff |

### Consultation invariants

- consultation may suggest likely causes but must avoid pretending to diagnose with certainty
- if urgent or unsafe symptom appears, recommend human/onsite support quickly

---

## Handoff matrix

Handoff should trigger for:

- complaint or refund dispute
- repeated workflow failures
- explicit human request
- abusive or highly frustrated conversation
- business data unavailable after repeated attempts
- safety-critical symptom ambiguity

| ID | Trigger | Expected action |
|---|---|---|
| H001 | cho toi gap nguoi that | create handoff immediately |
| H002 | chatbot hoi lap lai 3 lan | handoff or escalate recovery path |
| H003 | toi dang khieu nai | handoff with summary |
| H004 | booking failed repeatedly | handoff with attempted steps |

---

## Training and evaluation dataset design

Build the dataset in 4 layers.

### Layer 1: Gold happy paths

- one vehicle
- multiple vehicles
- one branch
- multiple branches
- one service in category
- multiple services in category

### Layer 2: Missing-data permutations

For each workflow, generate permutations where exactly 1 required field is missing, then 2 missing fields, then 3 missing fields.

Example for booking:

- missing vehicle only
- missing branch only
- missing detailed service only
- missing vehicle + branch
- missing vehicle + detailed service
- missing branch + date
- missing vehicle + service + branch

### Layer 3: Adversarial phrasing

Use variants such as:

- short text: `mai 9h bao duong`
- colloquial text
- typo/no-accent Vietnamese
- mixed Vietnamese/English
- ambiguous numbers: `cap 1`, `xe 1`, `chi nhanh 1`, `9h`
- correction turns: `khong, xe 2`

### Layer 4: State-transition regressions

These are the most important:

- user changes vehicle after service selected
- user changes service after branch selected
- slot becomes unavailable before confirmation
- repeated fallback into same missing-field question
- handoff after repeated failures

---

## What to log from real traffic

For each turn, persist enough structured telemetry to learn from failures:

- detected intent
- workflow step before turn
- workflow step after turn
- missing fields before turn
- missing fields after turn
- selected backend entities
- tool/function calls attempted
- rejection/handoff reason if any
- user correction in next turn

## Failure taxonomy

Every bad conversation should be tagged into one of these buckets:

- wrong intent
- skipped required field
- repeated already-known question
- hallucinated business data
- wrong entity binding
- wrong branch/vehicle/service selection from ambiguous numeric input
- premature finalization
- no handoff when needed
- handoff too early

---

## Recommended test implementation order

1. Unit tests for parser ambiguity
   - `xe 1` only selects vehicle in vehicle-selection context
   - `cap 1` does not select vehicle or branch
   - `9h` does not select branch
2. Booking workflow tests for every missing required field
3. Multi-turn state-transition tests
4. Service-order and retail-order deterministic workflow tests
5. Lookup truth-source tests
6. Handoff trigger tests

## Recommended fixture structure

Suggested future fixture layout:

- `backend.Tests/ChatbotScenarios/booking/*.json`
- `backend.Tests/ChatbotScenarios/lookups/*.json`
- `backend.Tests/ChatbotScenarios/service-order/*.json`
- `backend.Tests/ChatbotScenarios/retail/*.json`
- `backend.Tests/ChatbotScenarios/handoff/*.json`

Each scenario fixture should contain:

- `id`
- `intent`
- `initialState`
- `customerContext`
- `userTurns`
- `expectedStepSequence`
- `expectedPromptsContain`
- `expectedPromptsNotContain`
- `expectedBackendCalls`
- `expectedFinalOutcome`

## Practical rule for ongoing training

When a new real conversation fails:

1. classify the failure bucket
2. add one scenario row here if the situation is new
3. add one automated regression test
4. fix the deterministic backend rule first
5. update prompting only if the backend rule is already correct

That is how the chatbot gradually becomes robust across "all situations" in practice.
