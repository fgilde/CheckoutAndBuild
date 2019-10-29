using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FG.CheckoutAndBuild2.Common;
using FG.CheckoutAndBuild2.Extensions;
using Microsoft.TeamFoundation.Client;
using Microsoft.TeamFoundation.Framework.Client;
using Microsoft.TeamFoundation.Framework.Common;

namespace FG.CheckoutAndBuild2.Services
{
	public class TfsIdentityManager
	{
		private readonly TfsContext tfsContext;
		private readonly string[] fetchPropertiesForImage = { "Microsoft.TeamFoundation.Identity.Image.Id", "Microsoft.TeamFoundation.Identity.Image.Data", "Microsoft.TeamFoundation.Identity.Image.Type" };
		public Dictionary<IdentityDescriptor, TeamFoundationIdentity> Identities { get; private set; }
		public List<TeamFoundationIdentity> IdentityGroups { get; private set; }

		/// <summary>
		/// Initializes a new instance of the <see cref="T:System.Object"/> class.
		/// </summary>
		public TfsIdentityManager(TfsContext context)
		{
			tfsContext = context;
			tfsContext.ConnectionChanged += TfsContextOnConnectionChanged;
			UpdateIdentities();
		}

		public TeamFoundationIdentity GetIdentity(string uniqueName)
		{
			return (from @group in IdentityGroups from memDesc in @group.Members select Identities[memDesc]).FirstOrDefault(
					identity => identity.GetUniqueName() == uniqueName || identity.DisplayName == uniqueName);
		}

		public IEnumerable<TeamFoundationIdentity> GetAllIdentities()
		{			
			return (from @group in IdentityGroups from memDesc in @group.Members select Identities[memDesc])
				.Where(identity => identity.Members == null || !identity.Members.Any()).OrderBy(identity => identity.DisplayName).Distinct();
		}

		public Task<Image> GetImageAsync(TeamFoundationIdentity identity)
		{			
			return Task.Run(() =>
			{
				return Check.TryCatch<Image, Exception>(() =>
				{
					TeamFoundationIdentity readIdentity = tfsContext.IdentityManagementService2.ReadIdentity(IdentitySearchFactor.AccountName, identity.UniqueName, MembershipQuery.Direct, ReadIdentityOptions.ExtendedProperties, fetchPropertiesForImage, IdentityPropertyScope.Both);
					byte[] image = readIdentity.GetProperty("Microsoft.TeamFoundation.Identity.Image.Data") as byte[];
					if (image != null && image.Length > 0)
					{
						MemoryStream ms = new MemoryStream(image);
						return Image.FromStream(ms);
					}
					return null;
				} );
			} );
		}


		private void TfsContextOnConnectionChanged(object sender, ContextChangedEventArgs e)
		{
			UpdateIdentities();
		}

		private void UpdateIdentities()
		{
			IdentityGroups = new List<TeamFoundationIdentity>();
			Identities = new Dictionary<IdentityDescriptor, TeamFoundationIdentity>(IdentityDescriptorComparer.Instance);
			if (tfsContext.IsTfsConnected)
			{							
				TeamFoundationIdentity group = tfsContext.IdentityManagementService.ReadIdentity(GroupWellKnownDescriptors.EveryoneGroup,
					MembershipQuery.Direct, ReadIdentityOptions.None);
				Identities[group.Descriptor] = group;
				IdentityGroups.Add(group);
				
				// Get expanded membership of the Valid Users group, which is all identities in this host             
				group = tfsContext.IdentityManagementService.ReadIdentity(GroupWellKnownDescriptors.EveryoneGroup, MembershipQuery.Expanded,
					ReadIdentityOptions.None);
				FetchIdentities(group.Members);
			}
		}

		private void FetchIdentities(IdentityDescriptor[] descriptors)
		{
			TeamFoundationIdentity[] identities;

			// If total membership exceeds batch size limit for Read, break it up
			int batchSizeLimit = 100000;

			if (descriptors.Length > batchSizeLimit)
			{
				int batchNum = 0;
				int remainder = descriptors.Length;
				var batchDescriptors = new IdentityDescriptor[batchSizeLimit];

				while (remainder > 0)
				{
					int startAt = batchNum * batchSizeLimit;
					int length = batchSizeLimit;
					if (length > remainder)
					{
						length = remainder;
						batchDescriptors = new IdentityDescriptor[length];
					}

					Array.Copy(descriptors, startAt, batchDescriptors, 0, length);
					identities = tfsContext.IdentityManagementService.ReadIdentities(batchDescriptors, MembershipQuery.Direct, ReadIdentityOptions.None);
					SortIdentities(identities);
					remainder -= length;
				}
			}
			else
			{
				identities = tfsContext.IdentityManagementService.ReadIdentities(descriptors, MembershipQuery.Direct, ReadIdentityOptions.None);
				SortIdentities(identities);
			}
		}

		private void SortIdentities(IEnumerable<TeamFoundationIdentity> identities)
		{
			foreach (TeamFoundationIdentity identity in identities)
			{
				Identities[identity.Descriptor] = identity;

				if (identity.IsContainer)
				{
					IdentityGroups.Add(identity);
				}
			}
		}

	}
}