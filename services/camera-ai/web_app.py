"""Local browser interface for FastALPR. Run with the installed virtual environment."""
import base64
import argparse
import os
import json
import threading
import uuid
from pathlib import Path
from time import monotonic
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from time import perf_counter
from urllib.parse import urlsplit, parse_qs

from run_demo import ROOT, ALPR, cv2, ort, read_image
import numpy as np
from recognition import Recognition
from video_pipeline import VideoSession
from recognition import PROVIDERS
from garage_bridge import GarageBridge

if "CUDAExecutionProvider" in PROVIDERS:
    ort.preload_dlls(directory="")
engine = Recognition()
cv2.setNumThreads(2)
lock = threading.Lock()
engine.predict(read_image(ROOT / "assets/test_image.png"), "balanced")
sessions = {}
sessions_lock = threading.Lock()
preview_lock = threading.Lock()
garage = GarageBridge(os.environ.get("CAMERA_AI_BRIDGE_DIRECTORY", ROOT / "work"))
uploads = ROOT / "work" / "video-uploads"
uploads.mkdir(parents=True, exist_ok=True)


def add_session(path, owned=False):
    session = VideoSession(path, owned)
    session.garage_camera = garage.active
    try:
        with preview_lock:session.prepare_preview()
    except Exception:
        session.close()
        raise
    source = uuid.uuid4().hex
    with sessions_lock:
        expired = [key for key,s in sessions.items() if monotonic()-s.touched>1800]
        for key in expired:
            sessions.pop(key).close()
        if len(sessions)>=8:
            session.close()
            raise ValueError('Too many open videos; close another video first')
        sessions[source]=session
    return {'source':source,'preview':f'/video/media/{source}','fps':session.fps,'duration':session.frames/session.fps,'width':session.width,'height':session.height}


def expire_sessions():
    while not shutdown.wait(30):
        with sessions_lock:
            expired=[sessions.pop(key) for key in list(sessions) if monotonic()-sessions[key].touched>1800]
        for session in expired:session.close()


shutdown=threading.Event()


