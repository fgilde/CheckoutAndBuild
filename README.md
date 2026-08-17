# CheckoutAndBuild

Local one-click CI for Visual Studio: keep any number of solutions up to date with a single click — clean, git checkout, NuGet restore, build and test, all out of process and in parallel. Plus a full git cockpit (changes, stashes, history, branches, worktrees, multi-repo sync) and Azure DevOps work item tools.

![CheckoutAndBuild main window](art/screenshot-main.png)

## Install

- **Visual Studio Marketplace**: search for "CheckoutAndBuild" in Extensions → Manage Extensions
- **Manual**: download the `.vsix` from the [latest release](https://github.com/fgilde/CheckoutAndBuild/releases) and double-click it

Requires Visual Studio 2022 or 2026 (amd64) and git on PATH.

## Features

### Pipeline
- Clean → Checkout (git pull) → NuGet restore → Build → Test for every included solution
- Solutions with the same build priority run in parallel; priority groups run in order
- "Suggest Build Priorities" scans cross-solution references and orders the build automatically
- Status bar with elapsed time and ETA, per-solution progress, "retry failed only"
- Build errors and test failures in the Visual Studio Error List, double-click jumps to code
- Script export (`.bat`/`.ps1`), scheduled runs, background-finish notification, taskbar progress
- Working profiles, per-branch settings, per-solution service/property/target overrides
- MEF plugin API: custom services, pre/post actions, build property providers, settings pages

### Git cockpit
- Changes with real VS diff, stage/unstage, commit & push, patch export/apply, zip export
- Stashes, filtered history with per-file follow, branches with ahead/behind badges
- Multi-repo sync, same-branch checkout across repositories, merged-branch cleanup, PR links
- Worktree manager: list, add, remove, prune — switch worktrees straight from the main window

### Azure DevOps
- Work item query view and search & replace across work item text fields (REST, PAT auth)

## Build from source

```
git clone https://github.com/fgilde/CheckoutAndBuild.git
msbuild VisualStudio/CheckoutAndBuild.VisualStudio/CheckoutAndBuild.VisualStudio.csproj /restore
dotnet test VisualStudio/CheckoutAndBuild.Core.Tests
```

The engine (`CheckoutAndBuild.Core`, netstandard2.0) is IDE-free and runs every step through external processes (git, msbuild via vswhere, vstest, nuget/dotnet).

## Release

Push a tag like `v3.1.0`: the release workflow builds the VSIX, runs the tests, attaches the VSIX to a GitHub release and — when the `VS_MARKETPLACE_PAT` secret is configured — publishes it to the Visual Studio Marketplace.

## License

See [LICENSE](LICENSE). The previous generation of the extension lives under `Legacy/` for reference.
