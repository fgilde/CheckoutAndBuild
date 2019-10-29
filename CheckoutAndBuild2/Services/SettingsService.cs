using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Design;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml.Serialization;
using CheckoutAndBuild2.Contracts;
using CheckoutAndBuild2.Contracts.Settings;
using FG.CheckoutAndBuild2.Common;
using FG.CheckoutAndBuild2.Extensions;
using FG.CheckoutAndBuild2.Types;
using FG.CheckoutAndBuild2.ViewModels;
using FG.CheckoutAndBuild2.VisualStudio.Pages;
using Microsoft.TeamFoundation.VersionControl.Client;
using Microsoft.VisualStudio.Settings;
using Microsoft.VisualStudio.Shell.Settings;
using nExt.Core.Extensions;
using CoreExtensions = FG.CheckoutAndBuild2.Extensions.CoreExtensions;

namespace FG.CheckoutAndBuild2.Services
{
	public class SettingsService
	{	    
	    private const string keySeperator = "$";
		private const string collectionPath = "COAB2";
        private const string defaultWorkSpaceName = "DEFAULTWORKSPACE";
        private const string defaultServerName = "DEFAULTSERVER";
        private const string defaultTeamName = "DEFAULTTEAM";

        private readonly IServiceContainer serviceContainer;
		private readonly WritableSettingsStore settingsStore;
	    private SettingsManager vsSettingsManager;

        /// <summary>
        /// Initializes a new instance of the <see cref="T:System.Object"/> class.
        /// </summary>
        public SettingsService(IServiceContainer serviceContainer)
		{
			this.serviceContainer = serviceContainer;
            vsSettingsManager = new ShellSettingsManager(serviceContainer);
			settingsStore = vsSettingsManager.GetWritableSettingsStore(SettingsScope.UserSettings);

			bool collectionExists = settingsStore.CollectionExists(collectionPath);
			if (!collectionExists)
				settingsStore.CreateCollection(collectionPath);
		}

	    public Task<bool> CopySettingsAsync(WorkingProfile sourceProfile, Workspace sourceWorkspace,
	        WorkingProfile targetProfile, Workspace targetWorkspace)
	    {
	        return Task.Run(() => CopySettings(sourceProfile, sourceWorkspace, targetProfile, targetWorkspace));
	    }

	    public void Export()
	    {
	        var dlg = new SaveFileDialog
	        {
	            DefaultExt = "coab",
	            FileName = "Settings.coab"
	        };
	        if (dlg.ShowDialog() == DialogResult.OK)
	        {
	            Dictionary<string, object> dictionary = settingsStore.GetPropertyNames(collectionPath)
	                .ToDictionary(s => s, s => Get(s));
	            dictionary.BinarySerialize(dlg.FileName);
	        }
	    }

	    public bool Import()
	    {
	        var hasChanged = false;
            var dlg = new OpenFileDialog(){DefaultExt = "coab", FileName = "Settings.coab"};
	        if (dlg.ShowDialog() == DialogResult.OK)
	        {
	            Dictionary<string, object> dict;
	            SerializationHelper.TryBinaryDeserialize(dlg.FileName, out dict);
	            foreach (KeyValuePair<string, object> pair in dict)
	            {
                    var currentValue = Get(pair.Key);
	                if (currentValue != pair.Value)
	                {
	                    hasChanged = true;
                        Set(pair.Key, pair.Value);
	                }
	            }	            
	        }
	        return hasChanged;
	    }

