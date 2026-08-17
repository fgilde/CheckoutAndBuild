package org.gilde.coab

import java.io.File

/** Starts and stops the built executable of a project (newest exe below the project directory). */
object AppLauncher {

    fun findExecutable(project: CoabProject): File? {
        if (!ProjectType.isWindows) return null
        val skipped = setOf(".git", "node_modules", "obj", "packages", ".idea", ".vs")
        val candidates = mutableListOf<File>()
        fun walk(dir: File, depth: Int) {
            if (depth > 4) return
            dir.listFiles()?.forEach {
                if (it.isDirectory && it.name !in skipped) walk(it, depth + 1)
                else if (it.isFile && it.extension.equals("exe", true)) candidates.add(it)
            }
        }
        walk(project.directory, 0)
        return candidates.maxByOrNull { it.lastModified() }
    }

    fun start(project: CoabProject): String {
        val exe = findExecutable(project) ?: return "No executable found beneath ${project.directory.name}."
        return try {
            ProcessBuilder(exe.absolutePath).directory(exe.parentFile).start()
            "Started ${exe.name}"
        } catch (e: Exception) {
            "Start failed: ${e.message}"
        }
    }

    fun stop(project: CoabProject): String {
        val prefix = project.directory.absolutePath.lowercase()
        var killed = 0
        ProcessHandle.allProcesses().forEach { handle ->
            val command = handle.info().command().orElse("")
            if (command.isNotEmpty() && command.lowercase().startsWith(prefix)) {
                if (handle.destroyForcibly()) killed++
            }
        }
        return "Stopped $killed process(es)."
    }
}
