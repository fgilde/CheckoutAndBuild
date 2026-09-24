package org.gilde.coab

import com.intellij.ide.BrowserUtil
import com.intellij.openapi.ui.DialogBuilder
import com.intellij.openapi.util.Disposer
import com.intellij.ui.JBColor
import com.intellij.ui.jcef.JBCefApp
import com.intellij.ui.jcef.JBCefBrowser
import com.intellij.util.ui.JBUI
import java.awt.Color
import java.awt.Dimension
import java.io.File
import java.util.Locale
import javax.swing.JComponent

/** gilde.org connect widgets (contact/support) in an in-IDE dialog (JCEF), falling back to the browser. */
object ConnectDialog {

    fun show(parent: JComponent, widget: String) {
        val html = buildHtml(widget)
        val title = dialogTitle(widget)
        if (!JBCefApp.isSupported()) {
            val file = File.createTempFile("coab-connect-$widget", ".html")
            file.writeText(html)
            BrowserUtil.browse(file)
            return
        }
        val browser = JBCefBrowser()
        browser.loadHTML(html)
        browser.component.preferredSize = Dimension(640, 660)
        val builder = DialogBuilder(parent)
        builder.setTitle(title)
        builder.setCenterPanel(browser.component)
        builder.addCloseButton()
        builder.setOkOperation { builder.dialogWrapper.close(0) }
        try {
            builder.show()
        } finally {
            Disposer.dispose(browser)
        }
    }

    private fun dialogTitle(widget: String): String {
        val german = Locale.getDefault().language == "de"
        return if (widget == "contact")
            (if (german) "Kontakt CheckoutAndBuild" else "Contact CheckoutAndBuild")
        else
            (if (german) "CheckoutAndBuild unterstützen" else "Support CheckoutAndBuild")
    }

    private fun buildHtml(widget: String): String {
        val dark = !JBColor.isBright()
        val accent = toHex(JBUI.CurrentTheme.Link.Foreground.ENABLED)
        val language = if (Locale.getDefault().language == "de") "de" else "en"
        val background = if (dark) "#1e1f22" else "#ffffff"
        val extra =
            if (widget == "support")
                """ show-support-hint="false" support-layout="rows" show-support-icons="true" show-support-qr="true""""
            else
                """ title="${dialogTitle(widget)}""""
        return """<!DOCTYPE html>
<html lang="$language">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<script type="module" src="https://connect.gilde.org/widgets/v1.js"></script>
<style>html,body{margin:0;padding:16px;background:$background;display:flex;justify-content:center}</style>
</head>
<body>
<gilde-$widget project="fgilde/CheckoutAndBuild" widget="$widget" inline
  theme="${if (dark) "dark" else "light"}" accent="$accent" language="$language"
  width="560" radius="18" padding="28"
  show-logo="true" show-description="false" show-homepage="true"
  show-preview-notice="false" show-footer="false"
  footer-brand="CheckoutAndBuild" footer-tagline="gilde.org"$extra></gilde-$widget>
</body>
</html>"""
    }

    private fun toHex(color: Color): String = "#%02x%02x%02x".format(color.red, color.green, color.blue)
}
