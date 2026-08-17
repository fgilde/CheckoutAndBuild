package org.gilde.coab

/** Resolves the command line for a step: per-project override (current profile) first, then the project type default. */
object CommandResolver {

    fun resolve(project: CoabProject, step: StepKind): String? {
        val override = CoabState.get().override(step, project.key)
        if (!override.isNullOrBlank()) return override
        return project.type.commandFor(step, project)
    }
}
