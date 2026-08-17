package org.gilde.coab

import java.util.concurrent.Callable
import java.util.concurrent.Executors

/** Runs the enabled steps over the included projects: pull once per repository, then install → build → test, priority groups sequential and each group in parallel. */
class PipelineRunner(
    private val onLine: (String) -> Unit,
    private val onStatus: (CoabProject, String) -> Unit
) {
    @Volatile
    var cancelled = false

    fun run(projects: List<CoabProject>, priorityOf: (CoabProject) -> Int, steps: Set<StepKind>) {
        if (StepKind.PULL in steps) pullRepositories(projects)

        for (step in listOf(StepKind.INSTALL, StepKind.BUILD, StepKind.TEST)) {
            if (cancelled || step !in steps) continue
            val runnable = projects.filter { it.type.commandFor(step, it) != null }
            if (runnable.isEmpty()) continue
            onLine("")
            onLine("=== ${step.name.lowercase().replaceFirstChar { it.uppercase() }} (${runnable.size} project(s)) ===")
            val groups = runnable.groupBy(priorityOf).toSortedMap()
            val executor = Executors.newFixedThreadPool(minOf(Runtime.getRuntime().availableProcessors(), 6))
            try {
                for ((_, group) in groups) {
                    if (cancelled) break
                    executor.invokeAll(group.map { project -> Callable { runStep(project, step) } })
                }
            } finally {
                executor.shutdown()
            }
        }
        onLine("")
        onLine(if (cancelled) "=== Cancelled ===" else "=== Done ===")
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

    private fun runStep(project: CoabProject, step: StepKind) {
        if (cancelled) return
        val command = project.type.commandFor(step, project) ?: return
        onStatus(project, "${step.name.lowercase()}…")
        onLine("[${project.name}] $command")
        val exit = try {
            ProcessRunner.run(command, project.directory, { onLine("[${project.name}] $it") }, { cancelled })
        } catch (e: Exception) {
            onLine("[${project.name}] ${e.message}")
            -1
        }
        onStatus(project, if (exit == 0) "✓ ${step.name.lowercase()}" else "✗ ${step.name.lowercase()} ($exit)")
    }
}
