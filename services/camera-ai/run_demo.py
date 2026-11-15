"""Run the sample benchmark, or a live webcam/video with --source."""
import argparse
import json
import os
from dataclasses import asdict
from pathlib import Path
from time import perf_counter

ROOT = Path(__file__).resolve().parent
os.environ.setdefault("XDG_CACHE_HOME", str(ROOT / "model-cache"))

import cv2
import onnxruntime as ort
import numpy as np
from fast_alpr import ALPR
from fast_plate_ocr.inference import hub as ocr_hub
from open_image_models.detection.core import hub as detector_hub

ocr_hub.MODEL_CACHE_DIR = ROOT / "model-cache" / "fast-plate-ocr"
detector_hub.MODEL_CACHE_DIR = ROOT / "model-cache" / "open-image-models"


def read_image(path):
    return cv2.imdecode(np.fromfile(path, dtype=np.uint8), cv2.IMREAD_COLOR)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--source", help="Camera index, video path, or RTSP URL")
    parser.add_argument("--cpu", action="store_true")
    parser.add_argument("--runs", type=int, default=50)
    args = parser.parse_args()
    if not args.cpu:
        ort.preload_dlls(directory="")
    providers = ["CPUExecutionProvider"] if args.cpu else ["CUDAExecutionProvider", "CPUExecutionProvider"]
    alpr = ALPR(
        detector_model="yolo-v9-t-384-license-plate-end2end",
        ocr_model="cct-xs-v2-global-model",
        detector_providers=providers,
        ocr_providers=providers,
    )
    sessions = {}
    for name, component in (("detector", alpr.detector), ("ocr", alpr.ocr)):
        for obj in (component, *vars(component).values()):
            if hasattr(obj, "get_providers"):
                sessions[name] = obj.get_providers()
            elif hasattr(obj, "__dict__"):
                for value in vars(obj).values():
                    if hasattr(value, "get_providers"):
                        sessions[name] = value.get_providers()
    print("Active providers:", sessions, flush=True)
    if args.source is not None:
        source = int(args.source) if args.source.isdecimal() else args.source
        cap = cv2.VideoCapture(source)
        if not cap.isOpened():
            raise RuntimeError(f"Cannot open video source: {source}")
        try:
            while True:
                ok, frame = cap.read()
                if not ok:
                    break
                start = perf_counter()
                result = alpr.draw_predictions(frame)
                elapsed = perf_counter() - start
                cv2.putText(result.image, f"Detection + OCR: {1 / elapsed:.1f} FPS", (15, 30), cv2.FONT_HERSHEY_SIMPLEX, .7, (0, 255, 0), 2)
                cv2.imshow("FastALPR - press Q to quit", result.image)
                if cv2.waitKey(1) & 0xFF == ord("q"):
                    break
        finally:
            cap.release()
            cv2.destroyAllWindows()
        return
    frame = read_image(ROOT / "assets" / "test_image.png")
    for _ in range(5):
        alpr.predict(frame)
    times = []
    for _ in range(max(1, args.runs)):
        start = perf_counter()
        results = alpr.predict(frame)
        times.append(perf_counter() - start)
    output = ROOT / "demo-result.png"
    ok, encoded = cv2.imencode(".png", alpr.draw_predictions(frame).image)
    if not ok:
        raise RuntimeError("Cannot encode result image")
    encoded.tofile(output)
    report = {
        "providers": sessions,
        "image_size": [frame.shape[1], frame.shape[0]],
        "runs": len(times),
        "mean_ms": 1000 * sum(times) / len(times),
        "inference_fps": len(times) / sum(times),
        "scope": "Detection + OCR on repeated sample image; excludes camera capture, display and disk I/O",
        "results": [asdict(result) for result in results],
    }
    (ROOT / "benchmark.json").write_text(json.dumps(report, indent=2), encoding="utf-8")
    print(json.dumps(report, indent=2), flush=True)
    print("Saved image:", output, flush=True)


if __name__ == "__main__":
    main()
