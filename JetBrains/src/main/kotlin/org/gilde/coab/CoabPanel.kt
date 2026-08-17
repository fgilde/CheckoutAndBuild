package org.gilde.coab

import com.intellij.openapi.application.ApplicationManager
import com.intellij.openapi.fileChooser.FileChooser
import com.intellij.openapi.fileChooser.FileChooserDescriptorFactory
import com.intellij.openapi.ui.Messages
import com.intellij.ui.OnePixelSplitter
import com.intellij.ui.components.JBScrollPane
import com.intellij.ui.table.JBTable
import java.awt.BorderLayout
import java.awt.FlowLayout
import java.awt.Font
import javax.swing.JButton
import javax.swing.JCheckBox
import javax.swing.JPanel
import javax.swing.JTextArea
import javax.swing.table.AbstractTableModel

/** The CheckoutAndBuild tool window: working folders, project table with per-project include/priority, step toggles and a run console. */
class CoabPanel : JPanel(BorderLayout()) {

    private val state = CoabState.get().state
    private val projects = mutableListOf<CoabProject>()
    private val status = mutableMapOf<String, String>()

    private val tableModel = ProjectTableModel()
    private val table = JBTable(tableModel)
    private val console = JTextArea()

    private val pullBox = JCheckBox("Pull", state.pull)
    private val installBox = JCheckBox("Install/Restore", state.install)
    private val buildBox = JCheckBox("Build", state.build)
    private val testBox = JCheckBox("Test", state.test)
    private val runButton = JButton("CheckoutAndBuild")
    private val cancelButton = JButton("Cancel")

    private var runner: PipelineRunner? = null

    init {
        console.isEditable = false
        console.font = Font(Font.MONOSPACED, Font.PLAIN, 12)
        table.setShowGrid(false)
        table.columnModel.getColumn(0).maxWidth = 60
        table.columnModel.getColumn(2).maxWidth = 90
        table.columnModel.getColumn(3).maxWidth = 70

        val toolbar = JPanel(FlowLayout(FlowLayout.LEFT, 6, 4))
        val addButton = JButton("Add Folder…")
        val removeButton = JButton("Remove Folder…")
        val refreshButton = JButton("Rescan")
        cancelButton.isEnabled = false
        toolbar.add(addButton)
        toolbar.add(removeButton)
        toolbar.add(refreshButton)
        toolbar.add(pullBox)
        toolbar.add(installBox)
        toolbar.add(buildBox)
        toolbar.add(testBox)
        toolbar.add(runButton)
        toolbar.add(cancelButton)

        val splitter = OnePixelSplitter(true, 0.55f)
        splitter.firstComponent = JBScrollPane(table)
        splitter.secondComponent = JBScrollPane(console)

        add(toolbar, BorderLayout.NORTH)
        add(splitter, BorderLayout.CENTER)

        addButton.addActionListener { addFolder() }
        removeButton.addActionListener { removeFolder() }
        refreshButton.addActionListener { rescan() }
        runButton.addActionListener { runPipeline() }
        cancelButton.addActionListener { runner?.cancelled = true }
        pullBox.addActionListener { state.pull = pullBox.isSelected }
        installBox.addActionListener { state.install = installBox.isSelected }
        buildBox.addActionListener { state.build = buildBox.isSelected }
        testBox.addActionListener { state.test = testBox.isSelected }

        rescan()
    }

    private fun addFolder() {
        val descriptor = FileChooserDescriptorFactory.createSingleFolderDescriptor()
        val chosen = FileChooser.chooseFile(descriptor, null, null) ?: return
        val path = chosen.path.replace('/', java.io.File.separatorChar)
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
            val found = state.folders.map(::java9File).filter { it.isDirectory }.flatMap { ProjectScanner.scan(it) }
            ApplicationManager.getApplication().invokeLater {
                projects.clear()
                projects.addAll(found)
                status.clear()
                tableModel.fireTableDataChanged()
                appendLine("Found ${found.size} project(s).")
            }
        }
    }

    private fun java9File(path: String) = java.io.File(path)

    private fun runPipeline() {
        if (runner != null) return
        val included = projects.filter { it.key !in state.excluded }
        if (included.isEmpty()) {
            appendLine("Nothing to run — add a working folder first.")
            return
        }
        val steps = buildSet {
            if (state.pull) add(StepKind.PULL)
            if (state.install) add(StepKind.INSTALL)
            if (state.build) add(StepKind.BUILD)
            if (state.test) add(StepKind.TEST)
        }
        if (steps.isEmpty()) return

        console.text = ""
        runButton.isEnabled = false
        cancelButton.isEnabled = true
        val pipeline = PipelineRunner(::appendLineAsync) { project, text ->
            ApplicationManager.getApplication().invokeLater {
                status[project.key] = text
                tableModel.fireTableRowsUpdated(0, maxOf(0, projects.size - 1))
            }
        }
        runner = pipeline
        ApplicationManager.getApplication().executeOnPooledThread {
            try {
                pipeline.run(included, { state.priorities[it.key] ?: 0 }, steps)
            } finally {
                ApplicationManager.getApplication().invokeLater {
                    runner = null
                    runButton.isEnabled = true
                    cancelButton.isEnabled = false
                }
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
