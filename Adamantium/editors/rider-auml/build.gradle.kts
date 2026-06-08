import org.gradle.api.tasks.Exec
import org.gradle.api.tasks.bundling.Zip

plugins {
    id("java")
    id("org.jetbrains.kotlin.jvm") version "2.1.0"
    id("org.jetbrains.intellij.platform") version "2.6.0"
}

group = "com.adamantium"
version = "0.5.6"

repositories {
    mavenCentral()
    intellijPlatform {
        defaultRepositories()
    }
}

dependencies {
    intellijPlatform {
        // Built against IntelliJ IDEA Community (the plugin only uses platform + XML + LSP4IJ APIs,
        // so it installs in Rider too). Set platformVersion in gradle.properties to a version <= your Rider.
        intellijIdeaCommunity(providers.gradleProperty("platformVersion"))

        // The LSP4IJ plugin provides the LanguageServerFactory / connection-provider API.
        plugin(providers.gradleProperty("lsp4ijPlugin"))
    }
}

intellijPlatform {
    pluginConfiguration {
        ideaVersion {
            sinceBuild = providers.gradleProperty("sinceBuild")
            untilBuild = provider { null }   // don't cap the upper IDE version
        }
    }
}

kotlin {
    jvmToolchain(21)
}

// --- Bundle the AUML language server inside the plugin -----------------------------------------
// Framework-dependent publish (needs .NET 10 on the user's machine; keeps the server's type
// resolution working — a self-contained single-file build hides the runtime assemblies it needs).

val serverCsproj = file("../../Adamantium.UI.LanguageServer/Adamantium.UI.LanguageServer.csproj")
val serverConfiguration = providers.gradleProperty("serverConfiguration").getOrElse("Release")
val serverPublishDir = layout.buildDirectory.dir("server-publish")
val serverPackDir = layout.buildDirectory.dir("server-pack")

val publishServer by tasks.registering(Exec::class) {
    group = "build"
    description = "Publishes the AUML language server (framework-dependent) for bundling."
    inputs.files(fileTree(serverCsproj.parentFile) {
        include("**/*.cs", "**/*.csproj")
        exclude("**/bin/**", "**/obj/**")
    })
    outputs.dir(serverPublishDir)
    commandLine(
        "dotnet", "publish", serverCsproj.absolutePath,
        "-c", serverConfiguration, "-p:Platform=x64",
        "-o", serverPublishDir.get().asFile.absolutePath
    )
}

val packServer by tasks.registering(Zip::class) {
    dependsOn(publishServer)
    from(serverPublishDir)
    archiveFileName.set("server.zip")
    destinationDirectory.set(serverPackDir)
}

tasks.processResources {
    dependsOn(packServer)
    from(serverPackDir) {
        include("server.zip")
        into("server")
    }
}
