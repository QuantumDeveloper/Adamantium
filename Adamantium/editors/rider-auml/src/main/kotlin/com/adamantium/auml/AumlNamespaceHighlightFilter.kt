package com.adamantium.auml

import com.intellij.codeInsight.daemon.impl.HighlightInfo
import com.intellij.codeInsight.daemon.impl.HighlightInfoFilter
import com.intellij.lang.annotation.HighlightSeverity
import com.intellij.psi.PsiFile
import com.intellij.psi.util.PsiTreeUtil
import com.intellij.psi.xml.XmlAttribute

/**
 * Suppresses XML's "URI is not registered" problem on AUML xmlns declarations that point at a CLR
 * namespace, e.g. `xmlns:fluent="clr-namespace:Adamantium.UI.Themes.FluentTheme"`. These
 * URIs are dynamic (a different CLR namespace per file), so unlike the static `http://adamantium/ui`
 * namespaces (handled by AumlResourceProvider) they can't be pre-registered as ignored resources.
 * The AUML language server is the real validator here, so the built-in XML schema check is just noise.
 *
 * Matches on PSI structure (an xmlns attribute whose value starts with `clr-namespace:`) rather than
 * the problem's message text, so it is independent of the IDE's display language.
 */
class AumlNamespaceHighlightFilter : HighlightInfoFilter {
    override fun accept(info: HighlightInfo, file: PsiFile?): Boolean {
        if (file?.fileType != AumlFileType) return true
        if (info.severity < HighlightSeverity.WEAK_WARNING) return true   // leave info/markers untouched

        val leaf = file.findElementAt(info.startOffset) ?: return true
        val attribute = PsiTreeUtil.getParentOfType(leaf, XmlAttribute::class.java) ?: return true

        val isXmlns = attribute.name == "xmlns" || attribute.name.startsWith("xmlns:")
        val value = attribute.value ?: return true
        return !(isXmlns && value.startsWith("clr-namespace:"))
    }
}
