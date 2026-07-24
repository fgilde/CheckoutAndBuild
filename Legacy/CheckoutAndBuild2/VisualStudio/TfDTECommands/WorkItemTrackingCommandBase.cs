using System;
using Microsoft.TeamFoundation.WorkItemTracking.Client;
using Microsoft.VisualStudio.TeamFoundation;

namespace FG.CheckoutAndBuild2.VisualStudio.TfDTECommands
{
	internal abstract class WorkItemTrackingCommandBase : TfDTECommand
	{
		public TfsContext TfsContext { get { return CheckoutAndBuild2Package.GetGlobalService<TfsContext>(); } }
		public override bool IsAvailable
		{
			get
			{
				if (TfsContext != null && TfsContext.IsTfsConnected)
					return !string.IsNullOrEmpty(this.ProjectName);
				return false;
			}
		}

		protected WorkItemStore Store
		{
			get
			{
				return TfsContext.WorkItemStore;
			}
		}

		protected string ProjectName
		{
			get
			{
				return TfsContext.TfsContextManager.CurrentContext.TeamProjectName;
			}
		}

		internal WorkItemTrackingCommandBase(string name)
			: base(name, "wi")
		{
		}

		protected T GetQueryFromArgument<T>(string parameter) where T : QueryItem
		{
			T obj1 = default(T);
			Guid result = Guid.Empty;
			T obj2;
			if (Guid.TryParse(parameter, out result))
			{
				obj2 = this.Store.GetQueryHierarchy(this.Store.Projects[this.ProjectName]).Find(result) as T;
				if ((object)obj2 == null)
					throw new ArgumentException("Kacke 1");
			}
			else
			{
				obj2 = PackageHelper.GetQueryItemFromPath(parameter, this.ProjectName) as T;
				if ((object)obj2 == null)
					throw new ArgumentException("Kacke 2");
			}
			return obj2;
		}
	}
}