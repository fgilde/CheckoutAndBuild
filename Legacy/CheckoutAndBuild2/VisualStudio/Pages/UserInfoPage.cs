using System;
using System.Linq;
using FG.CheckoutAndBuild2.Types;
using FG.CheckoutAndBuild2.ViewModels;
using FG.CheckoutAndBuild2.VisualStudio.Sections;
using Microsoft.TeamFoundation.Controls;

namespace FG.CheckoutAndBuild2.VisualStudio.Pages
{
	/// <summary>
	/// Recent changes page.
	/// </summary>
	[TeamExplorerPage(GuidList.userInfoPage, Undockable = false, MultiInstances = false)]
	public class UserInfoPage : TeamExplorerBase
	{
	    public override string Title => $"Info {UserContext.UserName}";

        public UserInfoContext UserContext { get; private set; }

		public override object Content => new UserDetailViewModel(UserContext.Identity, serviceProvider);

	    public override void Initialize(object sender, IServiceProvider provider, object context)
		{
			base.Initialize(sender, provider, context);
			UserContext = context as UserInfoContext;
			foreach (var section in this.GetSections().OfType<IUserContextSection>())
				section.UserContext = UserContext;
		}
		
		/// <summary>
		/// The page should save context. This is called before navigation to another page, Team Project context switch, and so on
		/// </summary>
		public override void SaveContext(object sender, PageSaveContextEventArgs e)
		{
			base.SaveContext(sender, e);
			e.Context = UserContext;
		}

	}
}