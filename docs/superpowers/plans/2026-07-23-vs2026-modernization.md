# CheckoutAndBuild VS-2026-Modernisierung — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** IDE-freie CheckoutAndBuild-Engine (`CheckoutAndBuild.Core`) plus VS-2026-VSIX-Host mit Tool Window unter `VisualStudio/` bauen; Alt-Code bleibt als Referenz.

**Architecture:** Engine (netstandard2.0) orchestriert Pipeline Clean → Git-Checkout → NuGet-Restore → Build → Test ausschließlich über externe Prozesse (git.exe, nuget.exe, msbuild.exe via vswhere, vstest.console.exe). VSIX-Host (net48, `[18.0,19.0)`) ist dünne WPF-Schale: AsyncPackage + ToolWindow + portierte ViewModels/Views.

**Tech Stack:** .NET Framework 4.8 / netstandard2.0, WPF, VSSDK (18.x-NuGet-Pakete), xunit (net8.0-Testhost), System.Text.Json.

**Spec:** `docs/superpowers/specs/2026-07-23-vs-modernization-design.md`

**Status (2026-07-24):** Phase 1 KOMPLETT (Tasks 1.1–1.10, 60/60 Tests grün; Solution ist `CheckoutAndBuild.slnx`, Tests via `dotnet test VisualStudio/CheckoutAndBuild.Core.Tests`). Task 2.1 KOMPLETT (VSIX baut nur mit VS-msbuild `/restore`, nicht dotnet build; SDK-Pakete 17.14, läuft per API-Kompatibilitätsmodell auch in VS 2026 — keine 18.x-Pakete nötig). Task 2.2/2.3 KOMPLETT (Tool Window läuft, F5-Smoke vom User bestätigt).

**Scope-Erweiterung nach User-Review (2026-07-24): Feature-PARITÄT statt Minimalversion.** Zusätzlich zu 2.4–2.6: alten UI-Look portieren (Themes/Styles), dynamische Settings-UI aus [SettingsProperty], MEF-Plugin-Loading + Custom-Action-Hooks in Pipeline, Script-Export-Menü, Error-List-Integration, Git-Stash-Fenster, eigene Changes-Ansicht mit Export (Ersatz für tote Extended*-TE-Sections — VS bietet die Andockpunkte nicht mehr), Phase 4 WorkItem-REST bleibt. TFVC bleibt raus (User-Entscheid).

## Global Constraints

- Zielordner neu: `VisualStudio/` — Alt-Code in `CheckoutAndBuild2*/`, `SolutionPacker/` NICHT anfassen (Referenz; Umzug nach `Legacy/` erst ganz am Ende).
- Keine Referenz auf `_Assemblies/`-DLLs, keine `Microsoft.TeamFoundation.*`-Referenzen in Core. VSIX-Host: TeamFoundation nur für die dünne TE-Section (Task 2.6, optional).
- Kein `ExposedObject`/`ReflectionHelper`/IL-Emit portieren. Reflection auf VS-Interna verboten.
- Core hat null IDE-Abhängigkeiten — muss mit `dotnet build` bauen.
- VSIX: `InstallationTarget [18.0,19.0)`, AsyncPackage mit `AllowsBackgroundLoading`.
- Commits pro Task, Nachricht englisch `feat:`/`chore:`-Stil.
- Namespaces: `CheckoutAndBuild.Core.*`, `CheckoutAndBuild.VisualStudio.*`. Alt-Namespace `FG.CheckoutAndBuild2.*` nicht übernehmen.
- Portieren = Copy aus Alt-Datei + Entschlacken (TFVC/TE/Reflection raus), nicht Neuschreiben, wo Logik IDE-frei ist.

---

## Phase 1 — Core-Engine

### Task 1.1: Solution + Projektgerüst

**Files:**
- Create: `VisualStudio/CheckoutAndBuild.sln`
- Create: `VisualStudio/CheckoutAndBuild.Core/CheckoutAndBuild.Core.csproj`
- Create: `VisualStudio/CheckoutAndBuild.Core.Tests/CheckoutAndBuild.Core.Tests.csproj`

**Interfaces:** Produces: leere, bauende Solution.