        public bool CopySettings(WorkingProfile sourceProfile, Workspace sourceWorkspace, WorkingProfile targetProfile, Workspace targetWorkspace )
        {
            bool changed = false;
            string sourceWorkspaceName = sourceWorkspace != null ? sourceWorkspace.Name : defaultWorkSpaceName;
            string targetWorkspaceName = targetWorkspace != null ? targetWorkspace.Name : defaultWorkSpaceName;
            
            int xtraTarget = new Regex(Regex.Escape(keySeperator)).Matches(targetWorkspaceName).Count;

            var propertyNamesToCopy = GetPropertiesForWorkspace(sourceProfile,sourceWorkspace);
            foreach (var propertyName in propertyNamesToCopy)
            {
                string newPropertyName = propertyName.Replace(sourceProfile.Id.ToString(), targetProfile.IsDefault ? string.Empty:targetProfile.Id.ToString());
                if (!targetProfile.IsDefault && !newPropertyName.StartsWith(targetProfile.Id.ToString()))
                    newPropertyName = targetProfile.Id + newPropertyName;
                if (newPropertyName.Contains(keySeperator))
                {
                    var splits = newPropertyName.Split(new[]{ keySeperator }, StringSplitOptions.None); 
                    if (splits.Length > (3+ xtraTarget) && (splits[2+ xtraTarget] == sourceWorkspaceName || splits[2+ xtraTarget] == defaultWorkSpaceName))
                    {
                        splits[2+ xtraTarget] = targetWorkspaceName;
                        newPropertyName = splits.Aggregate("", (current, s) => current + keySeperator + s).Substring(1);
                    }                    
                }

                if (propertyName.Contains("Delphi"))
                {
                    
                }

                if (newPropertyName != propertyName)
                {                    
                    Set(newPropertyName, Get(propertyName));
                    changed = true;
                }
            }
            return changed;
        }

	    public bool HasSettings(WorkingProfile profile, Workspace workspace)
	    {
	        return GetPropertiesForWorkspace(profile,workspace).Any();
	    }

        public IEnumerable<string> GetPropertiesForWorkspace(WorkingProfile profile, Workspace workspace)
        {
            string sourceWorkspaceName = workspace != null ? workspace.Name : defaultWorkSpaceName;            
            int xtra = new Regex(Regex.Escape(keySeperator)).Matches(sourceWorkspaceName).Count;
            Func<string, bool> profileFilterFunc = s => profile == null || profile.IsDefault || s.StartsWith(profile.Id.ToString());
            Func<string, bool> workspaceFilterFunc = s =>
            {
                if (s.Contains(keySeperator))
                {
                    var splits = s.Split(new[] { keySeperator }, StringSplitOptions.None); 
                    return splits.Length > (3 + xtra) && (splits[2 + xtra] == sourceWorkspaceName || splits[2 + xtra] == defaultWorkSpaceName);
                }
                return false;
            };

            return settingsStore.GetPropertyNames(collectionPath).Where(profileFilterFunc).Where(workspaceFilterFunc);
        }

        public void ShowMainSettingsPage()
		{
			//((DTE)CheckoutAndBuild2Package.GetGlobalService<SDTE>()).ExecuteCommand("Tools.Options", GuidList.mainOptionsPage);
			CheckoutAndBuild2Package.GetGlobalService<CheckoutAndBuild2Package>().ShowOptionPage(typeof(CheckoutAndBuildOptionsPage));			
		}

		public void Reset()
		{
			if (settingsStore.CollectionExists(collectionPath))
				settingsStore.DeleteCollection(collectionPath);
			settingsStore.CreateCollection(collectionPath);
			serviceContainer.Get<MainViewModel>().Update();
		}

		public IServiceSettings GetMainServiceSettings()
		{
			return new ServiceSettingsSelectorViewModel(serviceContainer);
		}

		public object GenerateSettingsObjectForInspector(ISolutionProjectModel solutionProject = null, params object[] objectsToMerge)
		{
			var propertiesForInspector = new CustomClass("Properties", objectsToMerge);

			var settingsProviderClasses = CheckoutAndBuild2Package.GetExportedValues<ISettingsProviderClass>();
			foreach (ISettingsProviderClass settingsProviderClass in settingsProviderClasses)
			{
				var res = GetSettingsFromProvider(settingsProviderClass.GetType(), solutionProject);
				IEnumerable<PropertyInfo> settableProperties;
				if (solutionProject != null)
					settableProperties = res.GetSettableProperties(SettingsAvailability.ProjectSpecific, SettingsAvailability.GlobalWithProjectSpecificOverride);
				else
					settableProperties = res.GetSettableProperties(SettingsAvailability.Global, SettingsAvailability.GlobalWithProjectSpecificOverride);

				foreach (var propertyInfo in settableProperties)
				{
					SettingsKey settingsKey = SettingsExtensions.GetSettingsKey(propertyInfo, res, solutionProject);
					var value = Get(settingsKey, propertyInfo.PropertyType, propertyInfo.GetValue(res));
					propertiesForInspector.AddProperty(propertyInfo, value, o => Set(o.GetType(), settingsKey, o));
				}
			}

