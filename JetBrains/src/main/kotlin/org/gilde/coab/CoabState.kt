package org.gilde.coab

import com.intellij.openapi.application.ApplicationManager
import com.intellij.openapi.components.PersistentStateComponent
import com.intellij.openapi.components.Service
import com.intellij.openapi.components.State
import com.intellij.openapi.components.Storage

/** Persisted plugin state: working folders, exclusions, priorities, per-project command overrides and the enabled steps. */
@Service
@State(name = "CheckoutAndBuild", storages = [Storage("checkoutandbuild.xml")])
class CoabState : PersistentStateComponent<CoabState.Model> {

    class Model {
        var folders: MutableList<String> = mutableListOf()
        var excluded: MutableSet<String> = mutableSetOf()
        var priorities: MutableMap<String, Int> = mutableMapOf()
        var installOverrides: MutableMap<String, String> = mutableMapOf()
        var buildOverrides: MutableMap<String, String> = mutableMapOf()
        var testOverrides: MutableMap<String, String> = mutableMapOf()
        var pull: Boolean = true
        var install: Boolean = true
        var build: Boolean = true
        var test: Boolean = false
        var maxParallel: Int = 6
        var scanDepth: Int = 3
        var failFast: Boolean = false
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
