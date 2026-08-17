package org.gilde.coab

import com.intellij.notification.NotificationGroupManager
import com.intellij.notification.NotificationType
import com.intellij.openapi.application.ApplicationManager
import com.intellij.openapi.fileChooser.FileChooser
import com.intellij.openapi.fileChooser.FileChooserDescriptorFactory
import com.intellij.openapi.fileChooser.FileChooserFactory
import com.intellij.openapi.fileChooser.FileSaverDescriptor
import com.intellij.openapi.project.Project
import com.intellij.openapi.ui.DialogBuilder
import com.intellij.openapi.ui.DialogWrapper
import com.intellij.openapi.ui.Messages
import com.intellij.openapi.wm.WindowManager
import com.intellij.ui.OnePixelSplitter
import com.intellij.ui.components.JBScrollPane
import com.intellij.ui.components.JBTabbedPane
import com.intellij.ui.table.JBTable
import java.awt.BorderLayout
import java.awt.FlowLayout
import java.awt.Font
import java.awt.GridLayout
import java.awt.Taskbar
import java.awt.event.MouseAdapter
import java.awt.event.MouseEvent
import java.io.File
import java.time.LocalDate
import java.time.LocalTime
import javax.swing.JButton
import javax.swing.JCheckBox
import javax.swing.JComboBox
import javax.swing.JLabel
import javax.swing.JMenuItem
import javax.swing.JPanel
import javax.swing.JPopupMenu
import javax.swing.JSpinner
import javax.swing.JTextArea
import javax.swing.JTextField
import javax.swing.SpinnerNumberModel
import javax.swing.Timer
import javax.swing.table.AbstractTableModel

/** The CheckoutAndBuild tool window: pipeline, git, worktrees and work items tabs sharing one console, with profiles, ETA status line, retry-failed, scheduled runs and finish notifications. */
class CoabPanel(private val ideProject: Project) : JPanel(BorderLayout()) {

    private val coabState = CoabState.get()
    private val state = coabState.state
    private val projects = mutableListOf<CoabProject>()
    private val status = mutableMapOf<String, String>()

    private val tableModel = ProjectTableModel()
    private val table = JBTable(tableModel)
    private val console = JTextArea()
    private val gitPanel = GitPanel(::appendLineAsync)
    private val worktreePanel = WorktreePanel(::appendLineAsync) { path -> addFolderPath(path) }
    private val workItemsPanel = WorkItemsPanel(::appendLineAsync)

    private val profileCombo = JComboBox<String>()
    private val pullBox = JCheckBox("Pull")
    private val installBox = JCheckBox("Install/Restore")
    private val buildBox = JCheckBox("Build")
    private val testBox = JCheckBox("Test")
    private val runButton = JButton("▶ CheckoutAndBuild")
    private val cancelButton = JButton("Cancel")
    private val retryButton = JButton("Retry failed")
    private val statusLabel = JLabel("Ready")

    private var runner: PipelineRunner? = null
    private var lastFailed: Set<String> = emptySet()
    private var runStartMillis = 0L
    private var runEstimateSeconds = 0L
    private var currentStepText = ""
    private val elapsedTimer = Timer(1000) { updateStatusLine() }
    private val scheduleTimer = Timer(60000) { checkScheduledRun() }

