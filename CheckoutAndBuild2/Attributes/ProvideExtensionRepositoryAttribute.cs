using System;
using FG.CheckoutAndBuild2.Common;
using Microsoft.VisualStudio.Shell;
using Microsoft.Win32;

namespace FG.CheckoutAndBuild2.Attributes
{
	[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = true)]
	internal class ProvideExtensionRepositoryAttribute : RegistrationAttribute
	{
		public string Protocol { get; set; }
		public string DisplayNameResourceId { get; set; }
		public uint Priority { get; set; }
		public Guid Id { get; set; }
		public Uri Uri { get; private set; }

		public ProvideExtensionRepositoryAttribute(string id, string uri, uint priority, string protocol, string displayNameResourceId)
		{
			Id = Guid.ParseExact(id, "B");
			Uri = new Uri(uri, UriKind.Absolute);
			Priority = priority;
			Protocol = protocol;
			DisplayNameResourceId = displayNameResourceId;
			InitialRegister();
		}

		private void InitialRegister()
		{
			//RegistryKey baseKey = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Default);
			//using (var key = baseKey.CreateSubKey(string.Format(@"Software\Microsoft\VisualStudio\12.0\ExtensionManager\\Repositories\\{0}", Id.ToString("B"))))
			//{
			//	if (key != null)
			//	{
			//		key.SetValue("", Uri.ToString());
			//		key.SetValue("Priority", Priority, RegistryValueKind.DWord);
			//		key.SetValue("Protocol", Protocol);
			//		//key.SetValue("DisplayNameResourceID", DisplayNameResourceId);
			//		key.SetValue("DisplayName", DisplayNameResourceId);
			//		//key.SetValue("DisplayNamePackageGuid", Id.ToString("B"));
			//	}
			//}
		}

		public override void Register(RegistrationContext context)
		{
			using (Key key = context.CreateKey(string.Format("ExtensionManager\\Repositories\\{0}", Id.ToString("B"))))
			{
				key.SetValue("", Uri.ToString());
				key.SetValue("Priority", Priority);
				key.SetValue("Protocol", Protocol);
				//key.SetValue("DisplayNameResourceID", DisplayNameResourceId);
				key.SetValue("DisplayName", DisplayNameResourceId);
				//key.SetValue("DisplayNamePackageGuid", context.ComponentType.GUID.ToString("B"));				
			}
		}

		public override void Unregister(RegistrationContext context)
		{}
	}
}