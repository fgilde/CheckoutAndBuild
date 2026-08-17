package org.gilde.coab

import java.io.File
import java.util.concurrent.TimeUnit

/** Runs a command line through the platform shell, streaming output lines; cancellation kills the process tree. */
object ProcessRunner {

    fun run(commandLine: String, workingDir: File, onLine: (String) -> Unit, isCancelled: () -> Boolean): Int {
        val builder = if (ProjectType.isWindows)
            ProcessBuilder("cmd", "/s", "/c", commandLine)
        else
            ProcessBuilder("sh", "-c", commandLine)
        builder.directory(workingDir)
        builder.redirectErrorStream(true)
        val process = builder.start()

        val watchdog = Thread {
            while (process.isAlive) {
                if (isCancelled()) {
                    process.descendants().forEach { it.destroyForcibly() }
                    process.destroyForcibly()
                    return@Thread
                }
                Thread.sleep(200)
            }
        }
        watchdog.isDaemon = true
        watchdog.start()

        process.inputStream.bufferedReader().useLines { lines ->
            lines.forEach(onLine)
        }
        process.waitFor(10, TimeUnit.SECONDS)
        return if (process.isAlive) -1 else process.exitValue()
    }

    fun capture(commandLine: String, workingDir: File): Pair<Int, String> {
        val output = StringBuilder()
        val exit = run(commandLine, workingDir, { output.appendLine(it) }, { false })
        return exit to output.toString().trim()
    }
}