    init {
        console.isEditable = false
        console.font = Font(Font.MONOSPACED, Font.PLAIN, 12)
        table.setShowGrid(false)
        table.columnModel.getColumn(0).maxWidth = 60
        table.columnModel.getColumn(2).maxWidth = 90
        table.columnModel.getColumn(3).maxWidth = 70
        installContextMenu()

        val toolbar = JPanel(FlowLayout(FlowLayout.LEFT, 6, 4))
        val addButton = JButton("Add Folder…")
        val removeButton = JButton("Remove Folder…")
        val refreshButton = JButton("Rescan")
        val toolsButton = JButton("Tools ▾")
        cancelButton.isEnabled = false
        retryButton.isVisible = false
        runButton.font = runButton.font.deriveFont(Font.BOLD)
        toolbar.add(runButton)
        toolbar.add(cancelButton)
        toolbar.add(retryButton)
        toolbar.add(JLabel("Profile:"))
        toolbar.add(profileCombo)
        toolbar.add(pullBox)
        toolbar.add(installBox)
        toolbar.add(buildBox)
        toolbar.add(testBox)
        toolbar.add(addButton)
        toolbar.add(removeButton)
        toolbar.add(refreshButton)
        toolbar.add(toolsButton)

        val statusRow = JPanel(FlowLayout(FlowLayout.LEFT, 8, 2))
        statusLabel.font = statusLabel.font.deriveFont(Font.ITALIC)
        statusRow.add(statusLabel)

        val north = JPanel(BorderLayout())
        north.add(toolbar, BorderLayout.NORTH)
        north.add(statusRow, BorderLayout.SOUTH)

        val pipelineTab = JPanel(BorderLayout())
        pipelineTab.add(north, BorderLayout.NORTH)
        pipelineTab.add(JBScrollPane(table), BorderLayout.CENTER)

        val tabs = JBTabbedPane()
        tabs.addTab("Pipeline", pipelineTab)
        tabs.addTab("Git", gitPanel)
        tabs.addTab("Worktrees", worktreePanel)
        tabs.addTab("Work Items", workItemsPanel)

        val splitter = OnePixelSplitter(true, 0.6f)
        splitter.firstComponent = tabs
        splitter.secondComponent = JBScrollPane(console)
        add(splitter, BorderLayout.CENTER)

        addButton.addActionListener { addFolder() }
        removeButton.addActionListener { removeFolder() }
        refreshButton.addActionListener { rescan() }
        runButton.addActionListener { runPipeline(includedProjects()) }
        retryButton.addActionListener { runPipeline(includedProjects().filter { it.key in lastFailed }) }
        cancelButton.addActionListener { runner?.cancelled = true }
        toolsButton.addActionListener { toolsMenu().show(toolsButton, 0, toolsButton.height) }
        pullBox.addActionListener { coabState.setStepEnabled(StepKind.PULL, pullBox.isSelected) }
        installBox.addActionListener { coabState.setStepEnabled(StepKind.INSTALL, installBox.isSelected) }
        buildBox.addActionListener { coabState.setStepEnabled(StepKind.BUILD, buildBox.isSelected) }
        testBox.addActionListener { coabState.setStepEnabled(StepKind.TEST, testBox.isSelected) }

        reloadProfiles()
        profileCombo.addActionListener {
            val selected = profileCombo.selectedItem as? String ?: return@addActionListener
            if (selected != state.currentProfile) {
                state.currentProfile = selected
                reloadStepBoxes()
                tableModel.fireTableDataChanged()
            }
        }

        if (state.folders.isEmpty()) {
            ideProject.basePath?.let { base ->
                val path = base.replace('/', File.separatorChar)
                state.folders.add(path)
                appendLine("Added current project folder: $path")
            }
        }
        rescan()
        scheduleTimer.start()
    }

    private fun reloadProfiles() {
        profileCombo.removeAllItems()
        state.profiles.forEach { profileCombo.addItem(it) }
        profileCombo.selectedItem = state.currentProfile
        reloadStepBoxes()
    }

    private fun reloadStepBoxes() {
        pullBox.isSelected = coabState.stepEnabled(StepKind.PULL)
        installBox.isSelected = coabState.stepEnabled(StepKind.INSTALL)
        buildBox.isSelected = coabState.stepEnabled(StepKind.BUILD)
        testBox.isSelected = coabState.stepEnabled(StepKind.TEST)
    }

    private fun toolsMenu(): JPopupMenu {
        val menu = JPopupMenu()
        fun item(text: String, action: () -> Unit) {
            val entry = JMenuItem(text)
            entry.addActionListener { action() }
            menu.add(entry)
        }
        item("Suggest Build Priorities") { suggestPriorities() }
        menu.addSeparator()
        item("Export as .ps1…") { exportScript(powershell = true) }
        item("Export as .bat…") { exportScript(powershell = false) }
        menu.addSeparator()
        item("New Profile…") { newProfile() }
        item("Rename Profile…") { renameProfile() }
        item("Delete Profile") { deleteProfile() }
        menu.addSeparator()
        item("Settings…") { showSettings() }
        return menu
    }

