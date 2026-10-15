"""
Windows Credential Manager & Environment Token Resolver for GitHub.
Automatically retrieves the GitHub OAuth token stored in Windows Credential Manager
(`GitHub - https://api.github.com/ronaldtbriggs-blip`) or falls back to `GITHUB_TOKEN` / `GH_TOKEN`
when running inside GitHub Actions or Linux/macOS environments.
"""

import ctypes
from ctypes import wintypes
import os
import sys
from typing import Optional


class FILETIME(ctypes.Structure):
    _fields_ = [
        ("dwLowDateTime", wintypes.DWORD),
        ("dwHighDateTime", wintypes.DWORD),
    ]


class CREDENTIALW(ctypes.Structure):
    _fields_ = [
        ("Flags", wintypes.DWORD),
        ("Type", wintypes.DWORD),
        ("TargetName", wintypes.LPWSTR),
        ("Comment", wintypes.LPWSTR),
        ("LastWritten", FILETIME),
        ("CredentialBlobSize", wintypes.DWORD),
        ("CredentialBlob", ctypes.POINTER(ctypes.c_byte)),
        ("Persist", wintypes.DWORD),
        ("AttributeCount", wintypes.DWORD),
        ("Attributes", ctypes.c_void_p),
        ("TargetAlias", wintypes.LPWSTR),
        ("UserName", wintypes.LPWSTR),
    ]


PCREDENTIALW = ctypes.POINTER(CREDENTIALW)

CRED_TYPE_GENERIC = 1
DEFAULT_TARGETS = [
    "GitHub - https://api.github.com/ronaldtbriggs-blip",
    "git:https://github.com",
]


def read_windows_credential(target_name: str) -> Optional[str]:
    """Reads a generic credential password from Windows Credential Manager."""
    if sys.platform != "win32":
        return None
    try:
        advapi32 = ctypes.windll.advapi32
        cred_read = advapi32.CredReadW
        cred_read.argtypes = [
            wintypes.LPWSTR,
            wintypes.DWORD,
            wintypes.DWORD,
            ctypes.POINTER(PCREDENTIALW),
        ]
        cred_read.restype = wintypes.BOOL

        cred_free = advapi32.CredFree
        cred_free.argtypes = [ctypes.c_void_p]
        cred_free.restype = None

        cred_ptr = PCREDENTIALW()
        if cred_read(target_name, CRED_TYPE_GENERIC, 0, ctypes.byref(cred_ptr)):
            try:
                cred = cred_ptr.contents
                size = cred.CredentialBlobSize
                if size > 0:
                    raw = ctypes.string_at(cred.CredentialBlob, size)
                    try:
                        text = raw.decode("utf-8").rstrip("\x00").strip()
                        if text and "\x00" not in text:
                            return text
                    except UnicodeDecodeError:
                        pass
                    return raw.decode("utf-16-le", errors="ignore").rstrip("\x00").strip()
            finally:
                cred_free(cred_ptr)
    except Exception:
        return None
    return None


def get_github_token() -> str:
    """Resolves the GitHub token from environment variables or Windows Credential Manager."""
    for env_var in ("GITHUB_TOKEN", "GH_TOKEN", "ANTIGRAVITY_GITHUB_TOKEN"):
        val = os.environ.get(env_var, "").strip()
        if val:
            return val

    for target in DEFAULT_TARGETS:
        tok = read_windows_credential(target)
        if tok:
            return tok

    raise RuntimeError(
        "No GitHub token found in GITHUB_TOKEN/GH_TOKEN env vars or Windows Credential Manager."
    )