class Handler(BaseHTTPRequestHandler):
    def allowed_origin(self):
        return self.headers.get("Origin") in {
            "http://localhost:5173", "http://127.0.0.1:5173",
            f"http://localhost:{self.server.server_port}", f"http://127.0.0.1:{self.server.server_port}"}

    def do_OPTIONS(self):
        if not self.allowed_origin() or self.path != "/garage/connect":
            self.send(403, b"Forbidden origin", "text/plain")
            return
        self.send_response(204)
        self.send_header("Access-Control-Allow-Origin", self.headers["Origin"])
        self.send_header("Access-Control-Allow-Methods", "POST")
        self.send_header("Access-Control-Allow-Headers", "Content-Type")
        self.send_header("Vary", "Origin")
        self.send_header("Content-Length", "0")
        self.end_headers()

    def send(self, status, data, kind):
        self.send_response(status)
        if self.allowed_origin():
            self.send_header("Access-Control-Allow-Origin", self.headers["Origin"])
            self.send_header("Vary", "Origin")
        self.send_header("Content-Type", kind)
        self.send_header("Content-Length", str(len(data)))
        self.send_header("Cache-Control", "no-store")
        self.end_headers()
        try:
            self.wfile.write(data)
        except (BrokenPipeError, ConnectionResetError, ConnectionAbortedError):
            pass

    def do_GET(self):
        if self.path == "/garage/status":
            self.send(200, json.dumps(garage.status()).encode(), "application/json")
            return
        if self.path == "/health":
            self.send(200, json.dumps({"status": "ok", "service": "camera-ai", "providers": PROVIDERS}).encode(), "application/json")
            return
        if self.path.startswith('/video/media/'):
            with sessions_lock:session=sessions.get(self.path.removeprefix('/video/media/'))
            if session and session.preview_path:
                session.touched=monotonic()
                self.serve_video(session.preview_path)
            else:self.send(404,b'Video closed','text/plain')
            return
        if self.path == "/":
            self.send(200, (ROOT / "web.html").read_bytes(), "text/html; charset=utf-8")
        elif self.path == "/web.js":
            self.send(200, (ROOT / "web.js").read_bytes(), "application/javascript; charset=utf-8")
        elif self.path == "/tracking.js":
            self.send(200, (ROOT / "tracking.js").read_bytes(), "application/javascript; charset=utf-8")
        elif self.path == "/sample":
            self.send(200, (ROOT / "assets/test_image.png").read_bytes(), "image/png")
        elif self.path == "/traffic-sample":
            self.serve_video()
        else:
            self.send(404, b"Not found", "text/plain")

    def serve_video(self, path=None):
        path = path or ROOT / "assets" / "traffic-sample.mp4"
        if not path.exists():
            self.send(404, b"Sample video not found", "text/plain")
            return
        size = path.stat().st_size
        start, end = 0, size - 1
        partial = self.headers.get("Range")
        if partial:
            try:
                unit, value = partial.split("=", 1)
                first, last = value.split("-", 1)
                if unit != "bytes" or "," in value:
                    raise ValueError()
                if first:
                    start = int(first)
                    end = min(int(last), size - 1) if last else size - 1
                else:
                    start = max(0, size - int(last))
                if start < 0 or start >= size or end < start:
                    raise ValueError()
            except ValueError:
                self.send_response(416)
                self.send_header("Content-Range", f"bytes */{size}")
                self.send_header("Content-Length", "0")
                self.end_headers()
                return
        self.send_response(206 if partial else 200)
        self.send_header("Content-Type", "video/mp4")
        self.send_header("Accept-Ranges", "bytes")
        self.send_header("Content-Length", str(end - start + 1))
        if partial:
            self.send_header("Content-Range", f"bytes {start}-{end}/{size}")
        self.end_headers()
        with path.open("rb") as video:
            video.seek(start)
            remaining = end - start + 1
            while remaining:
                chunk = video.read(min(256 * 1024, remaining))
                if not chunk:
                    break
                try:
                    self.wfile.write(chunk)
                except (BrokenPipeError, ConnectionResetError, ConnectionAbortedError):
                    break
                remaining -= len(chunk)

    def do_POST(self):
        request_url = urlsplit(self.path)
        if request_url.path == "/garage/connect":
            if not self.allowed_origin() or self.headers.get_content_type() != "application/json":
                self.send(403, b'{"error":"Forbidden origin"}', "application/json")
                return
            try:
                length = int(self.headers.get("Content-Length", 0))
                if not 0 < length < 4096:
                    raise ValueError("Invalid pairing request")
                result = garage.connect(json.loads(self.rfile.read(length)))
                self.send(200, json.dumps(result).encode(), "application/json")
            except Exception:
                # Do not expose secrets, request bodies or remote exception URLs.
                self.send(400, b'{"error":"Pairing failed; verify Garage is running and camera key is valid"}', "application/json")
            return
        if request_url.path.startswith('/video/'):
            self.video_api(request_url.path)
            return
        if request_url.path != "/recognize":
            self.send(404, b"Not found", "text/plain")
            return
        try:
            length = int(self.headers.get("Content-Length", 0))
            if length <= 0 or length > 15_000_000:
                raise ValueError("Anh qua lon hoac rong (toi da 15 MB).")
            frame = cv2.imdecode(np.frombuffer(self.rfile.read(length), np.uint8), cv2.IMREAD_COLOR)
            if frame is None:
                raise ValueError("Khong doc duoc anh.")
            if frame.shape[0] * frame.shape[1] > 25_000_000:
                raise ValueError("Anh qua lon (toi da 25 megapixel).")
            with lock:
                start = perf_counter()
                mode = parse_qs(request_url.query).get("mode", ["accurate"])[0]
                if mode not in ("fast", "balanced", "accurate", "deep"):
                    raise ValueError("Unknown recognition mode")
                annotated, details = engine.predict(frame, mode)
                ms = (perf_counter() - start) * 1000
            ok, jpg = cv2.imencode(".jpg", annotated)
            if not ok:
                raise ValueError("Khong xuat duoc anh.")
            plates = [r["text"] for r in details if r["text"]]
            for row in details:
                x1, y1, x2, y2 = row["box"]
                crop = frame[max(0, y1):y2, max(0, x1):x2]
                if crop.size:
                    crop_ok, crop_jpg = cv2.imencode(".jpg", crop)
                    if crop_ok:
                        row["crop_image"] = base64.b64encode(crop_jpg).decode()
            payload = {"image": base64.b64encode(jpg).decode(), "plates": plates, "ms": ms, "fps": 1000 / ms,
                       "width": frame.shape[1], "height": frame.shape[0], "details": details, "mode": mode}
            garage.observe(frame, details)
            self.send(200, json.dumps(payload).encode(), "application/json")
        except Exception as exc:
            self.send(400, json.dumps({"error": str(exc)}).encode(), "application/json")

    def video_api(self, route):
        path = None
        try:
            length = int(self.headers.get('Content-Length',0))
            if route == '/video/upload':
                if not 0 < length <= 2*1024**3:
                    raise ValueError('Video phai nho hon 2 GB')
                path = uploads / (uuid.uuid4().hex+'.video')
                remaining = length
                with path.open('wb') as file:
                    while remaining:
                        data = self.rfile.read(min(1024*1024,remaining))
                        if not data:raise ValueError('Incomplete upload')
                        file.write(data);remaining-=len(data)
                result = add_session(path,True)
                path = None
            elif route == '/video/sample':
                result = add_session(ROOT/'assets/traffic-sample.mp4')
            else:
                if not 0 < length < 16384:raise ValueError('Invalid video request')
                data = json.loads(self.rfile.read(length))
                source = data.get('source')
                with sessions_lock:
                    session = sessions.get(source)
                    if route == '/video/close':session=sessions.pop(source,None)
                if route == '/video/close':
                    if session:session.close()
                    result = {'closed':True}
                elif route == '/video/analyze':
                    if not session:raise ValueError('Video da dong. Hay chon lai video.')
                    mode=data.get('mode','accurate')
                    if mode not in ('fast','balanced','accurate','deep'):raise ValueError('Invalid mode')
                    with session.lock:
                        result=session.analyze(engine,lock,float(data.get('time',0)),mode,bool(data.get('force',False)),int(data.get('max_width',1920)),int(data.get('sampling_fps',30)))
                        # Snapshot belongs to the same frame as the returned geometry.
                        frame = session.frame.copy()
                        if session.garage_camera is not None:
                            garage.observe(frame, result['details'], session.garage_camera)
                    if data.get('evidence', False):
                        ok, jpg = cv2.imencode('.jpg', frame, [cv2.IMWRITE_JPEG_QUALITY, 80])
                        if ok:
                            result['snapshot'] = base64.b64encode(jpg).decode()
                        for row in result['details']:
                            x1, y1, x2, y2 = row['box']
                            crop = frame[max(0,y1):y2,max(0,x1):x2]
                            if crop.size:
                                ok, jpg = cv2.imencode('.jpg', crop)
                                if ok: row['crop_image'] = base64.b64encode(jpg).decode()
                else:raise ValueError('Unknown video route')
            self.send(200,json.dumps(result).encode(),'application/json')
        except Exception as exc:
            if path:path.unlink(missing_ok=True)
            self.send(400,json.dumps({'error':str(exc)}).encode(),'application/json')


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description="MyGarage local Camera AI workbench")
    parser.add_argument("--port", type=int, default=7860)
    args = parser.parse_args()
    server=ThreadingHTTPServer(("127.0.0.1", args.port), Handler)
    garage.start()
    print(f"MyGarage Camera AI ready: http://localhost:{args.port}", flush=True)
    threading.Thread(target=expire_sessions,daemon=True).start()
    try:
        server.serve_forever()
    except KeyboardInterrupt:
        pass
    finally:
        shutdown.set();server.server_close()
        garage.close()
        for session in list(sessions.values()):session.close()
