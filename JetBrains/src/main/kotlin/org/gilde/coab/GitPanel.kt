package org.gilde.coab

import com.intellij.ide.BrowserUtil
import com.intellij.openapi.application.ApplicationManager
import com.intellij.openapi.fileChooser.FileChooserFactory
import com.intellij.openapi.fileChooser.FileSaverDescriptor
import com.intellij.openapi.ui.Messages
import com.intellij.ui.components.JBScrollPane
import com.intellij.ui.table.JBTable
import java.awt.BorderLayout
import java.awt.FlowLayout
import java.io.File
import javax.swing.JButton
import javax.swing.JFileChooser
import javax.swing.JPanel
import javax.swing.JTextField
import javax.swing.table.AbstractTableModel

/** Multi-repo git tab: sync state per repository plus fetch/pull/push, branches, stashes, history, patches, PR links, merged-branch cleanup and same-branch checkout across all repositories. */
class GitPanel(private val onLine: (String) -> Unit) : JPanel(BorderLayout()) {

    private var roots: List<File> = emptyList()
    private val infos = mutableListOf<RepoInfo>()
    private val model = RepoTableModel()
    private val table = JBTable(model)
    private val commitMessage = JTextField()

    init {
        table.setShowGrid(false)
        table.setDefaultRenderer(Object::class.java, RepoCellRenderer())

        val toolbar = JPanel(WrapLayout(FlowLayout.LEFT, 6, 2))
        val refresh = JButton("Refresh")
        val fetch = JButton("Fetch")
        val pull = JButton("Pull")
        val push = JButton("Push")
        val forcePush = JButton("Force Push")
        val checkout = JButton("Checkout…")
        val changes = JButton("Changes…")
        val stash = JButton("Stash")
        val stashes = JButton("Stashes…")
        val history = JButton("History…")
        val exportPatch = JButton("Export Patch…")
        val applyPatch = JButton("Apply Patch…")
        val exportZip = JButton("Export Zip…")
        val createPr = JButton("Create PR")
        val cleanup = JButton("Cleanup Merged…")
        val checkoutAll = JButton("Checkout in All…")
        val suggestBranch = JButton("Suggest Branch…")
        listOf(refresh, fetch, pull, push, forcePush, checkout, changes, stash, stashes, history,
            exportPatch, applyPatch, exportZip, createPr, cleanup, checkoutAll, suggestBranch)
            .forEach { toolbar.add(it) }

        val commitRow = JPanel(BorderLayout(6, 0))
        val commitButton = JButton("Commit All && Push")
        val fromWorkItem = JButton("From WI…")
        fromWorkItem.toolTipText = "Prefill the commit message from an Azure DevOps work item (AB#id: title)"
        val commitEast = JPanel(FlowLayout(FlowLayout.RIGHT, 6, 0))
        commitEast.add(fromWorkItem)
        commitEast.add(commitButton)
        commitRow.add(commitMessage, BorderLayout.CENTER)
        commitRow.add(commitEast, BorderLayout.EAST)
        commitMessage.toolTipText = "Commit message"

        val north = JPanel(BorderLayout())
        north.add(toolbar, BorderLayout.NORTH)
        north.add(commitRow, BorderLayout.SOUTH)

        add(north, BorderLayout.NORTH)
        add(JBScrollPane(table), BorderLayout.CENTER)

        refresh.addActionListener { refreshInfos() }
        fetch.addActionListener { onSelected { root -> report("fetch", root, GitOps.fetch(root)) } }
        pull.addActionListener {
            onSelected { root ->
                onLine("git pull: ${root.name}")
                val exit = GitOps.withAutoStash(root, CoabState.get().state.autoStash, { onLine("  $it") }) {
                    GitOps.pull(root, { onLine("  $it") }, { false })
                }
                if (exit != 0) onLine("  pull failed with exit code $exit")
                refreshInfos()
            }
        }
        push.addActionListener {
            onSelected { root ->
                val info = infos.firstOrNull { it.root == root }
                report("push", root, GitOps.push(root, setUpstream = info?.hasUpstream == false, branch = info?.branch ?: ""))
                refreshInfos()
            }
        }
        forcePush.addActionListener {
            val row = table.selectedRow
            if (row < 0 || row >= infos.size) {
                onLine("Select a repository first.")
                return@addActionListener
            }
            val info = infos[row]
            if (Messages.showYesNoDialog(
                    "Force push '${info.branch}' of ${info.root.name}?\n\nUses --force-with-lease, but still overwrites the remote branch.",
                    "Force Push", null) != Messages.YES) return@addActionListener
            ApplicationManager.getApplication().executeOnPooledThread {
                report("force push", info.root, GitOps.forcePush(info.root))
                refreshInfos()
            }
        }
        changes.addActionListener {
            onSelected { root ->
                val list = GitOps.changes(root)
                ApplicationManager.getApplication().invokeLater {
                    if (list.isEmpty()) onLine("No changes in ${root.name}.")
                    else Messages.showInfoMessage(list.joinToString("\n"), "Changes — ${root.name} (${list.size})")
                }
            }
        }
        exportZip.addActionListener {
            val row = table.selectedRow
            if (row < 0 || row >= infos.size) {
                onLine("Select a repository first.")
                return@addActionListener
            }
            val root = infos[row].root
            val descriptor = FileSaverDescriptor("Export Changes as Zip", "", "zip")
            val dialog = FileChooserFactory.getInstance().createSaveFileDialog(descriptor, this)
            val target = dialog.save(null as com.intellij.openapi.vfs.VirtualFile?, "${root.name}-changes") ?: return@addActionListener
            ApplicationManager.getApplication().executeOnPooledThread {
                report("export zip", root, GitOps.exportChangesAsZip(root, target.file))
            }
        }
        suggestBranch.addActionListener { suggestBranchFromWorkItem() }
        fromWorkItem.addActionListener { commitMessageFromWorkItem() }
        checkout.addActionListener { checkoutBranch() }
        commitButton.addActionListener { commitAndPush() }
        stash.addActionListener {
            onSelected { root ->
                report("stash", root, GitOps.stashPush(root, null))
                refreshInfos()
            }
        }
        stashes.addActionListener { manageStashes() }
        history.addActionListener { showHistory() }
        exportPatch.addActionListener { exportPatchAction() }
        applyPatch.addActionListener { applyPatchAction() }
        createPr.addActionListener {
            onSelected { root ->
                val remote = GitOps.remoteUrl(root)
                val info = infos.firstOrNull { it.root == root }
                val url = remote?.let { GitOps.pullRequestUrl(it, info?.branch ?: "") }
                if (url == null) onLine("Cannot build a PR URL for ${root.name} (unknown host).")
                else ApplicationManager.getApplication().invokeLater { BrowserUtil.browse(url) }
            }
        }
        cleanup.addActionListener { cleanupMerged() }
        checkoutAll.addActionListener { checkoutInAll() }
    }

