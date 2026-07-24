using System;

namespace FG.CheckoutAndBuild2
{

	internal static class GuidList
	{

#if DEBUG
		public const string guidCheckoutAndBuild2PkgString = "9221c587-d658-438b-a9d8-16092cbc68c6";
		public const string guidCheckoutAndBuild2CmdSetString = "4ced4240-ab95-44d7-ba2f-b13772a295ce";
		public const string guidErrorListString = "95B6ABC0-1273-4238-9B8B-0D9707069C8D";
		public const string guidWorkItemSearchReplace = "70F60B0F-F7F8-4913-8E15-798E6A71DD9F";
		public const string gitStashPage = "7AE2E2AB-53C3-412F-8B81-CB20E0F06A59";

	    public const string gitStashDetailSection = "EEE891D2-609B-4A6A-843F-BCEA1397C4BC";
	    public const string checkoutAndBuildGitStashesSection = "1F9B73D2-C52D-4766-ADB5-769598A7E2E4";
	    public const string checkoutAndBuildExtendedGitSyncSection = "007BFB21-CB7E-4AA1-84DB-4F1E6EFE6A39";
	    public const string checkoutAndBuildExtendedGitBranchSection = "71F356AE-E913-45E2-ACFA-AE24B1682836";
	    public const string checkoutAndBuildExtendedGitCheckinSection = "F33A419C-599E-49DD-9240-D814D8FA9503";
        public const string workItemSearchReplacePageId = "94A52883-CBFF-4552-BDF7-663160F8113F";
		public const string checkoutAndBuildTeamExplorerNavigationItem = "3BEBB394-E51A-455E-ABF0-E34CD2D9B747";
        public const string userPageTeamExplorerNavigationItem = "F46A2E10-B766-42F5-8A6B-105CA4012B5B";
        public const string disableUserInfoNavigationItemLink = "AE2FC644-724A-45D3-AD9C-DE3E29F33FF4";
		public const string checkoutAndBuildTeamExplorerSection = "ED6ED04D-5A54-4A09-8EB9-76E05F53C5EC";
		public const string outputPaneGuid = "8B2A4609-C69E-4AA6-9DCF-F290B7F33D88";
		public const string errorProviderGuid = "465B0C7A-7F4E-4B52-BED8-A68027690852";
		public const string checkoutAndBuildSettingsLinkId = "9CC51EC2-92AC-416E-830F-358588080601";
		public const string checkoutAndBuildSettingsSection = "318696CB-5F06-42B5-BAE6-6344B85D3816";
		public const string resetNotificationId = "25B8663D-238A-42BB-8CA2-4A9FFC4E91D7";
		public const string deleteMappingNotificationId = "43B2701D-0137-4E3E-9942-AB1278BD61CA";
		public const string checkoutAndBuildAboutLinkId = "949ECAD0-BE0B-487F-B404-8096F100E5AE";
		public const string checkoutAndBuildExtendedCheckinSection = "6E261BFB-A8B5-4B6A-A340-35E3CD7AD45E";
		public const string checkoutAndBuildExtendedChangesetSection = "8FB23019-22F9-47E1-92E7-2B1BC4D8D735";
		public const string allChangesSectionId = "633A152C-F920-40E3-809E-2BEF61B998AD";
		public const string myChangesSectionId = "7C390767-21EE-4AB9-B101-FBD0296FABC0";
		public const string userChangesSectionId = "8F0E5E76-41E9-4346-BD40-DE16A8D43DDD";
		public const string workItemsSectionId = "A948FD0E-9250-42E6-B596-2D67CD7F822A";
		public const string shelveSetSectionId = "CE3419E2-1C81-4AD5-835B-FBCF2C7EE7B5";
		public const string buildDetailsSectionId = "090DA76B-C6C6-4A61-A15C-7F77E9EA7888";
		public const string aboutPageId = "DFAEDB9F-D0B0-4471-B613-CF334D195CEF";
		public const string mainOptionsPage = "6741AF5D-79A6-4A05-A5DA-152BCB2E2C6A";
		public const string copySettingsOptionsPage = "73F051DC-9A31-4F71-867B-6630D536F543";
		public const string pluginOptionsPage = "73BB9CA2-BD57-4775-88D9-52CC9438B13B";
		public const string sectionOptionsPage = "9C0E5011-11B0-4CB3-9AEF-553A66E9CB85";
		public const string userInfoPage = "BFA6D5FA-2319-4A51-BB18-79FEC33EEE25";
		public const string checkoutAndBuildTeamExplorerMainPage = "F449ED4C-5493-4A02-90D5-C56C30A89A22";
		public const string recentChangesPage = "807B768E-6964-41D8-B5B4-642FE94E87BA";
		public const string historyPage = "30D134E2-DD0F-4D3C-A3C7-B477E769A6C9";
		public const string findChangesNavigationLink = "024FB6F7-B045-4D0E-A4C1-8B4ADA566C76";
		public const string recentChangesLink = "5D5CD8BB-A17C-4F58-951F-01FC5AFC6A5D";
		public const string userInfoNavigationLink = "D3693C2D-4990-4387-8288-9FCECEEFD601";
		public const string userPendingChangesSectionId = "BE46741D-293A-42A6-A915-969F9700FE88";
		public const string workspaceSpecificOptionsPage = "08EABAF3-7F8A-4A74-B4C8-1C9C2318D844";
#else
		public const string guidCheckoutAndBuild2PkgString = "07804054-fcda-4d80-aac2-abe81b9360c5";
		public const string guidCheckoutAndBuild2CmdSetString = "56697A9C-2233-42B9-869D-277F72E0EED9";
		public const string guidErrorListString = "E55A1EB9-A848-446E-A3CF-5AAFBC2DF450";
		public const string guidWorkItemSearchReplace = "7D3B0274-41A1-4952-A46D-EFCAD9215523";
        public const string gitStashPage = "5D393C7B-4180-4846-9CEE-FF8E4556FE04";