    private fun suggestPriorities() {
        val included = includedProjects()
        ApplicationManager.getApplication().executeOnPooledThread {
            val suggested = DependencyAnalyzer.suggest(included)
            ApplicationManager.getApplication().invokeLater {
                if (suggested.isEmpty()) {
                    appendLine("No cross-solution dependencies found (only .NET solutions are analyzed).")
                    return@invokeLater
                }
                var changed = 0
                for ((key, priority) in suggested) {
                    if (coabState.priority(key) != priority) {
                        coabState.setPriority(key, priority)
                        changed++
                    }
                }
                tableModel.fireTableDataChanged()
                appendLine("Build priorities suggested: ${suggested.values.max() + 1} level(s), $changed project(s) changed.")
            }
        }
    }

    private fun newProfile() {
        val name = Messages.showInputDialog("Profile name:", "New Profile", null)?.trim().orEmpty()
        if (name.isEmpty() || name in state.profiles) return
        state.profiles.add(name)
        state.currentProfile = name
        reloadProfiles()
    }

    private fun renameProfile() {
        val old = state.currentProfile
        if (old == "Default") {
            appendLine("The Default profile cannot be renamed.")
            return
        }
        val name = Messages.showInputDialog("New name for '$old':", "Rename Profile", null)?.trim().orEmpty()
        if (name.isEmpty() || name in state.profiles) return
        state.profiles[state.profiles.indexOf(old)] = name
        state.currentProfile = name
        renameProfileKeys(old, name)
        reloadProfiles()
    }

    private fun renameProfileKeys(old: String, new: String) {
        fun <V> migrate(map: MutableMap<String, V>) {
            val moved = map.filterKeys { it.startsWith("$old|") }
            moved.forEach { (key, value) ->
                map.remove(key)
                map["$new|${key.substringAfter('|')}"] = value
            }
        }
        migrate(state.priorities)
        migrate(state.installOverrides)
        migrate(state.buildOverrides)
        migrate(state.testOverrides)
        val excluded = state.excluded.filter { it.startsWith("$old|") }
        state.excluded.removeAll(excluded.toSet())
        excluded.forEach { state.excluded.add("$new|${it.substringAfter('|')}") }
        state.stepFlags.remove(old)?.let { state.stepFlags[new] = it }
    }

    private fun deleteProfile() {
        val name = state.currentProfile
        if (name == "Default") {
            appendLine("The Default profile cannot be deleted.")
            return
        }
        if (Messages.showYesNoDialog("Delete profile '$name'?", "Delete Profile", null) != Messages.YES) return
        state.profiles.remove(name)
        state.currentProfile = "Default"
        reloadProfiles()
        tableModel.fireTableDataChanged()
    }

    private fun installContextMenu() {
        table.addMouseListener(object : MouseAdapter() {
            override fun mousePressed(e: MouseEvent) = maybeShow(e)
            override fun mouseReleased(e: MouseEvent) = maybeShow(e)

            private fun maybeShow(e: MouseEvent) {
                if (!e.isPopupTrigger) return
                val row = table.rowAtPoint(e.point)
                if (row < 0 || row >= projects.size) return
                table.setRowSelectionInterval(row, row)
                buildContextMenu(projects[row]).show(table, e.x, e.y)
            }
        })
    }

