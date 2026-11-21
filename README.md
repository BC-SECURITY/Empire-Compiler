# Empire Compiler

The Empire Compiler started as the compiler of the Covenant project. It has since evolved to be an integral part of the Empire framework, aiding in the dynamic compilation and obfuscation of payloads.

## Prerequisites

- [.NET 10.0 SDK](https://dotnet.microsoft.com/download)
- Git

## Build

```bash
dotnet build
```

The repository has no submodules and bundles no library source, and the engine ships no prebuilt RunCOFF (ABI 4). Pass every library's source with `--source-root <dir>` (repeatable; ABI 4 adds `--reference-root <dir>`, repeatable, for pack reference DLLs), where each task's `ReferenceSourceLibraries[].Location` names a directory under one of the roots.

## Test

```bash
dotnet test
```

To run a specific test profile:

```bash
dotnet test --filter "profileName=CSharpPS"
```

Available profiles: `CSharpPS`, `FixtureLib`, `Powershell-New`

## Lint & Format

This project uses [`.editorconfig`](.editorconfig) and [.NET Analyzers](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/overview) to enforce code style and catch common issues.

To auto-fix formatting:

```bash
dotnet format
```

To check formatting without modifying files (used in CI):

```bash
dotnet format --verify-no-changes
```

## Official Discord Channel
Join us in our Discord with any comments, questions, concerns, or problems!

<p align="center">
<a href="https://discord.gg/P8PZPyf">
<img src="https://discordapp.com/api/guilds/716165691383873536/widget.png?style=banner3"/>
</p>
