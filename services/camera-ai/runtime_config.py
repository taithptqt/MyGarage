"""Local runtime settings shared by the camera demo and recognition service."""
import os


def execution_providers(available):
    backend = os.environ.get("CAMERA_AI_BACKEND", "cpu").strip().lower()
    if backend == "cpu":
        return ["CPUExecutionProvider"]
    if backend != "cuda":
        raise ValueError("CAMERA_AI_BACKEND must be cpu or cuda")
    if "CUDAExecutionProvider" not in available:
        raise RuntimeError("CUDA backend requested but not installed; use CAMERA_AI_BACKEND=cpu")
    return ["CUDAExecutionProvider", "CPUExecutionProvider"]
