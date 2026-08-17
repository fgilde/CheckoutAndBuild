package org.gilde.coab

import com.intellij.openapi.application.ApplicationManager
import com.intellij.ui.components.JBScrollPane
import com.intellij.ui.table.JBTable
import java.awt.BorderLayout
import java.awt.FlowLayout
import java.io.File
import javax.swing.JButton
import javax.swing.JPanel
import javax.swing.JTextField
import javax.swing.table.AbstractTableModel

/** Multi-repo git tab: branch, ahead/behind and dirty state per repository plus fetch/pull/push, branch checkout and commit-all-and-push. */
class GitPanel(private val onLine: (String) -> Unit) : JPanel(BorderLayout()) {

    private var roots: List<File> = emptyList()
    private val infos = mutableListOf<RepoInfo>()
    private val model = RepoTableModel()
    private val table = JBTable(model)
    private val commitMessage = JTextField()

    init {
        table.setShowGrid(false)

        val toolbar = JPanel(FlowLayout(FlowLayout.LEFT, 6, 4))
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

        val commitRow = JPanel(BorderLayout(6, 0))
        val commitButton = JButton("Commit All && Push")
        commitRow.add(commitMessage, BorderLayout.CENTER)
        commitRow.add(commitButton, BorderLayout.EAST)
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
                val index = com.intellij.openapi.ui.Messages.showChooseDialog(
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
