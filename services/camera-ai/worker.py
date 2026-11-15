"""Multi-camera RTSP worker for authenticated MyGarage observation ingestion."""
import argparse
import base64
import json
import logging
import os
import re
import signal
import ssl
import threading
import time
import uuid
from datetime import datetime, timezone
from pathlib import Path
from urllib.parse import urlparse
from urllib.request import Request, urlopen
from urllib.error import HTTPError

os.environ.setdefault("OPENCV_LOG_LEVEL", "SILENT")
os.environ.setdefault("OPENCV_FFMPEG_LOGLEVEL", "-8")

from event_spool import EventSpool
from recognition import Recognition, PROVIDERS
from run_demo import ROOT, cv2, ort
from video_pipeline import PlateCache

log = logging.getLogger("camera-worker")
stop = threading.Event()


def api(base, camera, key, route, payload=None):
    url = f"{base.rstrip('/')}/api/v1/camera-ai/devices/{camera}/{route}"
    body = json.dumps(payload).encode() if payload is not None else None
    request = Request(url, data=body, headers={"X-Camera-Key": key, "Content-Type": "application/json"})
    # System trust store or a configured CA file; never disable TLS verification.
    context = ssl.create_default_context(cafile=os.environ.get("CAMERA_AI_CA_FILE") or None)
    with urlopen(request, timeout=15, context=context) as response:
        content = response.read()
        return json.loads(content) if content else None


def jpeg(frame, max_width=1280):
    if frame is None or frame.size == 0:
        return None
    if frame.shape[1] > max_width:
        frame = cv2.resize(frame, (max_width, round(frame.shape[0] * max_width / frame.shape[1])))
    ok, data = cv2.imencode(".jpg", frame, [cv2.IMWRITE_JPEG_QUALITY, 80])
    if not ok or len(data) > 2_000_000:
        return None
    return base64.b64encode(data).decode()


def deliver(base, camera, key, spool):
    # Bound each flush so capture/heartbeat still run when the backlog is large.
    for _ in range(5):
        if stop.is_set():
            break
        pending = spool.first(camera)
        if not pending:
            break
        event_id, payload = pending
        api(base, camera, key, "observations", payload)
        spool.acknowledge(event_id)


def run_camera(base, device):
    camera = int(device["cameraId"])
    key = os.environ.get(device["keyEnvironmentVariable"], "")
    if not key:
        log.error("Camera %s has no worker credential in its configured environment variable", camera)
        return
    spool_path = ROOT / "work" / f"camera-{camera}.sqlite3"
    spool = EventSpool(spool_path)
    instance = uuid.uuid4().hex
    engine, tracking = Recognition(), PlateCache()
    cap = None
    shared = {"configuration": None, "health": "RECONNECTING"}

    def sender():
        queue = EventSpool(spool_path)
        last_config, last_heartbeat = 0, 0
        try:
            while not stop.is_set():
                try:
                    now = time.monotonic()
                    if shared["configuration"] is None or now-last_config > 30:
                        shared["configuration"] = api(base, camera, key, "configuration")
                        last_config = now
                    deliver(base, camera, key, queue)
                    if now-last_heartbeat > 10:
                        api(base, camera, key, "heartbeat", {"status": shared["health"]})
                        last_heartbeat = now
                    stop.wait(1)
                except Exception as error:
                    if isinstance(error, HTTPError) and error.code in (401,403):
                        shared["configuration"] = None
                    log.warning("Camera %s delivery retry after %s; events retained", camera, type(error).__name__)
                    stop.wait(5)
        finally:
            queue.close()

    delivery = threading.Thread(target=sender, name=f"camera-{camera}-delivery")
    delivery.start()
    configuration = None
    stable = {}
    try:
        while not stop.is_set():
            try:
                now = time.monotonic()
                updated = shared["configuration"]
                if updated is None:
                    if cap: cap.release(); cap = None
                    stop.wait(1)
                    continue
                if configuration and updated["streamUrl"] != configuration["streamUrl"] and cap:
                    cap.release(); cap = None
                configuration = updated
                if cap is None:
                    if configuration["streamUrl"].startswith("localvideo:"):
                        cap = cv2.VideoCapture(str(device.get("localVideo") or ROOT / "assets" / "traffic-sample.mp4"))
                    else:
                        cap = cv2.VideoCapture()
                        cap.open(configuration["streamUrl"], cv2.CAP_FFMPEG,
                                 [cv2.CAP_PROP_OPEN_TIMEOUT_MSEC, 5000, cv2.CAP_PROP_READ_TIMEOUT_MSEC, 5000])
                ok, frame = cap.read() if cap.isOpened() else (False, None)
                if not ok:
                    if configuration["streamUrl"].startswith("localvideo:") and cap.isOpened():
                        shared["health"] = "VIDEO_FINISHED"
                        stop.wait(1)
                        continue
                    shared["health"] = "RECONNECTING"
                    cap.release(); cap = None; stable.clear()
                    stop.wait(3)
                    continue
                shared["health"] = "ONLINE"
                rows, _, _ = tracking.process(engine, frame, "balanced", now)
                visible = set()
                for row in rows:
                    plate = re.sub("[^A-Z0-9]", "", row["text"].upper()) or "UNKNOWN"
                    visible.add(plate)
                    count = stable.get(plate, 0)+1
                    stable[plate] = count
                    if count < 3:
                        continue
                    x1,y1,x2,y2 = row["box"]
                    payload = {"eventKey": str(uuid.uuid4()), "workerInstance": instance,
                               "plate": plate, "confidence": min(row["detection_confidence"], row["ocr_confidence"]),
                               "observedAt": datetime.now(timezone.utc).isoformat(),
                               "snapshotBase64": jpeg(frame), "cropBase64": jpeg(frame[max(0,y1):y2,max(0,x1):x2], 640)}
                    spool.observe(camera, plate, payload)
                stable = {plate: count for plate, count in stable.items() if plate in visible}
                stop.wait(.1)
            except Exception as error:
                # Do not log URLs, request payloads or credentials from exception text.
                log.warning("Camera %s retrying after %s; queued events retained", camera, type(error).__name__)
                if cap: cap.release(); cap = None
                shared["health"] = "ERROR"
                stop.wait(3)
    finally:
        if cap: cap.release()
        stop.set()
        delivery.join(timeout=20)
        spool.close()


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--config", required=True)
    args = parser.parse_args()
    settings = json.loads(Path(args.config).read_text(encoding="utf-8-sig"))
    base = settings["myGarageUrl"]
    parsed = urlparse(base)
    if parsed.scheme != "https" and not (parsed.scheme == "http" and parsed.hostname in ("localhost", "127.0.0.1")):
        raise ValueError("MyGarage requires HTTPS except loopback development")
    devices = settings["cameras"]
    ids = [int(item["cameraId"]) for item in devices]
    if len(ids) != len(set(ids)):
        raise ValueError("Duplicate camera IDs in worker configuration")
    logging.basicConfig(level=logging.INFO, format="%(asctime)s %(levelname)s %(message)s")
    if "CUDAExecutionProvider" in PROVIDERS: ort.preload_dlls(directory="")
    signal.signal(signal.SIGINT, lambda *_: stop.set())
    signal.signal(signal.SIGTERM, lambda *_: stop.set())
    threads = [threading.Thread(target=run_camera, args=(base, item), name=f"camera-{item['cameraId']}") for item in devices]
    for thread in threads: thread.start()
    for thread in threads: thread.join()


if __name__ == "__main__": main()
