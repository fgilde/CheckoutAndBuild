package org.gilde.coab

import com.intellij.openapi.application.ApplicationManager
import com.intellij.openapi.project.Project
import com.intellij.openapi.wm.StatusBar
import com.intellij.openapi.wm.StatusBarWidget
import com.intellij.openapi.wm.StatusBarWidgetFactory
import com.intellij.openapi.wm.ToolWindowManager
import com.intellij.util.Consumer
import java.awt.Component
import java.awt.event.MouseEvent

/** Last/current pipeline state, published by the tool window and shown in the status bar widget. */
object CoabRunStatus {
    @Volatile
    var text: String = ""
        private set

    private val listeners = java.util.concurrent.CopyOnWriteArrayList<() -> Unit>()

    fun set(value: String) {
        if (value == text) return
        text = value
        listeners.forEach { it() }
    }

    fun addListener(listener: () -> Unit) = listeners.add(listener)

    fun removeListener(listener: () -> Unit) = listeners.remove(listener)
}

class CoabStatusBarWidgetFactory : StatusBarWidgetFactory {
    override fun getId() = "CheckoutAndBuildStatus"
    override fun getDisplayName() = "CheckoutAndBuild"
    override fun isAvailable(project: Project) = true
    override fun createWidget(project: Project): StatusBarWidget = CoabStatusBarWidget(project)
    override fun canBeEnabledOn(statusBar: StatusBar) = true
}

/** Status bar entry: shows the pipeline state; a click opens the CheckoutAndBuild tool window. */
class CoabStatusBarWidget(private val project: Project) : StatusBarWidget, StatusBarWidget.TextPresentation {
    private var statusBar: StatusBar? = null
    private val listener: () -> Unit = {
        ApplicationManager.getApplication().invokeLater { statusBar?.updateWidget(ID()) }
    }

    override fun ID() = "CheckoutAndBuildStatus"

    override fun install(statusBar: StatusBar) {
        this.statusBar = statusBar
        CoabRunStatus.addListener(listener)
    }

    override fun dispose() {
        CoabRunStatus.removeListener(listener)
        statusBar = null
    }

    override fun getPresentation(): StatusBarWidget.WidgetPresentation = this

    override fun getText(): String = CoabRunStatus.text.ifEmpty { "COAB" }

    override fun getAlignment(): Float = Component.CENTER_ALIGNMENT

    override fun getTooltipText() = "CheckoutAndBuild — click to open the tool window"

    override fun getClickConsumer(): Consumer<MouseEvent> = Consumer {
        ToolWindowManager.getInstance(project).getToolWindow("CheckoutAndBuild")?.show()
    }
}
