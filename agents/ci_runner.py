"""
Headless Agent Runner for Universal Modder GitHub Actions CI/CD Workflows.
Supports:
1. On-demand `workflow_dispatch` modding, recon, and KB tasks (`--event dispatch`)
2. Issue & `/agent`, `/mod`, or `@antigravity` comment triggers (`--event issue`)
3. Scheduled knowledge-base & publish-readiness audits (`--event schedule`)

Integrates directly with the `um` toolkit (`um.kb`, `um.publish`, `um.scan`) and uses
`GEMINI_API_KEY` when configured in GitHub Actions Secrets to synthesize deep modding plans,
with automatic deterministic fallback so workflows always succeed out-of-the-box.
"""

import argparse
import datetime
import json
import os
import pathlib
import sys
import urllib.error
import urllib.request
from typing import Any, Dict, List, Optional

ROOT_DIR = pathlib.Path(__file__).resolve().parents[1]
if str(ROOT_DIR) not in sys.path:
    sys.path.insert(0, str(ROOT_DIR))


def call_gemini_api(prompt: str, system_instruction: str) -> Optional[str]:
    """Calls Gemini API if GEMINI_API_KEY is available in the environment."""
    api_key = os.environ.get("GEMINI_API_KEY", "").strip()
    if not api_key:
        return None

    model = os.environ.get("GEMINI_MODEL", "gemini-2.5-flash")
    url = f"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={api_key}"
    payload = {
        "system_instruction": {"parts": [{"text": system_instruction}]},
        "contents": [{"role": "user", "parts": [{"text": prompt}]}],
        "generationConfig": {"temperature": 0.2},
    }
    req = urllib.request.Request(
        url,
        data=json.dumps(payload).encode("utf-8"),
        headers={"Content-Type": "application/json"},
        method="POST",
    )
    try:
        with urllib.request.urlopen(req, timeout=60) as resp:
            data = json.loads(resp.read().decode("utf-8"))
            candidates = data.get("candidates", [])
            if candidates:
                parts = candidates[0].get("content", {}).get("parts", [])
                return "".join(p.get("text", "") for p in parts)
    except Exception as e:
        return f"*(Gemini API call encountered an error: {e})*"
    return None


def github_api(method: str, path: str, payload: Optional[Dict[str, Any]] = None) -> Any:
    token = os.environ.get("GITHUB_TOKEN", "").strip()
    if not token:
        raise RuntimeError("GITHUB_TOKEN environment variable is required.")
    url = f"https://api.github.com{path}"
    data = json.dumps(payload).encode("utf-8") if payload is not None else None
    req = urllib.request.Request(
        url,
        data=data,
        headers={
            "Authorization": f"Bearer {token}",
            "Accept": "application/vnd.github+json",
            "X-GitHub-Api-Version": "2022-11-28",
            "User-Agent": "Antigravity-Universal-Modder-CI/1.0",
            "Content-Type": "application/json",
        },
        method=method,
    )
    with urllib.request.urlopen(req, timeout=30) as resp:
        raw = resp.read().decode("utf-8")
        return json.loads(raw) if raw.strip() else {}


def scan_modder_workspace(root: pathlib.Path, game_query: str = "") -> Dict[str, Any]:
    workflows: List[str] = []
    skills: List[str] = []
    playbooks: List[str] = []
    examples: List[str] = []
    um_modules: List[str] = []

    for p in sorted((root / ".github" / "workflows").glob("*.yml")):
        workflows.append(p.relative_to(root).as_posix())

    for p in sorted((root / "skills").glob("*/SKILL.md")):
        skills.append(p.parent.name)

    pb_dir = root / "skills" / "mod-any-game" / "references" / "engines"
    if pb_dir.is_dir():
        for p in sorted(pb_dir.glob("*.md")):
            playbooks.append(p.stem)

    ex_dir = root / "examples"
    if ex_dir.is_dir():
        for p in sorted(ex_dir.iterdir()):
            if p.is_dir():
                examples.append(p.name)

    for p in sorted((root / "um").glob("*.py")):
        if not p.name.startswith("_"):
            um_modules.append(p.stem)

    kb_stats: Dict[str, Any] = {"total_notes": 0, "valid": True, "failures": [], "query_hits": []}
    try:
        from um import kb

        kb_root = root / "knowledge"
        notes_list = list(kb.notes(kb_root))
        kb_stats["total_notes"] = len(notes_list)
        for np, _, _ in notes_list:
            fails, _ = kb.check_note(np, kb_root)
            if fails:
                kb_stats["valid"] = False
                kb_stats["failures"].append({"note": np.relative_to(root).as_posix(), "errors": fails})

        terms = [t for t in game_query.strip().split() if t]
        if not terms and game_query:
            terms = [game_query]
        if terms:
            hits = kb.search(kb_root, terms)[:8]
            kb_stats["query_hits"] = [
                {
                    "path": h.get("path"),
                    "title": h.get("title"),
                    "game": h.get("game"),
                    "engine": h.get("engine"),
                    "route": h.get("route"),
                }
                for h in hits
            ]
    except Exception as e:
        kb_stats["error"] = str(e)

    return {
        "workflows": workflows,
        "skills": skills,
        "engine_playbooks": playbooks,
        "examples": examples,
        "um_cli_modules": um_modules,
        "knowledge_base": kb_stats,
    }