    fun setProjects(projects: List<CoabProject>) {
        roots = projects.mapNotNull { GitOps.repositoryRoot(it.directory) }
            .distinctBy { it.absolutePath.lowercase() }
        refreshInfos()
    }

    fun refresh() = refreshInfos()

    /** Selects the row of the given repository root (after the pending refresh, if one is running). */
    fun selectRepository(root: File) {
        pendingSelection = root
        applyPendingSelection()
    }

    private var pendingSelection: File? = null

    private fun applyPendingSelection() {
        val target = pendingSelection ?: return
        val index = infos.indexOfFirst { it.root.absolutePath.equals(target.absolutePath, ignoreCase = true) }
        if (index >= 0) {
            pendingSelection = null
            table.setRowSelectionInterval(index, index)
            table.scrollRectToVisible(table.getCellRect(index, 0, true))
        }
    }

    private fun refreshInfos() {
        ApplicationManager.getApplication().executeOnPooledThread {
            val loaded = roots.map { GitOps.info(it) }
            ApplicationManager.getApplication().invokeLater {
                infos.clear()
                infos.addAll(loaded)
                model.fireTableDataChanged()
                applyPendingSelection()
            }
        }
    }

    private fun onSelected(action: (File) -> Unit) {
        val row = table.selectedRow
        if (row < 0 || row >= infos.size) {
            onLine("Select a repository first.")
            return
        }
        val root = infos[row].root
        ApplicationManager.getApplication().executeOnPooledThread { action(root) }
    }