    private fun buildContextMenu(project: CoabProject): JPopupMenu {
        val menu = JPopupMenu()
        fun item(text: String, action: () -> Unit) {
            val entry = JMenuItem(text)
            entry.addActionListener { action() }
            menu.add(entry)
        }
        item("Pull only") { runSingle(project, StepKind.PULL) }
        item("Install only") { runSingle(project, StepKind.INSTALL) }
        item("Build only") { runSingle(project, StepKind.BUILD) }
        item("Test only") { runSingle(project, StepKind.TEST) }
        menu.addSeparator()
        item("Start Application") {
            ApplicationManager.getApplication().executeOnPooledThread { appendLineAsync(AppLauncher.start(project)) }
        }
        item("Stop Application") {
            ApplicationManager.getApplication().executeOnPooledThread { appendLineAsync(AppLauncher.stop(project)) }
        }
        menu.addSeparator()
        item("Open in File Manager") {
            com.intellij.ide.actions.RevealFileAction.openDirectory(project.directory)
        }
        item("Copy Full Path") {
            java.awt.Toolkit.getDefaultToolkit().systemClipboard.setContents(
                java.awt.datatransfer.StringSelection(project.file.absolutePath), null)
        }
        menu.addSeparator()
        item("Project Settings…") { showProjectSettings(project) }
        return menu
    }

    private fun runSingle(project: CoabProject, step: StepKind) {
        if (runner != null) return
        val pipeline = createRunner(estimate = 0)
        ApplicationManager.getApplication().executeOnPooledThread {
            try {
                pipeline.runSingle(project, step)
            } finally {
                finishRun(pipeline)
            }
        }
    }

    private fun showProjectSettings(project: CoabProject) {
        val install = JTextField(coabState.override(StepKind.INSTALL, project.key) ?: "", 36)
        val build = JTextField(coabState.override(StepKind.BUILD, project.key) ?: "", 36)
        val test = JTextField(coabState.override(StepKind.TEST, project.key) ?: "", 36)
        install.toolTipText = project.type.commandFor(StepKind.INSTALL, project) ?: "(no default)"
        build.toolTipText = project.type.commandFor(StepKind.BUILD, project) ?: "(no default)"
        test.toolTipText = project.type.commandFor(StepKind.TEST, project) ?: "(no default)"

        val form = JPanel(GridLayout(0, 1, 4, 2))
        form.add(JLabel("Custom commands for ${project.name} (profile '${state.currentProfile}') — empty uses the ${project.type.name.lowercase()} default:"))
        form.add(JLabel("Install/Restore:"))
        form.add(install)
        form.add(JLabel("Build:"))
        form.add(build)
        form.add(JLabel("Test:"))
        form.add(test)

        val builder = DialogBuilder(this)
        builder.setTitle("Project Settings — ${project.name}")
        builder.setCenterPanel(form)
        builder.addOkAction()
        builder.addCancelAction()
        if (builder.show() == DialogWrapper.OK_EXIT_CODE) {
            coabState.setOverride(StepKind.INSTALL, project.key, install.text)
            coabState.setOverride(StepKind.BUILD, project.key, build.text)
            coabState.setOverride(StepKind.TEST, project.key, test.text)
        }
    }

    private fun showSettings() {
        val parallel = JSpinner(SpinnerNumberModel(state.maxParallel, 1, 16, 1))
        val depth = JSpinner(SpinnerNumberModel(state.scanDepth, 1, 8, 1))
        val failFast = JCheckBox("Stop remaining priority groups when a project fails", state.failFast)
        val scheduled = JCheckBox("Scheduled run (daily)", state.scheduledEnabled)
        val time = JTextField(state.scheduledTime, 6)

        val form = JPanel(GridLayout(0, 2, 8, 4))
        form.add(JLabel("Max parallel projects:"))
        form.add(parallel)
        form.add(JLabel("Folder scan depth:"))
        form.add(depth)
        form.add(failFast)
        form.add(JLabel(""))
        form.add(scheduled)
        form.add(time)

        val builder = DialogBuilder(this)
        builder.setTitle("CheckoutAndBuild Settings")
        builder.setCenterPanel(form)
        builder.addOkAction()
        builder.addCancelAction()
        if (builder.show() == DialogWrapper.OK_EXIT_CODE) {
            state.maxParallel = parallel.value as Int
            state.scanDepth = depth.value as Int
            state.failFast = failFast.isSelected
            state.scheduledEnabled = scheduled.isSelected
            state.scheduledTime = time.text.trim()
        }
    }

