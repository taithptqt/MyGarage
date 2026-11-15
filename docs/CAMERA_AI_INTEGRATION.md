# Camera AI integration

Status: standalone AI workbench with an asynchronous Garage event adapter. Date: 2026-11-15.

## Current standalone mode (2026-11-15)

Run all three independent processes with `powershell -File scripts/start-dev.ps1`.
The launcher reuses running services, stores logs in ignored `.dev-runtime/`, and
supports `-Stop` for only the process trees it started.

- Garage FE: http://localhost:5173/workshop/gate
- Garage BE: https://localhost:7009 (HTTP 5062 redirects to HTTPS).
- Camera AI: http://127.0.0.1:7861, using the original `web.html`, `web.js` and
  `tracking.js` with the tracking-display continuity fix described below. Recognition retains its original algorithm; the earlier
  CPU/CUDA provider-selection adaptation remains. The source project at
  `E:\AI biển số xe\AI moi nhat` is untouched.

On Garage's gate dashboard, Manager/Admin chooses an enabled camera and clicks
**Kết nối camera với AI (đổi khóa)**. This uses the existing authorized key rotation
and audit, then pairs the loopback AI service. It replaces any previous worker key
for that camera. Open the AI link and upload/play video using the original controls.
Garage no longer renders or drives the recognition video. The legacy scan/video
API remains available for compatibility but is not the dashboard's camera flow.

For local development, the adapter sends to the existing FE `/api` proxy at
`http://127.0.0.1:5173`, which reaches the HTTPS backend without disabling Python
TLS verification. Optional FE settings: `VITE_CAMERA_AI_URL` and
`VITE_CAMERA_GARAGE_URL`. Browser pairing accepts loopback targets only and CORS
permits the local FE and AI origins only. Remote deployment uses the existing
RTSP worker with HTTPS and a trusted CA instead of this local pairing endpoint.

`garage_bridge.py` persists observations (UUID, plate, confidence, wall-clock UTC
time, full/crop JPEG) in SQLite before a background sender contacts Garage.
Retries keep the event UUID; backend ingestion is idempotent. Presence deduplication
suppresses a stationary plate and rearms after 15 seconds without seeing it.
Keys/configuration and pending evidence live under ignored `services/camera-ai/work/`.
Status never returns keys. Do not publish this local AI workbench directly online.

Each opened video binds to the selected camera at upload/open time. Changing the
active camera does not relabel an already-open clip; close and reopen a clip after
changing entry/exit lanes. Distinct cameras retain their own queued events and keys.
Real multiple RTSP streams use the existing per-camera worker. AI only reports
observations: Garage determines branch/direction from the authenticated device,
matches vehicles/appointments/orders, and enforces Gate Pass and staff review.

The sections below describe the underlying gate module and earlier merge checks.

Tracking display fix (2026-11-15): reuse SVG nodes by visual track, bridge brief
detection misses for at most 650 ms, blend geometry over 100 ms, and debounce
confidence-color recovery. Seeking/resolution changes clear stale tracks. These
presentation-only rows never become Garage observations. Seven Node tests cover
timing, ID churn, geometry, clearing, confidence and the actual SVG painter.

Verification of standalone mode: five adapter tests plus a real Python workbench
HTTP test passed. The latter used an isolated HTTP receiver to verify original
UI assets, real sample recognition, entry/exit camera binding, evidence, origin
rejection and offline retry using the same event UUID. The 23 existing .NET gate
rule/HTTP tests passed separately. Frontend production build and UTF-8 checks passed.
Start, reuse, stop and restart of all three launcher-managed services were exercised.
No authenticated browser walkthrough, real RTSP or gate hardware was exercised.

## Current implementation and local operation

The user confirmed there is no physical RTSP camera yet. Local footage is the
acceptance input for this iteration. The following application routes are implemented:

- `/workshop/gate`: device heartbeat, realtime event list, image upload, browser
  webcam snapshot, local-video upload, frame scanning and manual review.
- `/workshop/gate/history`: server-side branch/camera/plate/direction/time/status
  filters, pagination, audit details and Manager/Admin CSV export (up to 10,000 rows).
- `/workshop/gate/cameras`: Manager/Admin registration and editing, encrypted RTSP
  sources, simulated `localvideo://sample` sources, thresholds, evidence retention,
  enable/disable and one-time worker credential creation/rotation.

Start the AI process from the repository root with `scripts/start-camera-ai.ps1`.
Its default port is 7861, matching the backend's `CameraAI:RecognitionUrl` default.
Run the backend and frontend as usual. A Manager/Admin must first create an entry
camera and an exit camera for an existing branch. For simulation set their source
to `localvideo://sample`; no physical camera credentials are needed. Select the
camera on the dashboard, upload a video and play it with automatic scanning enabled.
Alternatively pause at a clear frame and use the frame-scan button. The provided
local file is `services/camera-ai/assets/traffic-sample.mp4` (ignored by Git).

