package org.gilde.coab

import java.io.File

/** git.exe access for the pull step (repository discovery and pull). */
object GitOps {

    fun repositoryRoot(directory: File): File? {
        val (exit, output) = ProcessRunner.capture("git rev-parse --show-toplevel", directory)
        if (exit != 0 || output.isEmpty()) return null
        return File(output.lines().first().trim().replace('/', File.separatorChar))
    }

    fun pull(repositoryRoot: File, onLine: (String) -> Unit, isCancelled: () -> Boolean): Int =
        ProcessRunner.run("git pull", repositoryRoot, onLine, isCancelled)

    fun currentBranch(repositoryRoot: File): String? {
        val (exit, output) = ProcessRunner.capture("git rev-parse --abbrev-ref HEAD", repositoryRoot)
        return if (exit == 0) output.lines().firstOrNull()?.trim() else null
    }
}
