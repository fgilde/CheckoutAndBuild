package org.gilde.coab

import com.intellij.ide.impl.ProjectUtil
import com.intellij.openapi.application.ApplicationManager
import com.intellij.openapi.ui.DialogBuilder
import com.intellij.openapi.ui.DialogWrapper
import com.intellij.openapi.ui.Messages
import com.intellij.ui.JBColor
import com.intellij.ui.components.JBScrollPane
import com.intellij.ui.table.JBTable
import java.awt.BorderLayout
import java.awt.Component
import java.awt.GridLayout
import java.io.File
import javax.swing.JButton
import javax.swing.JCheckBox
import javax.swing.JComboBox
import javax.swing.JLabel
import javax.swing.JPanel
import javax.swing.JTable
import javax.swing.JTextField
import javax.swing.table.AbstractTableModel
import javax.swing.table.DefaultTableCellRenderer

/** Worktree manager tab: list, add, remove, prune, sync and repair git worktrees per repository. */
class WorktreePanel(
    private val onLine: (String) -> Unit,
    private val onAddFolder: (String) -> Unit,
    private val onBuildFolder: (String) -> Unit = {}
) : JPanel(BorderLayout()) {

    private val repoCombo = JComboBox<String>()
    private var roots: List<File> = emptyList()
    private val worktrees = mutableListOf<WorktreeInfo>()
    private val statuses = mutableMapOf<String, RepoInfo>()
    private val model = WorktreeTableModel()
    private val table = JBTable(model)

    init {
        table.setShowGrid(false)
        table.setDefaultRenderer(Any::class.java, WorktreeCellRenderer())

        val toolbar = JPanel(WrapLayout(java.awt.FlowLayout.LEFT, 6, 4))
        val add = JButton("Add…")
        val remove = JButton("Remove…")
        val prune = JButton("Prune")
        val pull = JButton("Pull")
        val push = JButton("Push")
        val update = JButton("Update from Base")
        val switch = JButton("Switch Branch…")
        val openIde = JButton("Open in IDE")
        val open = JButton("Open Folder")
        val asFolder = JButton("Add as Working Folder")
        val orphans = JButton("Find Orphans…")
        val refresh = JButton("Refresh")
        toolbar.add(JLabel("Repository:"))
        toolbar.add(repoCombo)
        toolbar.add(add)
        toolbar.add(remove)
        toolbar.add(prune)
        toolbar.add(pull)
        toolbar.add(push)
        toolbar.add(update)
        toolbar.add(switch)
        toolbar.add(openIde)
        toolbar.add(open)
        toolbar.add(asFolder)
        toolbar.add(orphans)
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
        pull.addActionListener { runOnSelected("pull") { wt -> ProcessRunner.capture("git pull", wt.path) } }
        push.addActionListener {
            runOnSelected("push") { wt ->
                val info = GitOps.info(wt.path)
                GitOps.push(wt.path, !info.hasUpstream, info.branch)
            }
        }
        update.addActionListener { runOnSelected("update from base") { wt -> GitOps.updateFromBase(wt.path) } }
        switch.addActionListener { switchBranch() }
        openIde.addActionListener {
            selectedWorktree()?.let { ProjectUtil.openOrImport(it.path.absolutePath, null, true) }
        }
        open.addActionListener {
            selectedWorktree()?.let { com.intellij.ide.actions.RevealFileAction.openDirectory(it.path) }
        }
        asFolder.addActionListener {
            selectedWorktree()?.let { onAddFolder(it.path.absolutePath) }
        }
        orphans.addActionListener { findOrphans() }
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
            worktrees.clear(); statuses.clear(); model.fireTableDataChanged(); return
        }
        background {
            val loaded = GitOps.worktrees(root)
            ApplicationManager.getApplication().invokeLater {
                worktrees.clear()
                worktrees.addAll(loaded)
                statuses.clear()
                model.fireTableDataChanged()
            }
            for (worktree in loaded) {
                if (!worktree.path.isDirectory) continue
                val info = GitOps.info(worktree.path)
                ApplicationManager.getApplication().invokeLater {
                    statuses[worktree.path.absolutePath] = info
                    model.fireTableDataChanged()
                }
            }
        }
    }

    private fun runOnSelected(action: String, operation: (WorktreeInfo) -> Pair<Int, String>) {
        val worktree = selectedWorktree() ?: run { onLine("Select a worktree first."); return }
        background {
            report("$action (${worktree.path.name})", operation(worktree))
            refreshWorktrees()
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

                val buildAfter = JCheckBox("Run install && build after create")
                val form = JPanel(GridLayout(0, 1, 4, 2))
                form.add(JLabel("Branch (existing = checkout, new name = created with -b):"))
                form.add(branchBox)
                form.add(JLabel("Worktree folder:"))
                form.add(pathField)
                form.add(buildAfter)

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
                    val result = GitOps.worktreeAdd(root, File(target), branch, create)
                    report("worktree add", result)
                    refreshWorktrees()
                    if (result.first == 0 && buildAfter.isSelected)
                        ApplicationManager.getApplication().invokeLater { onBuildFolder(target) }
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
        val deleteBranch = JCheckBox("Also delete branch '${worktree.branch}'")
        deleteBranch.isEnabled = !worktree.isDetached && worktree.branch.isNotEmpty()
        val form = JPanel(GridLayout(0, 1, 4, 2))
        form.add(JLabel("Remove worktree '${worktree.path.name}'? The folder is deleted."))
        form.add(force)
        form.add(deleteBranch)
        val builder = DialogBuilder(this)
        builder.setTitle("Remove Worktree")
        builder.setCenterPanel(form)
        builder.addOkAction()
        builder.addCancelAction()
        if (builder.show() != DialogWrapper.OK_EXIT_CODE) return
        background {
            val result = GitOps.worktreeRemove(root, worktree.path, force.isSelected)
            report("worktree remove", result)
            if (result.first == 0 && deleteBranch.isSelected)
                report("branch delete", GitOps.deleteBranch(root, worktree.branch, force.isSelected))
            refreshWorktrees()
        }
    }

    private fun switchBranch() {
        val worktree = selectedWorktree() ?: run { onLine("Select a worktree first."); return }
        val root = selectedRoot() ?: return
        background {
            val inUse = GitOps.worktrees(root).map { it.branch }.toSet()
            val branches = GitOps.branches(root).filter { it !in inUse }
            ApplicationManager.getApplication().invokeLater {
                if (branches.isEmpty()) { onLine("No free branches to switch to."); return@invokeLater }
                val choice = Messages.showEditableChooseDialog(
                    "Checkout branch in '${worktree.path.name}' (existing or new name):",
                    "Switch Branch", null, branches.toTypedArray(), branches.first(), null)
                    ?.trim() ?: return@invokeLater
                if (choice.isEmpty()) return@invokeLater
                background {
                    val result =
                        if (GitOps.branchExists(root, choice)) GitOps.checkout(worktree.path, choice)
                        else GitOps.createBranch(worktree.path, choice)
                    report("switch branch (${worktree.path.name})", result)
                    refreshWorktrees()
                }
            }
        }
    }

    private fun findOrphans() {
        val root = selectedRoot() ?: return
        background {
            val orphans = GitOps.orphanWorktreeDirs(root)
            ApplicationManager.getApplication().invokeLater {
                if (orphans.isEmpty()) {
                    onLine("No orphaned worktree folders found next to '${root.name}'.")
                    return@invokeLater
                }
                val list = orphans.joinToString("\n") { it.absolutePath }
                val answer = Messages.showYesNoDialog(
                    "These folders point to worktree metadata that no longer exists:\n\n$list\n\nDelete them?",
                    "Orphaned Worktree Folders", "Delete", "Cancel", Messages.getWarningIcon())
                if (answer != Messages.YES) return@invokeLater
                background {
                    for (dir in orphans) {
                        val ok = dir.deleteRecursively()
                        onLine(if (ok) "deleted: ${dir.absolutePath}" else "delete failed: ${dir.absolutePath}")
                    }
                    report("prune", GitOps.worktreePrune(root))
                    refreshWorktrees()
                }
            }
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
        private val columns = arrayOf("Name", "Branch", "Sync", "Dirty", "Flags", "Path")

        override fun getRowCount() = worktrees.size
        override fun getColumnCount() = columns.size
        override fun getColumnName(column: Int) = columns[column]

        override fun getValueAt(rowIndex: Int, columnIndex: Int): Any {
            val worktree = worktrees[rowIndex]
            val status = statuses[worktree.path.absolutePath]
            return when (columnIndex) {
                0 -> worktree.path.name
                1 -> if (worktree.isDetached) "(detached)" else worktree.branch
                2 -> when {
                    status == null -> "…"
                    !status.hasUpstream -> "no upstream"
                    status.ahead == 0 && status.behind == 0 -> "✓"
                    else -> buildList {
                        if (status.ahead > 0) add("↑${status.ahead}")
                        if (status.behind > 0) add("↓${status.behind}")
                    }.joinToString(" ")
                }
                3 -> when {
                    status == null -> ""
                    status.dirtyCount == 0 -> ""
                    else -> "● ${status.dirtyCount}"
                }
                4 -> buildList {
                    if (worktree.isMain) add("main")
                    if (worktree.isLocked) add("locked")
                    if (worktree.isPrunable) add("prunable")
                }.joinToString(" ")
                else -> worktree.path.absolutePath
            }
        }
    }

    private inner class WorktreeCellRenderer : DefaultTableCellRenderer() {
        override fun getTableCellRendererComponent(
            table: JTable, value: Any?, isSelected: Boolean, hasFocus: Boolean, row: Int, column: Int
        ): Component {
            val component = super.getTableCellRendererComponent(table, value, isSelected, hasFocus, row, column)
            if (!isSelected) {
                foreground = when {
                    column == 2 && value == "✓" -> JBColor(java.awt.Color(0, 128, 0), java.awt.Color(80, 200, 120))
                    column == 2 && value.toString().startsWith("↑") || column == 2 && value.toString().contains("↓") ->
                        JBColor(java.awt.Color(0, 90, 180), java.awt.Color(100, 160, 255))
                    column == 2 && value == "no upstream" -> JBColor.GRAY
                    column == 3 && value.toString().startsWith("●") ->
                        JBColor(java.awt.Color(200, 120, 0), java.awt.Color(240, 170, 60))
                    else -> table.foreground
                }
            }
            return component
        }
    }
}