The same backend ingestion persists events for AI workers and interactive scans.
Private evidence is stored in SQL varbinary columns; it is served only through
authorized endpoints. Evidence expires according to each camera's retention days
(30 by default, configurable 1–365); an hourly job clears image bytes, retaining
event metadata and audit. This default is a configurable local-test assumption.

`AddCameraGateAccess` creates three tables and seeds `GATE_SECURITY` while extending
the role-code constraint. `AddCameraGatePassExpiry` adds optional `ExpiresAtUtc` to
GatePass. Existing passes retain null expiry. The new release workflow rejects
expired, canceled, pending and consumed passes; it checks vehicle/branch match,
existing handover readiness (payment, QC, completed tasks), and old-parts confirmation.
Manager exceptions require a reason and are recorded. Confirmations run inside a
serializable transaction and a unique filtered index prevents confirmed events from
sharing a GatePass. Source observation IDs prevent duplicate device delivery.

### Verification commands

```powershell
dotnet ef database update --context MyGarage.Data.GarageContext --project backend/src/MyGarage.Infrastructure --startup-project backend/src/MyGarage.Api
dotnet test backend/tests/MyGarage.Tests/MyGarage.Tests.csproj --filter 'FullyQualifiedName~CameraGate|FullyQualifiedName~EndpointAuthCoverageTests'
$env:MYGARAGE_CAMERA_E2E='1'
dotnet test backend/tests/MyGarage.Tests/MyGarage.Tests.csproj --filter FullyQualifiedName~CameraVideoE2ETests
.\services\camera-ai\.venv\Scripts\python.exe -m pytest services/camera-ai/test/test_event_spool.py services/camera-ai/test/test_runtime_config.py -q
```

The optional E2E tests require the real local AI service and sample video. They use
an isolated in-memory business database and do not create fake customer jobs in the
configured garage database. They verify both sample-video and multipart-upload paths,
recognition of `65H02902`, repeat-frame deduplication, protected video-session ownership,
private snapshot/crop retrieval, confirmation and audit.

Latest verification: 26 backend gate/video/security tests passed (including two real
Python video round trips), 18 existing GatePass/audit regression tests passed,
69 frontend tests passed, and 6 Python configuration/spool/retry tests passed.
Frontend production build and UTF-8 checks passed. Both new migrations were applied
to the configured development database. An authenticated browser UI walk-through,
physical RTSP tests, GPU throughput and four-group accuracy benchmarking have not
been performed in this iteration.

### Remaining acceptance boundaries

- Uploaded-video simulation records one event per distinct plate per clip. Re-entry
  in a clip requires a separate upload; production worker presence rearms after
  15 seconds of absence and queues events durably in SQLite.
- Camera direction is configured per lane. Physical passage is confirmed by staff;
  lane crossing, reverse travel and tailgating need actual camera calibration.
- RTSP reconnect and per-camera capture/delivery threads are implemented, but there
  is no actual RTSP hardware available for an operational test.
- CPU/GPU capacity and day/night car/motorcycle accuracy are not certified by the
  single sample clip. Dedicated labeled footage is needed for that benchmark.
- There is no barrier actuator. Stage 4 remains gated on hardware and measured accuracy.
- Open-visit reconciliation and road-test/departure-return policies remain separate
  follow-up business decisions; current persistence is an audited access-event history.

The following sections retain the architecture and intended operating policies.

### Video tracking overlay

The integrated video player displays OCR labels and plate rectangles on the JPEG
frame that produced them. The authenticated API returns that frame alongside its
timestamp, dimensions, track IDs and geometry. This processed preview updates at
inference speed; playback can advance while inference and event persistence run.
The previous overlay discarded results over 350 ms old, leaving an apparently
unprocessed video on slower requests. Seeking aborts obsolete requests and resets
tracking; pausing also requests recognition at the paused position.
Continuous scanning permits one inference at a time and follows the latest video
position; mode and playback-speed controls support slower hardware. Previously
recorded plates skip repeat database ingestion for the session.

Tracking-specific verification: five frontend geometry/rendering tests passed;
the eight gate/video HTTP tests passed with the real local Python service,
including repeated-frame requests and tracking geometry contracts. Production
frontend build and UTF-8 checks passed. Browser visual inspection was unavailable
because the Windows Computer Use pipe could not connect.
The uploaded traffic clip produced `AF65JKV` at 23 seconds through the protected
.NET upload/analyze API with a nonempty JPEG preview (real local Python service,
in-memory test database). The analyze round trip took about 1.3 seconds, exceeding
the old overlay's 350 ms lifetime. This sample does not establish an accuracy
benchmark. Set `MYGARAGE_CAMERA_TEST_VIDEO`, `MYGARAGE_CAMERA_TEST_TIME` and
`MYGARAGE_CAMERA_TEST_PLATE` to repeat the upload test with a local clip.