        public const string gitStashDetailSection = "64B8F8B9-B5E5-4B6D-9326-E5F7BD94879F";
        public const string checkoutAndBuildGitStashesSection = "053A9C73-FE22-46FC-A550-C524C3908EC8";
        public const string checkoutAndBuildExtendedGitSyncSection = "3BE49128-84B8-4031-B5ED-2DE06B495EC3";
        public const string checkoutAndBuildExtendedGitBranchSection = "F5A1E66B-6191-44A2-8B12-FCCEA4488EB1";
        public const string checkoutAndBuildExtendedGitCheckinSection = "716B5398-4E18-4834-92E1-7F1CC8FDCB74";
		public const string workItemSearchReplacePageId = "3A2F78AC-A871-4080-BD3E-99A3EF7EE949";
		public const string checkoutAndBuildTeamExplorerNavigationItem = "00C9C1F8-C8E1-491C-A3DF-65AAE92D29C9";
        public const string userPageTeamExplorerNavigationItem = "8C2E253A-61B7-419B-94F6-268C17AF064B";
        public const string disableUserInfoNavigationItemLink = "4D8AE6E0-FBFA-43A7-B0C5-41CF12E57C13";
		public const string checkoutAndBuildTeamExplorerSection = "EEDE37A4-B515-42D0-9FDC-6386D90F4BEA";
		public const string outputPaneGuid = "93381043-C40D-4E03-A96C-7BD0414880E9";
		public const string errorProviderGuid = "822AEC7A-CC03-4094-B358-DDB2F99DA4E4";
		public const string checkoutAndBuildSettingsLinkId = "011E1FA8-818F-4B99-BD8A-392EF725B1CA";
		public const string checkoutAndBuildSettingsSection = "5BBAF971-4E82-4ECA-83FB-0304C5F1428A";
		public const string resetNotificationId = "1F39EE13-1CA0-44CE-A7FC-D92E699B9F04";
		public const string deleteMappingNotificationId = "BA3FDEC4-07D6-473B-9232-F0D3DDD8AB4A";
		public const string checkoutAndBuildAboutLinkId = "DDDE570E-96EE-4222-A6D1-2F065F68AB91";
		public const string checkoutAndBuildExtendedCheckinSection = "988816AE-C705-48F5-926B-E2C14F460091";
		public const string checkoutAndBuildExtendedChangesetSection = "EF0CF829-E2A5-4C0F-A54B-25AB0EF84308";
		public const string allChangesSectionId = "322AEB53-14CB-4FD3-9EEC-956178FDB3B8";
		public const string myChangesSectionId = "7A89BDF2-1015-4352-A5E2-E123F6757A17";
		public const string userChangesSectionId = "88CC3565-23FC-42E0-969F-D684DC7BB31D";
		public const string workItemsSectionId = "0255542D-337B-4A80-939C-20041CC79EE1";
		public const string shelveSetSectionId = "11285082-9D16-4546-85B5-4DABF53DA507";		
		public const string buildDetailsSectionId = "18604DFA-1C85-4C82-8E23-9E175A68592A";
		public const string aboutPageId = "2C39346E-C41F-47C3-81B9-314938F80B71";
		public const string mainOptionsPage = "D2AB2630-105A-4115-B69C-45E0067E1621";
        public const string copySettingsOptionsPage = "04017A2B-15D6-4908-A707-9C689C40E94F";
		public const string pluginOptionsPage = "1BB08738-3327-418B-95D9-BF64A4B51266";
		public const string sectionOptionsPage = "36EF3ED6-E42B-4654-A6AF-192B915AD5E1";
		public const string userInfoPage = "E0435460-893E-4A04-BA5F-3E918B16A3F7";
		public const string checkoutAndBuildTeamExplorerMainPage = "49B438FA-E19A-48DC-88FA-79412F15371B";
		public const string recentChangesPage = "BF14E082-443D-4645-8C00-1E3FCE42BC40";
		public const string historyPage = "764A6363-6148-4657-8DE3-7BCD93B9FF3C";
		public const string findChangesNavigationLink = "5652DA89-21B6-4525-B770-7F22E9BEA03C";
		public const string recentChangesLink = "91C3579B-9BF3-4AFA-A0AB-B140929EB4FF";
		public const string userInfoNavigationLink = "999216C6-55DD-4A14-B835-2B5D62174062";
		public const string userPendingChangesSectionId = "C1CD5E34-3A28-432B-92B9-242D23D06CE8";
        public const string workspaceSpecificOptionsPage = "63F58A8D-7E36-48F9-A87B-F116BD8E05E5";
#endif

