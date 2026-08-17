package org.gilde.coab

import com.intellij.openapi.application.ApplicationManager
import com.intellij.openapi.components.PersistentStateComponent
import com.intellij.openapi.components.Service
import com.intellij.openapi.components.State
import com.intellij.openapi.components.Storage

/** Persisted plugin state: working folders, exclusions, priorities and the enabled steps. */
@Service
@State(name = "CheckoutAndBuild", storages = [Storage("checkoutandbuild.xml")])
class CoabState : PersistentStateComponent<CoabState.Model> {

    class Model {
        var folders: MutableList<String> = mutableListOf()
        var excluded: MutableSet<String> = mutableSetOf()
        var priorities: MutableMap<String, Int> = mutableMapOf()
        var pull: Boolean = true
        var install: Boolean = true
        var build: Boolean = true
        var test: Boolean = false
    }

    private var model = Model()

    override fun getState(): Model = model

    override fun loadState(state: Model) {
        model = state
    }

    companion object {
        fun get(): CoabState = ApplicationManager.getApplication().getService(CoabState::class.java)
    }
}
