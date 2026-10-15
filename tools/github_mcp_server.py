"""
GitHub MCP Stdio Server for Antigravity IDE / Antigravity 2.0.
Exposes GitHub repository, file, issue, pull request, and GitHub Actions workflow tools
via the Model Context Protocol (JSON-RPC 2.0 over stdio).
"""

import base64
import json
import os
import sys
import time
import urllib.error
import urllib.parse
import urllib.request
from typing import Any, Dict, List, Optional

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from win_cred import get_github_token

sys.stdin.reconfigure(encoding="utf-8")
sys.stdout.reconfigure(encoding="utf-8")

DEFAULT_OWNER = "ronaldtbriggs-blip"


def log(msg: str) -> None:
    sys.stderr.write(f"[{time.strftime('%Y-%m-%d %H:%M:%S')}] [GitHub-MCP] {msg}\n")
    sys.stderr.flush()


class GitHubClient:
    def __init__(self, default_owner: str = DEFAULT_OWNER):
        self.base_url = "https://api.github.com"
        self.default_owner = default_owner

    def _headers(self) -> Dict[str, str]:
        token = get_github_token()
        return {
            "Authorization": f"Bearer {token}",
            "Accept": "application/vnd.github+json",
            "X-GitHub-Api-Version": "2022-11-28",
            "User-Agent": "Antigravity-GitHub-MCP/1.0",
            "Content-Type": "application/json",
        }

    def request(
        self,
        method: str,
        path: str,
        payload: Optional[Dict[str, Any]] = None,
        params: Optional[Dict[str, Any]] = None,
    ) -> Any:
        url = f"{self.base_url}{path}"
        if params:
            filtered = {k: v for k, v in params.items() if v is not None}
            if filtered:
                url = f"{url}?{urllib.parse.urlencode(filtered)}"

        data = json.dumps(payload).encode("utf-8") if payload is not None else None
        req = urllib.request.Request(url, data=data, headers=self._headers(), method=method)
        try:
            with urllib.request.urlopen(req, timeout=30) as resp:
                raw = resp.read().decode("utf-8")
                if not raw.strip():
                    return {"status": resp.status, "ok": True}
                return json.loads(raw)
        except urllib.error.HTTPError as e:
            err_body = e.read().decode("utf-8", errors="replace")
            raise RuntimeError(f"GitHub API HTTP {e.code} ({method} {path}): {err_body}") from e