    private fun checkScheduledRun() {
        if (!state.scheduledEnabled || runner != null) return
        val scheduled = runCatching { LocalTime.parse(state.scheduledTime) }.getOrNull() ?: return
        val now = LocalTime.now()
        if (now.isBefore(scheduled) || now.isAfter(scheduled.plusMinutes(5))) return
        val today = LocalDate.now().toString()
        if (state.lastScheduledRun == today) return
        state.lastScheduledRun = today
        appendLine("Scheduled run started (${state.scheduledTime}).")
        runPipeline(includedProjects())
    }

    private fun exportScript(powershell: Boolean) {
        val included = includedProjects()
        if (included.isEmpty()) {
            appendLine("Nothing to export.")
            return
        }
        val descriptor = FileSaverDescriptor("Export Pipeline Script", "", if (powershell) "ps1" else "bat")
        val dialog = FileChooserFactory.getInstance().createSaveFileDialog(descriptor, this)
        val target = dialog.save(null as com.intellij.openapi.vfs.VirtualFile?, "CheckoutAndBuild") ?: return
        ApplicationManager.getApplication().executeOnPooledThread {
            val script = ScriptExporter.build(included, enabledSteps(), powershell)
            target.file.writeText(script)
            appendLineAsync("Exported: ${target.file.absolutePath}")
        }
    }

    private fun addFolder() {
        val descriptor = FileChooserDescriptorFactory.createSingleFolderDescriptor()
        val chosen = FileChooser.chooseFile(descriptor, null, null) ?: return
        addFolderPath(chosen.path.replace('/', File.separatorChar))
    }

    private fun addFolderPath(path: String) {
        if (!state.folders.contains(path)) {
            state.folders.add(path)
            rescan()
        }
    }

    private fun removeFolder() {
        if (state.folders.isEmpty()) return
        val options = state.folders.toTypedArray()
        val index = Messages.showChooseDialog(
            "Remove which working folder?", "Remove Folder",
            options, options.first(), null)
        if (index >= 0) {
            state.folders.removeAt(index)
            rescan()
        }
    }

    private fun rescan() {
        appendLine("Scanning ${state.folders.size} folder(s)…")
        ApplicationManager.getApplication().executeOnPooledThread {
            val found = state.folders.map(::File).filter { it.isDirectory }.flatMap { ProjectScanner.scan(it) }
            ApplicationManager.getApplication().invokeLater {
                projects.clear()
                projects.addAll(found)
                status.clear()
                tableModel.fireTableDataChanged()
                appendLine("Found ${found.size} project(s).")
                gitPanel.setProjects(found)
                worktreePanel.setProjects(found)
            }
        }
    }

    private fun includedProjects() = projects.filter { !coabState.isExcluded(it.key) }

    private fun enabledSteps() = StepKind.entries.filter { coabState.stepEnabled(it) }.toSet()

    private fun createRunner(estimate: Long): PipelineRunner {
        runButton.isEnabled = false
        cancelButton.isEnabled = true
        retryButton.isVisible = false
        runStartMillis = System.currentTimeMillis()
        runEstimateSeconds = estimate
        currentStepText = "Running"
        elapsedTimer.start()
        val pipeline = PipelineRunner(::appendLineAsync, { project, text ->
            ApplicationManager.getApplication().invokeLater {
                status[project.key] = text
                tableModel.fireTableRowsUpdated(0, maxOf(0, projects.size - 1))
            }
        }, { progress ->
            ApplicationManager.getApplication().invokeLater { currentStepText = progress; updateStatusLine() }
        })
        runner = pipeline
        return pipeline
    }

    private fun finishRun(pipeline: PipelineRunner) {
        ApplicationManager.getApplication().invokeLater {
            runner = null
            runButton.isEnabled = true
            cancelButton.isEnabled = false
            elapsedTimer.stop()
            lastFailed = pipeline.failedProjects.toSet()
            retryButton.isVisible = lastFailed.isNotEmpty()
            val elapsed = formatSeconds((System.currentTimeMillis() - runStartMillis) / 1000)
            val failed = lastFailed.size
            statusLabel.text = when {
                pipeline.cancelled -> "✗ Cancelled after $elapsed"
                failed > 0 -> "✗ Finished in $elapsed — $failed project(s) failed"
                else -> "✓ Done in $elapsed"
            }
            setTaskbarProgress(-1)
            notifyFinished(pipeline.cancelled, failed)
        }
    }

