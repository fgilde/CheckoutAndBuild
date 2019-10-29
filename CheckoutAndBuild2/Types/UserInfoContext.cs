using Microsoft.TeamFoundation.Framework.Client;

namespace FG.CheckoutAndBuild2.Types
{
	public class UserInfoContext
	{
		public string UserName { get; set; }
		public TeamFoundationIdentity Identity { get; private set; }

		/// <summary>
		/// Initializes a new instance of the <see cref="T:System.Object"/> class.
		/// </summary>
		public UserInfoContext(TeamFoundationIdentity identity)
		{
			Identity = identity;
			if(identity != null)
				UserName = Identity.DisplayName;
		}
	}
}