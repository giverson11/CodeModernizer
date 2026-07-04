# Code Modernizer

A .NET 10 + React tool that points at a project folder and modernizes every source file
to a newer language version using Claude, then lets you review each change in a
merge-conflict-style side-by-side diff — accept it, revert it, or send the AI follow-up
instructions — before anything is written to disk. A second "overview" model checks that
the modernized program should still behave the same as the original.

## Architecture

```
frontend/                      React + Vite + TypeScript SPA
src/CodeModernizer.Core/           Domain models + abstractions (no dependencies)
src/CodeModernizer.Infrastructure/ Claude provider, skill loader, diff engine, orchestration
src/CodeModernizer.Api/            ASP.NET Core minimal API, serves the built SPA
skills/<skill-id>/                 Pluggable modernization skills (prompts + manifest)
```

Extensibility points:

- **Languages/versions** — add a folder under `skills/` with a `skill.json` manifest,
  a `prompt.md` (per-file modernizer system prompt) and a `review-prompt.md`
  (whole-program equivalence review prompt). It appears in the UI automatically.
- **AI providers** — implement `IAiProvider` (Core) and register it in `Program.cs`.
  The UI lets you pick the provider plus separate agent and overview models.

Currently shipped: `java-21` (Java → Java 21) on Anthropic Claude models.

## Prerequisites

- .NET 10 SDK **and** the ASP.NET Core runtime (`sudo pacman -S aspnet-runtime` on Arch/CachyOS)
- Node.js 20+ (only needed to build/develop the frontend)
- `ANTHROPIC_API_KEY` exported in the environment that runs the API

## Running

```bash
# 1. Build the frontend into the API's wwwroot (one-time / after UI changes)
cd frontend && npm install && npm run build && cd ..

# 2. Run the backend (serves API + UI on http://localhost:5210)
ANTHROPIC_API_KEY=sk-ant-... dotnet run --project src/CodeModernizer.Api
```

Frontend development with hot reload (proxies `/api` to port 5210):

```bash
cd frontend && npm run dev
```

## Workflow

1. Enter a project folder path, pick the skill, the agent model, and the overview model, then run.
2. Files are modernized in parallel (3 at a time); progress streams into the sidebar.
3. Open a file to review changes hunk-by-hunk: **Accept**, **Keep original**, or **Reset**
   each change; **Accept all** / **Revert all** per file; or **Request AI adjustments**
   to have the agent revise the file with your instructions.
4. **Run overview check** asks the overview model whether the program (with your current
   accept/reject decisions) still produces the same observable behavior; it reports
   `EQUIVALENT`, `POTENTIALLY_DIFFERENT` (with concrete risks), or `INSUFFICIENT_INFO`.
5. **Apply accepted changes** writes only accepted hunks back to disk — rejected and
   pending hunks keep the original code.

## Notes

- Nothing is written to disk until you click Apply; original content is kept in the session.
- Scans skip `.git`, `node_modules`, `build`, `target`, `bin`, `obj`, etc., files over
  256 KB, and caps a session at 500 files.
- Sessions are held in memory; restarting the API clears them.
