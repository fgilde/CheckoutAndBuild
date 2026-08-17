package org.gilde.coab

import com.intellij.ide.BrowserUtil
import com.intellij.openapi.application.ApplicationManager
import com.intellij.ui.components.JBPasswordField
import com.intellij.ui.components.JBScrollPane
import com.intellij.ui.table.JBTable
import java.awt.BorderLayout
import java.awt.FlowLayout
import java.awt.GridLayout
import java.awt.event.MouseAdapter
import java.awt.event.MouseEvent
import javax.swing.JButton
import javax.swing.JLabel
import javax.swing.JPanel
import javax.swing.JTextArea
import javax.swing.JTextField
import javax.swing.table.AbstractTableModel

/** Azure DevOps tab: WIQL query view plus text search & replace across work item fields (REST, PAT in the password safe). */
class WorkItemsPanel(private val onLine: (String) -> Unit) : JPanel(BorderLayout()) {

    private val state = CoabState.get().state
    private val organization = JTextField(state.azdoOrganization, 24)
    private val project = JTextField(state.azdoProject, 14)
    private val patField = JBPasswordField()
    private val wiql = JTextArea(state.azdoWiql, 2, 60)
    private val search = JTextField(14)
    private val replace = JTextField(14)

    private val items = mutableListOf<WorkItem>()
    private val matchedFields = mutableMapOf<Int, List<String>>()
    private var textFieldNames: Map<String, String> = emptyMap()
    private val model = ItemTableModel()
    private val table = JBTable(model)

    init {
        table.setShowGrid(false)
        patField.text = AzdoClient.pat

        val form = JPanel(GridLayout(0, 1, 2, 2))
        val row1 = JPanel(FlowLayout(FlowLayout.LEFT, 6, 2))
        row1.add(JLabel("Organization URL:"))
        row1.add(organization)
        row1.add(JLabel("Project:"))
        row1.add(project)
        row1.add(JLabel("PAT:"))
        patField.columns = 14
        row1.add(patField)
        val row2 = JPanel(BorderLayout(6, 0))
        row2.add(JLabel("WIQL:"), BorderLayout.WEST)
        row2.add(wiql, BorderLayout.CENTER)
        val row3 = JPanel(FlowLayout(FlowLayout.LEFT, 6, 2))
        val queryButton = JButton("Run Query")
        val newButton = JButton("New Work Item…")
        row3.add(queryButton)
        row3.add(newButton)
        row3.add(JLabel("Search:"))
        row3.add(search)
        row3.add(JLabel("Replace:"))
        row3.add(replace)
        val previewButton = JButton("Preview")
        val replaceButton = JButton("Replace All")
        row3.add(previewButton)
        row3.add(replaceButton)
        form.add(row1)
        form.add(row2)
        form.add(row3)

        add(form, BorderLayout.NORTH)
        add(JBScrollPane(table), BorderLayout.CENTER)

        queryButton.addActionListener { runQuery(filterTerm = null) }
        previewButton.addActionListener {
            val term = search.text.trim()
            if (term.isEmpty()) onLine("Enter a search term first.") else runQuery(term)
        }
        replaceButton.addActionListener { replaceAll() }
        newButton.addActionListener {
            saveConnection()
            val type = items.map { it.type }.firstOrNull { it.isNotEmpty() } ?: "Bug"
            BrowserUtil.browse(AzdoClient.createUrl(organization.text.trim(), project.text.trim(), type))
        }
        table.addMouseListener(object : MouseAdapter() {
            override fun mouseClicked(e: MouseEvent) {
                if (e.clickCount != 2) return
                val row = table.selectedRow
                if (row in items.indices)
                    BrowserUtil.browse(AzdoClient.itemUrl(organization.text.trim(), project.text.trim(), items[row].id))
            }
        })
    }

    private fun saveConnection() {
        state.azdoOrganization = organization.text.trim()
        state.azdoProject = project.text.trim()
        state.azdoWiql = wiql.text.trim()
        AzdoClient.pat = String(patField.password)
    }

    private fun runQuery(filterTerm: String?) {
        saveConnection()
        if (state.azdoOrganization.isEmpty() || state.azdoProject.isEmpty() || AzdoClient.pat.isEmpty()) {
            onLine("Configure organization URL, project and PAT first.")
            return
        }
        onLine(if (filterTerm == null) "Running work item query…" else "Searching for \"$filterTerm\"…")
        ApplicationManager.getApplication().executeOnPooledThread {
            try {
                val ids = AzdoClient.queryIds(state.azdoOrganization, state.azdoProject, state.azdoWiql)
                textFieldNames = AzdoClient.textFields(state.azdoOrganization)
                var loaded = AzdoClient.workItems(state.azdoOrganization, ids)
                val matches = mutableMapOf<Int, List<String>>()
                if (filterTerm != null) {
                    loaded = loaded.filter { item ->
                        val hits = item.fields.filterKeys { it in textFieldNames }
                            .filterValues { it.contains(filterTerm, ignoreCase = false) }
                            .keys.toList()
                        if (hits.isNotEmpty()) matches[item.id] = hits
                        hits.isNotEmpty()
                    }
                }
                ApplicationManager.getApplication().invokeLater {
                    items.clear()
                    items.addAll(loaded)
                    matchedFields.clear()
                    matchedFields.putAll(matches)
                    model.fireTableDataChanged()
                    onLine("${items.size} work item(s)${if (filterTerm != null) " match" else ""}.")
                }
            } catch (e: Exception) {
                onLine("Work item query failed: ${e.message}")
            }
        }
    }

    private fun replaceAll() {
        val term = search.text.trim()
        val replacement = replace.text
        if (term.isEmpty() || matchedFields.isEmpty()) {
            onLine("Run Preview first.")
            return
        }
        saveConnection()
        ApplicationManager.getApplication().executeOnPooledThread {
            try {
                var updated = 0
                for (item in items) {
                    val fields = matchedFields[item.id] ?: continue
                    val values = fields.mapNotNull { field ->
                        item.fields[field]?.let { field to it.replace(term, replacement) }
                    }.toMap()
                    if (values.isNotEmpty()) {
                        AzdoClient.updateFields(state.azdoOrganization, item.id, values)
                        updated++
                        onLine("Updated #${item.id}")
                    }
                }
                onLine("Replace complete: $updated work item(s) updated.")
            } catch (e: Exception) {
                onLine("Replace failed: ${e.message}")
            }
        }
    }

    private inner class ItemTableModel : AbstractTableModel() {
        private val columns = arrayOf("ID", "Type", "Title", "State", "Assigned To", "Matched Fields")

        override fun getRowCount() = items.size
        override fun getColumnCount() = columns.size
        override fun getColumnName(column: Int) = columns[column]

        override fun getValueAt(rowIndex: Int, columnIndex: Int): Any {
            val item = items[rowIndex]
            return when (columnIndex) {
                0 -> item.id
                1 -> item.type
                2 -> item.title
                3 -> item.state
                4 -> item.assignedTo
                else -> matchedFields[item.id]?.joinToString(", ") { textFieldNames[it] ?: it } ?: ""
            }
        }
    }
}
