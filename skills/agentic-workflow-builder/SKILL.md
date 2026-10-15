---
name: agentic-workflow-builder
description: Design, build, and deploy autonomous GitHub Actions workflows and Google Antigravity SDK multi-agent modding pipelines.
---

# Agentic Workflow Builder Skill (`agentic-workflow-builder`)

Use this skill when building new autonomous modding or CI workflows in `.github/workflows/` or extending the multi-agent orchestrator in `agents/antigravity_orchestrator.py`.

## Workflow Architecture Patterns

1. **On-Demand Modder Dispatch (`workflow_dispatch`)**:
   - Expose structured `inputs` (`task`, `mode`, `game_query`) so workflows can be triggered from the GitHub UI, the `github-agentic` MCP tool `trigger_workflow`, or `python tools/gh_cli.py dispatch`.
2. **Event-Driven Issue / PR Automation (`issues`, `issue_comment`, `pull_request`)**:
   - Filter on `@antigravity`, `/agent`, `/mod`, or the `agent` / `mod-request` labels to prevent infinite bot loops (`github.event.comment.user.type != 'Bot'`).
3. **Scheduled Autonomous Pulse (`schedule`)**:
   - Run daily validation of the Universal Modder Knowledge Base (`um kb check --index`) and pre-release security linting (`um publish check .`).
4. **Google Antigravity SDK Multi-Agent Delegation (`agents/antigravity_orchestrator.py`)**:
   - Combine `LocalAgentConfig`, custom `um` Python tools, and `types.SubagentConfig` (`mod_offline_worker`, `mod_game_worker`) to delegate reverse-engineering, asset generation, and live verification.