TOOLS_SCHEMA: List[Dict[str, Any]] = [
    {
        "name": "list_repos",
        "description": "List repositories for the authenticated GitHub user.",
        "inputSchema": {
            "type": "object",
            "properties": {
                "visibility": {
                    "type": "string",
                    "description": "Filter by visibility: all, public, or private (default: all).",
                },
                "limit": {
                    "type": "integer",
                    "description": "Maximum number of repositories to return (default: 30).",
                },
            },
        },
    },
    {
        "name": "create_repo",
        "description": "Create a new GitHub repository under the authenticated user's account.",
        "inputSchema": {
            "type": "object",
            "properties": {
                "name": {"type": "string", "description": "Repository name."},
                "description": {"type": "string", "description": "Repository description."},
                "private": {
                    "type": "boolean",
                    "description": "Whether the repository is private (default: false).",
                },
                "auto_init": {
                    "type": "boolean",
                    "description": "Initialize with an initial commit/README (default: false).",
                },
            },
            "required": ["name"],
        },
    },
    {
        "name": "get_file_contents",
        "description": "Read a file or directory listing from a GitHub repository.",
        "inputSchema": {
            "type": "object",
            "properties": {
                "repo": {"type": "string", "description": "Repository name (or owner/repo)."},
                "path": {"type": "string", "description": "Path inside the repository."},
                "ref": {"type": "string", "description": "Branch, tag, or commit SHA (optional)."},
            },
            "required": ["repo", "path"],
        },
    },
    {
        "name": "create_or_update_file",
        "description": "Create or update a file in a GitHub repository on a given branch.",
        "inputSchema": {
            "type": "object",
            "properties": {
                "repo": {"type": "string", "description": "Repository name (or owner/repo)."},
                "path": {"type": "string", "description": "File path inside the repository."},
                "content": {"type": "string", "description": "UTF-8 text content of the file."},
                "message": {"type": "string", "description": "Git commit message."},
                "branch": {"type": "string", "description": "Target branch (default: main)."},
            },
            "required": ["repo", "path", "content", "message"],
        },
    },
    {
        "name": "list_issues",
        "description": "List issues in a GitHub repository.",
        "inputSchema": {
            "type": "object",
            "properties": {
                "repo": {"type": "string", "description": "Repository name (or owner/repo)."},
                "state": {"type": "string", "description": "open, closed, or all (default: open)."},
            },
            "required": ["repo"],
        },
    },
    {
        "name": "create_issue",
        "description": "Create an issue in a GitHub repository.",
        "inputSchema": {
            "type": "object",
            "properties": {
                "repo": {"type": "string", "description": "Repository name (or owner/repo)."},
                "title": {"type": "string", "description": "Issue title."},
                "body": {"type": "string", "description": "Issue markdown body."},
                "labels": {
                    "type": "array",
                    "items": {"type": "string"},
                    "description": "Optional labels to attach (e.g. ['agent']).",
                },
            },
            "required": ["repo", "title"],
        },
    },
    {
        "name": "comment_on_issue",
        "description": "Post a comment on a GitHub issue or pull request.",
        "inputSchema": {
            "type": "object",
            "properties": {
                "repo": {"type": "string", "description": "Repository name (or owner/repo)."},
                "issue_number": {"type": "integer", "description": "Issue or PR number."},
                "body": {"type": "string", "description": "Markdown comment body."},
            },
            "required": ["repo", "issue_number", "body"],
        },
    },
    {
        "name": "create_pull_request",
        "description": "Create a pull request in a GitHub repository.",
        "inputSchema": {
            "type": "object",
            "properties": {
                "repo": {"type": "string", "description": "Repository name (or owner/repo)."},
                "title": {"type": "string", "description": "Pull request title."},
                "head": {"type": "string", "description": "Source branch name."},
                "base": {"type": "string", "description": "Target branch name (default: main)."},
                "body": {"type": "string", "description": "Pull request description."},
            },
            "required": ["repo", "title", "head"],
        },
    },
    {
        "name": "list_workflows",
        "description": "List GitHub Actions workflows in a repository.",
        "inputSchema": {
            "type": "object",
            "properties": {
                "repo": {"type": "string", "description": "Repository name (or owner/repo)."},
            },
            "required": ["repo"],
        },
    },
    {
        "name": "trigger_workflow",
        "description": "Trigger a workflow_dispatch GitHub Actions workflow run with custom inputs.",
        "inputSchema": {
            "type": "object",
            "properties": {
                "repo": {"type": "string", "description": "Repository name (or owner/repo)."},
                "workflow_id": {
                    "type": "string",
                    "description": "Workflow filename (e.g. 'agent-dispatch.yml') or numeric ID.",
                },
                "ref": {"type": "string", "description": "Git branch or tag ref (default: main)."},
                "inputs": {
                    "type": "object",
                    "description": "Workflow input key-value pairs (e.g. {'task': 'Audit repo'}).",
                },
            },
            "required": ["repo", "workflow_id"],
        },
    },
    {
        "name": "list_workflow_runs",
        "description": "List recent GitHub Actions workflow runs for a repository.",
        "inputSchema": {
            "type": "object",
            "properties": {
                "repo": {"type": "string", "description": "Repository name (or owner/repo)."},
                "limit": {"type": "integer", "description": "Number of runs to return (default: 10)."},
            },
            "required": ["repo"],
        },
    },
]


def resolve_repo(repo_arg: str, default_owner: str = DEFAULT_OWNER) -> str:
    if "/" in repo_arg:
        return repo_arg
    return f"{default_owner}/{repo_arg}"


