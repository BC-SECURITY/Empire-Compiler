# Empire Compiler

The Empire Compiler started as the compiler of the Covenant project. It has since evolved to be an integral part of the Empire framework, aiding in the dynamic compilation and obfuscation of payloads.

## Prerequisites

- [.NET 10.0 SDK](https://dotnet.microsoft.com/download)
- Git (with submodule support)

## Build

```bash
git submodule update --init --recursive
dotnet build
```

## Test

```bash
dotnet test
```

To run a specific test profile:

```bash
dotnet test --filter "profileName=CSharpPS"
```

Available profiles: `CSharpPS`, `Seatbelt`, `Powershell-Old`, `Powershell-New`

## Official Discord Channel
Join us in our Discord with any comments, questions, concerns, or problems!

<p align="center">
<a href="https://discord.gg/P8PZPyf">
<img src="https://discordapp.com/api/guilds/716165691383873536/widget.png?style=banner3"/>
</p>
