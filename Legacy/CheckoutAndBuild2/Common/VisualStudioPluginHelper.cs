using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Microsoft.Win32;

namespace FG.CheckoutAndBuild2.Common
{

	public class VisualStudioPluginHelper
	{
		public static IEnumerable<VisualStudioInfo> GetInstalledStudios()
		{
			IEnumerable<string> registryKeys = Registry.ClassesRoot.GetSubKeyNames().Where(s => s.StartsWith("VisualStudio.DTE")).ToList();
			return (from registryKey in registryKeys let version = registryKey.Replace("VisualStudio.DTE.", string.Empty) where !string.IsNullOrEmpty(version) let openSubKey = Registry.ClassesRoot.OpenSubKey(registryKey) select new VisualStudioInfo(version) into info where info.Exists select info).OrderBy(info => info.Version);
		}
	}

	public class VisualStudioInfo
	{
		readonly Dictionary<string, string> mapping = new Dictionary<string, string>
			{
				{"8.0","Visual Studio 2005"},
				{"9.0","Visual Studio 2008"},
				{"10.0","Visual Studio 2010"},
				{"11.0","Visual Studio 2012"},
				{"12.0","Visual Studio 2013"},
				{"13.0","Visual Studio 2014"},
				{"14.0","Visual Studio 2015"},
				{"15.0","Visual Studio 2017"},
				{"16.0","Visual Studio 2019"},
				{"17.0","Visual Studio 2022"},
				{"18.0","Visual Studio 2025/2026"},
			};

		public Version Version { get; }
		public string Name { get; }
		public string Path { get; }
		public bool Exists { get; }
		public string AddinPath { get; }

		public string ExePath
		{
			get
			{
				if (Path.EndsWith(".exe"))
					return Path;
				return System.IO.Path.Combine(Path, "Common7", "IDE", "devenv.exe");
			}
		}

		public VisualStudioInfo(string version)
		{
			try
			{
				Version = new Version(version);
				Name = mapping[version];
				Path = GetVSInstallDirectory(version);
				Exists = Directory.Exists(Path);

				if (Exists)
				{
					string folderPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
					AddinPath = System.IO.Path.Combine(folderPath, Name, "Addins");
					bool addinPathValid = Directory.Exists(AddinPath);
					if (!addinPathValid) // C:\Program Files (x86)\Microsoft Visual Studio 10.0\Common7\IDE\Addins
						AddinPath = System.IO.Path.Combine(Path, "Common7", "IDE", "Addins");
					Exists = Directory.Exists(AddinPath) && File.Exists(ExePath);
				}
			}
			catch 
			{
				Exists = false;
			}
		}

		private string GetVSInstallDirectory(string version)
		{
			RegistryKey regKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\VisualStudio\" + version + @"\Setup\VS");
			//string[] valueNames = regKey.GetValueNames();
			string vsInstallationPath = regKey.GetValue("ProductDir").ToString();
			regKey.Close();
			return vsInstallationPath;
		}

	}
}