# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Empire-Compiler is a .NET 10 console application that dynamically compiles C# task code into executable assemblies with optional obfuscation. It originated from the Covenant project and is now part of the Empire framework. It uses Roslyn for compilation and ConfuserEx for obfuscation.

## Build & Test Commands

```bash
# Prerequisites: .NET 10 SDK, git submodules
git submodule update --init --recursive
dotnet build

# Run all tests
dotnet test

# Run a specific test profile
dotnet test --filter "profileName=CSharpPS"
# Available profiles: CSharpPS, Seatbelt, Powershell-Old, Powershell-New

# Format / lint
dotnet format                        # auto-fix
dotnet format --verify-no-changes    # check only (used in CI)
```

## Architecture

### Compilation Pipeline

Base64 YAML input → deserialize to `SerializedGruntTask` → create `AgentTask` → Roslyn compilation → optional code optimization (unused type removal) → optional ConfuserEx obfuscation → output bytes to file.

### Key Source Files

- **`EmpireCompiler/Program.cs`** — CLI entry point using System.CommandLine. Options: `--output`, `--yaml` (base64), `--dotnet-version`, `--confuse`, `--debug`.
- **`EmpireCompiler/Core/Compiler.cs`** — Central compilation engine. `CompileCSharpRoslyn()` handles Roslyn compilation, code optimization via semantic analysis (type dependency graph traversal), and ConfuserEx obfuscation as a subprocess.
- **`EmpireCompiler/Core/Common.cs`** — Path constants (`EmpireDirectory`, `EmpireDataDirectory`, etc.), `DotNetVersion` enum, default reference assembly lists per framework version.
- **`EmpireCompiler/Models/Module/AgentTask.cs`** — Core compilation unit. Holds task code, references, embedded resources, source libraries. Has `Compile(DotNetVersion)` entry point and framework-specific compilation methods (`CompileDotNet35/40/45`).
- **`EmpireCompiler/Models/Module/TaskComponents.cs`** — Supporting models: `ReferenceAssembly`, `EmbeddedResource`, `ReferenceSourceLibrary` with junction tables.

### Data Directory (`EmpireCompiler/Data/`)

- **`AssemblyReferences/net35|net40|net45/`** — .NET Framework reference DLLs used during compilation.
- **`ReferenceSourceLibraries/`** — Git submodules (SharpSploit, Rubeus, Seatbelt, etc.) compiled as source libraries into tasks.
- **`EmbeddedResources/`** — Files embedded into compiled assemblies (e.g., `launcher.txt`).
- **`ConfuserEx-CLI/`** — Obfuscation tooling invoked as a subprocess.
- **`Tasks/`** — Example task templates (simple C# classes with static `Execute()` methods).

### YAML Task Format

Tasks are defined in YAML with fields: Name, Language (`csharp`), CompatibleDotNetVersions, Code (C# source), ReferenceSourceLibraries, ReferenceAssemblies, EmbeddedResources. The YAML is base64-encoded when passed to the CLI.

### Test Structure

- **`EmpireCompiler.Tests/Unit/`** — xUnit tests that compile from YAML profiles and verify output.
- **`EmpireCompiler.Tests/Integration/`** — End-to-end tests that spawn the compiler as a subprocess.
- **`EmpireCompiler.Tests/Helpers/`** — `TestHelper.cs` (path setup, temp files), `ProfileYamlData.cs` (base64-encoded YAML test profiles).

### Dependencies

- **Roslyn** (`Microsoft.CodeAnalysis.CSharp`) — C# parsing, semantic analysis, compilation
- **YamlDotNet** — Task YAML deserialization
- **ConfuserEx** (local DLLs in `refs/`) — Assembly obfuscation (rename, anti-ILDASM, control flow)
- **Newtonsoft.Json** — JSON serialization
- **System.CommandLine** — CLI argument parsing

## Code Style

Enforced via `.editorconfig` and .NET Analyzers (`Directory.Build.props`). `EnforceCodeStyleInBuild` is enabled. CI runs `dotnet format --verify-no-changes` on PRs.
