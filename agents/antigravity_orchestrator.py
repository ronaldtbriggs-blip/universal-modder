"""
Google Antigravity SDK Multi-Agent Modding & GitHub Orchestrator for `universal-modder`.
Combines the Universal Modder 3-worker architecture (`mod-orchestrator`, `mod-offline-worker`,
`mod-game-worker`) with native `um` Python tools and GitHub Agentic workflow automation.

Usage:
  python agents/antigravity_orchestrator.py "Scan my installed games and plan a mod"
"""

import argparse
import asyncio
import json
import os
import pathlib
import sys
from typing import List, Optional

ROOT_DIR = pathlib.Path(__file__).resolve().parents[1]
if str(ROOT_DIR) not in sys.path:
    sys.path.insert(0, str(ROOT_DIR))
sys.path.insert(0, str(ROOT_DIR / "tools"))

from um import backup, kb, publish, scan  # noqa: E402
from github_mcp_server import GitHubClient, execute_tool  # noqa: E402

_gh_client = GitHubClient()


# --- Universal Modder Tools Exposed to the Antigravity Agent ---

def um_scan_installed_games(query: str = "") -> str:
    """Scans installed PC games (Steam, Epic, Xbox) or fingerprints a specific game install/name.

    Args:
        query: Optional game name or install folder path to scan in detail. Leave empty to list all installed games.
    """
    if not query.strip():
        return json.dumps(scan.all_games(), indent=2)
    return json.dumps(scan.scan(query.strip()), indent=2)


def um_kb_search(query: str, game: str = "", engine: str = "", route: str = "") -> str:
    """Searches the Universal Modder Knowledge Base of reverse-engineering & modding field notes.

    Args:
        query: Space-separated search terms (e.g. 'bepinex unity il2cpp' or 'tmodloader').
        game: Optional game filter.
        engine: Optional engine filter.
        route: Optional modding route filter.
    """
    terms = [t for t in query.split() if t]
    hits = kb.search(
        ROOT_DIR / "knowledge",
        terms,
        game=game or None,
        engine=engine or None,
        route=route or None,
    )
    return json.dumps(hits[:15], indent=2)


def um_kb_read_note(note_path: str) -> str:
    """Reads a field note from the Universal Modder Knowledge Base.

    Args:
        note_path: Relative path under `knowledge/` (e.g. 'games/terraria/tmodloader-custom-weapons.md').
    """
    target = (ROOT_DIR / "knowledge" / note_path.lstrip("/")).resolve()
    if not target.is_file():
        return json.dumps({"error": f"Note not found: {note_path}"})
    return target.read_text(encoding="utf-8", errors="replace")


def um_backup_saves(save_dir: str, name: str, note: str = "pre-mod snapshot") -> str:
    """Creates a timestamped zip backup of a game's save directory before launching a modded build.

    Args:
        save_dir: Path to the save directory.
        name: Short slug for the game backup set.
        note: Label describing the snapshot.
    """
    snap = backup.create(save_dir, name=name, note=note)
    return json.dumps({"snapshot_zip": str(snap), "name": name, "note": note}, indent=2)


def um_publish_lint(mod_folder: str, game_folder: str = "") -> str:
    """Runs `um publish check` on a mod directory to ensure no game binaries, decompiles, or secrets are shipped.

    Args:
        mod_folder: Path to the mod directory to lint.
        game_folder: Optional path to the game install folder to check for verbatim copied files.
    """
    code = publish.check(mod_folder, game_folder or None)
    return json.dumps({"exit_code": code, "passed": code == 0}, indent=2)


# --- GitHub Automation Tools Exposed to the Antigravity Agent ---

def github_dispatch_mod_workflow(
    task: str,
    game_query: str = "",
    mode: str = "report-issue",
    repo: str = "universal-modder",
) -> str:
    """Dispatches the GitHub Actions `agent-dispatch.yml` workflow on `ronaldtbriggs-blip/universal-modder`.

    Args:
        task: Natural-language modding, recon, or audit task.
        game_query: Optional game/engine search query for the KB.
        mode: 'report-issue' or 'summary-only'.
        repo: Target repository name (default: 'universal-modder').
    """
    return json.dumps(
        execute_tool(
            _gh_client,
            "trigger_workflow",
            {
                "repo": repo,
                "workflow_id": "agent-dispatch.yml",
                "ref": "main",
                "inputs": {"task": task, "mode": mode, "game_query": game_query},
            },
        ),
        indent=2,
    )


