package org.gilde.coab

import java.io.File

/** Snapshot of one repository shown in the Git tab. */
data class RepoInfo(
    val root: File,
    val branch: String,
    val ahead: Int,
    val behind: Int,
    val hasUpstream: Boolean,
    val dirtyCount: Int
)

/** git.exe access: repository discovery, pull/fetch/push, branch handling, commit. */
object GitOps {

    fun repositoryRoot(directory: File): File? {
        val (exit, output) = ProcessRunner.capture("git rev-parse --show-toplevel", directory)
        if (exit != 0 || output.isEmpty()) return null
        return File(output.lines().first().trim().replace('/', File.separatorChar))
    }

    fun pull(repositoryRoot: File, onLine: (String) -> Unit, isCancelled: () -> Boolean): Int =
        ProcessRunner.run("git pull", repositoryRoot, onLine, isCancelled)

    fun info(root: File): RepoInfo {
        val branch = capture(root, "git rev-parse --abbrev-ref HEAD") ?: "?"
        val dirty = capture(root, "git status --porcelain")?.lines()?.count { it.isNotBlank() } ?: 0
        val counts = capture(root, "git rev-list --left-right --count @{upstream}...HEAD")
        var ahead = 0; var behind = 0; var hasUpstream = false
        if (counts != null) {
            val parts = counts.split(Regex("\\s+"))
            if (parts.size >= 2) {
                behind = parts[0].toIntOrNull() ?: 0
                ahead = parts[1].toIntOrNull() ?: 0
                hasUpstream = true
            }
        }
        return RepoInfo(root, branch, ahead, behind, hasUpstream, dirty)
    }

    fun branches(root: File): List<String> =
        capture(root, "git branch --format=%(refname:short)")?.lines()?.map { it.trim() }?.filter { it.isNotEmpty() }
            ?: emptyList()

    fun checkout(root: File, branch: String): Pair<Int, String> =
        ProcessRunner.capture("git checkout \"$branch\"", root)

    fun fetch(root: File): Pair<Int, String> = ProcessRunner.capture("git fetch", root)

    fun push(root: File, setUpstream: Boolean, branch: String): Pair<Int, String> =
        ProcessRunner.capture(if (setUpstream) "git push -u origin \"$branch\"" else "git push", root)

    fun commitAll(root: File, message: String): Pair<Int, String> {
        val add = ProcessRunner.capture("git add -A", root)
        if (add.first != 0) return add
        val file = File.createTempFile("coab-commit", ".txt")
        file.writeText(message)
        return try {
            ProcessRunner.capture("git commit -F \"${file.absolutePath}\"", root)
        } finally {
            file.delete()
        }
    }

    private fun capture(root: File, command: String): String? {
        val (exit, output) = ProcessRunner.capture(command, root)
        return if (exit == 0) output else null
    }
}
