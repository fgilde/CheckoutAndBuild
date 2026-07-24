using FG.CheckoutAndBuild2.Types;
using Microsoft.TeamFoundation.Controls;

namespace FG.CheckoutAndBuild2.VisualStudio.Sections
{
	public interface IUserContextSection : ITeamExplorerSection
	{
		UserInfoContext UserContext { get; set; }
	}
}