- [ ] Core.csproj: SDK-Style, `<TargetFramework>netstandard2.0</TargetFramework>`, `<LangVersion>latest</LangVersion>`, `<Nullable>disable</Nullable>` (Alt-Code ist nullable-unaware), PackageReference `System.Text.Json` (netstandard2.0-kompatible Version).
- [ ] Tests.csproj: `net8.0`, xunit + `Microsoft.NET.Test.Sdk` + `xunit.runner.visualstudio`, ProjectReference auf Core.
- [ ] `dotnet build VisualStudio/CheckoutAndBuild.sln` → grün; `dotnet test` → 0 Tests, grün.
- [ ] Commit `feat: scaffold CheckoutAndBuild.Core solution`

### Task 1.2: Contracts portieren

**Files:**
- Create: `VisualStudio/CheckoutAndBuild.Core/Contracts/` (aus `CheckoutAndBuild2.Contracts/`)
- Quelle: `CheckoutAndBuild2.Contracts/*.cs` — insbesondere `ServiceIds.cs`, `ServicePriorities.cs`, `Service/IOperationService.cs`, `Service/ICustomAction.cs`, `IScriptGenerator`, `IProjectBuildPropertiesProvider`, `IDefaultBuildPriorityManager`, `ISolutionProjectModel`, Settings-Attribute (`SettingsPropertyAttribute` usw.), `OperationInfo`, `GitRepository`/`GitStash`-Typen

**Interfaces:** Produces: `IOperationService { Guid ServiceId; int Order; Task ExecuteAsync(...); string GetScript(...); }` (Signaturen aus Alt-Code übernehmen, TFVC-Parameter entfernen), `ISolutionProjectModel`, `[SettingsProperty]`.

- [ ] Alle Interfaces/Typen kopieren, Namespace auf `CheckoutAndBuild.Core.Contracts`, TFS-Typen (`Workspace`, `VersionControlServer`, `ITfsContext`-TFVC-Teile) entfernen bzw. `ITfsContext` → `ISourceControlContext` mit Git-only-Surface (RepositoryPath, CurrentBranch).
- [ ] MEF-`[InheritedExport]` beibehalten (System.ComponentModel.Composition-Package für netstandard2.0).
- [ ] Build grün. Commit `feat: port plugin contracts to Core (git-only)`

### Task 1.3: ProcessRunner (Fundament aller Services)

**Files:**
- Create: `VisualStudio/CheckoutAndBuild.Core/Execution/ProcessRunner.cs`
- Test: `CheckoutAndBuild.Core.Tests/ProcessRunnerTests.cs`

**Interfaces:** Produces: `Task<ProcessResult> ProcessRunner.RunAsync(string exe, string args, string workingDir, Action<string> onOutput, CancellationToken ct)`; `ProcessResult { int ExitCode; string StdOut; string StdErr; }`.

