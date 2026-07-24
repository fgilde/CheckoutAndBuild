using System;
using System.Threading;

namespace FG.CheckoutAndBuild2.Common
{
	public class PauseCheckedActionScope : IDisposable
	{
		private readonly Action actionOut;
		private readonly Action actionIn;
	    private CancellationToken cancellationToken;
	    private readonly bool wait = true;

	    /// <summary>
	    /// Initializes a new instance.
	    /// </summary>
	    public PauseCheckedActionScope(Action actionIn, Action actionOut)
	        : this(actionIn, actionOut, default(CancellationToken))
	    {
	        wait = false;
	    }

	    public PauseCheckedActionScope(Action actionIn, Action actionOut, CancellationToken cancellationToken)
		{            
		    this.cancellationToken = cancellationToken;
            this.actionIn = actionIn;
            this.actionOut = actionOut;
            CallActionIn();			
		}

	    private async void CallActionIn()
	    {
            if(wait && cancellationToken != default(CancellationToken))
	            await cancellationToken.WaitWhenPaused();
	        actionIn();
	    }

	    /// <summary>
		/// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
		/// </summary>
		public async void Dispose()
		{
            if (wait && cancellationToken != default(CancellationToken))
                await cancellationToken.WaitWhenPaused();
            actionOut();
		}

	}
}