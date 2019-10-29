using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace FG.CheckoutAndBuild2.WorkItemPrinting
{
	public class StoryCardHelper
	{
		private readonly TableRow[] originalRows;
		private readonly List<TableCell[]> originalCells;
		private readonly string fromFilename;
		private readonly Table table;
		private readonly int maxColumns;

		public TableProperties TableProperties { get; private set; }

		public StoryCardHelper(string storyCardFile, int maxColumns)
		{
			using (var document = WordprocessingDocument.Open(storyCardFile, false))
			{
				var doc = document.MainDocumentPart.Document;
				Table oldTable = doc.Descendants<Table>().First();

				originalRows = oldTable.Descendants<TableRow>().ToArray();
				originalCells = new List<TableCell[]>();
				foreach (var row in originalRows)
					originalCells.Add(row.Descendants<TableCell>().ToArray());

				TableProperties = oldTable.Descendants<TableProperties>().First();
			}
			table = new Table();
			table.AppendChild(TableProperties.CloneNode(true));
			fromFilename = storyCardFile;
			this.maxColumns = maxColumns;
		}

		public void AddCards(Table table, List<StoryCard> storyCardList)
		{
			int maxY = GetY(storyCardList.Count);

			int cardsCreated = 0;
			for (int y = 0; y <= maxY; y++)
			{
				List<TableRow> currentRows = new List<TableRow>();
				int maxX = GetX(storyCardList.Count - cardsCreated);
				for (int x = 0; x < maxX; x++)
				{
					for (int rowIndex = 0; rowIndex < originalRows.Length; rowIndex++)
					{
						var row = originalRows[rowIndex];

						TableRow currentRow;
						if (x == 0)
						{
							currentRow = (TableRow)row.CloneNode(true);
							currentRow.RemoveAllChildren<TableCell>();
							currentRows.Add(currentRow);
							table.AppendChild(currentRow);
						}
						else
							currentRow = currentRows[rowIndex];

						var rowCells = originalCells[rowIndex];
						foreach (var cell in rowCells)
						{
							TableCell clonedCell = (TableCell)cell.CloneNode(true);
							currentRow.AppendChild(clonedCell);
							AddTextToBookmark(clonedCell, storyCardList[cardsCreated]);
						}
					}
					cardsCreated++;
				}
			}

			var properties = table.Descendants<TableProperties>().First();
			var oldWidth = properties.TableWidth;
			var newWidth = Convert.ToInt32(oldWidth.Width.Value) * GetX(storyCardList.Count);

			properties.TableWidth = new TableWidth { Width = newWidth.ToString(CultureInfo.InvariantCulture) };
		}

		private void AddTextToBookmark(TableCell cell, StoryCard storyCard)
		{
			var bookmark = cell.Descendants<BookmarkStart>().First();

			if (bookmark.Name == "Id")
				AppendText(storyCard.Id.ToString(), bookmark);
			else if (bookmark.Name == "Iteration")
				AppendText(storyCard.Iteration, bookmark);
			else if (bookmark.Name == "Titel")
				AppendText(storyCard.Title, bookmark);
		}

		private static void AppendText(string textValue, BookmarkStart bookmark)
		{
			var idParagraph = bookmark.Parent;

			Text text = new Text();
			text.Text = textValue;

			List<OpenXmlElement> siblings = new List<OpenXmlElement>();
			OpenXmlElement sibling = bookmark.NextSibling();
			while (sibling != null && !(sibling is BookmarkEnd))
			{
				siblings.Add(sibling);
				sibling = sibling.NextSibling();
			}

			var runSibling = bookmark.NextSibling<Run>();
			var runProperties = GetRunProperties(runSibling);

			foreach (var s in siblings)
				idParagraph.RemoveChild(s);

			var run = new Run();
			if (runProperties != null)
				run.AppendChild(runProperties);
			run.AppendChild(text);
			idParagraph.AppendChild(run);
		}

		private static RunProperties GetRunProperties(Run nextSibling)
		{
			var runProperties = nextSibling.Descendants<RunProperties>().FirstOrDefault();
			if (runProperties != null)
				runProperties = (RunProperties)runProperties.CloneNode(true);
			return runProperties;
		}

		private int GetX(int cardsLeft)
		{
			var x = cardsLeft < maxColumns ? cardsLeft : maxColumns;
			return x;
		}

		private int GetY(int cardsCount)
		{
			int maxX = GetX(cardsCount);
			var y = cardsCount / maxX;
			return y;
		}

		public void SaveToFile(string toFilename)
		{
			File.Copy(fromFilename, toFilename, true);
			using (var document = WordprocessingDocument.Open(toFilename, true))
			{
				var doc = document.MainDocumentPart.Document;
				Table oldTable = doc.Descendants<Table>().First();
				var parentOfTable = oldTable.Parent;
				parentOfTable.ReplaceChild(table, oldTable);

				doc.Save();
			}
		}

		public void AddCards(IEnumerable<StoryCard> cards)
		{
			AddCards(table, cards.ToList());
		}
	}
}
