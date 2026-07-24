using System;
using System.Security.Cryptography;
using System.Text;

namespace CheckoutAndBuild.VisualStudio.Common
{
	/// <summary>DPAPI (current user) protection for the Azure DevOps PAT stored in the settings file.</summary>
	internal static class PatProtector
	{
		public static string Protect(string value)
		{
			if (string.IsNullOrEmpty(value))
				return "";
			return Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(value), null, DataProtectionScope.CurrentUser));
		}

		public static string Unprotect(string stored)
		{
			if (string.IsNullOrEmpty(stored))
				return "";
			try
			{
				return Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(stored), null, DataProtectionScope.CurrentUser));
			}
			catch (Exception)
			{
				return ""; // value from another user/machine — just require re-entry
			}
		}
	}
}