    private fun notifyFinished(cancelled: Boolean, failed: Int) {
        val group = NotificationGroupManager.getInstance().getNotificationGroup("CheckoutAndBuild") ?: return
        val type = if (failed > 0) NotificationType.ERROR else NotificationType.INFORMATION
        val text = when {
            cancelled -> "Run cancelled."
            failed > 0 -> "Run finished — $failed project(s) failed."
            else -> "Run finished successfully."
        }
        group.createNotification("CheckoutAndBuild", text, type).notify(ideProject)
    }

    private fun updateStatusLine() {
        if (runner == null) return
        val elapsedSeconds = (System.currentTimeMillis() - runStartMillis) / 1000
        var text = "$currentStepText • ${formatSeconds(elapsedSeconds)}"
        if (runEstimateSeconds > elapsedSeconds)
            text += " • ~${formatSeconds(runEstimateSeconds - elapsedSeconds)} left"
        statusLabel.text = text
        if (runEstimateSeconds > 0)
            setTaskbarProgress(((elapsedSeconds * 100) / runEstimateSeconds).toInt().coerceIn(1, 99))
    }

    private fun setTaskbarProgress(percent: Int) {
        runCatching {
            if (!Taskbar.isTaskbarSupported()) return
            val taskbar = Taskbar.getTaskbar()
            if (!taskbar.isSupported(Taskbar.Feature.PROGRESS_VALUE_WINDOW)) return
            val frame = WindowManager.getInstance().getFrame(ideProject) ?: return
            taskbar.setWindowProgressValue(frame, percent)
        }
    }

    private fun formatSeconds(seconds: Long): String {
        val m = seconds / 60
        val s = seconds % 60
        return "%02d:%02d".format(m, s)
    }

    private fun runPipeline(included: List<CoabProject>) {
        if (runner != null) return
        if (included.isEmpty()) {
            appendLine("Nothing to run — add a working folder first.")
            return
        }
        val steps = enabledSteps()
        if (steps.isEmpty()) return

        console.text = ""
        val probe = PipelineRunner({}, { _, _ -> })
        val pipeline = createRunner(probe.estimateSeconds(included, steps))
        ApplicationManager.getApplication().executeOnPooledThread {
            try {
                pipeline.run(included, steps)
            } finally {
                finishRun(pipeline)
            }
        }
    }

    private fun appendLineAsync(line: String) {
        ApplicationManager.getApplication().invokeLater { appendLine(line) }
    }

    private fun appendLine(line: String) {
        console.append(line + "\n")
        console.caretPosition = console.document.length
    }

    private inner class ProjectTableModel : AbstractTableModel() {
        private val columns = arrayOf("Include", "Project", "Type", "Priority", "Status")

        override fun getRowCount() = projects.size
        override fun getColumnCount() = columns.size
        override fun getColumnName(column: Int) = columns[column]

        override fun getColumnClass(columnIndex: Int): Class<*> = when (columnIndex) {
            0 -> java.lang.Boolean::class.java
            3 -> java.lang.Integer::class.java
            else -> String::class.java
        }

        override fun isCellEditable(rowIndex: Int, columnIndex: Int) = columnIndex == 0 || columnIndex == 3

        override fun getValueAt(rowIndex: Int, columnIndex: Int): Any {
            val project = projects[rowIndex]
            return when (columnIndex) {
                0 -> !coabState.isExcluded(project.key)
                1 -> project.name
                2 -> project.type.name.lowercase()
                3 -> coabState.priority(project.key)
                else -> status[project.key] ?: ""
            }
        }

        override fun setValueAt(aValue: Any?, rowIndex: Int, columnIndex: Int) {
            val project = projects[rowIndex]
            when (columnIndex) {
                0 -> coabState.setExcluded(project.key, aValue != true)
                3 -> coabState.setPriority(project.key, (aValue as? Int) ?: 0)
            }
        }
    }
}
