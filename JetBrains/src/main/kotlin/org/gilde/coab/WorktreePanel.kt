package org.gilde.coab

import com.intellij.openapi.application.ApplicationManager
import com.intellij.openapi.ui.DialogBuilder
import com.intellij.openapi.ui.DialogWrapper
import com.intellij.openapi.ui.Messages
import com.intellij.ui.components.JBScrollPane
import com.intellij.ui.table.JBTable
import java.awt.BorderLayout
import java.awt.FlowLayout
import java.awt.GridLayout
import java.io.File
import javax.swing.JButton
import javax.swing.JCheckBox
import javax.swing.JComboBox
import javax.swing.JLabel
import javax.swing.JPanel
import javax.swing.JTextField
import javax.swing.table.AbstractTableModel

/** Worktree manager tab: list, add, remove and prune git worktrees per repository. */
class WorktreePanel(
    private val onLine: (String) -> Unit,
    private val onAddFolder: (String) -> Unit
) : JPanel(BorderLayout()) {

    private val repoCombo = JComboBox<String>()
    private var roots: List<File> = emptyList()
    private val worktrees = mutableListOf<WorktreeInfo>()
    private val model = WorktreeTableModel()
    private val table = JBTable(model)

    init {
        table.setShowGrid(false)

        val toolbar = JPanel(FlowLayout(FlowLayout.LEFT, 6, 4))
        val add = JButton("Add Worktree…")
        val remove = JButton("Remove…")
        val prune = JButton("Prune")
        val open = JButton("Open Folder")
        val asFolder = JButton("Add as Working Folder")
        val refresh = JButton("Refresh")
        toolbar.add(JLabel("Repository:"))
        toolbar.add(repoCombo)
        toolbar.add(add)
        toolbar.add(remove)
        toolbar.add(prune)
        toolbar.add(open)
        toolbar.add(asFolder)
        toolbar.add(refresh)

        add(toolbar, BorderLayout.NORTH)
        add(JBScrollPane(table), BorderLayout.CENTER)

        repoCombo.addActionListener { refreshWorktrees() }
        refresh.addActionListener { refreshWorktrees() }
        add.addActionListener { addWorktree() }
        remove.addActionListener { removeWorktree() }
        prune.addActionListener {
            selectedRoot()?.let { root ->
                background {
                    report("prune", GitOps.worktreePrune(root))
                    refreshWorktrees()
                }
            }
        }
        open.addActionListener {
            selectedWorktree()?.let { com.intellij.ide.actions.RevealFileAction.openDirectory(it.path) }
        }
        asFolder.addActionListener {
            selectedWorktree()?.let { onAddFolder(it.path.absolutePath) }
        }
    }

    fun setProjects(projects: List<CoabProject>) {
        roots = projects.mapNotNull { GitOps.repositoryRoot(it.directory) }
            .distinctBy { it.absolutePath.lowercase() }
        val selected = repoCombo.selectedItem as? String
        repoCombo.removeAllItems()
        roots.forEach { repoCombo.addItem(it.name) }
        if (selected != null && roots.any { it.name == selected }) repoCombo.selectedItem = selected
        refreshWorktrees()
    }

    private fun selectedRoot(): File? = roots.getOrNull(repoCombo.selectedIndex)

    private fun selectedWorktree(): WorktreeInfo? =
        worktrees.getOrNull(table.selectedRow)

    private fun refreshWorktrees() {
        val root = selectedRoot() ?: run {
            worktrees.clear(); model.fireTableDataChanged(); return
        }
        background {
            val loaded = GitOps.worktrees(root)
            ApplicationManager.getApplication().invokeLater {
                worktrees.clear()
                worktrees.addAll(loaded)
                model.fireTableDataChanged()
            }
        }
    }

    private fun addWorktree() {
        val root = selectedRoot() ?: return
        background {
            val branches = GitOps.branches(root)
            val inUse = GitOps.worktrees(root).map { it.branch }.toSet()
            ApplicationManager.getApplication().invokeLater {
                val branchBox = JComboBox(branches.filter { it !in inUse }.toTypedArray())
                branchBox.isEditable = true
                val pathField = JTextField(38)
                fun updatePreview() {
                    val branch = (branchBox.editor.item as? String)?.trim().orEmpty()
                    pathField.text = if (branch.isEmpty()) "" else GitOps.worktreeDefaultPath(root, branch).absolutePath
                }
                branchBox.addActionListener { updatePreview() }
                updatePreview()

                val form = JPanel(GridLayout(0, 1, 4, 2))
                form.add(JLabel("Branch (existing = checkout, new name = created with -b):"))
                form.add(branchBox)
                form.add(JLabel("Worktree folder:"))
                form.add(pathField)

                val builder = DialogBuilder(this)
                builder.setTitle("Add Worktree — ${root.name}")
                builder.setCenterPanel(form)
                builder.addOkAction()
                builder.addCancelAction()
                if (builder.show() != DialogWrapper.OK_EXIT_CODE) return@invokeLater
                val branch = (branchBox.editor.item as? String)?.trim().orEmpty()
                val target = pathField.text.trim()
                if (branch.isEmpty() || target.isEmpty()) return@invokeLater
                background {
                    val create = branch !in branches
                    report("worktree add", GitOps.worktreeAdd(root, File(target), branch, create))
                    refreshWorktrees()
                }
            }
        }
    }

    private fun removeWorktree() {
        val root = selectedRoot() ?: return
        val worktree = selectedWorktree() ?: run { onLine("Select a worktree first."); return }
        if (worktree.isMain) {
            onLine("The main worktree cannot be removed.")
            return
        }
        val force = JCheckBox("Force (also with uncommitted changes)")
        val form = JPanel(GridLayout(0, 1, 4, 2))
        form.add(JLabel("Remove worktree '${worktree.path.name}'? The folder is deleted; the branch stays."))
        form.add(force)
        val builder = DialogBuilder(this)
        builder.setTitle("Remove Worktree")
        builder.setCenterPanel(form)
        builder.addOkAction()
        builder.addCancelAction()
        if (builder.show() != DialogWrapper.OK_EXIT_CODE) return
        background {
            report("worktree remove", GitOps.worktreeRemove(root, worktree.path, force.isSelected))
            refreshWorktrees()
        }
    }

    private fun report(action: String, result: Pair<Int, String>) {
        if (result.second.isNotBlank()) onLine(result.second)
        if (result.first != 0) onLine("$action failed with exit code ${result.first}")
        else onLine("$action ok")
    }

    private fun background(action: () -> Unit) {
        ApplicationManager.getApplication().executeOnPooledThread(action)
    }

    private inner class WorktreeTableModel : AbstractTableModel() {
        private val columns = arrayOf("Name", "Branch", "Commit", "Flags", "Path")

        override fun getRowCount() = worktrees.size
        override fun getColumnCount() = columns.size
        override fun getColumnName(column: Int) = columns[column]

        override fun getValueAt(rowIndex: Int, columnIndex: Int): Any {
            val worktree = worktrees[rowIndex]
            return when (columnIndex) {
                0 -> worktree.path.name
                1 -> if (worktree.isDetached) "(detached)" else worktree.branch
                2 -> worktree.sha
                3 -> buildList {
                    if (worktree.isMain) add("main")
                    if (worktree.isLocked) add("locked")
                    if (worktree.isPrunable) add("prunable")
                }.joinToString(" ")
                else -> worktree.path.absolutePath
            }
        }
    }
}
