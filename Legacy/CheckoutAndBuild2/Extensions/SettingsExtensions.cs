using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using CheckoutAndBuild2.Contracts;
using CheckoutAndBuild2.Contracts.Service;
using CheckoutAndBuild2.Contracts.Settings;
using FG.CheckoutAndBuild2.Services;

namespace FG.CheckoutAndBuild2.Extensions
{
	public static class SettingsExtensions
	{
		private static readonly Type[] allowedTypes = { typeof(string[]), typeof(bool), typeof(string), typeof(int) };

		public static bool IsIncluded(this IOperationService service, ISolutionProjectModel solutionProjectModel = null)
		{
			var settingsService = CheckoutAndBuild2Package.GetGlobalService<SettingsService>();
			SettingsKey settingsKey = service.ServiceSettingsKey();
			bool defaultValue = settingsService.Get(settingsKey, service.Order <= ServicePriorities.BuildServicePriority);
			if (solutionProjectModel != null)
				settingsKey = service.ServiceSettingsKey(solutionProjectModel);
			bool isIncluded = settingsService.Get(settingsKey, defaultValue);
			return isIncluded;
		}

		public static CheckBox GetServiceSelector(this IOperationService service, ISolutionProjectModel solutionProjectModel = null)
		{
			SettingsKey settingsKey = service.ServiceSettingsKey();
			if (solutionProjectModel != null)
				settingsKey = service.ServiceSettingsKey(solutionProjectModel);
			var settingsService = CheckoutAndBuild2Package.GetGlobalService<SettingsService>();
			var isChecked = settingsService.Get(settingsKey, service.IsIncluded(solutionProjectModel));
			var res = new CheckBox { Content = service.OperationName, IsChecked = isChecked, Tag = service };
			res.Checked += (sender, args) => settingsService.Set(settingsKey, res.IsChecked ?? false);
			res.Unchecked += (sender, args) => settingsService.Set(settingsKey, res.IsChecked ?? false);
			return res;
		}

		private static bool IsForService(this PropertyInfo pi, IOperationService service)
		{
			if (service != null)
			{
				var attr = pi.GetAttributes<SettingsPropertyAttribute>(false).FirstOrDefault();
				if (!string.IsNullOrEmpty(attr?.ServiceId))
					return attr.ServiceId.ToLower() == service.ServiceId.ToString().ToLower();
			}
			return true;
		}

		public static IEnumerable<UIElement> GetUIElements(this ISettingsProviderClass settingsProvider,IOperationService specificService, params SettingsAvailability[] availabilities)
		{
			return settingsProvider.GetSettableProperties(availabilities).Where(info => info.IsForService(specificService)).
				Select(propertyInfo => GetUIElement(propertyInfo, settingsProvider));
		}

		public static IEnumerable<PropertyInfo> GetSettableProperties(this ISettingsProviderClass settingsProvider, params SettingsAvailability[] availabilities)
		{
			return settingsProvider.GetType().GetProperties()
					.Where(info => info.GetAttributes<SettingsPropertyAttribute>(false).Any()
					&& (availabilities == null || availabilities.Length == 0 || availabilities.Contains(info.GetAttributes<SettingsPropertyAttribute>(false).First().Availability))
					&& info.CanWrite && (info.PropertyType.IsEnum || allowedTypes.Contains(info.PropertyType)));
		} 

		private static UIElement GetUIElement(PropertyInfo propertyInfo, ISettingsProviderClass settingsProvider)
		{	
			var settingsService = CheckoutAndBuild2Package.GetGlobalService<SettingsService>();
			var attribute = propertyInfo.GetAttributes<SettingsPropertyAttribute>(false).First();
			SettingsKey settingsKey = GetSettingsKey(propertyInfo, settingsProvider);
			object defaultValue = null;
			DefaultValueAttribute defaultValueAttribute = propertyInfo.GetAttributes<DefaultValueAttribute>(false).FirstOrDefault();
			if (defaultValueAttribute != null)
				defaultValue = defaultValueAttribute.Value;
			return EditorTemplates.GetUIElement(propertyInfo, settingsService, settingsKey, defaultValue, attribute);
		}


		internal static SettingsKey GetSettingsKey(PropertyInfo propertyInfo, ISettingsProviderClass settingsProvider, ISolutionProjectModel solutionProject = null)
		{
			if (solutionProject == null)
				return new SettingsKey(string.Format("{0}_{1}", settingsProvider.GetType().FullName, propertyInfo.Name));
			string key = "_" + solutionProject.SolutionFileName + solutionProject.ItemPath.GetHashCode();
			return new SettingsKey(string.Format("{0}_{1}{2}", settingsProvider.GetType().FullName, propertyInfo.Name, key));
		}
	}
}