    private fun checkoutBranch() {
        val row = table.selectedRow
        if (row < 0 || row >= infos.size) {
            onLine("Select a repository first.")
            return
        }
        val root = infos[row].root
        ApplicationManager.getApplication().executeOnPooledThread {
            val branches = GitOps.branches(root)
            ApplicationManager.getApplication().invokeLater {
                if (branches.isEmpty()) return@invokeLater
                val index = Messages.showChooseDialog(
                    "Checkout which branch in ${root.name}?", "Checkout Branch",
                    branches.toTypedArray(), infos[row].branch, null)
                if (index >= 0) {
                    ApplicationManager.getApplication().executeOnPooledThread {
                        val result = GitOps.withAutoStash(root, CoabState.get().state.autoStash, onLine) {
                            GitOps.checkout(root, branches[index])
                        }
                        report("checkout", root, result)
                        refreshInfos()
                    }
                }
            }
        }
    }

    private fun manageStashes() {
        onSelected { root ->
            val stashes = GitOps.stashes(root)
            ApplicationManager.getApplication().invokeLater {
                if (stashes.isEmpty()) {
                    onLine("No stashes in ${root.name}.")
                    return@invokeLater
                }
                val labels = stashes.map { "stash@{${it.index}}  ${it.description}" }.toTypedArray()
                val index = Messages.showChooseDialog(
                    "Stashes in ${root.name}:", "Stashes", labels, labels.first(), null)
                if (index < 0) return@invokeLater
                val action = Messages.showChooseDialog(
                    "Action for stash@{${stashes[index].index}}?", "Stash Action",
                    arrayOf("apply", "pop", "drop"), "apply", null)
                if (action < 0) return@invokeLater
                ApplicationManager.getApplication().executeOnPooledThread {
                    report("stash", root, GitOps.stashAction(root, arrayOf("apply", "pop", "drop")[action], stashes[index].index))
                    refreshInfos()
                }
            }
        }
    }

    private fun showHistory() {
        onSelected { root ->
            val commits = GitOps.history(root, 100, mineOnly = false, grep = null)
            ApplicationManager.getApplication().invokeLater {
                if (commits.isEmpty()) {
                    onLine("No history in ${root.name}.")
                    return@invokeLater
                }
                val historyModel = object : AbstractTableModel() {
                    private val columns = arrayOf("Commit", "Date", "Author", "Message")
                    override fun getRowCount() = commits.size
                    override fun getColumnCount() = columns.size
                    override fun getColumnName(column: Int) = columns[column]
                    override fun getValueAt(row: Int, column: Int): Any = when (column) {
                        0 -> commits[row].sha
                        1 -> commits[row].date
                        2 -> commits[row].author
                        else -> commits[row].message
                    }
                }
                val historyTable = JBTable(historyModel)
                historyTable.setShowGrid(false)
                historyTable.columnModel.getColumn(0).maxWidth = 90
                historyTable.columnModel.getColumn(1).maxWidth = 100
                historyTable.columnModel.getColumn(2).preferredWidth = 140
                val scroll = JBScrollPane(historyTable)
                scroll.preferredSize = java.awt.Dimension(760, 420)
                val builder = com.intellij.openapi.ui.DialogBuilder(this)
                builder.setTitle("History — ${root.name} (${commits.size})")
                builder.setCenterPanel(scroll)
                builder.addOkAction()
                builder.show()
            }
        }
    }

