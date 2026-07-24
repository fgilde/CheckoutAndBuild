using System;
using System.Collections;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace FG.CheckoutAndBuild2.VisualStudio
{
	public static class VisualStudioTrackingSelection
	{
		private static SelectionContainer selContainer;

		public static void UpdateSelectionTracking(params object[] objects)
		{
			ArrayList listObjects = new ArrayList();
			foreach (var o in objects)
				listObjects.Add(o);
			SelectList(listObjects);
		}

		private static ITrackSelection trackSel;

		public static ITrackSelection TrackSelection
		{
			get { return trackSel ?? (trackSel = CheckoutAndBuild2Package.GetGlobalService<ITrackSelection>()); }
		}

		public static void UpdateSelection()
		{
			ITrackSelection track = TrackSelection;
			if (track != null)
				track.OnSelectChange(selContainer);
		}

		public static void SelectList(ArrayList list)
		{
			if (selContainer == null)
				selContainer = new SelectionContainer();
			selContainer.SelectableObjects = list;
			selContainer.SelectedObjects = list;
			UpdateSelection();
		}

	}
}