using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Drawing.Design;
using System.Linq;
using System.Windows.Forms.Design;
using CheckoutAndBuild2.Contracts;
using FG.CheckoutAndBuild2.Common;
using FG.CheckoutAndBuild2.Extensions;
using FG.CheckoutAndBuild2.Services;

namespace FG.CheckoutAndBuild2.ViewModels
{
	
	public class ProjectViewModelProperties
	{
		private readonly ProjectViewModel project;
		private readonly SettingsService settingsService;
		private readonly IDefaultTestSettingsProvider testSettingsProvider;

		/// <summary>
		/// Initializes a new instance of the <see cref="T:System.Object"/> class.
		/// </summary>
		public ProjectViewModelProperties(ProjectViewModel project)
		{
			this.project = project;
			settingsService = CheckoutAndBuild2Package.GetGlobalService<SettingsService>();
			testSettingsProvider = CheckoutAndBuild2Package.GetGlobalService<IDefaultTestSettingsProvider>();
			BuildTargets = new NamedObservableCollection<string>(project.BuildTargets, "Build Targets");
			BuildTargets.CollectionChanged += BuildTargetsOnCollectionChanged;
			BuildProperties = new NamedObservableCollection<BuildProperty>(project.BuildProperties.Select(pair => new BuildProperty(pair.Key, pair.Value)), "Build Properties");
			BuildProperties.CollectionChanged += BuildPropertiesOnCollectionChanged;
		}

		private void BuildPropertiesOnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
		{
			List<BindablePair<string, string>> bindablePairs = new List<BindablePair<string, string>>(BuildProperties.Select(property => new BindablePair<string, string>(property.Key, property.Value)));
			settingsService.Set(project.BuildPropertiesKey(), bindablePairs);
		}

		private void BuildTargetsOnCollectionChanged(object sender, NotifyCollectionChangedEventArgs notifyCollectionChangedEventArgs)
		{
			settingsService.Set(project.BuildTargetsKey(), BuildTargets.ToList());
		}
        
		[Browsable(true), DisplayName(@"Name")]
		[Category("Basic Informations")]
		[Description("Filename")]
		public string Name => project.SolutionFileName;

	    [Browsable(true), DisplayName(@"Project FileName")]
		[Category("Basic Informations")]
		[Description("Filepath")]
		public string FileName => project.ItemPath;

	    [Browsable(true), DisplayName(@"Build Priority")]
		[Category("Build Settings")]
		[Description("Build Priority: In this order your solutions will be build. All solutions with the same build priority will be build parallel")]
		public int BuildPriority
		{
			get { return project.BuildPriority; }
			set { project.BuildPriority = value; }
		}

		[Browsable(true), DisplayName(@"Build Targets")]
		[Category("Build Settings")]
		[Description("Used Build Targets for this Solution ")]
		public NamedObservableCollection<string> BuildTargets { get; set; }

		[Browsable(true), DisplayName(@"Build Properties")]
		[Category("Build Settings")]
		[Description("Used Build Properties for this Solution")]
		public NamedObservableCollection<BuildProperty> BuildProperties { get; set; }

		[Browsable(true), DisplayName(@"Test Settings File")]
		[Category("UnitTest")]
		[Editor(typeof(FileNameEditor), typeof(UITypeEditor))]
		[Description("Testsettings file will be used for the Unit Test Service ")]
		public string TestSettingsFile
		{
			get { return project.EnsureAbsolutePath(settingsService.Get(project.TestSettingsFileKey(), testSettingsProvider != null ? testSettingsProvider.GetTestSettingsFile(project, settingsService.GetMainServiceSettings()) : String.Empty)); }
			set { settingsService.Set(project.TestSettingsFileKey(), value); }
		}

		public override string ToString()
		{
			return $"{project.SolutionFileName}";
		}
	}

	public class NamedObservableCollection<T> : ObservableCollection<T>
	{
		public string Name { get; set; }

		/// <summary>
		/// Initializes a new instance of the <see cref="T:System.Collections.ObjectModel.ObservableCollection`1"/> class that contains elements copied from the specified collection.
		/// </summary>
		public NamedObservableCollection(IEnumerable<T> collection, string name = null) : base(collection)
		{
			Name = name;
		}

		public override string ToString()
		{
			return $"{Name} ({Count})";
		}
	}

	public class BuildProperty
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="T:System.Object"/> class.
		/// </summary>
		public BuildProperty(string key, string value)
		{
			Key = key;
			Value = value;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="T:System.Object"/> class.
		/// </summary>
		public BuildProperty()
		{}

		[Browsable(true), DisplayName(@"Key")]		
		[Description("Keyname for your build property")]
		public string Key { get; set; }

		[Browsable(true), DisplayName(@"Value")]
		[Description("Value for your build property")]
		public string Value { get; set; }

		/// <summary>
		/// Returns a string that represents the current object.
		/// </summary>
		/// <returns>
		/// A string that represents the current object.
		/// </returns>
		public override string ToString()
		{
			return $"{Key}={Value}";
		}
	}

}