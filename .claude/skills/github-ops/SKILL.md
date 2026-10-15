---
name: github-ops
description: Manage GitHub repositories, issues, pull requests, files, and GitHub Actions workflows for ronaldtbriggs-blip/universal-modder using the local GitHub MCP server and CLI tools.
---

# GitHub Operations Skill (`github-ops`)

Use this skill whenever the user asks to create or inspect GitHub repositories, open modding issues or pull requests, push files across repositories, or trigger/monitor GitHub Actions workflows in `ronaldtbriggs-blip/universal-modder`.

## 1. Available Tools

### A. Native MCP Server (`github-agentic`)
Backed by `tools/github_mcp_server.py`:
- `list_repos`: List public and private repositories on `ronaldtbriggs-blip`.
- `create_repo`: Create a new GitHub repository.
- `get_file_contents` / `create_or_update_file`: Read or commit files directly via the GitHub API.
- `list_issues` / `create_issue` / `comment_on_issue`: Manage modding issues and post agent updates.
- `create_pull_request`: Open pull requests across branches.
- `list_workflows` / `trigger_workflow` / `list_workflow_runs`: Inspect and dispatch GitHub Actions workflows.

### B. CLI Utility (`tools/gh_cli.py`)
Run directly from PowerShell or bash:
```powershell
python tools/gh_cli.py list-repos
python tools/gh_cli.py list-workflows --repo universal-modder
python tools/gh_cli.py dispatch --repo universal-modder --workflow agent-dispatch.yml --task "<modding or recon task>"
python tools/gh_cli.py list-runs --repo universal-modder
```
