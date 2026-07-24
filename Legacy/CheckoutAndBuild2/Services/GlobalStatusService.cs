using System;
using CheckoutAndBuild2.Contracts;
using EnvDTE;
using FG.CheckoutAndBuild2.Common;
using FG.CheckoutAndBuild2.Extensions;
using FG.CheckoutAndBuild2.Properties;

namespace FG.CheckoutAndBuild2.Services
{
	public class GlobalStatusService : NotificationObject
	{
		private readonly IServiceProvider serviceProvider;
		private int maximum;
		private int step;
		private bool isActive;
		private string currentAction;

		/// <summary>
		/// Initializes a new instance of the <see cref="T:System.Object"/> class.
		/// </summary>
		public GlobalStatusService(IServiceProvider serviceProvider)
		{
			this.serviceProvider = serviceProvider;
		}

		#region Properties

		public string CurrentAction
		{
			get { return currentAction; }
			set
			{
				SetProperty(ref currentAction, value);
				SyncVsProgress();
			}
		}

		public int Step
		{
			get { return step; }
			set
			{
				if (SetProperty(ref step, value))
				{
					IsActive = isActive && step < maximum;
					SyncVsProgress();
				}
			}
		}

		public int Maximum
		{
			get { return maximum; }
			set
			{
				if (SetProperty(ref maximum, value))
				{
					IsActive = isActive && step < maximum;
					SyncVsProgress();
				}
			}
		}

		public bool IsActive
		{
			get { return isActive; }
			set
			{
				SetProperty(ref isActive, value);
				SyncVsProgress();
			}
		}

		#endregion

		#region Public Methods

		public void InitOrAttach(int maxValue, string text)
		{
			if (!IsActive)
			{
				Maximum = maxValue;
				Start();
			}
			CurrentAction = text;
		}

		public void IncrementStep()
		{
			if (!IsActive)
				return;
			Step ++;
		}

		private void Start()
		{
			Step = 0;
			IsActive = true;
		}

		public void Stop()
		{
			IsActive = false;
		}

		#endregion

		#region Private Methods

		private void SyncVsProgress()
		{
			Check.TryCatch<Exception>(() =>
			{
				var dte = serviceProvider.Get<DTE>();
			    dte?.StatusBar?.Progress(IsActive, $"{Const.ApplicationName} - {CurrentAction}", Step, Maximum);
			});
		}

		#endregion

	}
}