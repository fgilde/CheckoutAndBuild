using System;

namespace FG.CheckoutAndBuild2.Types
{
	internal class DisposableCookie : IDisposable
	{
		private readonly Action action;

		public DisposableCookie(Action action)
		{
			this.action = action;
		}

		public void Dispose()
		{
			action();
		}
	}
}
