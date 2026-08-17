package org.gilde.coab

import java.io.File

/** Suggests build priorities for .NET solutions by matching referenced assembly/package names against the assemblies other solutions produce (port of the Visual Studio dependency scan). */
object DependencyAnalyzer {

    fun suggest(projects: List<CoabProject>): Map<String, Int> {
        val dotnet = projects.filter { it.type == ProjectType.DOTNET }
        if (dotnet.size < 2) return emptyMap()

        val produced = mutableMapOf<String, Set<String>>()
        val referenced = mutableMapOf<String, Set<String>>()
        for (solution in dotnet) {
            val projectFiles = solutionProjects(solution.file)
            produced[solution.key] = projectFiles.map { assemblyName(it) }.toSet()
            referenced[solution.key] = projectFiles.flatMap { referencedNames(it) }.toSet()
        }

        val dependsOn = dotnet.associate { solution ->
            solution.key to dotnet.filter { other ->
                other.key != solution.key &&
                    referenced.getValue(solution.key).any { it in produced.getValue(other.key) }
            }.map { it.key }
        }

        val depth = mutableMapOf<String, Int>()
        val onStack = mutableSetOf<String>()
        fun depthOf(key: String): Int {
            depth[key]?.let { return it }
            if (!onStack.add(key)) return 0
            val value = (dependsOn[key] ?: emptyList()).maxOfOrNull { depthOf(it) + 1 } ?: 0
            onStack.remove(key)
            depth[key] = value
            return value
        }
        dotnet.forEach { depthOf(it.key) }
        return depth
    }

    private fun solutionProjects(solution: File): List<File> {
        if (!solution.isFile) return emptyList()
        val regex = Regex("Project\\([^)]*\\)\\s*=\\s*\"[^\"]+\",\\s*\"([^\"]+\\.[a-z]{2}proj)\"", RegexOption.IGNORE_CASE)
        return regex.findAll(solution.readText()).mapNotNull { match ->
            val relative = match.groupValues[1].replace('\\', File.separatorChar).replace('/', File.separatorChar)
            val file = File(solution.parentFile, relative)
            if (file.isFile) file else null
        }.toList()
    }

    private fun assemblyName(projectFile: File): String {
        val text = runCatching { projectFile.readText() }.getOrNull() ?: return projectFile.nameWithoutExtension
        return Regex("<AssemblyName>([^<]+)</AssemblyName>").find(text)?.groupValues?.get(1)?.trim()
            ?: projectFile.nameWithoutExtension
    }

    private fun referencedNames(projectFile: File): List<String> {
        val text = runCatching { projectFile.readText() }.getOrNull() ?: return emptyList()
        val names = mutableListOf<String>()
        Regex("<(Reference|PackageReference)\\s+Include=\"([^\"]+)\"").findAll(text).forEach {
            names.add(it.groupValues[2].substringBefore(',').trim())
        }
        Regex("<ProjectReference\\s+Include=\"([^\"]+)\"").findAll(text).forEach {
            names.add(File(it.groupValues[1].replace('\\', '/')).nameWithoutExtension)
        }
        return names
    }
}
