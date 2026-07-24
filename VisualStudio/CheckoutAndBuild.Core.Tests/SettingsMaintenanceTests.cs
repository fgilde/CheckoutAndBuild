using System;
using System.IO;
using CheckoutAndBuild.Core.Settings;
using Xunit;

namespace CheckoutAndBuild.Core.Tests
{
	public class SettingsMaintenanceTests : IDisposable
	{
		private readonly string tempDir = Directory.CreateDirectory(
			Path.Combine(Path.GetTempPath(), "COAB.Tests", Guid.NewGuid().ToString("N"))).FullName;

		public void Dispose()
		{
			try { Directory.Delete(tempDir, true); } catch { }
		}

		private JsonSettingsService CreateService(string name = "settings.json")
			=> new JsonSettingsService(Path.Combine(tempDir, name));

		[Fact]
		public void ExportImport_Roundtrip_MergesValues()
		{
			var source = CreateService("a.json");
			source.Set("Key1", new SettingsContext(), "value1");
			string export = Path.Combine(tempDir, "export.coab");
			source.ExportTo(export);

			var target = CreateService("b.json");
			target.Set("Key2", new SettingsContext(), "value2");
			int imported = target.ImportFrom(export);

			Assert.True(imported > 0);
			Assert.Equal("value1", target.Get("Key1", new SettingsContext(), ""));
			Assert.Equal("value2", target.Get("Key2", new SettingsContext(), "")); // merge keeps existing keys
		}

		[Fact]
		public void CopyProfile_CopiesAllKeys_AndKeepsSource()
		{
			var service = CreateService();
			var sourceContext = new SettingsContext { Profile = "Source" };
			service.Set("A", sourceContext, 1);
			service.Set("B", sourceContext, "x");

			int copied = service.CopyProfile("Source", "Target");

			Assert.Equal(2, copied);
			Assert.Equal(1, service.Get("A", new SettingsContext { Profile = "Target" }, 0));
			Assert.Equal(1, service.Get("A", sourceContext, 0));
			Assert.Equal(0, service.CopyProfile("Source", "Source")); // same profile is a no-op
		}

		[Fact]
		public void ResetAll_WipesStore()
		{
			var service = CreateService();
			service.Set("Key", new SettingsContext(), "value");

			service.ResetAll();

			Assert.Equal("missing", service.Get("Key", new SettingsContext(), "missing"));
		}
	}
}