    private fun suggestBranchFromWorkItem() {
        val row = table.selectedRow
        if (row < 0 || row >= infos.size) {
            onLine("Select a repository first.")
            return
        }
        val root = infos[row].root
        val input = Messages.showInputDialog(
            "Work item id (uses the Work Items connection) — the branch name becomes prefix/id-title:",
            "Suggest Branch", null)?.trim().orEmpty()
        val id = input.toIntOrNull() ?: return
        val prefixIndex = Messages.showChooseDialog(
            "Branch prefix:", "Suggest Branch", arrayOf("wip", "feature", "bugfix", "hotfix"), "wip", null)
        if (prefixIndex < 0) return
        val prefix = arrayOf("wip", "feature", "bugfix", "hotfix")[prefixIndex]
        ApplicationManager.getApplication().executeOnPooledThread {
            val state = CoabState.get().state
            val title = runCatching {
                AzdoClient.workItems(state.azdoOrganization, listOf(id)).firstOrNull()?.title
            }.getOrNull()
            val slug = title?.lowercase()?.map { if (it.isLetterOrDigit()) it else '-' }?.joinToString("")
                ?.replace(Regex("-+"), "-")?.trim('-')?.take(40)
            val branch = if (slug.isNullOrEmpty()) "$prefix/$id" else "$prefix/$id-$slug"
            ApplicationManager.getApplication().invokeLater {
                if (Messages.showYesNoDialog("Create and checkout branch '$branch' in ${root.name}?",
                        "Suggest Branch", null) != Messages.YES) return@invokeLater
                ApplicationManager.getApplication().executeOnPooledThread {
                    report("checkout -b", root, GitOps.createBranch(root, branch))
                    refreshInfos()
                }
            }
        }
    }

    /** Prefills the commit message as "AB#id: title" — the id defaults to the number in the current branch name. */
    private fun commitMessageFromWorkItem() {
        val row = table.selectedRow
        val branch = if (row in infos.indices) infos[row].branch else ""
        val guessed = Regex("(\\d{2,})").find(branch)?.groupValues?.get(1) ?: ""
        val input = Messages.showInputDialog(
            "Work item id (uses the Work Items connection):", "Commit Message from Work Item", null, guessed, null)
            ?.trim().orEmpty()
        val id = input.toIntOrNull() ?: return
        ApplicationManager.getApplication().executeOnPooledThread {
            val state = CoabState.get().state
            val title = runCatching {
                AzdoClient.workItems(state.azdoOrganization, listOf(id)).firstOrNull()?.title
            }.getOrNull()
            ApplicationManager.getApplication().invokeLater {
                commitMessage.text = if (title.isNullOrBlank()) "AB#$id: " else "AB#$id: $title"
                commitMessage.requestFocusInWindow()
                if (title.isNullOrBlank())
                    onLine("Work item title not available — configure the Work Items connection for automatic titles.")
            }
        }
    }

    private fun exportPatchAction() {
        val row = table.selectedRow
        if (row < 0 || row >= infos.size) {
            onLine("Select a repository first.")
            return
        }
        val root = infos[row].root
        val descriptor = FileSaverDescriptor("Export Patch", "", "patch")
        val dialog = FileChooserFactory.getInstance().createSaveFileDialog(descriptor, this)
        val target = dialog.save(null as com.intellij.openapi.vfs.VirtualFile?, "${root.name}-changes") ?: return
        ApplicationManager.getApplication().executeOnPooledThread {
            report("export patch", root, GitOps.exportPatch(root, target.file))
        }
    }

    private fun applyPatchAction() {
        val row = table.selectedRow
        if (row < 0 || row >= infos.size) {
            onLine("Select a repository first.")
            return
        }
        val root = infos[row].root
        val chooser = JFileChooser()
        if (chooser.showOpenDialog(this) != JFileChooser.APPROVE_OPTION) return
        val patch = chooser.selectedFile
        ApplicationManager.getApplication().executeOnPooledThread {
            report("apply patch", root, GitOps.applyPatch(root, patch))
            refreshInfos()
        }
    }

    private fun cleanupMerged() {
        ApplicationManager.getApplication().executeOnPooledThread {
            val candidates = mutableListOf<Pair<File, String>>()
            for (root in roots) {
                val target = GitOps.defaultBranch(root) ?: continue
                GitOps.mergedBranches(root, target).forEach { candidates.add(root to it) }
            }
            ApplicationManager.getApplication().invokeLater {
                if (candidates.isEmpty()) {
                    onLine("No merged branches to clean up.")
                    return@invokeLater
                }
                val labels = candidates.map { "${it.first.name}: ${it.second}" }
                val joined = labels.joinToString("\n")
                val answer = Messages.showYesNoDialog(
                    "Delete ${candidates.size} merged branch(es)?\n\n$joined", "Cleanup Merged Branches", null)
                if (answer != Messages.YES) return@invokeLater
                ApplicationManager.getApplication().executeOnPooledThread {
                    var deleted = 0
                    for ((root, branch) in candidates) {
                        val result = GitOps.deleteBranch(root, branch)
                        if (result.first == 0) deleted++ else onLine("${root.name}: could not delete $branch")
                    }
                    onLine("Deleted $deleted merged branch(es).")
                }
            }
        }
    }

