# MyGarage Camera AI

Imported from the local FastALPR project `E:\AI biển số xe\AI moi nhat` on
2026-11-14. Upstream FastALPR is MIT licensed; retain `LICENSE` when distributing.
The recognition algorithms, OCR models, video tracking and browser workbench
are preserved. The MyGarage adaptation adds CPU/CUDA selection, a health route,
a configurable local port and a repository-level launcher.

## Run on Windows

For the complete local system, run `powershell -File scripts/start-dev.ps1` from
MyGarage. Garage and this workbench run independently. Pair an enabled camera in
Garage's gate dashboard, then open this original AI UI to scan video or webcam.
Recognition events are delivered by `garage_bridge.py` through a durable SQLite
queue; Garage performs the entry/exit business decisions. See
`../../docs/CAMERA_AI_INTEGRATION.md` for pairing, directions and deployment limits.

From the MyGarage repository root, with Python 3.12 installed:

```powershell
.\scripts\start-camera-ai.ps1 -Install -PythonExe 'C:\path\to\python.exe'
```

Later runs:

```powershell
.\scripts\start-camera-ai.ps1
# Optional NVIDIA runtime; install the GPU extra and compatible CUDA libraries first:
.\scripts\start-camera-ai.ps1 -Install -Backend cuda
```

Open http://localhost:7861. MyGarage continues to run on http://localhost:5173.
The workbench supports uploaded images, uploaded videos, browser webcam and
the included sample image. The large demo video is local-only/ignored by Git;
on a fresh checkout use an uploaded video, or provide `assets/traffic-sample.mp4`.

The first scan for each recognition mode may download its model. Local imported
model cache is ignored by Git. A fresh machine requires model download access.
`CAMERA_AI_BACKEND=cpu` is the default. CUDA is explicitly opt-in.
CPU mode preserves the models but generally has different processing speed.

```powershell
# Existing CLI also supports a webcam, video path or RTSP source with native display:
.\services\camera-ai\.venv\Scripts\python.exe .\services\camera-ai\run_demo.py --cpu --source 0
```

## Local interfaces

- `GET /health`: service readiness and configured ONNX providers; available after warmup.
- `POST /recognize?mode=fast|balanced|accurate|deep`: raw image bytes;
  JSON contains `plates`, `details`, annotated image and timings.
- `POST /video/upload`: raw video upload, returns a temporary session ID.
- `POST /video/analyze`: existing timestamp-based video analysis with tracking.
- `POST /video/close`: closes a temporary video session.

The server binds to loopback. It is a local workbench without MyGarage user
authentication; do not expose it publicly. Camera credentials must not be placed
in this repository. Model cache, uploads and previews remain local runtime data.

## Integration status

MyGarage's gate dashboard is at `/workshop/gate`, history at
`/workshop/gate/history`, and camera management at `/workshop/gate/cameras`.
The .NET API calls this local AI process for image and uploaded-video recognition.
Results and private JPEG evidence are persisted through authenticated MyGarage APIs.
The `GATE_SECURITY` role confirms physical passage. Manager/Admin manage cameras
and review exit exceptions. Receptionists can view events and correct plates.

For local video testing, register a camera using `localvideo://sample`, then select
it on the gate dashboard and upload a local video. Play it and enable automatic
scanning, or pause and scan a chosen frame. One distinct plate is recorded once
per uploaded clip; this simulation does not infer physical gate crossings.

For unattended RTSP or local-file processing, register a camera, copy its one-time
credential into the environment variable referenced by `worker.example.json`, and run:

```powershell
.\scripts\start-camera-worker.ps1 -Config .\services\camera-ai\worker.example.json
```

Use a `worker.local.json` file (ignored by Git) for local settings. Optional
`localVideo` on a camera entry selects a local video file when its registered
source uses the `localvideo` scheme. RTSP credentials are encrypted by .NET Data
Protection and delivered only to the authenticated worker. Preserve the backend's
Data Protection keys across deployment/restarts; losing keys loses access to saved
stream addresses. For HTTPS development, trust the backend certificate or set
`CAMERA_AI_CA_FILE` to its PEM CA; the worker never disables TLS verification.

The worker uses one capture thread and one delivery thread per camera. SQLite
queues retain unacknowledged events across outages and restarts; continuous plate
presence suppresses duplicate events and rearms after 15 seconds of absence.
See `../../docs/CAMERA_AI_INTEGRATION.md` for status and verification.

## Tests

```powershell
.\services\camera-ai\.venv\Scripts\python.exe -m pip install pytest
.\services\camera-ai\.venv\Scripts\python.exe -m pytest services/camera-ai/test/test_runtime_config.py
```

`test/test_alpr.py` contains upstream end-to-end tests; the full parametrized suite
downloads several OCR models. A single smoke test can use the provided sample
image and a cached model without downloading the entire suite.
