# v8 Engine ABI Implementation Plan (Empire-Compiler)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Turn Empire-Compiler into a toolchain engine that compiles from external `--source-root`s, obfuscates a prebuilt assembly standalone under a server-passed policy, ships a prebuilt RunCOFF, and reports an integer ABI — the Phase-0 keystone for #1735 / #1691.

**Architecture:** Additive CLI on the existing `System.CommandLine` entry (`Program.cs`): new `--source-root` (repeatable), `--obfuscate-only`, `--confuser-project`, and an `abi` subcommand. Two new pure helpers — a source-root resolver and a crproj renderer — keep the logic unit-testable; `AgentTask` and `Compiler` consume them. An ABI constant is the single source for both the `abi` subcommand and an MSBuild-emitted `Data/abi`. RunCOFF (relocated wrapper + `CS_COFFLoader`) builds once to net40 into `Data/Prebuilt/`.

**Tech Stack:** .NET 10 console app, C#, `System.CommandLine` 2.0.0, xUnit (`dotnet test`), MSBuild, mono+ConfuserEx (obfuscation, toolchain-gated in CI).

**Spec:** `docs/superpowers/specs/2026-11-14-v8-engine-abi-design.md` (design-gate PASSED round 2).

## Global Constraints

- **Repo & branch:** `BC-SECURITY/Empire-Compiler`, dedicated **`v8` branch** (must include the `update-sharpire-7.0-dev` .NET 10 work). Released as its own tags; the v8 server pins them, v7 stays `v2.0.3`.
- **ABI starts at 1** (this release). The library-strip + **ABI 2** is **Release N+1, OUT OF THIS PLAN** (depends on the server resolving all 47 library-backed modules via packs) — see the follow-on note at the end.
- **Scope is `ReferenceSourceLibraries` only**; framework `AssemblyReferences` stay bundled and are never source-root-resolved.
- **Backward compatible:** with no new options, behavior is byte-for-byte today's. A new server gates new options on ABI ≥ 1 (server-side, #1735) — the engine never requires them.
- **ABI value has one source:** the `Core/Abi.cs` constant; `abi` subcommand and `Data/abi` both derive from it.
- **crproj placeholder contract (exactly four):** `{confuser_base_dir}`, `{confuser_output_dir}`, `{confuser_module_path}`, `{confuser_proj_dir}`; `str.format`-compatible `{{`/`}}` escaping; fail on an unknown `{name}`.
- Lint: `dotnet format --verify-no-changes` (CI `lint.yml`). Tests: `dotnet test`; obfuscation/integration legs gated on mono+ConfuserEx availability as today.
- Server-side consumption (`dotnet.py`, `data_manager.py`, `module_service.py`, deleting `runcoff.yaml`'s `Code`) is **#1735 Phase 1, not this plan.**

## Review Focus

- **Path escape via `Location`:** a `..`, a rooted path, or a symlink inside a registry pack must not resolve outside its root — the engine confines it on the real (symlink-resolved) path. → Task 3.
- **Empty/partial external dir silently shadowing bundled:** a root "matches" only when `root/Location` is a non-empty directory with source files; otherwise it falls through, not shadows. → Task 3.
- **crproj unknown placeholder / brace escaping:** an operator template with `{{`/`}}` or an unknown `{name}` must render correctly or fail loudly, not emit literal braces or wrong policy. → Task 2.
- **`--obfuscate-only` success contract:** it must emit `Final Task Path:` on stdout or the server reads every obfuscate as a failure. → Task 5.
- **Concurrent obfuscation:** two obfuscate runs must not collide on shared `Data/Temp` (per-run temp dir holds crproj, input, output, log). → Task 5.

---

### Task 1: ABI constant, `abi` subcommand, MSBuild-emitted `Data/abi`

**Files:**
- Create: `EmpireCompiler/Core/Abi.cs`
- Modify: `EmpireCompiler/Program.cs` (add `abi` subcommand)
- Modify: `EmpireCompiler/EmpireCompiler.csproj` (post-build target writing `Data/abi`)
- Test: `EmpireCompiler.Tests/Unit/AbiTests.cs`

**Interfaces:**
- Produces: `EmpireCompiler.Core.Abi.Version` (`public const int Version = 1;`).
- Produces: CLI `EmpireCompiler abi` → prints the integer to stdout.
- Produces: `Data/abi` file containing the integer, emitted on every build (local `dotnet build`/`publish`, not only CI).

- [ ] **Step 1: Write the failing tests**

```csharp
[Fact] public void Abi_Version_Is_1() => Assert.Equal(1, Abi.Version);

[Fact] public void DataAbiFile_Matches_Constant() {
    var path = Path.Combine(Common.EmpireDataDirectory, "abi");
    Assert.True(File.Exists(path), "Data/abi should be emitted by the build");
    Assert.Equal(Abi.Version, int.Parse(File.ReadAllText(path).Trim()));
}
```

- [ ] **Step 2: Run, verify fail** — `dotnet test --filter AbiTests` → FAIL (type/file missing).
- [ ] **Step 3: Implement.** Single source: define `<EngineAbi>1</EngineAbi>` as an MSBuild property in the csproj. A `BeforeTargets="BeforeBuild"` target generates `Core/Abi.cs` (`namespace EmpireCompiler.Core; public static class Abi { public const int Version = $(EngineAbi); }`), and a `BeforeTargets="Build"` target `WriteLinesToFile`s `$(MSBuildProjectDirectory)/Data/abi` with `$(EngineAbi)`. Add an `abi` subcommand to `Program.cs` printing `Abi.Version`. Step 1's `DataAbiFile_Matches_Constant` test is the backstop.
- [ ] **Step 4: Run, verify pass** — `dotnet test --filter AbiTests` → PASS.
- [ ] **Step 5: Commit** — `feat(abi): report engine ABI via subcommand and Data/abi`.

---

### Task 2: Crproj renderer (4-placeholder, `str.format` semantics)

**Files:**
- Create: `EmpireCompiler/Core/CrprojRenderer.cs`
- Test: `EmpireCompiler.Tests/Unit/CrprojRendererTests.cs`

**Interfaces:**
- Produces: `static string CrprojRenderer.Render(string template, IReadOnlyDictionary<string,string> values)` — substitutes `{name}` for the four known keys, treats `{{`→`{` and `}}`→`}`, and throws `CrprojRenderException` on an unknown `{name}` or an unmatched brace.
- Produces: `static readonly string[] CrprojRenderer.Placeholders` = the four names.

- [ ] **Step 1: Write the failing tests**

```csharp
[Fact] public void Renders_All_Four_Placeholders() { /* each of the 4 names substituted */ }
[Fact] public void Escapes_Double_Braces() => Assert.Equal("{x}", Render("{{x}}", Empty));
[Fact] public void Unknown_Placeholder_Throws() => Assert.Throws<CrprojRenderException>(() => Render("{nope}", Empty));
[Fact] public void Golden_Render_Of_Shipped_Confuser_Crproj() {
    // render data/confuser.crproj's placeholder set with sample values; assert no leftover "{name}" and all probePaths resolved
}
```

- [ ] **Step 2: Run, verify fail** — `dotnet test --filter CrprojRendererTests`.
- [ ] **Step 3: Implement** `CrprojRenderer.Render`. The algorithm is Python-`str.format`-compatible single-brace substitution with `{{`/`}}` escaping and unknown-key rejection — implement a single left-to-right scan (not `string.Replace`, which can't honor escaping).
- [ ] **Step 4: Run, verify pass.**
- [ ] **Step 5: Commit** — `feat(obf): add crproj template renderer with four-placeholder contract`.

---

### Task 3: Source-root resolver (precedence, confinement, match, no-match error)

**Files:**
- Create: `EmpireCompiler/Core/SourceRootResolver.cs`
- Test: `EmpireCompiler.Tests/Unit/SourceRootResolverTests.cs`

**Interfaces:**
- Produces: `static string SourceRootResolver.Resolve(string location, IReadOnlyList<string> sourceRoots, string bundledRoot)` — returns the absolute directory for `location`: the first `sourceRoot` that **matches**, else `bundledRoot` if it matches, else throws `SourceLibraryNotFoundException($"library '{location}' not found in any source root")`.
- Match = `root/location` canonicalizes (real path, symlinks resolved) to a path **inside `root`**, is an existing **non-empty** directory **containing source files** (`*.cs`). A rooted `location` or one escaping `root` is rejected (treated as no-match for that root; a rooted/`..` location that matches nothing throws).

- [ ] **Step 1: Write the failing tests** (use `tmp` dirs)

```csharp
[Fact] public void External_Root_Shadows_Bundled() { /* lib in root0 and bundled → returns root0 path */ }
[Fact] public void First_Matching_Root_Wins() { /* root0 empty, root1 has it → root1 */ }
[Fact] public void Empty_Or_Partial_Dir_Is_Not_A_Match() { /* root0 has empty dir → falls through to bundled */ }
[Fact] public void Rejects_DotDot_Escape() { /* location "../x" → not matched, throws if nothing else */ }
[Fact] public void Rejects_Rooted_Location() { /* location "/etc" → rejected */ }
[Fact] public void Rejects_Symlink_Escape() { /* root/location is a symlink pointing outside root → not a match */ }
[Fact] public void No_Match_Throws_Clear_Error() => Assert.Throws<SourceLibraryNotFoundException>(() => Resolve("Ghost", new[]{emptyRoot}, emptyBundled));
```

- [ ] **Step 2: Run, verify fail.**
- [ ] **Step 3: Implement.** Use `Path.GetFullPath` then resolve symlinks (`new DirectoryInfo(p).ResolveLinkTarget(true)` / `File.ResolveLinkTarget`) before the `StartsWith(root)` containment check; `Directory.EnumerateFiles(dir, "*.cs", AllDirectories).Any()` for non-empty.
- [ ] **Step 4: Run, verify pass.**
- [ ] **Step 5: Commit** — `feat(source-roots): add confined source-root resolver`.

---

### Task 4: Wire `--source-root` into compilation

**Files:**
- Modify: `EmpireCompiler/Program.cs` (add repeatable `--source-root`, thread to `AgentTask`)
- Modify: `EmpireCompiler/Models/Module/AgentTask.cs:210` (use `SourceRootResolver`)
- Test: `EmpireCompiler.Tests/Integration/SourceRootCompilationTests.cs`

**Interfaces:**
- Consumes: `SourceRootResolver.Resolve` (Task 3).
- Produces: `--source-root <abs dir>` (repeatable) on the root command; `AgentTask` gains `IReadOnlyList<string> SourceRoots` used when building `SourceDirectories`.

- [ ] **Step 1: Write the failing test** — compile a task whose library lives only under a passed `--source-root` (not bundled) and assert output is produced; a second test asserting no `--source-root` still compiles from bundled (backward compat).
- [ ] **Step 2: Run, verify fail.**
- [ ] **Step 3: Implement** — replace the `Common.EmpireReferenceSourceLibraries + RSL.Location` concat at `AgentTask.cs:210` with `SourceRootResolver.Resolve(RSL.Location, this.SourceRoots, Common.EmpireReferenceSourceLibraries)`; parse `--source-root` (allow multiple) in `Program.cs` and set `agentTask.SourceRoots`.
- [ ] **Step 4: Run, verify pass** (toolchain-gated leg).
- [ ] **Step 5: Commit** — `feat(source-roots): compile from external --source-root dirs`.

---

### Task 5: Standalone obfuscation (`--obfuscate-only` + `--confuser-project`, per-run crproj)

**Files:**
- Modify: `EmpireCompiler/Core/Compiler.cs` (refactor `ConfuseAssembly`; add a per-run obfuscate path)
- Modify: `EmpireCompiler/Program.cs` (add `--obfuscate-only`, `--confuser-project`; relax `Required`; validate per mode; route `--confuse` through the renderer)
- Test: `EmpireCompiler.Tests/Integration/ObfuscateOnlyTests.cs`

**Interfaces:**
- Consumes: `CrprojRenderer.Render` (Task 2).
- Produces: `Compiler.ObfuscateFile(string inputAssembly, string outputPath, string confuserProjectTemplate, string? dotNetProbeDir = null)` — renders a per-run crproj into a fresh temp dir (crproj, `confused.exe`, output dir, log all inside it), runs the mono ConfuserEx subprocess, writes `outputPath`, cleans the temp dir on success and failure.
- Produces: CLI `--obfuscate-only <assembly>` + `--confuser-project <template>`; `--output` required in this mode; `--yaml` not; prints `Final Task Path: <output>` on success. `--confuse` compile path renders its crproj the same way (no shared `Data/Temp`).

- [ ] **Step 1: Write the failing tests**

```csharp
[Fact] public void ObfuscateOnly_Produces_Output_And_Prints_Final_Task_Path() { /* run on a prebuilt asm with a template; assert output exists + stdout has "Final Task Path:" */ }
[Fact] public void ObfuscateOnly_And_Confuse_Apply_Same_Protections() { /* both paths on same input → comparable protected output (e.g. renamed metadata present) */ }
[Fact] public void Concurrent_ObfuscateOnly_Do_Not_Collide() { /* two parallel runs, distinct temp dirs, both succeed */ }
```

- [ ] **Step 2: Run, verify fail.**
- [ ] **Step 3: Implement** — extract the mono-invocation core of `ConfuseAssembly` into a helper taking an explicit crproj path + temp dir; add `ObfuscateFile` (reads the input assembly, renders the template via Task 2, runs the helper); wire `Program.cs` modes (relax global `Required`, add manual per-mode validation, emit `Final Task Path:`); point `--confuse` at the per-run renderer too.
- [ ] **Step 4: Run, verify pass** (toolchain-gated).
- [ ] **Step 5: Commit** — `feat(obf): standalone --obfuscate-only with server-passed per-run crproj`.

---

### Task 6: Prebuilt RunCOFF (relocate wrapper + build net40 artifact)

**Files:**
- Create: `RunCOFF/` engine-side wrapper source (relocated from the server's `runcoff.yaml` `Code`) + a net40 build project referencing the bundled `CS_COFFLoader` submodule
- Modify: `EmpireCompiler/EmpireCompiler.csproj` or a build script (emit `Data/Prebuilt/` on build, incl. local builds)
- Test: `EmpireCompiler.Tests/Unit/PrebuiltRunCoffTests.cs`

**Interfaces:**
- Produces: `Data/Prebuilt/<RunCOFF artifact>` — a net40 assembly built once (platform-independent IL), pinned to the engine's bundled `CS_COFFLoader`, exempt from source-root shadowing.

- [ ] **Step 1: Write the failing test** — after build, assert `Data/Prebuilt/` contains the RunCOFF artifact and it is a net40 managed assembly (read the PE/CLI header or assembly metadata).
- [ ] **Step 2: Run, verify fail.**
- [ ] **Step 3: Implement** — bring the wrapper source into the engine repo; add a net40 build (separate `.csproj` targeting `net40`, or an MSBuild/`csc` invocation) that compiles wrapper + `CS_COFFLoader` into the artifact; wire a build target so a plain `dotnet build` emits `Data/Prebuilt/` (so local-directory engines have it, not only CI).
- [ ] **Step 4: Run, verify pass.**
- [ ] **Step 5: Commit** — `feat(runcoff): build prebuilt net40 RunCOFF artifact into Data/Prebuilt`.

*Note (spec scope verdict): RunCOFF is the separable piece — if it slips, Tasks 1–5 + 7 still deliver the source-root/obfuscate/ABI engine; RunCOFF can follow.*

---

### Task 7: Release CI — build-once, archive `Data/abi` + `Data/Prebuilt`

**Files:**
- Modify: `.github/workflows/release.yml`
- Verification: the release archive (existing `EmpireCompiler/EmpireCompiler` + `Data/` shape) contains `Data/abi` and `Data/Prebuilt/`, and RunCOFF is built once (identical hash across OS legs).

- [ ] **Step 1:** Add/confirm a release step that builds the RunCOFF artifact **once** and attaches it to every leg; ensure `Data/abi` (from Task 1) and `Data/Prebuilt/` (Task 6) are inside the packaged archive.
- [ ] **Step 2: Verify** — inspect a dry-run/`act` or a tag build artifact: both paths present; RunCOFF hash identical across legs.
- [ ] **Step 3: Commit** — `ci(release): ship Data/abi and build-once RunCOFF in the engine archive`.

---

## Follow-on (NOT in this plan)

- **Release N+1 — library strip + ABI 2:** remove the library submodules (keep `CS_COFFLoader`, Sharpire, `AssemblyReferences`, `ConfuserEx-CLI`), bump `Abi.Version` to 2, and ensure the §1 no-match error fires for an unresolved library. Gated on the server resolving all 47 library-backed modules via packs (#1691 + #1735). Its own plan.
- **Server side (#1735 Phase 1):** pass `--source-root`/`--obfuscate-only`/`--confuser-project` gated on ABI ≥ 1; render `empire.crproj` only for ABI 0; read `Data/abi` in `sync_empire_compiler`; route all three RunCOFF compile sites (`generate_script_bof`, `format_bof_output`, standalone `RunCoff` module) through the prebuilt with the `.compiled` wrapping; loud-fail when a pack is held but the engine is ABI 0.
