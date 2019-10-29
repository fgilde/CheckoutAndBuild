using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Drawing.Design;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Forms.Design;
using System.Windows.Media;
using CheckoutAndBuild2.Contracts.Settings;
using FG.CheckoutAndBuild2.Common;
using FG.CheckoutAndBuild2.Controls;
using FG.CheckoutAndBuild2.Services;
using Microsoft.VisualStudio.PlatformUI;
using nExt.Core.Extensions;

namespace FG.CheckoutAndBuild2
{
	//TODO: Eigentlich schrott, es müsste ein echten DatatemplateSelector und einen wpf viewmodel mit binding etc geben
	internal static class EditorTemplates
	{
	    internal static T EditValue<T>(this UITypeEditor editor, T value) where T : class
	    {
            var runtimeServiceProvider = new RuntimeUIServiceProvider();
            object obj = editor.EditValue(runtimeServiceProvider, runtimeServiceProvider, value);
	        if (typeof(T) == typeof(string))
	            return obj?.ToString() as T;
	        return (T)obj;
	    }

		internal static UIElement GetUIElement(PropertyInfo propertyInfo, SettingsService settingsService,
			SettingsKey settingsKey, object defaultValue, SettingsPropertyAttribute attribute)
		{
		    Brush fgBrush = Application.Current.Resources[EnvironmentColors.BrandedUITextBrushKey] as Brush;
            var labelName = attribute.Name ?? propertyInfo.Name;
			if (propertyInfo.PropertyType == typeof(bool))
			{
				var isChecked = settingsService.Get(settingsKey, (bool?) defaultValue ?? false);				
				var res = new CheckBox { Content = labelName, IsChecked = isChecked };
				res.Checked += (sender, args) => settingsService.Set(settingsKey, res.IsChecked ?? false);
				res.Unchecked += (sender, args) => settingsService.Set(settingsKey, res.IsChecked ?? false);
				res.ToolTip = attribute.Description;
                return res;
			}
			if (propertyInfo.PropertyType == typeof(string) || propertyInfo.PropertyType == typeof(int))
			{
			    var editorAttribute = propertyInfo.GetCustomAttribute<EditorAttribute>();
			    var grid = new Grid();
                grid.ColumnDefinitions.Add(new ColumnDefinition {Width = GridLength.Auto});
                grid.ColumnDefinitions.Add(new ColumnDefinition {Width = new GridLength(1, GridUnitType.Star)});
                if(editorAttribute != null)
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var textBox = CreateTextBox(propertyInfo, settingsService, settingsKey, defaultValue);
                Grid.SetColumn(textBox, 1);                
			    grid.ToolTip = attribute.Description;			    
                
			    grid.Children.Add(new Label {Foreground = fgBrush, Content = labelName + ":", ToolTip = labelName});
				grid.Children.Add(textBox);
			    if (editorAttribute != null)
			    {
			        var uiTypeEditor = Activator.CreateInstance(Type.GetType(editorAttribute.EditorTypeName)) as UITypeEditor;                    
			        var panel = new StackPanel();
                    Grid.SetColumn(panel, 2);
                    grid.Children.Add(panel);
                    if (editorAttribute.EditorTypeName.Contains(typeof(FileNameEditor).FullName))
                    {                                                
                        var btnSelect = new Button {Content = "...", Width = 22, Height = 21, Margin = new Thickness(-2,2,0,0), MinHeight = 1, MinWidth = 1};
                        btnSelect.Click += (sender, args) =>
                        {
                            var res = uiTypeEditor.EditValue(textBox.Text);
                            if (res != null)
                                textBox.Text = res;
                        };
                        panel.Children.Add(btnSelect);
                    }
			    }                
                return grid;
			}
			if (propertyInfo.PropertyType.IsEnum)
			{
				var value = settingsService.Get(settingsKey, propertyInfo.PropertyType, defaultValue);
				var grid = new UniformGrid { Columns = 2 };
				var combo = new ComboBox()
				{
                    Width = 180,
					Style = Application.Current.TryFindResource("COABComboBoxStyle") as Style,
					Margin = new Thickness(0, 2, 2, 2),
					SelectedValue = Enum.Parse(propertyInfo.PropertyType, value.ToString()),//value,					
					ItemsSource = Enum.GetValues(propertyInfo.PropertyType)
				};
				combo.SelectionChanged += (sender, args) => settingsService.Set(propertyInfo.PropertyType, settingsKey, combo.SelectedValue);
				grid.ToolTip = attribute.Description;
				grid.Children.Add(new Label { Foreground = fgBrush, Content = labelName + ":" });
				grid.Children.Add(combo);
                return grid;
			}

		    if (propertyInfo.PropertyType.IsArray)
		    {
		        var grp = new GroupBox {Header = labelName, Margin = new Thickness(0,5,0,5)};		        
                var value = settingsService.Get(settingsKey, new string[0]);
                var itemSource = new ObservableCollection<Bindable<string>>(value.Select(s => new Bindable<string>(s)));
		        var edit = new StringListEdit {Height = 150, DataContext = itemSource, BrowseMode = BrowseMode.Directories };
                var dock = new DockPanel();
		        dock.Children.Add(edit);
                Label labelDes = new Label {Content = attribute.Description};
                DockPanel.SetDock(labelDes, Dock.Bottom);
                dock.Children.Add(labelDes);
                grp.Content = dock;
		        PropertyChangedEventHandler saveAction = (sender, args) => settingsService.Set(settingsKey, itemSource.Select(b => b.Value).ToArray());
		        itemSource.Apply(bindable => bindable.PropertyChanged += saveAction);
		        itemSource.CollectionChanged += (sender, args) =>
		        {
		            settingsService.Set(settingsKey, itemSource.Select(bindable => bindable.Value).ToArray());
		            args.NewItems?.OfType<Bindable<string>>().Apply(bindable => bindable.PropertyChanged += saveAction);
		            args.OldItems?.OfType<Bindable<string>>().Apply(bindable => bindable.PropertyChanged -= saveAction);
		        };
		        edit.LostFocus += (sender, args) => saveAction(sender, new PropertyChangedEventArgs(""));
                return grp;
		    }

		    throw new NotSupportedException(propertyInfo.PropertyType + " is not supported");
		}

	    private static TextBox CreateTextBox(PropertyInfo propertyInfo, SettingsService settingsService, SettingsKey settingsKey,
	        object defaultValue)
	    {
	        var textBox = new TextBox
	        {
	            Style = Application.Current.TryFindResource("COABTextBoxStyle") as Style,
	            Margin = new Thickness(0, 2, 2, 2),
                MinWidth = 250d,
                BorderThickness = new Thickness(1),
                BorderBrush = Brushes.Gray,
	            Text = propertyInfo.PropertyType == typeof(string)
	                    ? settingsService.Get(settingsKey, defaultValue != null ? (string) defaultValue : string.Empty)
	                    : settingsService.Get(settingsKey, defaultValue?.MapTo<int>() ?? 0).ToString()
	        };
	        if (propertyInfo.PropertyType == typeof(int))
	        {
	            textBox.PreviewTextInput += (sender, e) =>
	            {
	                Regex regex = new Regex("[^0-9]+");
	                e.Handled = regex.IsMatch(e.Text);
	            };
	        }
	        textBox.TextChanged += (sender, args) => settingsService.Set(propertyInfo.PropertyType, settingsKey, textBox.Text);
	        return textBox;
	    }
	}
}