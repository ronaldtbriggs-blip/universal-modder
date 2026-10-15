"""
Command-line interface for GitHub Agentic Operations.
Uses the same GitHubClient and tool implementations as `github_mcp_server.py`.

Examples:
  python tools/gh_cli.py list-repos
  python tools/gh_cli.py create-repo --name my-repo --description "Created by Antigravity"
  python tools/gh_cli.py list-workflows --repo antigravity-agentic-workflows
  python tools/gh_cli.py dispatch --repo antigravity-agentic-workflows --workflow agent-dispatch.yml --task "Summarize repo structure"
  python tools/gh_cli.py list-runs --repo antigravity-agentic-workflows
"""

import argparse
import json
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from github_mcp_server import GitHubClient, execute_tool


def main() -> None:
    parser = argparse.ArgumentParser(description="Antigravity GitHub Agentic CLI")
    sub = parser.add_subparsers(dest="cmd", required=True)

    p_repos = sub.add_parser("list-repos", help="List repositories")
    p_repos.add_argument("--visibility", default="all", choices=["all", "public", "private"])
    p_repos.add_argument("--limit", type=int, default=30)

    p_create = sub.add_parser("create-repo", help="Create a new repository")
    p_create.add_argument("--name", required=True)
    p_create.add_argument("--description", default="")
    p_create.add_argument("--private", action="store_true")

    p_wf = sub.add_parser("list-workflows", help="List GitHub Actions workflows")
    p_wf.add_argument("--repo", required=True)

    p_disp = sub.add_parser("dispatch", help="Trigger a workflow_dispatch run")
    p_disp.add_argument("--repo", required=True)
    p_disp.add_argument("--workflow", default="agent-dispatch.yml")
    p_disp.add_argument("--ref", default="main")
    p_disp.add_argument("--task", required=True, help="Task prompt for the agent workflow")
    p_disp.add_argument("--mode", default="report-issue", help="Execution mode")

    p_runs = sub.add_parser("list-runs", help="List recent workflow runs")
    p_runs.add_argument("--repo", required=True)
    p_runs.add_argument("--limit", type=int, default=10)

    p_issue = sub.add_parser("create-issue", help="Create an issue")
    p_issue.add_argument("--repo", required=True)
    p_issue.add_argument("--title", required=True)
    p_issue.add_argument("--body", default="")
    p_issue.add_argument("--label", action="append", default=[])

    args = parser.parse_args()
    client = GitHubClient()

    if args.cmd == "list-repos":
        res = execute_tool(client, "list_repos", {"visibility": args.visibility, "limit": args.limit})
    elif args.cmd == "create-repo":
        res = execute_tool(
            client,
            "create_repo",
            {"name": args.name, "description": args.description, "private": args.private},
        )
    elif args.cmd == "list-workflows":
        res = execute_tool(client, "list_workflows", {"repo": args.repo})
    elif args.cmd == "dispatch":
        res = execute_tool(
            client,
            "trigger_workflow",
            {
                "repo": args.repo,
                "workflow_id": args.workflow,
                "ref": args.ref,
                "inputs": {"task": args.task, "mode": args.mode},
            },
        )
    elif args.cmd == "list-runs":
        res = execute_tool(client, "list_workflow_runs", {"repo": args.repo, "limit": args.limit})
    elif args.cmd == "create-issue":
        res = execute_tool(
            client,
            "create_issue",
            {"repo": args.repo, "title": args.title, "body": args.body, "labels": args.label},
        )
    else:
        raise ValueError(f"Unsupported command: {args.cmd}")

    print(json.dumps(res, indent=2))


if __name__ == "__main__":
    main()