def build_agent_response(
    event_type: str, task: str, game_query: str, summary: Dict[str, Any]
) -> str:
    now = datetime.datetime.now(datetime.timezone.utc).strftime("%Y-%m-%d %H:%M:%S UTC")
    system_prompt = (
        "You are the Antigravity Universal Modder Agent running inside GitHub Actions for "
        "ronaldtbriggs-blip/universal-modder. Follow the Universal Modder 10-step loop "
        "(KB search -> recon -> route -> lab/backups -> reverse engineering -> vertical slice -> "
        "assets -> live verification -> showcase -> publish check) and provide a concise, "
        "actionable Markdown report."
    )
    user_prompt = (
        f"Event Type: {event_type}\n"
        f"Task / Trigger Input:\n{task}\n"
        f"Game / Engine Query: {game_query or '(none)'}\n\n"
        f"Universal Modder Snapshot:\n```json\n{json.dumps(summary, indent=2)}\n```"
    )
    ai_output = call_gemini_api(user_prompt, system_prompt)

    header = (
        f"## 🎮 Antigravity Universal Modder Agent Report\n\n"
        f"- **Timestamp**: `{now}`\n"
        f"- **Trigger Event**: `{event_type}`\n"
        f"- **Task**: {task}\n"
    )
    if game_query:
        header += f"- **Knowledge Base Query**: `{game_query}`\n"
    header += "\n"

    if ai_output:
        body = f"### Agent Modding Analysis & Execution Plan\n\n{ai_output}\n\n"
    else:
        kb_info = summary["knowledge_base"]
        wf_list = "\n".join(f"  - `{w}`" for w in summary["workflows"])
        skill_list = ", ".join(f"`{s}`" for s in summary["skills"])
        pb_list = ", ".join(f"`{p}`" for p in summary["engine_playbooks"])
        ex_list = ", ".join(f"`{e}`" for e in summary["examples"])
        mod_list = ", ".join(f"`um {m}`" for m in summary["um_cli_modules"])

        hits_md = ""
        if kb_info.get("query_hits"):
            hits_lines = [
                f"  - **`{h['title']}`** (`knowledge/{h['path']}`) — Game: `{h.get('game') or 'N/A'}`, Engine: `{h.get('engine') or 'N/A'}`, Route: `{h.get('route') or 'N/A'}`"
                for h in kb_info["query_hits"]
            ]
            hits_md = "### Matching Knowledge Base Field Notes\n\n" + "\n".join(hits_lines) + "\n\n"

        body = (
            f"{hits_md}"
            f"### Universal Modder & Antigravity Inventory\n\n"
            f"- **Knowledge Base Status**: `{kb_info['total_notes']} field notes` "
            f"({'✅ All Valid' if kb_info['valid'] else '⚠️ Validation Errors Found'})\n"
            f"- **CLI Subcommands (`bin/um`)**: {mod_list}\n"
            f"- **Active Agent Skills ({len(summary['skills'])})**: {skill_list}\n"
            f"- **Engine Playbooks ({len(summary['engine_playbooks'])})**: {pb_list}\n"
            f"- **Worked Reference Mods ({len(summary['examples'])})**: {ex_list}\n"
            f"- **Active GitHub Actions Workflows**:\n{wf_list}\n\n"
            f"### Recommended Next Steps in Antigravity\n\n"
            f"1. **Search the KB**: `python -m um kb search \"<game or engine>\"`\n"
            f"2. **Scan Installed Games**: `python -m um scan`\n"
            f"3. **Snapshot Saves Before Modding**: `python -m um backup create \"<save-folder>\" --name <game>`\n"
            f"4. **Run Multi-Agent Mod Orchestrator**: `python agents/antigravity_orchestrator.py \"<modding goal>\"`\n"
        )

    return header + body


def main() -> None:
    parser = argparse.ArgumentParser(description="Antigravity Universal Modder CI Runner")
    parser.add_argument("--event", required=True, choices=["dispatch", "issue", "schedule"])
    parser.add_argument("--task", default="Audit Universal Modder toolkit and knowledge base.")
    parser.add_argument("--mode", default="report-issue")
    parser.add_argument("--game-query", default="")
    parser.add_argument("--issue-number", type=int, default=0)
    args = parser.parse_args()

    repo = os.environ.get("GITHUB_REPOSITORY", "ronaldtbriggs-blip/universal-modder")
    workspace = pathlib.Path(os.environ.get("GITHUB_WORKSPACE", str(ROOT_DIR))).resolve()

    query = args.game_query.strip()
    if not query and args.event == "issue":
        # Extract non-flag words from the task for KB lookup
        words = [
            w.strip(".,!?:;()[]\"'")
            for w in args.task.split()
            if not w.startswith(("@", "/")) and len(w) > 3
        ]
        query = " ".join(words[:4])

    summary = scan_modder_workspace(workspace, game_query=query)
    report = build_agent_response(args.event, args.task, query, summary)

    step_summary = os.environ.get("GITHUB_STEP_SUMMARY")
    if step_summary:
        with open(step_summary, "a", encoding="utf-8") as f:
            f.write(report + "\n")

    print(report)

    if os.environ.get("GITHUB_TOKEN"):
        if args.event == "issue" and args.issue_number > 0:
            github_api(
                "POST",
                f"/repos/{repo}/issues/{args.issue_number}/comments",
                {"body": report},
            )
            print(f"Posted Universal Modder agent response to issue #{args.issue_number}.")
        elif args.event == "dispatch" and args.mode == "report-issue":
            short_task = (args.task[:60] + "...") if len(args.task) > 60 else args.task
            created = github_api(
                "POST",
                f"/repos/{repo}/issues",
                {
                    "title": f"🎮 Modder Agent Report: {short_task}",
                    "body": report,
                    "labels": ["agent-report"],
                },
            )
            print(f"Created Universal Modder report issue: {created.get('html_url')}")


if __name__ == "__main__":
    main()
