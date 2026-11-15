"""Local adapter: preserve the AI workbench, deliver durable gate observations separately."""
import base64
import json
import logging
import math
import os
import re
import ssl
import threading
import time
import uuid
from datetime import datetime, timezone
from pathlib import Path
from urllib.parse import urlsplit
from urllib.request import Request, urlopen

from event_spool import EventSpool
from run_demo import cv2

log = logging.getLogger("garage-bridge")


def request_device(target, route, payload=None):
    body = None if payload is None else json.dumps(payload).encode()
    request = Request(f"{target['garageUrl']}/api/v1/camera-ai/devices/{target['cameraId']}/{route}",
                      data=body, headers={"X-Camera-Key": target["credential"], "Content-Type": "application/json"})
    context = ssl.create_default_context(cafile=os.environ.get("CAMERA_AI_CA_FILE") or None)
    with urlopen(request, timeout=10, context=context) as response:
        data = response.read()
        return json.loads(data) if data else None


def jpeg(frame, width=1280):
    if frame is None or frame.size == 0:
        return None
    if frame.shape[1] > width:
        frame = cv2.resize(frame, (width, round(frame.shape[0] * width / frame.shape[1])))
    ok, data = cv2.imencode(".jpg", frame, [cv2.IMWRITE_JPEG_QUALITY, 80])
    return base64.b64encode(data).decode() if ok and len(data) <= 2_000_000 else None


class GarageBridge:
    def __init__(self, directory, transport=request_device):
        self.directory = Path(directory)
        self.directory.mkdir(parents=True, exist_ok=True)
        self.config_path = self.directory / "garage-bridge.json"
        self.spool_path = self.directory / "garage-events.sqlite3"
        self.transport = transport
        self.lock = threading.RLock()
        self.stop = threading.Event()
        self.targets, self.active, self.last_seen, self.errors = {}, None, {}, {}
        self.last_frames = {}
        self.instance = f"AI_WORKBENCH:{uuid.uuid4().hex}"
        if self.config_path.exists():
            saved = json.loads(self.config_path.read_text(encoding="utf-8"))
            self.targets = {int(key): value for key, value in saved["targets"].items()}
            self.active = saved.get("activeCameraId")
        self.sender = None

    def connect(self, settings):
        url = str(settings.get("garageUrl", "")).rstrip("/")
        parsed = urlsplit(url)
        # This browser pairing endpoint is a local-development adapter. A remotely
        # deployed worker uses its own HTTPS/CA configuration instead.
        if parsed.scheme not in ("http", "https") or parsed.hostname not in ("localhost", "127.0.0.1", "::1") or parsed.username or parsed.password or parsed.path or parsed.query or parsed.fragment:
            raise ValueError("Garage URL must be a loopback HTTP(S) origin")
        camera = int(settings.get("cameraId", 0))
        credential = str(settings.get("credential", ""))
        if camera <= 0 or not re.fullmatch(r"[A-Fa-f0-9]{64}", credential):
            raise ValueError("Invalid camera credential")
        target = {"garageUrl": url, "cameraId": camera, "credential": credential}
        configuration = self.transport(target, "configuration")  # Validate key and enabled camera before saving.
        target["direction"] = configuration["direction"]
        with self.lock:
            targets = {**self.targets, camera: target}
            temporary = self.config_path.with_suffix(".tmp")
            temporary.write_text(json.dumps({"targets": targets, "activeCameraId": camera}), encoding="utf-8")
            temporary.replace(self.config_path)
            self.targets, self.active = targets, camera
            self.errors.pop(camera, None)
        return self.status()

    def status(self):
        with self.lock:
            with_queue = EventSpool(self.spool_path)
            try:
                count = with_queue.db.execute("SELECT COUNT(*) FROM pending").fetchone()[0]
            finally:
                with_queue.close()
            return {"activeCameraId": self.active, "pending": count,
                    "cameras": [{"cameraId": key, "direction": value.get("direction"),
                                 "delivery": self.errors.get(key, "READY")} for key, value in self.targets.items()]}

    def observe(self, frame, details, camera_id=None):
        # Never perform a Garage HTTP request on the inference thread.
        with self.lock:
            camera = camera_id if camera_id is not None else self.active
            if camera not in self.targets:
                return
            self.last_frames[camera] = time.monotonic()
            now = time.time()
            self.last_seen = {key: value for key, value in self.last_seen.items() if now - value < 60}
            spool = EventSpool(self.spool_path)
            try:
                snapshot = None
                for row in details:
                    plate = re.sub("[^A-Z0-9]", "", str(row.get("text", "")).upper())[:40]
                    if len(plate) < 4:
                        continue
                    key = (camera, plate)
                    if now - self.last_seen.get(key, 0) < 15:
                        spool.touch(camera, plate, now)
                        self.last_seen[key] = now
                        continue
                    x1, y1, x2, y2 = map(int, row["box"])
                    if snapshot is None:
                        snapshot = jpeg(frame)
                    confidence = min(float(row.get("detection_confidence", 0)), float(row.get("ocr_confidence", 0)))
                    if not math.isfinite(confidence):
                        confidence = 0
                    payload = {"eventKey": str(uuid.uuid4()), "workerInstance": self.instance,
                               "plate": plate, "confidence": max(0, min(1, confidence)),
                               "observedAt": datetime.now(timezone.utc).isoformat(),
                               "snapshotBase64": snapshot,
                               "cropBase64": jpeg(frame[max(0,y1):y2, max(0,x1):x2], 640)}
                    spool.observe(camera, plate, payload, now=now)
                    self.last_seen[key] = now
            finally:
                spool.close()

    def flush_once(self):
        with self.lock:
            targets = list(self.targets.items())
        spool = EventSpool(self.spool_path)
        try:
            for camera, target in targets:
                try:
                    for _ in range(5):
                        pending = spool.first(camera)
                        if not pending:
                            break
                        event_id, payload = pending
                        self.transport(target, "observations", payload)
                        spool.acknowledge(event_id)
                    with self.lock:
                        recent_frame = time.monotonic() - self.last_frames.get(camera, float('-inf')) < 15
                    self.transport(target, "heartbeat", {"status": "ONLINE" if recent_frame else "RECONNECTING"})
                    with self.lock:
                        self.errors.pop(camera, None)
                except Exception as error:
                    with self.lock:
                        self.errors[camera] = "RETRYING"
                    log.warning("Camera %s delivery retry (%s); durable queue retained", camera, type(error).__name__)
        finally:
            spool.close()

    def start(self):
        def run():
            while not self.stop.is_set():
                try:
                    self.flush_once()
                except Exception as error:
                    log.warning("Garage delivery retry (%s)", type(error).__name__)
                self.stop.wait(2)
        self.sender = threading.Thread(target=run, name="garage-delivery", daemon=True)
        self.sender.start()

    def close(self):
        self.stop.set()
        if self.sender:
            self.sender.join(timeout=12)
