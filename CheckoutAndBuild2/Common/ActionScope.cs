using System;

namespace FG.CheckoutAndBuild2.Common
{
	public class ActionScope : IDisposable
	{
		private readonly Action actionOut;

		/// <summary>
		/// Initializes a new instance.
		/// </summary>
		public ActionScope(Action actionIn, Action actionOut)
		{
			actionIn();
			this.actionOut = actionOut;
		}

		/// <summary>
		/// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
		/// </summary>
		public void Dispose()
		{
			actionOut();
		}

	}
}