			return propertiesForInspector;
		}

		public object Get(SettingsKey settingsKey, Type targetType, object defaultValue = null)
		{            
            string key = PrepareKey(settingsKey);
            if(!string.IsNullOrEmpty(key))
                return Get(key, targetType, defaultValue, settingsKey.SerializationMode);
		    return defaultValue;
		}



	    private object Get(string key, Type targetType = null, object defaultValue = null, SerializationMode serializationMode = SerializationMode.Xml)
	    {
	        try
	        {
	            if (targetType == null)	                            
	                targetType = ConvertSettingsType(Check.TryCatch<SettingsType, Exception>(() => settingsStore.GetPropertyType(collectionPath, key)));
	            
	            object result = null;

	            if (targetType.IsEnum)
	                targetType = typeof (int);
	            if (targetType == typeof (bool))
	                result = settingsStore.GetBoolean(collectionPath, key, Convert.ToBoolean(defaultValue));
	            else if (targetType == typeof (string))
	                result = settingsStore.GetString(collectionPath, key, defaultValue as string);
	            else if (targetType == typeof (int))
	                result = settingsStore.GetInt32(collectionPath, key, Convert.ToInt32(defaultValue));
	            else
	            {
	                if (settingsStore.PropertyExists(collectionPath, key))
	                {
	                    if (serializationMode == SerializationMode.Xml)
	                    {
	                        string xmlContent = settingsStore.GetString(collectionPath, key);
	                        var serializer = new XmlSerializer(targetType);
	                        MemoryStream ms = new MemoryStream(Encoding.UTF8.GetBytes(xmlContent));
	                        var res = serializer.Deserialize(ms);
	                        return res;
	                    }
	                    else
	                    {
	                        var ms = settingsStore.GetMemoryStream(collectionPath, key);
	                        if (ms != null)
	                        {
	                            var serializer = new BinaryFormatter();
	                            result = serializer.Deserialize(ms);
	                        }
	                    }
	                }
	            }
	            return result ?? defaultValue;
	        }
	        catch (Exception)
	        {
	            return defaultValue;
	        }
	    }

	    private static Type ConvertSettingsType(SettingsType t, Type fallbackType = null)
	    {	        
	        if (t == SettingsType.Int32 || t == SettingsType.Int64)
	            return typeof (int);
	        if (t == SettingsType.String)
	            return typeof (string);
	        return fallbackType ?? typeof(object);	        
	    }


	    public T Get<T>(SettingsKey settingsKey, T defaultValue = default(T))
		{
			return (T)Get(settingsKey, typeof (T), defaultValue);			
		}

	    private void Set(string key, object value, Type targetType = null, SerializationMode serializationMode = SerializationMode.Xml)
	    {
	        if (targetType == null)
	        {
                if (value is int && (int) value == 0)
	            {
	                value = false;
	                targetType = typeof (bool);
	            }else if (value is int && (int) value == 1)
	            {
                    value = true;
                    targetType = typeof(bool);
                }
                else { 
                    targetType = ConvertSettingsType(Check.TryCatch<SettingsType, Exception>(() => settingsStore.GetPropertyType(collectionPath, key)),value?.GetType());
                }
	        }

	        if (targetType.IsEnum)
                targetType = typeof(int);

            if (targetType == typeof(bool))
                settingsStore.SetBoolean(collectionPath, key, Convert.ToBoolean(value));
            else if (targetType == typeof(string))
            {
                var s = value?.ToString() ?? string.Empty;
                settingsStore.SetString(collectionPath, key, s);
            }
            else if (targetType == typeof(int))
                settingsStore.SetInt32(collectionPath, key, Convert.ToInt32(value));
            else
            {
                using (var ms = new MemoryStream())
                {
                    if (serializationMode == SerializationMode.Binary)
                    {
                        var serializer = new BinaryFormatter();
                        serializer.Serialize(ms, value);
                        ms.Position = 0;
                        settingsStore.SetMemoryStream(collectionPath, key, ms);
                    }
                    else if (serializationMode == SerializationMode.Xml)
                    {
                        var serializer = new XmlSerializer(targetType);
                        serializer.Serialize(ms, value);
                        string xmlContent = Encoding.UTF8.GetString(ms.ToArray());
                        settingsStore.SetString(collectionPath, key, xmlContent);
                    }

                }
            }
        }

