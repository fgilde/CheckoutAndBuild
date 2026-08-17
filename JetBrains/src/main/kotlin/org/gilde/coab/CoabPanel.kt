package org.gilde.coab

import com.intellij.openapi.application.ApplicationManager
import com.intellij.openapi.fileChooser.FileChooser
import com.intellij.openapi.fileChooser.FileChooserDescriptorFactory
import com.intellij.openapi.project.Project
import com.intellij.openapi.ui.DialogBuilder
import com.intellij.openapi.ui.Messages
import com.intellij.ui.OnePixelSplitter
import com.intellij.ui.components.JBScrollPane
import com.intellij.ui.components.JBTabbedPane
import com.intellij.ui.table.JBTable
import java.awt.BorderLayout
import java.awt.FlowLayout
import java.awt.Font
import java.awt.GridLayout
import java.awt.event.MouseAdapter
import java.awt.event.MouseEvent
import java.io.File
import javax.swing.JButton
import javax.swing.JCheckBox
import javax.swing.JLabel
import javax.swing.JMenuItem
import javax.swing.JPanel
import javax.swing.JPopupMenu
import javax.swing.JSpinner
import javax.swing.JTextArea
import javax.swing.JTextField
import javax.swing.SpinnerNumberModel
import javax.swing.table.AbstractTableModel

/** The CheckoutAndBuild tool window: pipeline tab (working folders, project table, steps, export) and git tab, sharing one console. */
class CoabPanel(ideProject: Project) : JPanel(BorderLayout()) {

    private val state = CoabState.get().state
    private val projects = mutableListOf<CoabProject>()
    private val status = mutableMapOf<String, String>()

    private val tableModel = ProjectTableModel()
    private val table = JBTable(tableModel)
    private val console = JTextArea()
    private val gitPanel = GitPanel(::appendLineAsync)

    private val pullBox = JCheckBox("Pull", state.pull)
    private val installBox = JCheckBox("Install/Restore", state.install)
    private val buildBox = JCheckBox("Build", state.build)
    private val testBox = JCheckBox("Test", state.test)
    private val runButton = JButton("▶ CheckoutAndBuild")
    private val cancelButton = JButton("Cancel")

    private var runner: PipelineRunner? = null

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
        val settingsButton = JButton("⚙")
        val exportButton = JButton("Export…")
        cancelButton.isEnabled = false
        runButton.font = runButton.font.deriveFont(Font.BOLD)
        toolbar.add(runButton)
        toolbar.add(cancelButton)
        toolbar.add(pullBox)
        toolbar.add(installBox)
        toolbar.add(buildBox)
        toolbar.add(testBox)
        toolbar.add(addButton)
        toolbar.add(removeButton)
        toolbar.add(refreshButton)
        toolbar.add(exportButton)
        toolbar.add(settingsButton)

        val pipelineTab = JPanel(BorderLayout())
        pipelineTab.add(toolbar, BorderLayout.NORTH)
        pipelineTab.add(JBScrollPane(table), BorderLayout.CENTER)

        val tabs = JBTabbedPane()
        tabs.addTab("Pipeline", pipelineTab)
        tabs.addTab("Git", gitPanel)

        val splitter = OnePixelSplitter(true, 0.6f)
        splitter.firstComponent = tabs
        splitter.secondComponent = JBScrollPane(console)
        add(splitter, BorderLayout.CENTER)

        addButton.addActionListener { addFolder() }
        removeButton.addActionListener { removeFolder() }
        refreshButton.addActionListener { rescan() }
        runButton.addActionListener { runPipeline() }
        cancelButton.addActionListener { runner?.cancelled = true }
        settingsButton.addActionListener { showSettings() }
        exportButton.addActionListener { exportScript() }
        pullBox.addActionListener { state.pull = pullBox.isSelected }
        installBox.addActionListener { state.install = installBox.isSelected }
        buildBox.addActionListener { state.build = buildBox.isSelected }
        testBox.addActionListener { state.test = testBox.isSelected }

        if (state.folders.isEmpty()) {
            ideProject.basePath?.let { base ->
                val path = base.replace('/', File.separatorChar)
                state.folders.add(path)
                appendLine("Added current project folder: $path")
            }
        }
        rescan()
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
        fun item(text: String, action: () -> Unit): JMenuItem {
            val entry = JMenuItem(text)
            entry.addActionListener { action() }
            menu.add(entry)
            return entry
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
        val pipeline = createRunner()
        ApplicationManager.getApplication().executeOnPooledThread {
            try {
                pipeline.runSingle(project, step)
            } finally {
                finishRun()
            }
        }
    }

