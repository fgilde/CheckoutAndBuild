using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using FG.CheckoutAndBuild2.Controls;
using Microsoft.TeamFoundation.Controls;

namespace FG.CheckoutAndBuild2.VisualStudio.Pages
{
    [TeamExplorerPage(GuidList.aboutPageId)]
    public class AboutPage : TeamExplorerBase
    {
        private readonly AboutControl aboutControl;

        public AboutPage()
        {
            string path = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "extension.vsixmanifest");
            if (File.Exists(path))
            {
                var xDocument = XDocument.Load(path);
                Name = xDocument.Descendants().First(x => x.Name.LocalName == "DisplayName").Value;
				Url = xDocument.Descendants().First(x => x.Name.LocalName == "MoreInfo").Value;
                Version = xDocument.Descendants().First(x => x.Name.LocalName == "Identity").Attribute("Version").Value;
            }
            Name = Name ?? GetAssemblyInfo<AssemblyProductAttribute>(a => a.Product);
            Version = $"Version: {Version ?? Assembly.GetExecutingAssembly().GetName().Version.ToString()}";
            Copyright =
                $"{GetAssemblyInfo<AssemblyCopyrightAttribute>(a => a.Copyright)} {GetAssemblyInfo<AssemblyCompanyAttribute>(a => a.Company)}";
            aboutControl = new AboutControl { DataContext = this };
        }

        public override string Title => $"About {Name}";

        public override object Content => aboutControl;

        public string Url { get; private set; }
        public string Name { get; private set; }
        public string Version { get; private set; }
        public string Copyright { get; private set; }

        private static string GetAssemblyInfo<TAttribute>(Func<TAttribute, string> getValueFunc)
        {
            object[] attributes = Assembly.GetExecutingAssembly().GetCustomAttributes(typeof(TAttribute), false);
            if (attributes.Length == 0)
                return string.Empty;
            return getValueFunc((TAttribute)attributes[0]);
        }
    }
}