        public const string workItemSearchProvider = "2233C65A-2A41-4F9B-8AD7-049C58F4E68D";
		public static Guid UpdateNotificationId = new Guid("B59E43AD-14C4-4CF6-A2A5-A6F2513E15E4");
		public static readonly Guid guidCheckoutAndBuild2CmdSet = new Guid(guidCheckoutAndBuild2CmdSetString);
		public static Guid ToGuid(this string s)
		{
			return new Guid(s);
		}
	};

	
	internal static class PkgCmdIDList
	{
		public const uint cmdidMyCommand = 0x100;
		public const uint commandIdErrorList = 0x200;
		public const uint cmdidWorkItemSearchAndReplace = 0x300;
	};

	internal static class ProjectTypeGuids
	{
		public const string TestProject = "3AC096D0-A1C2-E12C-1390-A8335801FDAB";
	}

	public static class VisualStudioIds
	{
		public const string TeamExplorerToolWindowId = "131369F2-062D-44A2-8671-91FF31EFB4F4";
		public const string TestResultToolWindowId = "519E8A32-1C95-4A42-956F-2CEE2F28EB0F";
		public const string TestResultServiceId = "7A1B6659-3B5C-48b3-B3B7-750B8171AF4E";
		public const string TestManagmentPackageId = "A9405AE6-9AC6-4f0e-A03F-7AFE45F6FCB7";
	}

	public static class VSViewKinds
	{
		public const string vsViewKindAny = "{FFFFFFFF-FFFF-FFFF-FFFF-FFFFFFFFFFFF}";

		public const string vsViewKindCode = "{7651A701-06E5-11D1-8EBD-00A0C90F26EA}";

		public const string vsViewKindDebugging = "{7651A700-06E5-11D1-8EBD-00A0C90F26EA}";

		public const string vsViewKindDesigner = "{7651A702-06E5-11D1-8EBD-00A0C90F26EA}";

		public const string vsViewKindPrimary = "{00000000-0000-0000-0000-000000000000}";

		public const string vsViewKindTextView = "{7651A703-06E5-11D1-8EBD-00A0C90F26EA}";
	}	
}