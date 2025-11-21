using System;
using System.Reflection;
using System.Resources;
using System.Runtime.InteropServices;

// General Information about an assembly is controlled through the following 
// set of attributes. Change these attribute values to modify the information
// associated with an assembly.
using FG.CheckoutAndBuild2.Properties;

[assembly: AssemblyTitle("CheckoutAndBuild2")]
[assembly: AssemblyDescription("")]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCompany("Gilde Enterprises")]
[assembly: AssemblyProduct(Const.ApplicationName)]
[assembly: AssemblyCopyright("© 2019-2026")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]   
[assembly: ComVisible(false)]     
[assembly: CLSCompliant(false)]
[assembly: NeutralResourcesLanguage("en-US")]

// Version information for an assembly consists of the following four values:
//
//      Major Version
//      Minor Version 
//      Build Number
//      Revision
//
// You can specify all the values or you can default the Revision and Build Numbers 
// by using the '*' as shown below:

[assembly: AssemblyVersion(Const.Version)]
[assembly: AssemblyFileVersion(Const.Version)]


namespace FG.CheckoutAndBuild2.Properties
{
	public class Const
	{
		public const string ApplicationName = "CheckoutAndBuild 2019-2026";
		public const string Version = "2.3.1.1";

	    internal static string[] DefaultSupportedProjectExtensions => new[] { ".sln", ".dproj"};
	}
}