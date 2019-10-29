using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace FG.CheckoutAndBuild2.Controls.Forms
{
	public class Keyword
	{
		public static List<Keyword> Keywords = new List<Keyword>()
		                                        	{
		                                        		new Keyword("Success", Color.Green),
		                                        		new Keyword("Failure", Color.Red),
		                                        		new Keyword("Failed", Color.Red),
		                                        		new Keyword("Error", Color.Red),
		                                        		new Keyword("Ok", Color.Green),
		                                        	};

		public Keyword(string word, Color color)
		{
			Word = word;
			ForeColor = color;
		}

		public Color ForeColor { get; set; }
		public string Word { get; set; }
		public int WordLength { get { return Word.Length; } }

		public static void ColorizeKeywords(RichTextBox textbox)
		{
			ColorizeKeywords(textbox, Keywords);
		}
		public static void ColorizeKeywords(RichTextBox textbox, IEnumerable<Keyword> wordList)
		{
			int selStart = textbox.SelectionStart;
			Color selColor = textbox.SelectionColor;
			bool modified = false;
			try
			{
				foreach (Keyword word in wordList)
				{
					int wordStartIndex = textbox.Text.IndexOf(
						word.Word,
						StringComparison.OrdinalIgnoreCase);
					while (wordStartIndex >= 0)
					{
						textbox.Select(wordStartIndex, word.WordLength);
						textbox.SelectionColor = word.ForeColor;
						textbox.Select(wordStartIndex + word.WordLength, 0);
						textbox.SelectionColor = Color.Black;
						modified = true;

						wordStartIndex = textbox.Text.IndexOf(
							word.Word,
							wordStartIndex + word.WordLength,
							StringComparison.OrdinalIgnoreCase);
					}
				}
			}
			finally
			{
				if (modified)
				{
					// Position wiederherstellen
					textbox.Select(selStart, 0);
					textbox.SelectionColor = Color.Black;
					textbox.Invalidate();
				}
			}
		}
	}
}