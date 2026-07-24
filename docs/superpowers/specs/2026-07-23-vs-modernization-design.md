# CheckoutAndBuild — Modernisierung für VS 2026 + Rider-Port

Datum: 2026-07-23 · Status: vom User genehmigt

## Ziel

Die VS-2015-Extension CheckoutAndBuild2 (lokale One-Click-CI: Clean → Checkout →
NuGet-Restore → Build → Test über mehrere Solutions) für Visual Studio 2026
neu aufsetzen, danach als Rider-Plugin nachbauen.

## Entscheidungen (User)

| Frage | Entscheidung |
|---|---|
| VCS | **Nur Git** (TFVC/Workspaces/Shelvesets entfallen) |
| UI | **Tool Window als Haupt-UI + dünne TeamExplorer-Section** als Einstieg |
| Features | Kern-Pipeline, Git-Stash-Verwaltung, WorkItem-Tools (via Azure-DevOps-REST, spätere Phase), moderner Test-Runner |
| VS-Ziel | **Nur VS 2026** (`[18.0,19.0)`), net48 |
| Build-Engine | **Out-of-process `msbuild.exe`** (via vswhere) statt in-process MSBuild-API |
| Settings | **JSON** in `%AppData%\COAB` (Profile pro Repo/Branch) statt VS SettingsStore |
| Alter Code | Bleibt während Portierung als Referenz, danach nach `Legacy/` |

## Architektur

Engine ist IDE-frei; IDE-Hosts sind dünne UI-Schalen. Alles Externe läuft
out-of-process (msbuild.exe, vstest.console.exe, git.exe, nuget.exe).

```
VisualStudio/
├── CheckoutAndBuild.sln
├── CheckoutAndBuild.Core/          # netstandard2.0, IDE-frei
│   ├── Pipeline: Orchestrierung (aus MainLogic), Pause/Resume/Cancel
│   ├── Services (IOperationService): Clean, GitCheckout, NugetRestore,
│   │     Build (msbuild.exe, parallel, BuildPriority, Merged-Build via
│   │     SolutionPacker-Logik), Test (vstest.console.exe + TRX-Parsing)
│   ├── Settings: JSON, [SettingsProperty]-Modell portiert
│   ├── Script-Export (.bat/.ps1)
│   └── Contracts (Plugin-SDK, portiert aus CheckoutAndBuild2.Contracts)
└── CheckoutAndBuild.VisualStudio/  # VSIX net48, nur VS 2026
    ├── AsyncPackage (AllowsBackgroundLoading)
    ├── ToolWindow mit portierter WPF-UI (MainView, WorkingFolderTree,
    │     Profile/Branch-Selectors, Progress, Run/Pause/Cancel)
    ├── Dünne TeamExplorer-Section (öffnet Tool Window)
    ├── Options-Pages, Error-List-Integration
    └── Git-Stash-Fenster
```

## Stirbt ersatzlos

`TeamControlFactory` (Reflection auf interne TFS-Dialoge), `ExposedObject`/
`ExposedClass`/`ReflectionHelper` (IL-Emit), WCF-Gallery-Updater +
`VsPackageService`, `VersionControlExt`-DTE-Automation, TFVC-Checkout,
`_Assemblies`-Ordner (31 alte DLLs), MSTest.exe-Runner, WorkItem-Printing,
alle TFVC-Sections (Shelvesets, Changesets, PendingChanges, Checkin).

## Wird portiert (Copy + Entschlacken aus CheckoutAndBuild2/)

- `Services/MainLogic.cs` → Core.Pipeline
- `Services/{CleanupService,CheckoutService,NugetRestoreService,LocalBuildService,UnitTestService}.cs` → Core.Services (Ausführung auf externe Prozesse umgestellt)
- `Services/SettingsService.cs` (Modell + Kontext-Scoping) → Core.Settings, Persistenz JSON
- `ScriptExportProvider` + `GetScript`-Logik → Core
- `SolutionPacker/Packer.cs` Merge-Logik → Core (Merged-Build)
- ViewModels (`MainViewModel`, `WorkingFolderListViewModel`, `ProjectViewModel`, Selectors) + WPF-Views → VSIX-Host, VS-Theming via Shell-18-Ressourcen
- `CheckoutAndBuild2.Contracts` Interfaces → Core.Contracts (netstandard2.0)

## Phasen

1. **Core-Engine**: Projektgerüst, Contracts, Pipeline, 5 Services, Settings,
   Script-Export, Merged-Build. Build grün + Smoke-Checks.
2. **VSIX-Host**: AsyncPackage, ToolWindow + portierte WPF-UI, VSIX-Manifest
   `[18.0,19.0)`, Deployment in Experimental Instance.
3. **Integration**: Git-Stash-Fenster, TE-Section, Options-Pages, Error-List.
4. **Erweitert**: vstest-Runner-Feinschliff, WorkItem-Tools über
   Azure-DevOps-REST (`Microsoft.TeamFoundation.WorkItemTracking.WebApi`).
5. **Rider-Plugin**: Kotlin-Frontend (ToolWindow), Backend nutzt Core-Engine.

## Risiken

- VSIX-Tooling für VS 2026 (18.x SDK-Pakete, Community.VisualStudio.Toolkit-
  Verfügbarkeit) — beim Gerüstbau verifizieren.
- TeamExplorer-Section gegen 18.6-DLLs: nur dünner Link, Fallback = weglassen.
- WPF-XAML-Portierung: alte `EnvironmentColors`-Keys sollten in Shell 18
  weiter existieren; einzeln prüfen.
