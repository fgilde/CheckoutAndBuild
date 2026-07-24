using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.TeamFoundation.Threading;

namespace FG.CheckoutAndBuild2
{
	public class TaskQueue
	{
		public TaskFactory TaskFactory { get; private set; }
		public OrderedTaskScheduler TaskScheduler { get; private set; }
		public CancellationTokenSource CancellationTokenSource { get; private set; }

		/// <summary>
		/// Initializes a new instance of the <see cref="T:System.Object"/> class.
		/// </summary>
		public TaskQueue(CancellationTokenSource cancellationTokenSource = null)
		{

			TaskScheduler = new OrderedTaskScheduler();
			CancellationTokenSource = cancellationTokenSource ?? new CancellationTokenSource();
			TaskFactory = new TaskFactory(CancellationTokenSource.Token, TaskCreationOptions.HideScheduler, TaskContinuationOptions.None, TaskScheduler);

		}

		public void CancelAll()
		{
			CancellationTokenSource.Cancel();
		}

		public Task<TResult> QueueWorkAsync<TResult>(Func<CancellationToken, TResult> action)
		{
			if (TaskFactory != null)
				return TaskFactory.StartNew(() => action(CancellationTokenSource.Token));
			return Task.FromResult(default(TResult));
		}

		public Task QueueWorkAsync(Action<CancellationToken> action)
		{
			if (TaskFactory != null)
				return TaskFactory.StartNew(() => action(CancellationTokenSource.Token));
			return Task.FromResult(0);
		}
	}
}