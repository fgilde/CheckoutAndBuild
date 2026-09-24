using System.Globalization;
using System.IO;

namespace CheckoutAndBuild.VisualStudio
{
	/// <summary>gilde.org connect widgets (contact/support): writes a themed page and opens it in the default browser.</summary>
	internal static class ConnectPages
	{
		public static void Open(string widget)
		{
			bool german = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "de";
			string language = german ? "de" : "en";
			string title = widget == "contact"
				? (german ? "Kontakt CheckoutAndBuild" : "Contact CheckoutAndBuild")
				: (german ? "CheckoutAndBuild unterstützen" : "Support CheckoutAndBuild");
			string extra = widget == "support"
				? " show-support-hint=\"false\" support-layout=\"rows\" show-support-icons=\"true\" show-support-qr=\"true\""
				: $" title=\"{title}\"";
			string html = $@"<!DOCTYPE html>
<html lang=""{language}"">
<head>
<meta charset=""utf-8"">
<meta name=""viewport"" content=""width=device-width, initial-scale=1"">
<title>{title}</title>
<script type=""module"" src=""https://connect.gilde.org/widgets/v1.js""></script>
<style>html,body{{margin:0;padding:24px;background:#0d1117;display:flex;justify-content:center}}</style>
</head>
<body>
<gilde-{widget} project=""fgilde/CheckoutAndBuild"" widget=""{widget}"" inline
  theme=""dark"" accent=""#2ea7ff"" language=""{language}""
  width=""560"" radius=""18"" padding=""28""
  show-logo=""true"" show-description=""false"" show-homepage=""true""
  show-preview-notice=""false"" show-footer=""false""
  footer-brand=""CheckoutAndBuild"" footer-tagline=""gilde.org""{extra}></gilde-{widget}>
</body>
</html>";
			string path = Path.Combine(Path.GetTempPath(), $"coab-connect-{widget}.html");
			File.WriteAllText(path, html);
			System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
		}
	}
}
