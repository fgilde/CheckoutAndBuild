using System;

namespace FG.CheckoutAndBuild2.Common
{
	public class EventArgs<T>: EventArgs
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="T:System.EventArgs"/> class.
		/// </summary>
		public EventArgs(T value)
		{
			Value = value;
		}

		public T Value { get; private set; }
	}
}