## Architecture decision

Keep Python/OpenCV/ONNX as a separate process in `services/camera-ai`.
MyGarage .NET remains responsible for authentication, branch scope, access-event
storage and gate decisions. This preserves the existing recognition engine and
isolates CPU/GPU workloads, at the cost of one additional process to operate.
An in-process .NET/Python bridge was considered; it adds runtime coupling without
benefiting the current recognition workflow. Revisit deployment topology when
actual camera count and measured inference capacity are available.

## Delivery stages

1. Source merge: preserve image/video/webcam recognition, model assets, license,
   local workbench, explicit CPU/CUDA setting, health endpoint and launcher.
2. Entry events: camera/lane/branch configuration, continuous RTSP worker,
   reconnect, multi-frame observations, durable local spool and idempotent
   authenticated ingestion to .NET. Persist raw and confirmed plate separately.
3. Staff workflow: entry review, appointment/vehicle matching, manual corrections,
   branch-scoped history and SignalR updates. Add `GATE_SECURITY` with migration
   of the existing role-code constraint, permission policies and role routing.
4. Exit workflow: validate the existing Gate Pass/handover/payment/QC conditions,
   confirm physical departure and record audit. Manager handles exceptions.
5. Operational validation: real entrance/exit recordings, night/day tests,
   offline recovery, duplicate delivery, multiple vehicles and cross-branch tests.

## Responsibilities

| Actor | Branch scope and responsibility |
| --- | --- |
| Gate security | Review plate evidence and confirm physical entry/exit at assigned branch |
| Receptionist | Match arriving vehicle/customer/appointment and perform reception |
| Branch manager | Review exceptions, reconcile occupancy and manage assigned cameras |
| Admin | Configure service identities, global camera settings and audit access |
| Accountant | Supply financial clearance through existing business workflow |
| Technician/QC | See relevant job state; gate approval is not implied by workshop access |
| Camera service | Submit observations for assigned cameras; no customer or Gate Pass write access |

Authorization must be enforced on list/detail/image/command endpoints and realtime
subscriptions, not just UI navigation. Reuse branch-scope checks and add explicit
camera permissions instead of giving all Staff administrative access.

## Events and visits

Recognition observations, human decisions, access permission and physical passage
are separate facts. A detected/denied exit still belongs in history even if a
vehicle actually leaves without authorization; create an incident rather than
discarding the observation or pretending the vehicle remains inside.

Track an open vehicle visit with confirmed entry and exit, with explicit handling
for road tests, temporary departure/return, unknown vehicles and missing entries.
Camera direction requires calibrated lanes/crossing logic; a visible plate alone
does not prove a vehicle crossed the gate. Prevent duplicates with persistent
event IDs, worker-instance IDs and server-side idempotency, not track IDs alone.

## Integration contracts

- Camera observation: event ID, camera ID, worker instance, observation timestamp,
  raw plate, detection/OCR confidence, direction evidence and snapshot reference.
- Server derives branch/lane from authenticated device registration.
- Separate observed time and server receipt time; use UTC for new event timestamps.
- Keep evidence private; access images through authorization-checked endpoints.
- Confirming an exit must recheck authorization and Gate Pass eligibility in a
  transaction, consume a pass once and handle concurrent staff confirmations.
- AI corrections are audited without silently changing the registered vehicle plate.

## Existing integration concerns

MyGarage currently contains both legacy Gate Pass CRUD and an application workflow.
The `GatePassStatusCode` enum and application workflow use different status sets.
Audit and reconcile their database constraints, endpoint authorization and state
transitions before connecting automatic observations to gate decisions.

Camera count, stream configuration, evidence retention and barrier hardware remain
deployment inputs. Thresholds and throughput targets require measurements at the
actual garage. Current merge does not add barrier control.

## Merge verification (2026-11-14)

- Installed an independent Python 3.12 virtual environment in the imported service.
- Three provider-selection tests and one upstream drawing/recognition test passed.
- `pip check` and Python compilation passed.
- Started through `scripts/start-camera-ai.ps1 -Port 7861`; workbench and health
  returned HTTP 200 on loopback.
- Image HTTP recognition returned the expected `5AU5341` in all four modes.
  Accurate/deep also produced the short candidate `4N4`, flagged uncertain by the
  existing engine. This is a smoke check, not an accuracy benchmark.
- Video sample session opened, decoded two 1280x720 frames with tracking and
  returned `65H02902` at both timestamps; session closed successfully.
- Original web HTML/JavaScript hashes match the source project.
- Webcam hardware, RTSP camera, GPU inference and the full multi-model upstream
  test suite were not exercised. Browser webcam code is retained.
- Camera source project and existing MyGarage application changes were preserved.