    private fun showProjectSettings(project: CoabProject) {
        val install = JTextField(state.installOverrides[project.key] ?: "", 36)
        val build = JTextField(state.buildOverrides[project.key] ?: "", 36)
        val test = JTextField(state.testOverrides[project.key] ?: "", 36)
        install.toolTipText = project.type.commandFor(StepKind.INSTALL, project) ?: "(no default)"
        build.toolTipText = project.type.commandFor(StepKind.BUILD, project) ?: "(no default)"
        test.toolTipText = project.type.commandFor(StepKind.TEST, project) ?: "(no default)"

        val form = JPanel(GridLayout(0, 1, 4, 2))
        form.add(JLabel("Custom commands for ${project.name} — empty uses the ${project.type.name.lowercase()} default (shown as tooltip):"))
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
        if (builder.show() == com.intellij.openapi.ui.DialogWrapper.OK_EXIT_CODE) {
            fun apply(map: MutableMap<String, String>, field: JTextField) {
                val value = field.text.trim()
                if (value.isEmpty()) map.remove(project.key) else map[project.key] = value
            }
            apply(state.installOverrides, install)
            apply(state.buildOverrides, build)
            apply(state.testOverrides, test)
        }
    }

    private fun showSettings() {
        val parallel = JSpinner(SpinnerNumberModel(state.maxParallel, 1, 16, 1))
        val depth = JSpinner(SpinnerNumberModel(state.scanDepth, 1, 8, 1))
        val failFast = JCheckBox("Stop remaining priority groups when a project fails", state.failFast)

        val form = JPanel(GridLayout(0, 2, 8, 4))
        form.add(JLabel("Max parallel projects:"))
        form.add(parallel)
        form.add(JLabel("Folder scan depth:"))
        form.add(depth)
        form.add(failFast)
        form.add(JLabel(""))

        val builder = DialogBuilder(this)
        builder.setTitle("CheckoutAndBuild Settings")
        builder.setCenterPanel(form)
        builder.addOkAction()
        builder.addCancelAction()
        if (builder.show() == com.intellij.openapi.ui.DialogWrapper.OK_EXIT_CODE) {
            state.maxParallel = parallel.value as Int
            state.scanDepth = depth.value as Int
            state.failFast = failFast.isSelected
        }
    }

    private fun exportScript() {
        val included = includedProjects()
        if (included.isEmpty()) {
            appendLine("Nothing to export.")
            return
        }
        val choice = Messages.showChooseDialog(
            "Export the configured pipeline as:", "Export Script",
            arrayOf("PowerShell (.ps1)", "Batch (.bat)"), "PowerShell (.ps1)", null)
        if (choice < 0) return
        val powershell = choice == 0
        val descriptor = com.intellij.openapi.fileChooser.FileSaverDescriptor(
            "Export Pipeline Script", "", if (powershell) "ps1" else "bat")
        val dialog = com.intellij.openapi.fileChooser.FileChooserFactory.getInstance().createSaveFileDialog(descriptor, this)
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
        val path = chosen.path.replace('/', File.separatorChar)
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
            }
        }
    }

    private fun includedProjects() = projects.filter { it.key !in state.excluded }

    private fun enabledSteps() = buildSet {
        if (state.pull) add(StepKind.PULL)
        if (state.install) add(StepKind.INSTALL)
        if (state.build) add(StepKind.BUILD)
        if (state.test) add(StepKind.TEST)
    }

    private fun createRunner(): PipelineRunner {
        runButton.isEnabled = false
        cancelButton.isEnabled = true
        val pipeline = PipelineRunner(::appendLineAsync) { project, text ->
            ApplicationManager.getApplication().invokeLater {
                status[project.key] = text
                tableModel.fireTableRowsUpdated(0, maxOf(0, projects.size - 1))
            }
        }
        runner = pipeline
        return pipeline
    }

    private fun finishRun() {
        ApplicationManager.getApplication().invokeLater {
            runner = null
            runButton.isEnabled = true
            cancelButton.isEnabled = false
        }
    }

    private fun runPipeline() {
        if (runner != null) return
        val included = includedProjects()
        if (included.isEmpty()) {
            appendLine("Nothing to run — add a working folder first.")
            return
        }
        val steps = enabledSteps()
        if (steps.isEmpty()) return

        console.text = ""
        val pipeline = createRunner()
        ApplicationManager.getApplication().executeOnPooledThread {
            try {
                pipeline.run(included, steps)
            } finally {
                finishRun()
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
                0 -> project.key !in state.excluded
                1 -> project.name
                2 -> project.type.name.lowercase()
                3 -> state.priorities[project.key] ?: 0
                else -> status[project.key] ?: ""
            }
        }

        override fun setValueAt(aValue: Any?, rowIndex: Int, columnIndex: Int) {
            val project = projects[rowIndex]
            when (columnIndex) {
                0 -> if (aValue == true) state.excluded.remove(project.key) else state.excluded.add(project.key)
                3 -> state.priorities[project.key] = (aValue as? Int) ?: 0
            }
        }
    }
}
