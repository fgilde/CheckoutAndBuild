package org.gilde.coab

import java.awt.Component
import java.awt.Container
import java.awt.Dimension
import java.awt.FlowLayout
import javax.swing.JScrollPane
import javax.swing.SwingUtilities

/** FlowLayout that reports a wrapped preferred size, so toolbars break into multiple lines instead of clipping. */
class WrapLayout(align: Int = LEFT, hgap: Int = 6, vgap: Int = 4) : FlowLayout(align, hgap, vgap) {

    override fun preferredLayoutSize(target: Container): Dimension = layoutSize(target, preferred = true)

    override fun minimumLayoutSize(target: Container): Dimension {
        val minimum = layoutSize(target, preferred = false)
        minimum.width -= hgap + 1
        return minimum
    }

    private fun layoutSize(target: Container, preferred: Boolean): Dimension {
        synchronized(target.treeLock) {
            var targetWidth = target.size.width
            if (targetWidth == 0) targetWidth = Int.MAX_VALUE
            val insets = target.insets
            val horizontalInsets = insets.left + insets.right + hgap * 2
            val maxWidth = targetWidth - horizontalInsets

            val dimension = Dimension(0, 0)
            var rowWidth = 0
            var rowHeight = 0
            for (i in 0 until target.componentCount) {
                val component: Component = target.getComponent(i)
                if (!component.isVisible) continue
                val size = if (preferred) component.preferredSize else component.minimumSize
                if (rowWidth + size.width > maxWidth) {
                    addRow(dimension, rowWidth, rowHeight)
                    rowWidth = 0
                    rowHeight = 0
                }
                if (rowWidth != 0) rowWidth += hgap
                rowWidth += size.width
                rowHeight = maxOf(rowHeight, size.height)
            }
            addRow(dimension, rowWidth, rowHeight)

            dimension.width += horizontalInsets
            dimension.height += insets.top + insets.bottom + vgap * 2
            val scrollPane = SwingUtilities.getAncestorOfClass(JScrollPane::class.java, target)
            if (scrollPane != null && target.isValid) dimension.width -= hgap + 1
            return dimension
        }
    }

    private fun addRow(dimension: Dimension, rowWidth: Int, rowHeight: Int) {
        dimension.width = maxOf(dimension.width, rowWidth)
        if (dimension.height > 0) dimension.height += vgap
        dimension.height += rowHeight
    }
}
