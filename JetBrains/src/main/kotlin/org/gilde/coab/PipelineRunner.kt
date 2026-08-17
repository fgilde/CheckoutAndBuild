package org.gilde.coab

import java.util.concurrent.Callable
import java.util.concurrent.Executors
import java.util.concurrent.atomic.AtomicBoolean

/** Runs the enabled steps over the included projects: pull once per repository, then install → build → test, priority groups sequential and each group in parallel. */
class PipelineRunner(
    private val onLine: (String) -> Unit,
    private val onStatus: (CoabProject, String) -> Unit
) {
    @Volatile
    var cancelled = false

    fun run(projects: List<CoabProject>, steps: Set<StepKind>) {
        val state = CoabState.get().state
        if (StepKind.PULL in steps) pullRepositories(projects)

        for (step in listOf(StepKind.INSTALL, StepKind.BUILD, StepKind.TEST)) {
            if (cancelled || step !in steps) continue
            val runnable = projects.filter { CommandResolver.resolve(it, step) != null }
            if (runnable.isEmpty()) continue
            onLine("")
            onLine("=== ${step.name.lowercase().replaceFirstChar { it.uppercase() }} (${runnable.size} project(s)) ===")
            val groups = runnable.groupBy { state.priorities[it.key] ?: 0 }.toSortedMap()
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
        val exit = try {
            ProcessRunner.run(command, project.directory, { onLine("[${project.name}] $it") }, { cancelled })
        } catch (e: Exception) {
            onLine("[${project.name}] ${e.message}")
            -1
        }
        onStatus(project, if (exit == 0) "✓ ${step.name.lowercase()}" else "✗ ${step.name.lowercase()} ($exit)")
        return exit == 0
    }
}
