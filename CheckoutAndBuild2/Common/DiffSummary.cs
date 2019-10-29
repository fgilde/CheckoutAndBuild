using System;
using Microsoft.TeamFoundation.VersionControl.Client;
using Microsoft.TeamFoundation.VersionControl.Common;

namespace FG.CheckoutAndBuild2.Common
{
	public class DiffSummary
	{
		public static DiffSummary DiffFiles(String source, Int32 sourceCodePage,
											String target, Int32 targetCodePage,
											DiffOptions diffOptions)
		{
			DiffSegment currSegment = Difference.DiffFiles(source, sourceCodePage, target, targetCodePage, diffOptions);
			DiffSummary summary = new DiffSummary();

			// Initialize a set of position markers which will be used to walk
			// through the files
			Int32 currentOriginalPosition = 0;
			Int32 currentModifiedPosition = 0;

			while (currSegment != null)
			{
				// Everything between the postiion markers and the start of the current common segment
				// will be either lines which were deleted or lines which were added
				Int32 linesDeleted = currSegment.OriginalStart - currentOriginalPosition;
				Int32 linesAdded = currSegment.ModifiedStart - currentModifiedPosition;
				
				summary.TotalLinesModified += Math.Min(linesDeleted, linesAdded);
				summary.TotalLinesAdded += Math.Max(0, linesAdded - linesDeleted);
				summary.TotalLinesDeleted += Math.Max(0, linesDeleted - linesAdded);

				// Advance the position markers to the end of the common section
				currentOriginalPosition = currSegment.OriginalStart + currSegment.OriginalLength;
				currentModifiedPosition = currSegment.ModifiedStart + currSegment.ModifiedLength;				

				// Move to the next segment in the linked-list
				currSegment = currSegment.Next;
			}

			// After walking the linked-list of common sections, the position markers
			// will be pointing to the end of the file, thus we can infer how many lines
			// are in each file.
			summary.OriginalLineCount = currentOriginalPosition;
			summary.ModifiedLineCount = currentModifiedPosition;

			return summary;
		}

		public Int32 OriginalLineCount { get; private set; }
		public Int32 ModifiedLineCount { get; private set; }
		public Int32 TotalLinesAdded { get; private set; }
		public Int32 TotalLinesDeleted { get; private set; }
		public Int32 TotalLinesModified { get; private set; }
	}

}