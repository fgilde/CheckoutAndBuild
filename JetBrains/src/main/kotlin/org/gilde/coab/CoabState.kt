package org.gilde.coab

import com.intellij.openapi.application.ApplicationManager
import com.intellij.openapi.components.PersistentStateComponent
import com.intellij.openapi.components.Service
import com.intellij.openapi.components.State
import com.intellij.openapi.components.Storage

/** Persisted plugin state: working folders, profiles (per-profile exclusions, priorities, overrides and step flags), durations and global options. */
@Service
@State(name = "CheckoutAndBuild", storages = [Storage("checkoutandbuild.xml")])
class CoabState : PersistentStateComponent<CoabState.Model> {

    class Model {
        var folders: MutableList<String> = mutableListOf()
        var customProjects: MutableList<String> = mutableListOf()
        var profiles: MutableList<String> = mutableListOf("Default")
        var currentProfile: String = "Default"
        var excluded: MutableSet<String> = mutableSetOf()
        var priorities: MutableMap<String, Int> = mutableMapOf()
        var installOverrides: MutableMap<String, String> = mutableMapOf()
        var buildOverrides: MutableMap<String, String> = mutableMapOf()
        var testOverrides: MutableMap<String, String> = mutableMapOf()
        var stepFlags: MutableMap<String, String> = mutableMapOf()
        var durations: MutableMap<String, Long> = mutableMapOf()
        var maxParallel: Int = 6
        var scanDepth: Int = 3
        var failFast: Boolean = false
        var scheduledEnabled: Boolean = false
        var scheduledTime: String = "08:00"
        var lastScheduledRun: String = ""
        var skipUnchanged: Boolean = false
        var autoStash: Boolean = true
        var watchEnabled: Boolean = false
        var watchIntervalMinutes: Int = 10
        var azdoOrganization: String = ""
        var azdoProject: String = ""
        var azdoWiql: String = "SELECT [System.Id] FROM WorkItems WHERE [System.TeamProject] = @project"
    }

    private var model = Model()

    override fun getState(): Model = model

    override fun loadState(state: Model) {
        model = state
        if (model.profiles.isEmpty()) model.profiles.add("Default")
        if (model.currentProfile !in model.profiles) model.currentProfile = model.profiles.first()
    }

    private fun scoped(key: String) = "${model.currentProfile}|$key"

    fun isExcluded(projectKey: String) = scoped(projectKey) in model.excluded

    fun setExcluded(projectKey: String, excluded: Boolean) {
        if (excluded) model.excluded.add(scoped(projectKey)) else model.excluded.remove(scoped(projectKey))
    }

    fun priority(projectKey: String) = model.priorities[scoped(projectKey)] ?: 0

    fun setPriority(projectKey: String, value: Int) {
        model.priorities[scoped(projectKey)] = value
    }

    fun override(step: StepKind, projectKey: String): String? = overrideMap(step)[scoped(projectKey)]

    fun setOverride(step: StepKind, projectKey: String, value: String?) {
        if (value.isNullOrBlank()) overrideMap(step).remove(scoped(projectKey))
        else overrideMap(step)[scoped(projectKey)] = value
    }

    private fun overrideMap(step: StepKind): MutableMap<String, String> = when (step) {
        StepKind.INSTALL -> model.installOverrides
        StepKind.TEST -> model.testOverrides
        else -> model.buildOverrides
    }

    fun stepEnabled(step: StepKind): Boolean {
        val flags = model.stepFlags[model.currentProfile] ?: "PULL,INSTALL,BUILD"
        return step.name in flags.split(',')
    }

    fun setStepEnabled(step: StepKind, enabled: Boolean) {
        val current = (model.stepFlags[model.currentProfile] ?: "PULL,INSTALL,BUILD")
            .split(',').filter { it.isNotBlank() }.toMutableSet()
        if (enabled) current.add(step.name) else current.remove(step.name)
        model.stepFlags[model.currentProfile] = current.joinToString(",")
    }

    fun duration(projectKey: String, step: StepKind): Long? = model.durations["$projectKey|${step.name}"]

    fun setDuration(projectKey: String, step: StepKind, seconds: Long) {
        model.durations["$projectKey|${step.name}"] = seconds
    }

    companion object {
        fun get(): CoabState = ApplicationManager.getApplication().getService(CoabState::class.java)
    }
}