		public void Set(Type valueType, SettingsKey settingsKey, object value)
		{
		    Set(PrepareKey(settingsKey), value, valueType, settingsKey.SerializationMode);
		}

		public void Set<T>(SettingsKey settingsKey, T value)
		{
			Set(typeof(T), settingsKey, value);
		}

		private string PrepareKey(SettingsKey key)
		{
		    try
		    {
		        if (key.IsGlobal)
		            return key.Key;
		        string profileKey = "";
		        string workSpaceName = defaultWorkSpaceName;
		        string serverName = defaultServerName;
		        string teamName =  defaultTeamName;
		        TfsContext tfsContext = serviceContainer.Get<TfsContext>();
		        if (tfsContext != null && tfsContext.IsTfsConnected)
		        {
		            if(key.IsServerDepending)
		                serverName = tfsContext.VersionControlServer.ServerGuid.ToString();
		            if(key.IsTeamProjectDepending)
		                teamName = tfsContext.TeamProjectCollection.DisplayName;
		            if (tfsContext.SelectedWorkspace != null && key.IsWorkspaceDepending)
		                workSpaceName = tfsContext.SelectedWorkspace.Name;
                    if(!string.IsNullOrEmpty(tfsContext.SelectedGitBranch) && key.IsGitBranchDepending)
                        workSpaceName += $" ({tfsContext.SelectedGitBranch})";

                    if (key.IsProfileDepending && tfsContext.SelectedProfile != null && !tfsContext.SelectedProfile.IsDefault)
		                profileKey = tfsContext.SelectedProfile.Id.ToString();
		        }
		        return $"{profileKey}{serverName}{keySeperator}{teamName}{keySeperator}{workSpaceName}{keySeperator}{key.Key}";
		    }
		    catch (Exception e)
		    {
		        return string.Empty;
		    }
		}

		public ISettingsProviderClass GetSettingsFromProvider(Type settingsProviderType, ISolutionProjectModel solutionProject = null)			
		{
			var res = ReflectionHelper.CreateInstance(settingsProviderType) as ISettingsProviderClass;
			foreach (PropertyInfo propertyInfo in res.GetSettableProperties())
			{
				var settingsPropertyAttribute = CoreExtensions.GetAttributes<SettingsPropertyAttribute>(propertyInfo, false).First();
				SettingsKey settingsKey = SettingsExtensions.GetSettingsKey(propertyInfo, res);

				object defaultValue = null;
				DefaultValueAttribute defaultValueAttribute = CoreExtensions.GetAttributes<DefaultValueAttribute>(propertyInfo, false).FirstOrDefault();
				if (defaultValueAttribute != null)
					defaultValue = defaultValueAttribute.Value;
				
				var loadedValue = Get(settingsKey, propertyInfo.PropertyType, defaultValue ?? propertyInfo.GetValue(res));
				if (solutionProject != null && settingsPropertyAttribute.Availability == SettingsAvailability.ProjectSpecific || settingsPropertyAttribute.Availability == SettingsAvailability.GlobalWithProjectSpecificOverride)
				{
					settingsKey = SettingsExtensions.GetSettingsKey(propertyInfo, res, solutionProject);
					loadedValue = Get(settingsKey, propertyInfo.PropertyType, loadedValue);
				}
				
				propertyInfo.SetValue(res, loadedValue);
			}
			return res;
		}
	

		public T GetSettingsFromProvider<T>(ISolutionProjectModel solutionProject = null) 
			where T : ISettingsProviderClass, new()
		{
			return (T)GetSettingsFromProvider(typeof (T), solutionProject);
		}

		private static string pluginPath;
		public static string GetPluginDirectory()
		{
			return FileHelper.EnsureDirectory(pluginPath ?? (pluginPath = Path.Combine(Path.GetDirectoryName(Assembly.GetCallingAssembly().Location), "Plugins")));
		}
	}
}