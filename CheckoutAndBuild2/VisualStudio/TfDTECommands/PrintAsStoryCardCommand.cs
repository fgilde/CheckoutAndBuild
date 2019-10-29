using Microsoft.TeamFoundation.MVVM;
using Microsoft.TeamFoundation.WorkItemTracking.Client;

namespace FG.CheckoutAndBuild2.VisualStudio.TfDTECommands
{
	internal class PrintAsStoryCardCommand : QueryCommandBase
	{
		public override bool IsAvailable => true;

	    internal PrintAsStoryCardCommand()
			: base("PrintAsStoryCardCommand")
		{ }

		internal override void TfDTEExecute(QueryItem query)
		{

		}

	}
}