def execute_tool(client: GitHubClient, name: str, args: Dict[str, Any]) -> Any:
    if name == "list_repos":
        limit = int(args.get("limit", 30))
        vis = args.get("visibility", "all")
        repos = client.request(
            "GET",
            "/user/repos",
            params={"visibility": vis, "per_page": min(limit, 100), "sort": "updated"},
        )
        return [
            {
                "full_name": r["full_name"],
                "private": r["private"],
                "description": r.get("description"),
                "html_url": r["html_url"],
                "default_branch": r.get("default_branch"),
            }
            for r in repos[:limit]
        ]

    if name == "create_repo":
        payload = {
            "name": args["name"],
            "description": args.get("description", ""),
            "private": bool(args.get("private", False)),
            "auto_init": bool(args.get("auto_init", False)),
        }
        r = client.request("POST", "/user/repos", payload=payload)
        return {
            "full_name": r["full_name"],
            "private": r["private"],
            "html_url": r["html_url"],
            "clone_url": r["clone_url"],
        }

    if name == "get_file_contents":
        full_repo = resolve_repo(args["repo"])
        path = args["path"].lstrip("/")
        params = {"ref": args.get("ref")} if args.get("ref") else None
        res = client.request("GET", f"/repos/{full_repo}/contents/{path}", params=params)
        if isinstance(res, dict) and res.get("encoding") == "base64" and "content" in res:
            res["decoded_content"] = base64.b64decode(res["content"]).decode("utf-8", errors="replace")
            del res["content"]
        return res

    if name == "create_or_update_file":
        full_repo = resolve_repo(args["repo"])
        path = args["path"].lstrip("/")
        branch = args.get("branch", "main")
        sha = None
        try:
            existing = client.request(
                "GET", f"/repos/{full_repo}/contents/{path}", params={"ref": branch}
            )
            if isinstance(existing, dict):
                sha = existing.get("sha")
        except Exception:
            pass
        payload: Dict[str, Any] = {
            "message": args["message"],
            "content": base64.b64encode(args["content"].encode("utf-8")).decode("ascii"),
            "branch": branch,
        }
        if sha:
            payload["sha"] = sha
        res = client.request("PUT", f"/repos/{full_repo}/contents/{path}", payload=payload)
        return {
            "path": res.get("content", {}).get("path", path),
            "commit_sha": res.get("commit", {}).get("sha"),
            "html_url": res.get("content", {}).get("html_url"),
        }

    if name == "list_issues":
        full_repo = resolve_repo(args["repo"])
        state = args.get("state", "open")
        issues = client.request(
            "GET", f"/repos/{full_repo}/issues", params={"state": state, "per_page": 30}
        )
        return [
            {
                "number": i["number"],
                "title": i["title"],
                "state": i["state"],
                "labels": [lbl["name"] for lbl in i.get("labels", [])],
                "html_url": i["html_url"],
            }
            for i in issues
        ]

    if name == "create_issue":
        full_repo = resolve_repo(args["repo"])
        payload = {
            "title": args["title"],
            "body": args.get("body", ""),
            "labels": args.get("labels", []),
        }
        i = client.request("POST", f"/repos/{full_repo}/issues", payload=payload)
        return {"number": i["number"], "title": i["title"], "html_url": i["html_url"]}

    if name == "comment_on_issue":
        full_repo = resolve_repo(args["repo"])
        num = int(args["issue_number"])
        c = client.request(
            "POST", f"/repos/{full_repo}/issues/{num}/comments", payload={"body": args["body"]}
        )
        return {"id": c["id"], "html_url": c["html_url"]}

    if name == "create_pull_request":
        full_repo = resolve_repo(args["repo"])
        payload = {
            "title": args["title"],
            "head": args["head"],
            "base": args.get("base", "main"),
            "body": args.get("body", ""),
        }
        pr = client.request("POST", f"/repos/{full_repo}/pulls", payload=payload)
        return {"number": pr["number"], "title": pr["title"], "html_url": pr["html_url"]}

    if name == "list_workflows":
        full_repo = resolve_repo(args["repo"])
        res = client.request("GET", f"/repos/{full_repo}/actions/workflows")
        return [
            {
                "id": w["id"],
                "name": w["name"],
                "path": w["path"],
                "state": w["state"],
                "html_url": w["html_url"],
            }
            for w in res.get("workflows", [])
        ]

    if name == "trigger_workflow":
        full_repo = resolve_repo(args["repo"])
        wf_id = args["workflow_id"]
        payload = {
            "ref": args.get("ref", "main"),
            "inputs": args.get("inputs", {}),
        }
        client.request(
            "POST", f"/repos/{full_repo}/actions/workflows/{wf_id}/dispatches", payload=payload
        )
        return {
            "dispatched": True,
            "repo": full_repo,
            "workflow_id": wf_id,
            "ref": payload["ref"],
            "inputs": payload["inputs"],
        }

    if name == "list_workflow_runs":
        full_repo = resolve_repo(args["repo"])
        limit = int(args.get("limit", 10))
        res = client.request(
            "GET", f"/repos/{full_repo}/actions/runs", params={"per_page": min(limit, 50)}
        )
        return [
            {
                "id": r["id"],
                "name": r.get("name"),
                "event": r.get("event"),
                "status": r.get("status"),
                "conclusion": r.get("conclusion"),
                "created_at": r.get("created_at"),
                "html_url": r.get("html_url"),
            }
            for r in res.get("workflow_runs", [])[:limit]
        ]

    raise ValueError(f"Unknown tool: {name}")


