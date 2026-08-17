package org.gilde.coab

import java.io.File

enum class StepKind { PULL, INSTALL, BUILD, TEST }

/** One buildable project found beneath a working folder. */
data class CoabProject(val file: File, val type: ProjectType) {
    val name: String get() = file.name
    val directory: File get() = if (file.isDirectory) file else file.parentFile
    val key: String get() = file.absolutePath
}

/** Supported build systems: detection markers and per-step command lines. */
enum class ProjectType {
    DOTNET, GRADLE, MAVEN, CARGO, GO, COMPOSER, NPM;

    fun commandFor(step: StepKind, project: CoabProject): String? {
        val quoted = "\"${project.file.absolutePath}\""
        return when (this) {
            DOTNET -> when (step) {
                StepKind.INSTALL -> "dotnet restore $quoted"
                StepKind.BUILD -> "dotnet build $quoted"
                StepKind.TEST -> "dotnet test $quoted"
                else -> null
            }
            GRADLE -> when (step) {
                StepKind.BUILD -> "${gradleCommand(project.directory)} build -x test"
                StepKind.TEST -> "${gradleCommand(project.directory)} test"
                else -> null
            }
            MAVEN -> when (step) {
                StepKind.BUILD -> "${mavenCommand(project.directory)} -B package -DskipTests"
                StepKind.TEST -> "${mavenCommand(project.directory)} -B test"
                else -> null
            }
            CARGO -> when (step) {
                StepKind.BUILD -> "cargo build"
                StepKind.TEST -> "cargo test"
                else -> null
            }
            GO -> when (step) {
                StepKind.BUILD -> "go build ./..."
                StepKind.TEST -> "go test ./..."
                else -> null
            }
            COMPOSER -> when (step) {
                StepKind.INSTALL -> "composer install --no-interaction"
                else -> null
            }
            NPM -> when (step) {
                StepKind.INSTALL -> "npm install"
                StepKind.BUILD -> "npm run build --if-present"
                StepKind.TEST -> "npm run test --if-present"
                else -> null
            }
        }
    }

    private fun gradleCommand(dir: File): String =
        if (File(dir, "gradlew.bat").exists() || File(dir, "gradlew").exists())
            if (isWindows) "gradlew.bat" else "./gradlew"
        else "gradle"

    private fun mavenCommand(dir: File): String =
        if (File(dir, "mvnw.cmd").exists() || File(dir, "mvnw").exists())
            if (isWindows) "mvnw.cmd" else "./mvnw"
        else "mvn"

    companion object {
        val isWindows = System.getProperty("os.name").lowercase().contains("win")
    }
}

/** Scans working folders for projects; a matched directory is a project root and is not descended further. */
object ProjectScanner {
    private const val maxDepth = 3
    private val skipped = setOf(".git", ".idea", ".vs", "node_modules", "bin", "obj", "target", "build", "dist", "out", "packages")

    fun scan(root: File): List<CoabProject> {
        val result = mutableListOf<CoabProject>()
        scanDirectory(root, 0, result)
        return result.sortedBy { it.name.lowercase() }
    }

    private fun scanDirectory(dir: File, depth: Int, result: MutableList<CoabProject>) {
        if (!dir.isDirectory) return
        val found = detect(dir)
        if (found.isNotEmpty()) {
            result.addAll(found)
            return
        }
        if (depth >= maxDepth) return
        dir.listFiles()?.filter { it.isDirectory && it.name !in skipped }?.forEach {
            scanDirectory(it, depth + 1, result)
        }
    }

    private fun detect(dir: File): List<CoabProject> {
        val files = dir.listFiles()?.filter { it.isFile } ?: return emptyList()
        val solutions = files.filter { it.extension.equals("sln", true) || it.extension.equals("slnx", true) }
        if (solutions.isNotEmpty()) return solutions.map { CoabProject(it, ProjectType.DOTNET) }

        fun first(name: String) = files.firstOrNull { it.name.equals(name, true) }
        (first("settings.gradle.kts") ?: first("settings.gradle") ?: first("build.gradle.kts") ?: first("build.gradle"))
            ?.let { return listOf(CoabProject(dir, ProjectType.GRADLE)) }
        first("pom.xml")?.let { return listOf(CoabProject(it, ProjectType.MAVEN)) }
        first("Cargo.toml")?.let { return listOf(CoabProject(dir, ProjectType.CARGO)) }
        first("go.mod")?.let { return listOf(CoabProject(dir, ProjectType.GO)) }
        first("composer.json")?.let { return listOf(CoabProject(dir, ProjectType.COMPOSER)) }
        first("package.json")?.let { return listOf(CoabProject(dir, ProjectType.NPM)) }
        return emptyList()
    }
}
