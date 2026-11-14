# Kickoff prompt — v8 Engine ABI execution (paste into a session started in this worktree)

> Start the session here first: `cd /home/dredg/orca/workspaces/Empire-Compiler/v8` (branch `v8`).
> Verify `dotnet --version` reports a .NET 10 SDK before starting — the plan is all `dotnet build`/`test`.

---

Execute the approved implementation plan at `docs/superpowers/plans/2026-11-14-v8-engine-abi.md` using **subagent-driven-development** (invoke the `superpowers:subagent-driven-development` skill). Read the plan and its spec `docs/superpowers/specs/2026-11-14-v8-engine-abi-design.md` first, then run the tasks in order with a fresh implementer subagent per task and a fresh reviewer gate before each next task, and a whole-branch review at the end.

Context you need:
- This is `BC-SECURITY/Empire-Compiler` on branch **`v8`** (local, cut from `origin/main` @ 638ffb5, net10.0). It is the engine for the v8 Empire line; server-side consumption is tracked separately in `BC-SECURITY/Empire-Sponsors` #1735 and is **out of scope** here.
- Scope is **Release N / ABI 1 only** (7 tasks). The library-strip + **ABI 2** and all server-side wiring are explicit follow-ons in the plan — do not build them.
- Read the repo's own `CLAUDE.md` for build/test/lint commands. Baseline: `git submodule update --init --recursive` then `dotnet build`; tests `dotnet test`; obfuscation/integration tests need `mono` + ConfuserEx (gate those legs on toolchain availability, as the existing tests do); lint is `dotnet format --verify-no-changes`.
- **Task 6 (RunCOFF)** relocates the RunCOFF wrapper source *from the server repo*: the current `Code:` block in `/home/dredg/Empire-Sponsors/empire/server/modules/csharp/code_execution/RunCoff.yaml` is the wrapper to bring in-tree and build (net40) with the bundled `CS_COFFLoader` submodule. RunCOFF is the separable task — if it blocks, Tasks 1–5 + 7 still deliver the source-root/obfuscate/ABI engine.

Standing rules (do not violate):
- **No push and no PR without an explicit instruction** to do so. Implement, test, commit locally on `v8`; stop before pushing.
- **Do not use Haiku subagents** on this work (rejected on Empire security code) — use Sonnet or Opus for implementer/reviewer subagents.
- Run `/code-review` and `/simplify` (the review gates) only from this worktree-rooted session — they act on the session's start directory.
- Follow the plan's Global Constraints verbatim (ABI single-source via the `<EngineAbi>` MSBuild property; the four-placeholder crproj contract; `ReferenceSourceLibraries`-only scope; backward compatibility with no new options).

Start by confirming the build is green on the untouched `v8` branch (`dotnet build` + `dotnet test`), then begin Task 1.