def main() -> None:
    client = GitHubClient()
    log("GitHub MCP Stdio Server started.")

    for line in sys.stdin:
        line = line.strip()
        if not line:
            continue
        try:
            req = json.loads(line)
        except Exception as e:
            log(f"Invalid JSON: {e}")
            continue

        method = req.get("method")
        msg_id = req.get("id")

        if method == "initialize":
            resp = {
                "jsonrpc": "2.0",
                "id": msg_id,
                "result": {
                    "protocolVersion": req.get("params", {}).get("protocolVersion", "2024-11-05"),
                    "capabilities": {"tools": {}},
                    "serverInfo": {"name": "github-agentic", "version": "1.0.0"},
                },
            }
            sys.stdout.write(json.dumps(resp) + "\n")
            sys.stdout.flush()
        elif method == "notifications/initialized":
            continue
        elif method == "ping":
            sys.stdout.write(json.dumps({"jsonrpc": "2.0", "id": msg_id, "result": {}}) + "\n")
            sys.stdout.flush()
        elif method == "tools/list":
            resp = {
                "jsonrpc": "2.0",
                "id": msg_id,
                "result": {"tools": TOOLS_SCHEMA},
            }
            sys.stdout.write(json.dumps(resp) + "\n")
            sys.stdout.flush()
        elif method == "tools/call":
            params = req.get("params", {})
            tool_name = params.get("name", "")
            tool_args = params.get("arguments", {}) or {}
            try:
                output = execute_tool(client, tool_name, tool_args)
                resp = {
                    "jsonrpc": "2.0",
                    "id": msg_id,
                    "result": {
                        "content": [
                            {"type": "text", "text": json.dumps(output, indent=2)}
                        ],
                        "isError": False,
                    },
                }
            except Exception as e:
                resp = {
                    "jsonrpc": "2.0",
                    "id": msg_id,
                    "result": {
                        "content": [{"type": "text", "text": f"Error: {e}"}],
                        "isError": True,
                    },
                }
            sys.stdout.write(json.dumps(resp) + "\n")
            sys.stdout.flush()
        else:
            if msg_id is not None:
                resp = {
                    "jsonrpc": "2.0",
                    "id": msg_id,
                    "error": {"code": -32601, "message": f"Method not found: {method}"},
                }
                sys.stdout.write(json.dumps(resp) + "\n")
                sys.stdout.flush()


if __name__ == "__main__":
    main()
