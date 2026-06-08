package com.adamantium.auml

import com.intellij.javaee.ResourceRegistrar
import com.intellij.javaee.StandardResourceProvider

/**
 * Marks the AUML xmlns URIs as "known" so the IDE's XML support stops flagging them as
 * "URI is not registered". No schema is bound, so elements/attributes stay lax — real
 * validation comes from the AUML language server, not from XML schema checks.
 */
class AumlResourceProvider : StandardResourceProvider {
    override fun registerResources(registrar: ResourceRegistrar) {
        for (uri in NAMESPACES) registrar.addIgnoredResource(uri)
    }

    private companion object {
        val NAMESPACES = listOf(
            "http://adamantium/ui",
            "http://adamantium/ui/resources",
            "http://adamantium/ui/xaml/extensions",
        )
    }
}
