plugins {
    id("java")
    id("org.jetbrains.kotlin.jvm") version "2.0.21"
    id("org.jetbrains.intellij.platform") version "2.3.0"
}

group = "org.gilde"
version = providers.gradleProperty("pluginVersion").get()

repositories {
    mavenCentral()
    intellijPlatform {
        defaultRepositories()
    }
}

dependencies {
    intellijPlatform {
        create("IC", "2024.2.5")
    }
}

kotlin {
    jvmToolchain(21)
}

intellijPlatform {
    pluginConfiguration {
        id.set("org.gilde.checkoutandbuild")
        name.set("CheckoutAndBuild")
        version.set(project.version.toString())
        ideaVersion {
            sinceBuild.set("242")
            untilBuild.set(provider { null })
        }
    }
    buildSearchableOptions.set(false)
    publishing {
        token.set(System.getenv("JETBRAINS_MARKETPLACE_TOKEN") ?: "")
    }
}
