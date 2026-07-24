using System;
using System.Threading;
using System.Threading.Tasks;
using nExt.Core.Helper;

namespace FG.CheckoutAndBuild2.TeamRooms
{
	//https://www.visualstudio.com/en-us/integrate/api/overview
	public class TeamRoomHelper
	{
		private const string apiVersion = "1.0";
		private readonly Uri teamProjectCollectionUri;
		private TaskQueue queue;
		private readonly CancellationTokenSource cancellationTokenSource;

		/// <summary>
		/// Initializes a new instance of the <see cref="T:System.Object"/> class.
		/// </summary>
		public TeamRoomHelper(Uri teamProjectCollectionUri)
		{			
			cancellationTokenSource = new CancellationTokenSource();
			this.teamProjectCollectionUri = teamProjectCollectionUri;
			queue = new TaskQueue(cancellationTokenSource);
		}

		public Task<object> QueryRoomsAsync()
		{
			return queue.QueueWorkAsync(token => Request<object>(teamProjectCollectionUri.Append("_apis/chat/rooms?api-version=1.0")));
		}

		public void Cancel()
		{
			cancellationTokenSource.Cancel();
		}

		private TResult Request<TResult>(Uri uri)
		{
			return default(TResult);
		}

	}
}