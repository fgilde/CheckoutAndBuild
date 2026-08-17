package org.gilde.coab

/** Resolves the command line for a step: per-project override first, then the project type default. */
object CommandResolver {

    fun resolve(project: CoabProject, step: StepKind): String? {
        val state = CoabState.get().state
        val override = when (step) {
            StepKind.INSTALL -> state.installOverrides[project.key]
            StepKind.BUILD -> state.buildOverrides[project.key]
            StepKind.TEST -> state.testOverrides[project.key]
            else -> null
        }
        if (!override.isNullOrBlank()) return override
        return project.type.commandFor(step, project)
    }
}
