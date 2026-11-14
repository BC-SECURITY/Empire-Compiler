# v8 Engine ABI: source-from-server, standalone obfuscate, RunCOFF artifact, min-engine

Status: DRAFT (brainstormed 2026-11-14; design-gate PASSED round 2 (APPROVE); 3 medium / 3 low advisory)
Date: 2026-11-14
Owner: Cx01N
Repo: implementation in `BC-SECURITY/Empire-Compiler` (the engine) on a dedicated
long-lived **`v8` branch** (released as its own tag lineage; the v8 server pins those tags,
v7/`private-main` stays on `v2.0.3`); the server-side consumer is `BC-SECURITY/Empire-Sponsors`
#1735.
Tracked by: #1735 (C# compile tier + compiler-as-engine). Siblings:
`2026-11-13-v8-agent-engine-restructuring-design.md` (Spec #2 — this resolves its Open
decisions #1, #2, #4), `2026-11-13-v8-modular-marketplace-design.md` (Spec #1), #1691
(library packs, consumes this ABI). **Phase 0 of the v8 line; ships on the `v8` branch.**

## Intent / why

Spec #2 (#1735) requires the compiler to become a **toolchain-only engine** that compiles
source handed to it, instead of resolving `ReferenceSourceLibraries` against its own
embedded submodule tree. That is the keystone the server compile tier (#1735 Phase 1) and
operator-resolved library packs (#1691) both consume: once the engine accepts external
source roots, a C# library fix reaches a running server by swapping a library-pack source
tree and recompiling locally — no engine or Empire release. This spec pins the exact engine
ABI so Phase 1's assumed constants (`ENGINE_SOURCE_ROOTS_FLAG`, `ENGINE_OBFUSCATE_ONLY_FLAG`,
`ENGINE_ABI`) become real.

## Goals

- **External source roots:** the engine compiles `ReferenceSourceLibraries` from directories
  the caller passes, not only from its embedded `Data/ReferenceSourceLibraries/`.
- **Standalone obfuscation:** ConfuserEx runnable on an already-built assembly without a
  Roslyn recompile, so the server can obfuscate a cached/prebuilt artifact — under the same
  server-owned protection policy as the compile path.
- **Prebuilt RunCOFF:** the engine CI builds the RunCOFF wrapper + CoffLoader (`CS_COFFLoader`)
  into a net40 artifact shipped in the engine release, for Spec #1 / #1735 core.
- **`min-engine` ABI:** the engine reports an integer contract version the server can gate
  plugins on, via the existing `dependencies:` mechanism.
- **Transition path:** strip the library submodules from the engine once the server resolves
  them via packs, with an ABI bump marking the change.

## Non-goals

- The server-side resolver, library-pack registry kind, version-collision forbid, and cache
  (that is #1691 + #1735 Phase 1; this engine stays dumb — it compiles whatever roots it is
  given and has no library-version knowledge).
- Vendoring the engine build into the Empire repo (Spec #2 non-goal; engine stays a separate
  downloaded toolchain).
- Moving framework `AssemblyReferences` out of the engine (they are toolchain, not
  upgradeable library source).
- Plugin-shipped `EmbeddedResources` / `ReferenceAssemblies` handling (server-side, #1735).
- Sharpire relocation to `data/agent/` (#1735 Phase 3; the engine keeps consuming Sharpire
  source during transition).

## Anti-goals

- An ABI so loose the server can't tell whether an installed engine supports source roots, or
  whether it still bundles library fallbacks (hence the integer `min-engine` gate, bumped at
  the strip).
- A transition that requires server and engine to upgrade in lockstep (a new server must never
  send an unknown option to an old engine).
- **Silent staleness:** compiling old bundled source when the operator has installed a pack to
  replace it. Where the server cannot honor a pack (old engine) or the engine cannot resolve a
  library (post-strip, no pack), the build fails loudly rather than compiling the wrong source.
- `--confuse` and `--obfuscate-only` applying *different* obfuscation policy.
- Leaking server filesystem paths into the task payload (rules out rewriting `Location`s to
  absolute paths inside the YAML — approach B).

## Constraints / verified current state (Empire-Compiler, branch `update-sharpire-7.0-dev`)

- **.NET 10 console app**, `System.CommandLine`. CLI today (`EmpireCompiler/Program.cs`):
  `--output` and `--yaml` are **`Required = true`**; plus `--dotnet-version`, `--confuse`,
  `--debug`, `--merge-references`. All task data, including `ReferenceSourceLibraries[].Location`,
  rides inside the base64 YAML. The server treats a missing `"Final Task Path:"` on stdout as
  failure (`dotnet.py:119`).
- **Source resolution** (`Models/Module/AgentTask.cs:210`): `SourceDirectories =
  ReferenceSourceLibraries.Select(RSL => Common.EmpireReferenceSourceLibraries + RSL.Location)`
  — string concatenation prefixing `Data/ReferenceSourceLibraries/` (`Common.cs:22`). The
  `Optimize` heuristic (`AgentTask.cs:218`) keys on `RSL.Name` (root-independent).
  `Compiler.cs:93` reads sources via `Directory.GetFiles`, which throws `DirectoryNotFoundException`
  on a missing directory.
- **Obfuscation policy is server-owned.** `Core/Compiler.cs:375-446` runs
  `Confuser.CLI.exe -n <Data/Temp/empire.crproj>` (workingDir `Data/ConfuserEx-CLI`). The
  `empire.crproj` is rendered **at install by the server** (`data_manager._configure_compiler`,
  ~`data_manager.py:291-320`) from the operator-configurable template `confuser_proj`
  (`config.yaml`, today `empire/server/data/confuser.crproj`), whose policy includes rename /
  anti-ildasm / control-flow and `namespace('Program')` carve-outs. Paths `confused.exe`,
  `confused_out`, `empire.crproj` live in the shared `Data/Temp`.
- **Libraries are git submodules** under `Data/ReferenceSourceLibraries/` (SharpSploit,
  Rubeus, Seatbelt, …, and **`CS_COFFLoader`**). CoffLoader source already lives with the engine.
- The server also supports a **local engine directory** build (`compiler_config.directory`
  override in `sync_empire_compiler`), not only a downloaded release archive.
- Framework reference DLLs live in `Data/AssemblyReferences/net35…net48/` (`Common.cs`).
- `tools/VendorTool/` exists in-repo (role to confirm at plan time — likely submodule→vendored
  source management, relevant to the strip step / #1731).

## Approaches considered

- **Source handoff (chosen: A).** A — keep `--yaml`, add repeatable `--source-root <abs path>`;
  `Location`s resolve against given roots ∪ bundled. **Chosen:** smallest change, backward
  compatible, engine stays YAML-driven, supports the overlap transition. B — server rewrites
  each `Location` to an absolute path inside the YAML (rejected: leaks server paths into the
  payload, loses the roots/bundled distinction, messier transition). C — new manifest input
  replacing the base64 blob (rejected: breaks the existing contract for no present benefit).
- **`min-engine` (chosen: integer).** Integer contract version bumped on a breaking CLI/YAML
  change **and at the library-strip** (**chosen** — consistent with `plugin_api`/`dependencies:`)
  vs engine release semver (rejected: bumps on every release) vs capability flags (rejected:
  most machinery for no present need). *The integer must bump at the strip — see §5.*

## The decided design

### 1. External source roots (`--source-root`)
- New repeatable option `--source-root <absolute dir>` on the root command (`Program.cs`).
- Resolution precedence (change localized to `AgentTask.cs:210`): for each
  `ReferenceSourceLibrary`, resolve its `Location` against the given `--source-root`s **in
  order, first match wins**, falling back to the bundled `Common.EmpireReferenceSourceLibraries`
  only when no given root contains it. External always shadows bundled; bundled is a
  transition-only fallback. **Whole-library shadow** — a root either provides the complete
  library tree at that `Location` or is not a match; file sets are never merged across roots. A root **matches** a library when `root/Location` is a non-empty directory containing source files (not merely present); an empty/partial dir does not shadow bundled.
- **Path confinement (engine-enforced):** the engine canonicalizes `root/Location`, rejects a
  rooted `Location` and any result that escapes `root` (e.g. via `..`). `Location` is
  YAML-supplied and therefore untrusted at the engine boundary even though the server also
  validates it; the engine is the component that joins the path, so it confines it. Containment
  resolves the **real path** (following symlinks) before the check, since `Directory.GetFiles`
  (`Compiler.cs:93`) follows symlinks and a registry-sourced pack could contain one that escapes
  `root`. Covered by engine tests. (Replaces the current bare string concat at `AgentTask.cs:210`.)
- **No-match behavior:** if a library resolves to no given root and no bundled copy (post-strip),
  the engine emits a clear `library '<Name>' not found in any source root` error, never the raw
  `DirectoryNotFoundException` from `Compiler.cs:93`.
- Scope: `ReferenceSourceLibraries` only. Framework `AssemblyReferences` stay bundled and are
  never resolved from source roots.
- When no `--source-root` is passed, behavior is identical to today (pure backward compat).

### 2. Standalone obfuscation (`--obfuscate-only`) + crproj ownership
- **Obfuscation policy stays server-owned and is passed in, not embedded.** New engine input
  `--confuser-project <template path>`. The server passes its configured `confuser_proj`
  template on every obfuscated call; the engine renders that template's placeholders into a
  **per-run crproj in a per-run temp dir**, then runs the existing ConfuserEx subprocess. The
  **placeholder contract is exactly four names** (as used today in `data_manager._configure_compiler`
  and `data/confuser.crproj`): `{confuser_base_dir}`, `{confuser_output_dir}`,
  `{confuser_module_path}`, `{confuser_proj_dir}` (the last feeds every `probePath` and is
  engine-owned, pointing at `Data/AssemblyReferences`). The engine's C# renderer must match
  Python `str.format` semantics — including `{{`/`}}` escaping — so operator templates keep
  working, and must fail on an unknown `{name}`. A golden-render test runs against the shipped
  `confuser.crproj`.
- **Both obf paths use it.** `--confuse` (compile) and `--obfuscate-only` both render the passed
  template per-run. This gives identical policy across the two paths, keeps `confuser_proj` a
  live operator knob, and removes the shared-`Data/Temp` race for **both** paths. For **ABI ≥ 1**
  the server passes `--confuser-project` and the engine renders per-run, so the install-time
  `data_manager._configure_compiler` render of a shared `empire.crproj` is no longer used; for
  the **ABI 0 legacy path** the server still renders `empire.crproj` as today, because an old
  engine reads only that shared file (server-side change, #1735). So `_configure_compiler` keeps
  its ABI-0 render job and is **not** the ABI read site (see §4).
- `--obfuscate-only <assembly path>` skips Roslyn/`AgentTask.Compile`, renders the passed
  template against the input assembly, runs ConfuserEx, writes `--output`, and **emits
  `Final Task Path:`** so the server's existing stdout success check works unchanged. The per-run
  temp dir holds the rendered crproj, the ConfuserEx input/`confused.exe`, the output dir, and the
  Confuser log (nothing in shared `Data/Temp`), and is cleaned up on success and failure.
- Needs the `Data/ConfuserEx-CLI/` tooling (stays in the engine). `--dotnet-version` is **not**
  accepted in obfuscate-only mode: the template's `probePath`s are fixed (`{confuser_proj_dir}`),
  so there is no version-specific probing to drive — adding it would be dead surface.

### 3. Prebuilt RunCOFF artifact
- Engine CI builds the **RunCOFF wrapper + `CS_COFFLoader`** into one **net40** artifact.
  Verified prebuildable: the current `runcoff` path passes `Entrypoint`/`File`/`HexData` as
  **runtime** base64-JSON args (`module_service.py:946-953`), so the wrapper is static. The
  wrapper source **relocates from `runcoff.yaml`'s `Code` into the engine repo** (a prebuilt
  artifact cannot depend on server-side YAML `Code`). *Cross-repo source-of-truth move — kept
  deliberately; RunCOFF is separable and could be its own phase if preferred.*
- **Built once** as platform-independent net40 IL and attached to every release leg (one hash
  across OS legs; `release.yml` must not rebuild it per leg).
- Ships at `Data/Prebuilt/` in the engine release archive. Pinned to the engine's **bundled**
  CoffLoader and **exempt from pack shadowing** (a pack shipping `CoffLoader` at the same
  `Location` affects per-task compiles only, never the prebuilt — so the two can't diverge
  silently).
- **All three** per-task RunCOFF compile sites route through the prebuilt (server loads the
  cached assembly, passes args at runtime): `generate_script_bof`, `format_bof_output` (the site
  most BOF modules use — `patchit`, `unhook`, `bof/credentials/*`, …), and the standalone
  `RunCoff` module (`RunCoff.yaml`/`RunCoff.py`). `runcoff.yaml`'s `Code` and all three per-task
  compiles go away together, so none is left dangling (server-side, #1735 Phase 1).
- **Missing-prebuilt fallback:** a local-directory engine build (`compiler_config.directory`) with
  ABI ≥ 1 may lack `Data/Prebuilt/` (only `release.yml` produces it). The MSBuild step that emits
  `Data/abi` also emits `Data/Prebuilt/` on a plain build so local builds have it; if it is still
  absent the server falls back to the per-task compile path (or fails loudly), never silently. The
  server is responsible for the `.compiled` zlib wrapping the prebuilt route must replicate
  (today done inside `compile_task`).
- **Obfuscation of the prebuilt:** when a task needs obfuscation, the server runs
  `--obfuscate-only` on the cached prebuilt per task, so each agent still receives a freshly
  obfuscated binary (preserves today's per-task ConfuserEx behavior). Unobfuscated tasks share
  the static cached artifact.

### 4. `min-engine` integer ABI
- The engine reports its contract version two ways, both derived from **one compiled constant**:
  an `abi` **subcommand** (`EmpireCompiler abi` prints the integer) and a plaintext `Data/abi`
  file **generated from that same constant by an MSBuild step in the csproj** (so a plain
  `dotnet publish` and a local dev-directory build both produce it, not just CI). An engine test
  asserts the subcommand output equals the file.
- Starting value **1** = this spec's ABI (`--source-root`, `--obfuscate-only`,
  `--confuser-project`, bundled libraries still present).
- **Absence = ABI 0** (pre-this-spec). The server reads `Data/abi` at engine sync
  (`sync_empire_compiler` / the `_configure_compiler` funnel is the single read site); a missing
  or malformed file is treated as **0** and logged. (The ABI read happens in `sync_empire_compiler`, not in `_configure_compiler`, which keeps its ABI-0 crproj-render job per §2.) The server passes `--source-root` /
  `--obfuscate-only` / `--confuser-project` **only when ABI ≥ 1**, else the legacy call — so a
  new server never sends an unknown option to an old engine (System.CommandLine hard-errors on
  those). **Loud-fail rule:** if the server holds a pack-resolved root for a requested library
  but the installed engine is ABI 0, the compile fails with an actionable error rather than
  silently compiling bundled source. Gate + loud-fail are server-side (#1735); the engine's duty
  is to report the integer and (post-strip) to error on an unresolved library.
- The server refuses a plugin whose declared `min-engine` exceeds the installed engine's ABI,
  via the existing `dependencies:` mechanism.

### 5. Transition (strip bundled libraries), with ABI bump
- **Release N (ABI 1):** engine accepts `--source-root`, `--obfuscate-only`, `--confuser-project`;
  ships the prebuilt RunCOFF; reports ABI 1; **still bundles** the library submodules. Both
  resolve, external wins. Server adopts source roots incrementally.
- **Release N+1 (ABI 2):** the library submodules are removed (CoffLoader stays for the prebuilt;
  Sharpire stays until #1735 Phase 3; framework `AssemblyReferences` and `ConfuserEx-CLI` stay).
  **The ABI bumps to 2** — this is the honest signal: a task whose YAML references a library with
  no pack compiles on N (bundled fallback) but errors on N+1 (§1 no-match error), so bundled
  presence is part of the contract and the integer must distinguish them. Optionally `Data/abi`
  also lists `bundled-libraries` so the server can check availability before dispatching; the
  integer bump is the minimum. The server gates pack-required behavior on ABI ≥ 2 where it
  matters.

## What's left to the builder

- `Program.cs`: add `--source-root` (repeatable), `--obfuscate-only <path>`, `--confuser-project
  <path>` options and an `abi` subcommand; relax the global `Required` on `--output`/`--yaml`
  and validate per mode; ensure `--obfuscate-only` emits `Final Task Path:`.
- `AgentTask.cs` / `Compiler.cs`: thread source roots into `SourceDirectories` with the
  precedence + path-confinement + no-match-error rules; refactor the ConfuserEx step to render a
  per-run crproj from `--confuser-project` and be callable standalone; route `--confuse` through
  the same per-run renderer.
- ABI: the compiled constant, the `abi` subcommand, and the MSBuild step emitting `Data/abi`;
  a test asserting they match.
- CI (`release.yml`): build the RunCOFF net40 artifact once, attach to all legs, place at
  `Data/Prebuilt/`; confirm `tools/VendorTool`'s role and whether it drives the strip step.
- Tests (`EmpireCompiler.Tests`): `--source-root` resolving an external library; precedence
  (external shadows bundled); path-escape rejection; no-match error (post-strip); `--obfuscate-only`
  on a prebuilt using a passed `--confuser-project`; `--confuse` and `--obfuscate-only` produce
  the same protections; ABI subcommand/file match; backward compat with no new options. Real
  compiles on the CI legs that have the toolchain; args-level assertions elsewhere.
- CHANGELOG + README/CLAUDE.md in Empire-Compiler for the new options, ABI, and the ABI-2 strip.

## Open decisions (remaining)

- **Trust tier for compiled/library content** — shared with Spec #1/#2 decision #3
  (signing/provenance of registry library source). Out of this engine ABI; the engine compiles
  whatever source it is given (with path confinement, §1), so the trust boundary is enforced
  server-side before source reaches the engine.
- **Engine `v8` branch point** — `main` vs `update-sharpire-7.0-dev` (which carries the
  unreleased .NET 10 / Sharpire work the engine needs). A builder call; the `v8` branch must
  include that .NET 10 work. (Branch/release lineage itself resolved: dedicated `v8` branch,
  own tags, v8 server pins them, v7 stays `v2.0.3`.)

## Adversarial review rounds

### Round 1 — 2026-11-14 — 1 critical / 6 medium / 2 low
- `--obfuscate-only` crproj policy ownership undefined; §2 contradicts Q2 — design — revised (server-owned template passed via `--confuser-project`, rendered per-run; §2 rewritten). Mediums/lows folded into §1–§5.

### Round 2 — 2026-11-14 — 0 critical / 3 medium / 3 low (APPROVE; round-1 critical resolved)
- All 3 medium + 3 low folded into §1–§5 post-pass (ABI-0-only crproj render + read-site in sync_empire_compiler; 4-placeholder contract + escaping + golden test; all three RunCOFF compile sites + local-build fallback + .compiled; per-run temp for all obf artifacts; symlink-safe path confinement + match definition; dropped --dotnet-version in obfuscate-only).
