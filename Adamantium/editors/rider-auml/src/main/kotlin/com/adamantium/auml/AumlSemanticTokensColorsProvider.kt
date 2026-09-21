package com.adamantium.auml

import com.intellij.openapi.editor.DefaultLanguageHighlighterColors
import com.intellij.openapi.editor.colors.CodeInsightColors
import com.intellij.openapi.editor.colors.TextAttributesKey
import com.intellij.psi.PsiFile
import com.redhat.devtools.lsp4ij.features.semanticTokens.SemanticTokensColorsProvider

/**
 * Maps the AUML language server's semantic token types to editor colours. The point of overriding
 * LSP4IJ's default is `unknown` (an element whose type doesn't resolve) → the red "unresolved
 * reference" colour, so deleting an xmlns turns the controls red like ReSharper. The other types are
 * mapped to sensible defaults too, so colouring is consistent whether this provider augments or
 * replaces the default one.
 */
class AumlSemanticTokensColorsProvider : SemanticTokensColorsProvider {
    override fun getTextAttributesKey(tokenType: String, tokenModifiers: List<String>, file: PsiFile): TextAttributesKey? =
        when (tokenType) {
            "unknown" -> CodeInsightColors.WRONG_REFERENCES_ATTRIBUTES   // red: type not in any imported namespace
            "type" -> DefaultLanguageHighlighterColors.CLASS_NAME
            "namespace" -> DefaultLanguageHighlighterColors.CLASS_REFERENCE
            "property" -> DefaultLanguageHighlighterColors.INSTANCE_FIELD
            "macro" -> DefaultLanguageHighlighterColors.METADATA
            else -> null
        }
}
