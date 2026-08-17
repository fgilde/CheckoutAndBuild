package org.gilde.coab

import java.util.concurrent.Callable
import java.util.concurrent.ConcurrentHashMap
import java.util.concurrent.Executors
import java.util.concurrent.atomic.AtomicBoolean

/** Runs the enabled steps over the included projects: pull once per repository, then install → build → test, priority groups sequential and each group in parallel. Records per-step durations for the ETA. */
class PipelineRunner(
    private val onLine: (String) -> Unit,
    private val onStatus: (CoabProject, String) -> Unit,
    private val onProgress: (String) -> Unit = {}
) {
    @Volatile
    var cancelled = false

    val failedProjects: MutableSet<String> = ConcurrentHashMap.newKeySet()

    fun estimateSeconds(projects: List<CoabProject>, steps: Set<StepKind>): Long {
        val state = CoabState.get()
        var total = 0L
        for (step in listOf(StepKind.INSTALL, StepKind.BUILD, StepKind.TEST)) {
            if (step !in steps) continue
            for (project in projects)
                if (CommandResolver.resolve(project, step) != null)
                    total += state.duration(project.key, step) ?: 0
        }
        return total
    }

    fun run(projects: List<CoabProject>, steps: Set<StepKind>) {
        val state = CoabState.get().state
        val active = listOf(StepKind.INSTALL, StepKind.BUILD, StepKind.TEST).filter { it in steps }
        var stepIndex = 0
        val stepCount = active.size + (if (StepKind.PULL in steps) 1 else 0)

        if (StepKind.PULL in steps) {
            onProgress("Pull (1/$stepCount)")
            pullRepositories(projects)
            stepIndex = 1
        }

        for (step in active) {
            if (cancelled) break
            stepIndex++
            onProgress("${step.name.lowercase().replaceFirstChar { it.uppercase() }} ($stepIndex/$stepCount)")
            val runnable = projects.filter { CommandResolver.resolve(it, step) != null }
            if (runnable.isEmpty()) continue
            onLine("")
            onLine("=== ${step.name.lowercase().replaceFirstChar { it.uppercase() }} (${runnable.size} project(s)) ===")
            val groups = runnable.groupBy { CoabState.get().priority(it.key) }.toSortedMap()
            val executor = Executors.newFixedThreadPool(state.maxParallel.coerceIn(1, 16))
            val anyFailed = AtomicBoolean(false)
            try {
                for ((_, group) in groups) {
                    if (cancelled) break
                    executor.invokeAll(group.map { project ->
                        Callable { if (!runStep(project, step)) anyFailed.set(true) }
                    })
                    if (state.failFast && anyFailed.get()) {
                        onLine("=== Stopping remaining groups (fail fast) ===")
                        break
                    }
                }
            } finally {
                executor.shutdown()
            }
        }
        onLine("")
        onLine(if (cancelled) "=== Cancelled ===" else "=== Done ===")
    }

    fun runSingle(project: CoabProject, step: StepKind) {
        if (step == StepKind.PULL) {
            val root = GitOps.repositoryRoot(project.directory)
            if (root == null) {
                onLine("[${project.name}] not inside a git repository")
                return
            }
            onStatus(project, "pull…")
            val exit = GitOps.pull(root, { onLine("[${project.name}] $it") }, { cancelled })
            onStatus(project, if (exit == 0) "✓ pull" else "✗ pull ($exit)")
            return
        }
        runStep(project, step)
    }

    private fun pullRepositories(projects: List<CoabProject>) {
        val roots = projects.mapNotNull { GitOps.repositoryRoot(it.directory) }
            .distinctBy { it.absolutePath.lowercase() }
        if (roots.isEmpty()) return
        onLine("=== Pull (${roots.size} repositories) ===")
        for (root in roots) {
            if (cancelled) return
            onLine("git pull: ${root.name}")
            val exit = GitOps.pull(root, { onLine("  $it") }, { cancelled })
            if (exit != 0) onLine("  pull failed with exit code $exit")
        }
    }

    private fun runStep(project: CoabProject, step: StepKind): Boolean {
        if (cancelled) return true
        val command = CommandResolver.resolve(project, step) ?: return true
        onStatus(project, "${step.name.lowercase()}…")
        onLine("[${project.name}] $command")
        val started = System.currentTimeMillis()
        val exit = try {
            ProcessRunner.run(command, project.directory, { onLine("[${project.name}] $it") }, { cancelled })
        } catch (e: Exception) {
            onLine("[${project.name}] ${e.message}")
            -1
        }
        if (exit == 0 && !cancelled)
            CoabState.get().setDuration(project.key, step, (System.currentTimeMillis() - started) / 1000)
        if (exit != 0) failedProjects.add(project.key)
        onStatus(project, if (exit == 0) "✓ ${step.name.lowercase()}" else "✗ ${step.name.lowercase()} ($exit)")
        return exit == 0
    }
}
