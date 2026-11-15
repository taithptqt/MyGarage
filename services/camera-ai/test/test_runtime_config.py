"""Explicit provider selection must work on a CPU-only garage workstation."""
import pytest

from runtime_config import execution_providers


def test_cpu_default_even_when_cuda_is_installed(monkeypatch):
    monkeypatch.delenv("CAMERA_AI_BACKEND", raising=False)
    assert execution_providers(["CUDAExecutionProvider", "CPUExecutionProvider"]) == [
        "CPUExecutionProvider"
    ]


def test_cuda_requires_installed_provider(monkeypatch):
    monkeypatch.setenv("CAMERA_AI_BACKEND", "cuda")
    with pytest.raises(RuntimeError, match="not installed"):
        execution_providers(["CPUExecutionProvider"])
    assert execution_providers(["CUDAExecutionProvider", "CPUExecutionProvider"]) == [
        "CUDAExecutionProvider", "CPUExecutionProvider"
    ]


def test_invalid_backend_is_rejected(monkeypatch):
    monkeypatch.setenv("CAMERA_AI_BACKEND", "invalid")
    with pytest.raises(ValueError, match="cpu or cuda"):
        execution_providers(["CPUExecutionProvider"])