def github_create_mod_issue(
    title: str, body: str, labels: Optional[List[str]] = None, repo: str = "universal-modder"
) -> str:
    """Creates a modding tracking issue or field-note draft in `ronaldtbriggs-blip/universal-modder`.

    Args:
        title: Issue title.
        body: Markdown body.
        labels: Optional list of labels (e.g. ['agent', 'mod-request']).
        repo: Repository name (default: 'universal-modder').
    """
    return json.dumps(
        execute_tool(
            _gh_client,
            "create_issue",
            {"repo": repo, "title": title, "body": body, "labels": labels or ["agent"]},
        ),
        indent=2,
    )


def build_agent_config():
    """Builds the Google Antigravity SDK `LocalAgentConfig` with Universal Modder subagents and tools."""
    from google.antigravity import LocalAgentConfig, types

    mod_offline_worker = types.SubagentConfig(
        name="mod_offline_worker",
        description=(
            "Offline reverse-engineering & mod architecture worker. Studies engine playbooks, "
            "SDKs, decompiles, and knowledge base notes, and writes mod source code, patches, and asset scripts."
        ),
        capabilities=types.SubagentCapabilities(
            agent_behavior=types.AgentBehavior.AUTONOMOUS,
            enabled_tools=[
                types.BuiltinTools.VIEW_FILE,
                types.BuiltinTools.WRITE_TO_FILE,
                types.BuiltinTools.REPLACE_FILE_CONTENT,
            ],
        ),
    )

    mod_game_worker = types.SubagentConfig(
        name="mod_game_worker",
        description=(
            "Live game verification & automation worker. Manages save backups, builds/deploys the mod, "
            "verifies logs/screenshots, and runs pre-publish checks."
        ),
        capabilities=types.SubagentCapabilities(
            agent_behavior=types.AgentBehavior.AUTONOMOUS,
            enabled_tools=[
                types.BuiltinTools.VIEW_FILE,
                types.BuiltinTools.RUN_COMMAND,
            ],
        ),
    )

    return LocalAgentConfig(
        system_instructions=(
            "You are the Antigravity Universal Modder Orchestrator (`mod-orchestrator`) for ronaldtbriggs-blip. "
            "Follow the Universal Modder loop (`skills/mod-any-game/SKILL.md`): "
            "1) Search the knowledge base (`um_kb_search`), "
            "2) Recon the game & pick the cleanest modding route (`um_scan_installed_games`), "
            "3) Back up saves before any modded launch (`um_backup_saves`), "
            "4) Delegate offline reverse-engineering & code authoring to `mod_offline_worker` and "
            "live verification to `mod_game_worker`, "
            "5) Lint with `um_publish_lint` before publishing."
        ),
        tools=[
            um_scan_installed_games,
            um_kb_search,
            um_kb_read_note,
            um_backup_saves,
            um_publish_lint,
            github_dispatch_mod_workflow,
            github_create_mod_issue,
        ],
        subagents=[mod_offline_worker, mod_game_worker],
        capabilities=types.CapabilitiesConfig(
            enable_subagents=True,
            max_subagent_depth=2,
        ),
    )


async def run_prompt(prompt: str) -> None:
    from google.antigravity import Agent

    config = build_agent_config()
    async with Agent(config) as agent:
        response = await agent.chat(prompt)
        async for chunk in response:
            print(chunk, end="", flush=True)
        print()


def main() -> None:
    parser = argparse.ArgumentParser(
        description="Run the Antigravity Universal Modder Multi-Agent Orchestrator"
    )
    parser.add_argument("prompt", help="Modding goal or task prompt for the orchestrator")
    args = parser.parse_args()
    asyncio.run(run_prompt(args.prompt))


if __name__ == "__main__":
    main()
