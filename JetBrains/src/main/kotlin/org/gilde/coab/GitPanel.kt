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

        val toolbar = JPanel(FlowLayout(FlowLayout.LEFT, 6, 2))
        val refresh = JButton("Refresh")
        val fetch = JButton("Fetch")
        val pull = JButton("Pull")
        val push = JButton("Push")
        val checkout = JButton("Checkout…")
        toolbar.add(refresh)
        toolbar.add(fetch)
        toolbar.add(pull)
        toolbar.add(push)
        toolbar.add(checkout)

        val toolbar2 = JPanel(FlowLayout(FlowLayout.LEFT, 6, 2))
        val stash = JButton("Stash")
        val stashes = JButton("Stashes…")
        val history = JButton("History…")
        val exportPatch = JButton("Export Patch…")
        val applyPatch = JButton("Apply Patch…")
        val createPr = JButton("Create PR")
        val cleanup = JButton("Cleanup Merged…")
        val checkoutAll = JButton("Checkout in All…")
        toolbar2.add(stash)
        toolbar2.add(stashes)
        toolbar2.add(history)
        toolbar2.add(exportPatch)
        toolbar2.add(applyPatch)
        toolbar2.add(createPr)
        toolbar2.add(cleanup)
        toolbar2.add(checkoutAll)

        val commitRow = JPanel(BorderLayout(6, 0))
        val commitButton = JButton("Commit All && Push")
        commitRow.add(commitMessage, BorderLayout.CENTER)
        commitRow.add(commitButton, BorderLayout.EAST)
        commitMessage.toolTipText = "Commit message"

        val north = JPanel(BorderLayout())
        val toolbars = JPanel(BorderLayout())
        toolbars.add(toolbar, BorderLayout.NORTH)
        toolbars.add(toolbar2, BorderLayout.SOUTH)
        north.add(toolbars, BorderLayout.NORTH)
        north.add(commitRow, BorderLayout.SOUTH)

        add(north, BorderLayout.NORTH)
        add(JBScrollPane(table), BorderLayout.CENTER)

        refresh.addActionListener { refreshInfos() }
        fetch.addActionListener { onSelected { root -> report("fetch", root, GitOps.fetch(root)) } }
        pull.addActionListener {
            onSelected { root ->
                onLine("git pull: ${root.name}")
                val exit = GitOps.pull(root, { onLine("  $it") }, { false })
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

    private fun refreshInfos() {
        ApplicationManager.getApplication().executeOnPooledThread {
            val loaded = roots.map { GitOps.info(it) }
            ApplicationManager.getApplication().invokeLater {
                infos.clear()
                infos.addAll(loaded)
                model.fireTableDataChanged()
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
                        report("checkout", root, GitOps.checkout(root, branches[index]))
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
            val commits = GitOps.history(root, 60, mineOnly = false, grep = null)
            ApplicationManager.getApplication().invokeLater {
                if (commits.isEmpty()) {
                    onLine("No history in ${root.name}.")
                    return@invokeLater
                }
                onLine("=== History: ${root.name} (${commits.size}) ===")
                commits.forEach { onLine("${it.sha}  ${it.date}  ${it.author.padEnd(18).take(18)}  ${it.message}") }
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
        private val columns = arrayOf("Repository", "Branch", "Ahead", "Behind", "Dirty")

        override fun getRowCount() = infos.size
        override fun getColumnCount() = columns.size
        override fun getColumnName(column: Int) = columns[column]

        override fun getValueAt(rowIndex: Int, columnIndex: Int): Any {
            val info = infos[rowIndex]
            return when (columnIndex) {
                0 -> info.root.name
                1 -> info.branch
                2 -> if (info.hasUpstream) info.ahead else "-"
                3 -> if (info.hasUpstream) info.behind else "-"
                else -> info.dirtyCount
            }
        }
    }
}
