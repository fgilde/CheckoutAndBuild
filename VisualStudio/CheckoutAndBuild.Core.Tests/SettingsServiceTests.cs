using CheckoutAndBuild.Core.Settings;

namespace CheckoutAndBuild.Core.Tests;

public sealed class SettingsServiceTests : IDisposable
{
	private readonly string filePath = Path.Combine(Path.GetTempPath(), $"coab-settings-{Guid.NewGuid():N}.json");
	private readonly SettingsContext repoContext = new() { RepositoryPath = @"C:\repos\demo" };

	private JsonSettingsService CreateService() => new(filePath);

	public void Dispose()
	{
		if (File.Exists(filePath))
			File.Delete(filePath);
	}

	[Fact]
	public void SetGet_String_Roundtrips()
	{
		var service = CreateService();
		service.Set("msbuildPath", repoContext, @"C:\msbuild.exe");
		Assert.Equal(@"C:\msbuild.exe", service.Get<string>("msbuildPath", repoContext));
	}

	[Fact]
	public void SetGet_Bool_Roundtrips()
	{
		var service = CreateService();
		service.Set("cancelOnFailures", repoContext, true);
		Assert.True(service.Get<bool>("cancelOnFailures", repoContext));
	}

	[Fact]
	public void SetGet_Int_Roundtrips()
	{
		var service = CreateService();
		service.Set("maxNodeCount", repoContext, 8);
		Assert.Equal(8, service.Get<int>("maxNodeCount", repoContext));
	}

	public sealed class ComplexSetting
	{
		public string Name { get; set; } = "";
		public int Count { get; set; }
		public string[] Paths { get; set; } = [];
	}

	[Fact]
	public void SetGet_ComplexObject_Roundtrips()
	{
		var service = CreateService();
		var value = new ComplexSetting { Name = "clean", Count = 3, Paths = ["bin", "obj"] };
		service.Set("complex", repoContext, value);

		var loaded = service.Get<ComplexSetting>("complex", repoContext);
		Assert.NotNull(loaded);
		Assert.Equal("clean", loaded.Name);
		Assert.Equal(3, loaded.Count);
		Assert.Equal(new[] { "bin", "obj" }, loaded.Paths);
	}

	[Fact]
	public void Get_MissingKey_ReturnsDefault()
	{
		var service = CreateService();
		Assert.Equal(42, service.Get("missing", repoContext, 42));
	}

	[Fact]
	public void BranchScoping_SameKeyDifferentBranch_HasDifferentValues()
	{
		var service = CreateService();
		var main = new SettingsContext { RepositoryPath = @"C:\repos\demo", Branch = "main" };
		var feature = new SettingsContext { RepositoryPath = @"C:\repos\demo", Branch = "feature/x" };

		service.Set("configuration", main, "Release");
		service.Set("configuration", feature, "Debug");

		Assert.Equal("Release", service.Get<string>("configuration", main));
		Assert.Equal("Debug", service.Get<string>("configuration", feature));
	}

	[Fact]
	public void Fallback_BranchValueNotSet_ReturnsRepoValue()
	{
		var service = CreateService();
		service.Set("configuration", repoContext, "Release");

		var branchContext = new SettingsContext { RepositoryPath = repoContext.RepositoryPath, Branch = "feature/x" };
		Assert.Equal("Release", service.Get<string>("configuration", branchContext));
	}

	[Fact]
	public void Fallback_RepoValueNotSet_ReturnsGlobalValue()
	{
		var service = CreateService();
		service.Set("maxNodeCount", new SettingsContext(), 4);

		var branchContext = new SettingsContext { RepositoryPath = @"C:\repos\demo", Branch = "main" };
		Assert.Equal(4, service.Get<int>("maxNodeCount", branchContext));
	}

	[Fact]
	public void Profiles_AreIsolated()
	{
		var service = CreateService();
		var other = new SettingsContext { Profile = "Nightly", RepositoryPath = repoContext.RepositoryPath };

		service.Set("maxNodeCount", repoContext, 2);
		service.Set("maxNodeCount", other, 16);

		Assert.Equal(2, service.Get<int>("maxNodeCount", repoContext));
		Assert.Equal(16, service.Get<int>("maxNodeCount", other));
	}

	[Fact]
	public void RenameProfile_MovesAllKeysAndLeavesOtherProfilesAlone()
	{
		var service = CreateService();
		var nightlyRepo = new SettingsContext { Profile = "Nightly", RepositoryPath = repoContext.RepositoryPath };
		var nightlyGlobal = new SettingsContext { Profile = "Nightly" };
		service.Set("maxNodeCount", nightlyRepo, 16);
		service.Set("msbuildPath", nightlyGlobal, @"C:\msbuild.exe");
		service.Set("maxNodeCount", repoContext, 2);

		service.RenameProfile("Nightly", "Weekly");

		var weeklyRepo = new SettingsContext { Profile = "Weekly", RepositoryPath = repoContext.RepositoryPath };
		Assert.Equal(16, service.Get<int>("maxNodeCount", weeklyRepo));
		Assert.Equal(@"C:\msbuild.exe", service.Get<string>("msbuildPath", new SettingsContext { Profile = "Weekly" }));
		Assert.Equal(0, service.Get("maxNodeCount", nightlyRepo, 0));
		Assert.Equal(2, service.Get<int>("maxNodeCount", repoContext));
	}

	[Fact]
	public void Set_CreatesFile()
	{
		var service = CreateService();
		service.Set("anything", repoContext, "value");
		Assert.True(File.Exists(filePath));
	}

	[Fact]
	public void Persistence_NewServiceOnSameFile_ReadsValues()
	{
		var first = CreateService();
		first.Set("msbuildPath", repoContext, @"C:\msbuild.exe");
		first.Set("maxNodeCount", new SettingsContext(), 4);

		var second = CreateService();
		Assert.Equal(@"C:\msbuild.exe", second.Get<string>("msbuildPath", repoContext));
		Assert.Equal(4, second.Get<int>("maxNodeCount", new SettingsContext()));
	}
}