    private fun checkoutInAll() {
        val branch = Messages.showInputDialog(
            "Checkout (or create) which branch in all ${roots.size} repositories?", "Checkout in All", null)
            ?.trim().orEmpty()
        if (branch.isEmpty()) return
        ApplicationManager.getApplication().executeOnPooledThread {
            var switched = 0; var created = 0; var failed = 0
            for (root in roots) {
                val result = if (GitOps.branchExists(root, branch)) {
                    switched++; GitOps.checkout(root, branch)
                } else {
                    created++; GitOps.createBranch(root, branch)
                }
                if (result.first != 0) {
                    failed++
                    onLine("${root.name}: ${result.second}")
                }
            }
            onLine("Checkout '$branch': $switched switched, $created created, $failed failed.")
            refreshInfos()
        }
    }

    private fun commitAndPush() {
        val message = commitMessage.text?.trim().orEmpty()
        if (message.isEmpty()) {
            onLine("Enter a commit message first.")
            return
        }
        onSelected { root ->
            report("commit", root, GitOps.commitAll(root, message))
            val info = infos.firstOrNull { it.root == root }
            report("push", root, GitOps.push(root, setUpstream = info?.hasUpstream == false, branch = info?.branch ?: ""))
            ApplicationManager.getApplication().invokeLater { commitMessage.text = "" }
            refreshInfos()
        }
    }

    private fun report(action: String, root: File, result: Pair<Int, String>) {
        onLine("git $action: ${root.name}${if (result.second.isNotBlank()) "\n  " + result.second.replace("\n", "\n  ") else ""}")
        if (result.first != 0) onLine("  $action failed with exit code ${result.first}")
    }

    private fun report(action: String, result: Pair<Int, String>) {
        if (result.second.isNotBlank()) onLine(result.second)
        if (result.first != 0) onLine("$action failed with exit code ${result.first}")
    }

    private inner class RepoTableModel : AbstractTableModel() {
        private val columns = arrayOf("Repository", "Branch", "Sync", "Dirty")

        override fun getRowCount() = infos.size
        override fun getColumnCount() = columns.size
        override fun getColumnName(column: Int) = columns[column]

        override fun getValueAt(rowIndex: Int, columnIndex: Int): Any {
            val info = infos[rowIndex]
            return when (columnIndex) {
                0 -> info.root.name
                1 -> info.branch
                2 -> when {
                    !info.hasUpstream -> "no upstream"
                    info.ahead == 0 && info.behind == 0 -> "✓"
                    else -> "↑${info.ahead} ↓${info.behind}"
                }
                else -> if (info.dirtyCount == 0) "" else "● ${info.dirtyCount}"
            }
        }
    }

    private inner class RepoCellRenderer : javax.swing.table.DefaultTableCellRenderer() {
        override fun getTableCellRendererComponent(
            table: javax.swing.JTable, value: Any?, isSelected: Boolean, hasFocus: Boolean, row: Int, column: Int
        ): java.awt.Component {
            val component = super.getTableCellRendererComponent(table, value, isSelected, hasFocus, row, column)
            if (!isSelected && row < infos.size) {
                val info = infos[row]
                foreground = when (column) {
                    2 -> when {
                        !info.hasUpstream -> java.awt.Color.GRAY
                        info.ahead == 0 && info.behind == 0 -> java.awt.Color(0x3F, 0xB9, 0x50)
                        else -> java.awt.Color(0x2E, 0xA7, 0xFF)
                    }
                    3 -> if (info.dirtyCount > 0) java.awt.Color(0xE8, 0x8C, 0x00) else table.foreground
                    else -> table.foreground
                }
            }
            return component
        }
    }
}
