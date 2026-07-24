using System;
using Microsoft.TeamFoundation.WorkItemTracking.Client;

namespace FG.CheckoutAndBuild2.VisualStudio.TfDTECommands
{
	internal abstract class QueryCommandBase : WorkItemTrackingCommandBase
	{
		internal QueryCommandBase(string name)
			: base(name)
		{
		}

		public override bool TfDTEExecute()
		{
			string[] argumentsAsArray = this.Arguments.GetFreeArgumentsAsArray();
			if (argumentsAsArray.Length == 0)
				throw new ArgumentException("Kacke 3");
			if (argumentsAsArray.Length > 1)
				throw new ArgumentException("Kacke 4");
			this.TfDTEExecute((QueryItem)this.GetQueryFromArgument<QueryDefinition>(argumentsAsArray[0]));
			return true;
		}

		internal abstract void TfDTEExecute(QueryItem item);
	}
}