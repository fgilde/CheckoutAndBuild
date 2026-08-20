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

data class StashInfo(val index: Int, val description: String)

data class CommitInfo(val sha: String, val author: String, val date: String, val message: String)

data class WorktreeInfo(
    val path: File,
    val branch: String,
    val sha: String,
    val isMain: Boolean,
    val isDetached: Boolean,
    val isLocked: Boolean,
    val isPrunable: Boolean
)

/** git.exe access: repository discovery, sync, branches, stashes, history, patches and worktrees. */
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

    fun branchExists(root: File, branch: String): Boolean =
        ProcessRunner.capture("git rev-parse --verify --quiet \"refs/heads/$branch\"", root).first == 0 ||
            ProcessRunner.capture("git rev-parse --verify --quiet \"refs/remotes/origin/$branch\"", root).first == 0

    fun createBranch(root: File, branch: String): Pair<Int, String> =
        ProcessRunner.capture("git checkout -b \"$branch\"", root)

    fun checkout(root: File, branch: String): Pair<Int, String> =
        ProcessRunner.capture("git checkout \"$branch\"", root)

    fun deleteBranch(root: File, branch: String, force: Boolean = false): Pair<Int, String> =
        ProcessRunner.capture("git branch ${if (force) "-D" else "-d"} \"$branch\"", root)

    fun defaultBranch(root: File): String? {
        val head = capture(root, "git symbolic-ref refs/remotes/origin/HEAD --short")
        if (head != null) {
            val name = head.lines().first().trim()
            val slash = name.indexOf('/')
            if (slash >= 0) return name.substring(slash + 1)
        }
        val local = branches(root)
        return local.firstOrNull { it == "main" } ?: local.firstOrNull { it == "master" } ?: local.firstOrNull()
    }

    fun mergedBranches(root: File, target: String): List<String> {
        val current = capture(root, "git rev-parse --abbrev-ref HEAD")?.trim()
        return capture(root, "git branch --merged \"$target\" --format=%(refname:short)")?.lines()
            ?.map { it.trim() }?.filter { it.isNotEmpty() && it != target && it != current } ?: emptyList()
    }

    fun fetch(root: File): Pair<Int, String> = ProcessRunner.capture("git fetch", root)

    fun push(root: File, setUpstream: Boolean, branch: String): Pair<Int, String> =
        ProcessRunner.capture(if (setUpstream) "git push -u origin \"$branch\"" else "git push", root)

    fun forcePush(root: File): Pair<Int, String> =
        ProcessRunner.capture("git push --force-with-lease", root)

    fun revision(root: File): String? = capture(root, "git rev-parse HEAD")?.trim()

    /**
     * Stashes uncommitted changes before the action and restores them afterwards (when enabled and dirty).
     * A failing pop leaves the changes safely in stash@{0} and reports it.
     */
    fun <T> withAutoStash(root: File, enabled: Boolean, onLine: (String) -> Unit, action: () -> T): T {
        val stashed = enabled && changes(root).isNotEmpty() && stashPush(root, "coab-auto").first == 0
        if (stashed) onLine("auto-stashed local changes in ${root.name}")
        try {
            return action()
        } finally {
            if (stashed) {
                val pop = stashAction(root, "pop", 0)
                onLine(
                    if (pop.first == 0) "auto-stash restored in ${root.name}"
                    else "stash pop failed in ${root.name} — your changes remain in stash@{0}")
            }
        }
    }

    fun sync(root: File, autoStash: Boolean = false, onLine: (String) -> Unit = {}): String {
        val fetch = fetch(root)
        if (fetch.first != 0) return "fetch failed: ${fetch.second.lines().firstOrNull().orEmpty()}"
        var info = info(root)
        val messages = mutableListOf<String>()
        if (info.hasUpstream && info.behind > 0) {
            val pull = withAutoStash(root, autoStash, onLine) { ProcessRunner.capture("git pull", root) }
            if (pull.first != 0) return "pull failed: ${pull.second.lines().lastOrNull { it.isNotBlank() }.orEmpty()}"
            messages.add("pulled ${info.behind}")
            info = info(root)
        }
        if (!info.hasUpstream || info.ahead > 0) {
            val push = push(root, !info.hasUpstream, info.branch)
            if (push.first != 0) return "push failed: ${push.second.lines().lastOrNull { it.isNotBlank() }.orEmpty()}"
            messages.add(if (info.hasUpstream) "pushed ${info.ahead}" else "pushed (upstream set)")
        }
        return if (messages.isEmpty()) "up to date" else messages.joinToString(", ")
    }

    fun changes(root: File): List<String> =
        capture(root, "git status --porcelain")?.lines()?.filter { it.isNotBlank() } ?: emptyList()

    fun exportChangesAsZip(root: File, target: File): Pair<Int, String> {
        val entries = changes(root).mapNotNull { line ->
            if (line.length < 4 || line.startsWith(" D") || line.startsWith("D ")) null
            else line.substring(3).trim().removeSurrounding("\"")
        }
        if (entries.isEmpty()) return 1 to "no changes to export"
        java.util.zip.ZipOutputStream(target.outputStream()).use { zip ->
            for (relative in entries) {
                val file = File(root, relative.replace('/', File.separatorChar))
                if (!file.isFile) continue
                zip.putNextEntry(java.util.zip.ZipEntry(relative))
                file.inputStream().use { it.copyTo(zip) }
                zip.closeEntry()
            }
        }
        return 0 to "written: ${target.absolutePath}"
    }

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

    fun stashes(root: File): List<StashInfo> =
        capture(root, "git stash list")?.lines()?.filter { it.isNotBlank() }?.mapIndexed { index, line ->
            StashInfo(index, line.substringAfter(": ", line))
        } ?: emptyList()

    fun stashPush(root: File, message: String?): Pair<Int, String> =
        ProcessRunner.capture(
            if (message.isNullOrBlank()) "git stash push" else "git stash push -m \"$message\"", root)

    fun stashAction(root: File, action: String, index: Int): Pair<Int, String> =
        ProcessRunner.capture("git stash $action stash@{$index}", root)

    fun history(root: File, maxCount: Int, mineOnly: Boolean, grep: String?): List<CommitInfo> {
        var args = "git log --max-count=$maxCount --date=short --format=%h%x09%an%x09%ad%x09%s"
        if (mineOnly) {
            val user = capture(root, "git config user.name")?.trim()
            if (!user.isNullOrEmpty()) args += " -i --author=\"$user\""
        }
        if (!grep.isNullOrBlank()) args += " -i --grep=\"$grep\""
        return capture(root, args)?.lines()?.filter { it.isNotBlank() }?.mapNotNull { line ->
            val parts = line.split('\t', limit = 4)
            if (parts.size == 4) CommitInfo(parts[0], parts[1], parts[2], parts[3]) else null
        } ?: emptyList()
    }

    fun exportPatch(root: File, target: File): Pair<Int, String> {
        val (exit, output) = ProcessRunner.capture("git diff HEAD", root)
        if (exit != 0) return exit to output
        target.writeText(output + System.lineSeparator())
        return 0 to "written: ${target.absolutePath}"
    }

    fun applyPatch(root: File, patch: File): Pair<Int, String> =
        ProcessRunner.capture("git apply --3way \"${patch.absolutePath}\"", root)

    fun remoteUrl(root: File): String? = capture(root, "git config --get remote.origin.url")?.trim()

    /** Commit subjects since the latest tag (or the last 50 without tags) as markdown bullet lines. */
    fun changelog(root: File): String {
        val tag = capture(root, "git describe --tags --abbrev=0")?.trim()?.takeIf { it.isNotEmpty() }
        val range = if (tag == null) "--max-count=50" else "\"$tag..HEAD\""
        val log = capture(root, "git log $range --no-merges --format=\"- %s\"") ?: ""
        val header = if (tag == null) "## Changes (last 50 commits)" else "## Changes since $tag"
        return header + "\n\n" + log.trim() + "\n"
    }

    fun pullRequestUrl(remoteUrl: String, branch: String): String? {
        var url = remoteUrl.trim()
        if (url.endsWith(".git", true)) url = url.dropLast(4)
        if (url.startsWith("git@", true)) url = "https://" + url.substring(4).replace(":", "/")
        val encoded = java.net.URLEncoder.encode(branch, Charsets.UTF_8)
        return when {
            url.contains("github.com", true) -> "$url/compare/$encoded?expand=1"
            url.contains("dev.azure.com", true) || url.contains("visualstudio.com", true) || url.contains("/_git/", true) ->
                "$url/pullrequestcreate?sourceRef=$encoded"
            else -> null
        }
    }

    fun worktrees(root: File): List<WorktreeInfo> {
        val output = capture(root, "git worktree list --porcelain") ?: return emptyList()
        val result = mutableListOf<WorktreeInfo>()
        var path: File? = null; var branch = ""; var sha = ""; var detached = false; var locked = false; var prunable = false
        fun flush() {
            path?.let { result.add(WorktreeInfo(it, branch, sha, result.isEmpty(), detached, locked, prunable)) }
            path = null; branch = ""; sha = ""; detached = false; locked = false; prunable = false
        }
        for (raw in output.lines()) {
            val line = raw.trim()
            when {
                line.startsWith("worktree ") -> { flush(); path = File(line.removePrefix("worktree ").replace('/', File.separatorChar)) }
                line.startsWith("HEAD ") -> sha = line.removePrefix("HEAD ").take(8)
                line.startsWith("branch ") -> branch = line.removePrefix("branch ").removePrefix("refs/heads/")
                line == "detached" -> detached = true
                line.startsWith("locked") -> locked = true
                line.startsWith("prunable") -> prunable = true
            }
        }
        flush()
        return result
    }

    fun worktreeDefaultPath(root: File, branch: String): File {
        val sanitized = branch.replace('/', '-').replace('\\', '-')
        return File(root.parentFile ?: root, "${root.name}-$sanitized")
    }

    fun worktreeAdd(root: File, path: File, branch: String, create: Boolean): Pair<Int, String> =
        ProcessRunner.capture(
            if (create) "git worktree add -b \"$branch\" \"${path.absolutePath}\""
            else "git worktree add \"${path.absolutePath}\" \"$branch\"", root)

    fun worktreeRemove(root: File, path: File, force: Boolean): Pair<Int, String> =
        ProcessRunner.capture("git worktree remove ${if (force) "--force " else ""}\"${path.absolutePath}\"", root)

    fun worktreePrune(root: File): Pair<Int, String> = ProcessRunner.capture("git worktree prune", root)

    fun updateFromBase(worktree: File): Pair<Int, String> {
        fetch(worktree)
        val base = defaultBranch(worktree) ?: return 1 to "no default branch found"
        return ProcessRunner.capture("git merge \"origin/$base\" --no-edit", worktree)
    }

    fun abortMerge(worktree: File): Pair<Int, String> =
        ProcessRunner.capture("git merge --abort", worktree)

    fun orphanWorktreeDirs(root: File): List<File> {
        val parent = root.parentFile ?: return emptyList()
        return parent.listFiles()?.filter { candidate ->
            if (!candidate.isDirectory || candidate == root) return@filter false
            val gitFile = File(candidate, ".git")
            if (!gitFile.isFile) return@filter false
            val content = runCatching { gitFile.readText() }.getOrNull() ?: return@filter false
            if (!content.startsWith("gitdir:")) return@filter false
            val gitDir = File(content.removePrefix("gitdir:").trim().replace('/', File.separatorChar))
            !gitDir.exists()
        } ?: emptyList()
    }

    private fun capture(root: File, command: String): String? {
        val (exit, output) = ProcessRunner.capture(command, root)
        return if (exit == 0) output else null
    }
}
