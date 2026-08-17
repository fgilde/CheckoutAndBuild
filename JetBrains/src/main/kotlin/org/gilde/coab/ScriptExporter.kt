package org.gilde.coab

/** Exports the configured pipeline as a standalone .bat or .ps1 script. */
object ScriptExporter {

    fun build(projects: List<CoabProject>, steps: Set<StepKind>, powershell: Boolean): String {
        val lines = mutableListOf<String>()
        lines += if (powershell) "# CheckoutAndBuild pipeline export" else "@echo off"

        if (StepKind.PULL in steps) {
            val roots = projects.mapNotNull { GitOps.repositoryRoot(it.directory) }
                .distinctBy { it.absolutePath.lowercase() }
            for (root in roots)
                lines += if (powershell) "git -C \"${root.absolutePath}\" pull" else "git -C \"${root.absolutePath}\" pull"
        }

        val state = CoabState.get().state
        for (step in listOf(StepKind.INSTALL, StepKind.BUILD, StepKind.TEST)) {
            if (step !in steps) continue
            val runnable = projects.filter { CommandResolver.resolve(it, step) != null }
            if (runnable.isEmpty()) continue
            lines += ""
            lines += if (powershell) "# ${step.name.lowercase()}" else "rem ${step.name.lowercase()}"
            for (project in runnable.sortedBy { state.priorities[it.key] ?: 0 }) {
                val command = CommandResolver.resolve(project, step) ?: continue
                if (powershell) {
                    lines += "Push-Location \"${project.directory.absolutePath}\""
                    lines += command
                    lines += "Pop-Location"
                } else {
                    lines += "pushd \"${project.directory.absolutePath}\""
                    lines += "call $command"
                    lines += "popd"
                }
            }
        }
        return lines.joinToString(System.lineSeparator())
    }
}