- [ ] Test: `cmd /c echo hi` liefert ExitCode 0 + "hi" in StdOut; Cancellation killt Prozess.
- [ ] Implementierung: `Process` mit async Output-Streaming, Kill-on-cancel (Prozessbaum via `taskkill /T /PID` — Alt-Verhalten „KillDependendProcesses").
- [ ] `dotnet test` grün. Commit `feat: add ProcessRunner`

### Task 1.4: Solution-/Projektmodell

**Files:**
- Create: `VisualStudio/CheckoutAndBuild.Core/Model/SolutionProjectModel.cs`, `SolutionParser.cs`
- Quelle: `CheckoutAndBuild2/Types/SolutionProject.cs`, `SolutionPacker/CSProjParser`-Nutzung, `CheckoutAndBuild2/Types/` Modelle (`ChangesetInfo` NICHT — TFVC)
- Test: `CheckoutAndBuild.Core.Tests/SolutionParserTests.cs` (Fixture: Mini-.sln + .csproj im Testordner)

**Interfaces:** Produces: `ISolutionProjectModel`-Implementierung mit `SolutionFileName`, `OutputPath`, `BuildPriority`, `IsIncluded`, enthaltene Projekte.

- [ ] .sln-Parser (Projekteinträge + Konfigurationen) ohne VS-Abhängigkeit — Alt-Logik übernehmen, wo vorhanden; sonst simples Zeilenparsing.
- [ ] Test: parst Fixture-Solution, findet Projekte + OutputPath aus csproj (klassisch UND SDK-Style).
- [ ] Commit `feat: add IDE-free solution/project model`

### Task 1.5: Settings (JSON)

**Files:**
- Create: `VisualStudio/CheckoutAndBuild.Core/Settings/SettingsService.cs`, `SettingsContext.cs`
- Quelle: `CheckoutAndBuild2/Services/SettingsService.cs` (Kontext-Scoping `PrepareKey`, Zeile ~361), `CheckoutAndBuild2/Types/*Settings.cs` (BuildServiceSettings usw.)
- Test: `SettingsServiceTests.cs`

**Interfaces:** Produces: `ISettingsService { T Get<T>(SettingsKey key, T default); void Set<T>(...); }` mit Scoping Profil/Repo/Branch; Persistenz `%AppData%\COAB\settings.json` (Pfad injizierbar für Tests).

- [ ] Settings-Typklassen (`BuildServiceSettings`, `CheckoutServiceSettings`, `CleanServiceSettings`, `NugetServiceSettings`, `UnitTestServiceSettings`, `MiscellaneousSettings`) portieren, TFVC-Properties raus.
- [ ] Test: Set/Get roundtrip, Branch-Scoping (gleicher Key, anderer Branch ⇒ anderer Wert), Datei entsteht.
- [ ] Commit `feat: JSON settings service with repo/branch scoping`

### Task 1.6: Git-Service

**Files:**
- Create: `VisualStudio/CheckoutAndBuild.Core/Git/GitService.cs`
- Quelle: `CheckoutAndBuild2/Git/GitHelper.cs` (Logik), Ausführung neu über ProcessRunner
- Test: `GitServiceTests.cs` (legt temporäres echtes Git-Repo an — git.exe ist auf Dev-Maschinen da)

**Interfaces:** Produces: `GitService { string GetCurrentBranch(dir); Task PullAsync(dir,...); IReadOnlyList<GitStash> GetStashes(dir); Task StashApplyAsync/DropAsync/PushAsync(...); IReadOnlyList<string> GetBranches(dir); }`.

- [ ] Test: init temp repo, commit, branch → GetCurrentBranch stimmt; stash push/list/drop roundtrip.
- [ ] Commit `feat: git.exe-based GitService`

### Task 1.7: Pipeline-Orchestrierung

**Files:**
- Create: `VisualStudio/CheckoutAndBuild.Core/Pipeline/PipelineRunner.cs`, `PausableCancellationToken.cs`
- Quelle: `CheckoutAndBuild2/Services/MainLogic.cs` (`RunCheckoutAndBuild`, `GetIncludedServices`), `PausableCancellationToken`-Typ aus Alt-Code
- Test: `PipelineRunnerTests.cs` mit Fake-`IOperationService`s

**Interfaces:** Produces: `PipelineRunner.RunAsync(IEnumerable<ISolutionProjectModel>, IEnumerable<IOperationService>, PipelineContext, PausableCancellationToken)`; Reihenfolge nach `Order`, Pre/Post-Script-Hooks, Progress-Events (`IProgress<OperationProgress>`).

- [ ] Test: Services laufen in Order-Reihenfolge; Cancel stoppt; Pause blockiert bis Resume; Pre/Post-Hook wird gerufen.
- [ ] Commit `feat: port pipeline orchestration`

### Task 1.8: Die fünf Operation-Services

**Files:**
- Create: `VisualStudio/CheckoutAndBuild.Core/Services/{CleanService,GitCheckoutService,NugetRestoreService,BuildService,TestService}.cs`
- Quellen: `CheckoutAndBuild2/Services/{CleanupService,CheckoutService,NugetRestoreService,LocalBuildService,UnitTestService}.cs`
- Test: je Datei ein Testfile; Build/Test-Service gegen Mini-Fixture-Solution

**Interfaces:** Consumes: ProcessRunner, GitService, SettingsService, SolutionParser. Produces: 5 × `IOperationService` mit alten `ServiceIds`.

- [ ] **CleanService**: OutputPath/IntermediatePath + CustomCleanPathes löschen (Alt-Logik 1:1, IDE-frei).
- [ ] **GitCheckoutService**: `git pull` (Alt: `CheckoutService.cs:264`-Pfad); TFVC-Zweige weglassen.
- [ ] **NugetRestoreService**: `nuget.exe restore` bzw. `dotnet restore` für SDK-Style; nuget.exe-Pfad aus Settings, Fallback `dotnet restore`.
- [ ] **BuildService**: msbuild.exe via vswhere (`vswhere -latest -requires Microsoft.Component.MSBuild -find MSBuild\**\Bin\MSBuild.exe`), Gruppen nach `BuildPriority` sequenziell, innerhalb Gruppe parallel (`MaxNodeCount`/`m:`), Output-Parsing für Fehler (Regex auf `error XX1234:`) → `BuildError`-Liste.
- [ ] **TestService**: vstest.console.exe (via vswhere), `.trx`-Logger, TRX-Parsing portieren aus `MSTest/MSTestRunResult.cs`; `CancelOnFailures` beibehalten.
- [ ] Tests: Clean löscht Fixture-Ordner; Build baut Mini-Solution wirklich (msbuild vorhanden — VS 2026 installiert); Test-Service parst eingechecktes Beispiel-TRX.
- [ ] Commit je Service oder gesammelt `feat: port operation services to out-of-process execution`

### Task 1.9: Script-Export

**Files:**
- Create: `VisualStudio/CheckoutAndBuild.Core/Scripting/ScriptExporter.cs`
- Quelle: `CheckoutAndBuild2/ScriptExportProvider.cs` + `GetScript`-Implementierungen der Alt-Services
- Test: `ScriptExporterTests.cs`

- [ ] `.bat` und `.ps1` Export der konfigurierten Pipeline; Services liefern `GetScript` (bereits in 1.8 mitportiert).
- [ ] Test: Export enthält erwartete Befehle in Service-Reihenfolge.
- [ ] Commit `feat: port script export`

### Task 1.10: Merged-Build (SolutionPacker-Logik)

**Files:**
- Create: `VisualStudio/CheckoutAndBuild.Core/Merge/SolutionMerger.cs`
- Quelle: `SolutionPacker/Packer.cs` (+ CWDev.SLNTools-Ersatz: eigenes sln-Modell aus Task 1.4 erweitern)
- Test: `SolutionMergerTests.cs`

- [ ] Mehrere Fixture-.sln zu einer `Build.sln` mergen, Projekt-GUIDs/Configs korrekt.
- [ ] ponytail: nur Merge-Feature, kein LibZ/log4net/CLI-Gedöns aus SolutionPacker.
- [ ] Commit `feat: port merged-build solution packing`

**Phase-1-Gate:** `dotnet build` + `dotnet test` komplett grün. Danach Phase 2.

---

## Phase 2 — VSIX-Host (VS 2026)

### Task 2.1: VSIX-Projektgerüst

**Files:**
- Create: `VisualStudio/CheckoutAndBuild.VisualStudio/CheckoutAndBuild.VisualStudio.csproj`, `source.extension.vsixmanifest`, `CheckoutAndBuildPackage.cs`
- Modify: `VisualStudio/CheckoutAndBuild.sln`

- [ ] Zuerst verifizieren: aktuelle VSSDK-NuGet-Paketlage für VS 2026 (18.x) — `Microsoft.VisualStudio.SDK` 18.x + `Microsoft.VSSDK.BuildTools` 18.x (WebSearch/nuget.org, dann festnageln). net48, klassisches csproj falls SDK-Style-VSIX mit 18.x nicht sauber geht.
- [ ] `CheckoutAndBuildPackage : AsyncPackage` mit `[PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]`, `InitializeAsync` leer.
- [ ] Manifest: Identity neu (eigene GUID), DisplayName "CheckoutAndBuild", `InstallationTarget [18.0,19.0)` für `Microsoft.VisualStudio.Pro` (deckt alle Editionen? → `Community/Pro/Enterprise`-IDs prüfen; 17.x+ reicht `Microsoft.VisualStudio.Pro` + `amd64`-ProductArchitecture-Attribut!).
- [ ] Build in msbuild (nicht dotnet) → `.vsix` entsteht. Commit `feat: scaffold VS 2026 VSIX host with AsyncPackage`

### Task 2.2: ToolWindow + Main-UI portieren

**Files:**
- Create: `.../ToolWindows/MainToolWindow.cs`, `MainToolWindowControl.xaml`
- Create: `.../ViewModels/` + `.../Controls/` — portiert aus `CheckoutAndBuild2/ViewModels/{MainViewModel,WorkingFolderListViewModel,WorkingFolderViewModel,ProjectViewModel,ProfileSelectorViewModel,GitBranchSelectorViewModel,ServiceSettingsSelectorViewModel}.cs` und `CheckoutAndBuild2/Controls/{CheckoutAndBuildMainSectionView,WorkingFolderTree,GitBranchSelector,ProfileSelector,ServiceSettingsSelector,ProjectViewModelContent,ProjectViewModelOptions}.xaml`
- Create: Befehl im View-Menü (`[ProvideToolWindow]`, VSCT mit einem Button „CheckoutAndBuild")

**Interfaces:** Consumes: Core (PipelineRunner, Services, SettingsService, GitService). WorkspaceSelector (TFVC) entfällt — Working-Folder-Quelle ist konfigurierbare Ordnerliste + Git-Repo-Erkennung.

- [ ] ViewModels entschlacken: `ITeamExplorer`-, TFVC-, `VersionControlExt`-Pfade raus; `DelegateCommand`/`NotificationObject` aus Alt-Code mitnehmen (oder CommunityToolkit.Mvvm — nur falls trivial integrierbar).
- [ ] XAML: `EnvironmentColors`-Keys beibehalten (Shell 18 liefert sie); kaputte Keys einzeln fixen.
- [ ] F5 → Experimental Instance: Fenster öffnet, Ordner wählen, Solutions erscheinen im Tree.
- [ ] Commit `feat: main tool window with ported WPF UI`

### Task 2.3: Pipeline-Ausführung aus der UI

- [ ] Run/Pause/Resume/Cancel verdrahten (MainViewModel-Commands → PipelineRunner), Progress-Bindings, Statusanzeigen pro Solution (Alt: `ProjectViewModel`-Status).
- [ ] Error-List-Integration neu: `ErrorListProvider` (Shell-API, kein Reflection) für Build-/Testfehler; Doppelklick öffnet Datei.
- [ ] Manueller Smoke: echte Mini-Solution durch komplette Pipeline in Experimental Instance.
- [ ] Commit `feat: wire pipeline execution into tool window`

### Task 2.4: Options + Settings-UI

- [ ] Options-Pages portieren (nur relevante: Main + Service-Settings); Settings-Objekt via `GenerateSettingsObjectForInspector`-Ansatz aus Alt-Code oder simples WPF-PropertyGrid.
- [ ] Script-Export-Menü („More…") im ToolWindow.
- [ ] Commit `feat: options pages and script export UI`

### Task 2.5: Git-Stash-Fenster

- [ ] Eigenes kleines ToolWindow, portiert aus `GitStashsSection`/`GitStashDetailSection`-Views, Daten via `GitService` (kein TeamFoundation.Git.Client).
- [ ] Commit `feat: git stash tool window`

### Task 2.6 (optional, Risiko): Dünne TeamExplorer-Section

- [ ] Nur wenn mit 18.6-`Microsoft.TeamFoundation.Controls` (Referenz aus VS-Installationsordner, `Private=False`) eine minimale `ITeamExplorerSection` lädt: Section mit einem Button „Open CheckoutAndBuild" → ToolWindow. Bei Problemen: weglassen, im README dokumentieren.
- [ ] Commit `feat: thin team explorer entry section` (oder Skip-Notiz)

**Phase-2-Gate:** VSIX installierbar in echter VS-2026-Instanz, Pipeline läuft. User-Review.

---

## Phase 3 — Feinschliff

- Task 3.1: MEF-Plugin-Loading im Host (Plugins-Ordner scannen, `ICheckoutAndBuildPlugin.Init`) — async, nicht im Konstruktor.
- Task 3.2: Profile/Branch-abhängige Settings in UI (ProfileSelector), Export/Import `.coab`→JSON.
- Task 3.3: Delphi-/Sonderprojekt-Support nur falls User braucht (nachfragen) — sonst streichen.

## Phase 4 — WorkItem-Tools (Azure-DevOps-REST)

- Task 4.1: `Microsoft.TeamFoundation.WorkItemTracking.WebApi` + `Microsoft.VisualStudio.Services.Client` NuGet; Auth via PAT/`VssConnection`.
- Task 4.2: WorkItem Search&Replace als ToolWindow (portiert aus `WorkItemSearchReplaceViewModel`), Query per WIQL.
- Task 4.3: Recent-Changes/User-Dashboard nur auf User-Wunsch.

## Phase 5 — Rider (separater Plan nach Phase-2-Review)

Eigener Plan `docs/superpowers/plans/YYYY-MM-DD-rider-port.md`, wenn VS-Version läuft: Kotlin-ToolWindow-Frontend, Backend-Prozess hostet Core-Engine (net-Bibliothek), Kommunikation via Rider-Plugin-SDK (ReSharper-Backend) oder simpler: eigenständiger Prozess + Protokoll.

## Abschluss

- Alt-Projekte (`CheckoutAndBuild2*`, `SolutionPacker`, `_Assemblies`, `packages`, alte `CheckoutAndBuild2.sln`) nach `Legacy/` verschieben, README aktualisieren.
