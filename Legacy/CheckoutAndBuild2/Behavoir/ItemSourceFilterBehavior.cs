//============================================================================================
// ItemSourceFilterBehavior
//--------------------------------------------------------------------------------------------
// File	ItemSourceFilterBehavior.cs
//
// (C) Copyright Florian Gilde 
// http://www.nksoft.de
//
// Alle Rechte vorbehalten. All rights reserved.
//============================================================================================

using Microsoft.Xaml.Behaviors;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace FG.CheckoutAndBuild2.Behavoir
{
	/// <summary>
	/// ItemSourceFilterBehavior ist ein behavoir um ein ItemsControl zu Filtern, 
	/// und funtkiniert automatisch bei ItemsControl und ItemsPresentern für templates
	/// </summary>
	public class ItemSourceFilterBehavior : Behavior<FrameworkElement> 
		// von Framework element, da es auch in Templates funktionieren soll
	{

		private Predicate<object> oldFilter;
		private ComboBox comboBox;

		private readonly static KeyGesture searchFocusGesture = new KeyGesture(Key.E, ModifierKeys.Control);

		#region Static / Dependency

		/// <summary>
		/// returns the WaterMarkTextProperty as string.
		/// </summary>
		public static string GetWaterMarkText(DependencyObject obj)
		{
			return (string)obj.GetValue(WaterMarkTextProperty);
		}

		/// <summary>
		/// returns the FocusSearchboxKeyGestureProperty as KeyGesture.
		/// </summary>
		public static KeyGesture GetFocusSearchboxKeyGesture(DependencyObject obj)
		{
			return (KeyGesture)obj.GetValue(FocusSearchboxKeyGestureProperty);
		}

		/// <summary>
		/// Sets the FocusSearchboxKeyGestureProperty.
		/// </summary>
		public static void SetFocusSearchboxKeyGesture(DependencyObject obj, KeyGesture value)
		{
			obj.SetValue(FocusSearchboxKeyGestureProperty, value);
		}

		/// <summary>
		/// Shortcut, um direkt in die Suchbox zu springen
		/// </summary>
		public static readonly DependencyProperty FocusSearchboxKeyGestureProperty =
			DependencyProperty.RegisterAttached("FocusSearchboxKeyGesture", typeof(KeyGesture), typeof(ItemSourceFilterBehavior), new UIPropertyMetadata(searchFocusGesture, KeyGestureChanged));

		private static void KeyGestureChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
		{
			SetWaterMarkText(dependencyObject, GetWatermarkTextSuggestion(dependencyObject));
		}

		private static string GetWatermarkTextSuggestion()
		{
			return GetWatermarkTextForGesture(searchFocusGesture);
		}

		private static string GetWatermarkTextSuggestion(DependencyObject dependencyObject)
		{
			var gesture = GetFocusSearchboxKeyGesture(dependencyObject);
			return GetWatermarkTextForGesture(gesture);
		}

		private static string GetWatermarkTextForGesture(KeyGesture gesture)
		{
			if (gesture == null)
				return "Search Content";
			var shortCut = new KeyGestureConverter().ConvertToString(gesture);
			return String.Format("{0} ({1})", "Search Content", shortCut);
		}

		/// <summary>
		/// Sets the WaterMarkTextProperty.
		/// </summary>
		public static void SetWaterMarkText(DependencyObject obj, string value)
		{
			obj.SetValue(WaterMarkTextProperty, value);
		}


		/// <summary>
		/// WaterMarkTextProperty
		/// </summary>
		public static readonly DependencyProperty WaterMarkTextProperty =
			DependencyProperty.RegisterAttached("WaterMarkText", typeof(string), typeof(ItemSourceFilterBehavior), new UIPropertyMetadata(GetWatermarkTextSuggestion()));


		/// <summary>
		/// returns the IsEnabledProperty as bool.
		/// </summary>
		public static bool GetIsFilterEnabled(DependencyObject obj)
		{
			return (bool)obj.GetValue(IsFilterEnabledProperty);
		}

		/// <summary>
		/// Sets the IsEnabledProperty.
		/// </summary>
		public static void SetIsFilterEnabled(DependencyObject obj, bool value)
		{
			obj.SetValue(IsFilterEnabledProperty, value);
		}

		/// <summary>
		/// <see cref="IsCaseSensitive"/>
		/// </summary>
		public static readonly DependencyProperty IsCaseSensitiveProperty =
			DependencyProperty.Register("IsCaseSensitive", typeof(bool), typeof(ItemSourceFilterBehavior), new UIPropertyMetadata(false));

		/// <summary>
		/// IsEnabledProperty
		/// </summary>
		public static readonly DependencyProperty IsFilterEnabledProperty =
			DependencyProperty.RegisterAttached("IsFilterEnabled", typeof(bool), typeof(ItemSourceFilterBehavior), new UIPropertyMetadata(true));

		/// <summary>
		/// <see cref="ItemsControl"/>
		/// </summary>
		public static readonly DependencyProperty ItemsControlProperty =
			DependencyProperty.Register("ItemsControl", typeof(ItemsControl), typeof(ItemSourceFilterBehavior), new UIPropertyMetadata(null, ItemsControlChanged));


		/// <summary>
		/// <see cref="TextBox"/>
		/// </summary>
		public static readonly DependencyProperty TextBoxProperty =
			DependencyProperty.Register("TextBox", typeof(TextBox), typeof(ItemSourceFilterBehavior), new UIPropertyMetadata(null, TextBoxControlChanged));

		/// <summary>
		/// returns the PropertyNameToFilterProperty as string.
		/// </summary>
		public static string GetPropertyNameToFilter(DependencyObject obj)
		{
			return (string)obj.GetValue(PropertyNameToFilterProperty);
		}

		/// <summary>
		/// Sets the PropertyNameToFilterProperty.
		/// </summary>
		public static void SetPropertyNameToFilter(DependencyObject obj, string value)
		{
			obj.SetValue(PropertyNameToFilterProperty, value);
		}

		/// <summary>
		/// PropertyNameToFilterProperty
		/// </summary>
		public static readonly DependencyProperty PropertyNameToFilterProperty =
			DependencyProperty.RegisterAttached("PropertyNameToFilter", typeof(string), typeof(ItemSourceFilterBehavior), new UIPropertyMetadata(string.Empty));


		private static void ItemsControlChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
		{
			var oldComboBox = e.OldValue as ComboBox;
			if (oldComboBox != null)
				oldComboBox.DropDownClosed -= ((ItemSourceFilterBehavior)dependencyObject).ComboBoxOnDropDownClosed;
			var newComboBox = e.NewValue as ComboBox;
			if (newComboBox != null)
				newComboBox.DropDownClosed += ((ItemSourceFilterBehavior)dependencyObject).ComboBoxOnDropDownClosed;
		}

		private static void TextBoxControlChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
		{
			if (e.OldValue != null)
			{
				((TextBox)e.OldValue).TextChanged -= ((ItemSourceFilterBehavior)dependencyObject).TextBoxOnTextChanged;
				((TextBox)e.OldValue).PreviewKeyDown -= ((ItemSourceFilterBehavior)dependencyObject).TextBoxOnKeyDown;
			}
			if (e.NewValue != null)
			{
				((TextBox)e.NewValue).PreviewKeyDown += ((ItemSourceFilterBehavior)dependencyObject).TextBoxOnKeyDown;
				((TextBox)e.NewValue).TextChanged += ((ItemSourceFilterBehavior)dependencyObject).TextBoxOnTextChanged;
			}
		}
		#endregion

		/// <summary>
		/// Text der als Platzhalter benutzt wird
		/// </summary>
		public string WaterMarkText
		{
			get { return GetWaterMarkText(ItemsControl); }
			set { SetWaterMarkText(ItemsControl, value); }
		}

		/// <summary>
		/// Shortcut um den Focus zu setzen
		/// </summary>
		public KeyGesture FocusSearchboxKeyGesture
		{
			get { return GetFocusSearchboxKeyGesture(ItemsControl); }
			set { SetFocusSearchboxKeyGesture(ItemsControl, value); }
		}

		/// <summary>
		/// Gibt an, ob der filter groß und klein schreibung beachtet
		/// </summary>
		public bool IsCaseSensitive
		{
			get { return (bool)GetValue(IsCaseSensitiveProperty); }
			set { SetValue(IsCaseSensitiveProperty, value); }
		}

		/// <summary>
		/// Gibt an ob der filter aktiv ist
		/// </summary>
		public bool IsFilterEnabled
		{
			get { return GetIsFilterEnabled(ItemsControl); }
			set { SetIsFilterEnabled(ItemsControl, value); }
		}


		/// <summary>
		/// Eigenschaft, auf die der Filter greift
		/// </summary>
		public string PropertyNameToFilter
		{
			get { return GetPropertyNameToFilter(ItemsControl); }
			set { SetPropertyNameToFilter(ItemsControl, value); }
		}


		/// <summary>
		/// Das ItemsControl, welches fürs Filtern benutzt werden soll
		/// </summary>
		public ItemsControl ItemsControl
		{
			get { return (ItemsControl)GetValue(ItemsControlProperty); }
			set { SetValue(ItemsControlProperty, value); }
		}

		/// <summary>
		/// Die Textbox, die zum Filtern benutzt werden soll
		/// </summary>
		public TextBox TextBox
		{
			get { return (TextBox)GetValue(TextBoxProperty); }
			set { SetValue(TextBoxProperty, value); }
		}

		/// <summary>
		/// Called when [attached].
		/// </summary>
		protected override void OnAttached()
		{
			base.OnAttached();
			if (ItemsControl == null)
			{
				if (AssociatedObject is ItemsControl)
					ItemsControl = (ItemsControl)AssociatedObject;
				else if (AssociatedObject is ItemsPresenter)
					ItemsControl = AssociatedObject.TemplatedParent as ItemsControl;
				if (ItemsControl != null && ItemsControl.Items != null)
				{
					oldFilter = ItemsControl.Items.Filter;
				}
				if (TextBox != null)
				{
					comboBox = TextBox.TemplatedParent as ComboBox;
					if (comboBox != null)
					{
						comboBox.DropDownOpened += ComboBoxOnDropDownOpened;
						if (FocusSearchboxKeyGesture != null)
						{
							WaterMarkText = GetWatermarkTextSuggestion(ItemsControl);
							comboBox.KeyDown += CheckFocusShortcut;
						}
					}
				}
			}
		}

		private void CheckFocusShortcut(object sender, KeyEventArgs e)
		{
			if (e.Key == FocusSearchboxKeyGesture.Key && Keyboard.Modifiers == FocusSearchboxKeyGesture.Modifiers)
			{
				SetFocusToSearchElement();
				e.Handled = true;
			}
		}

		private void ComboBoxOnDropDownOpened(object sender, EventArgs eventArgs)
		{
			SetFocusToSearchElement();
		}

		private void SetFocusToSearchElement()
		{
			if (TextBox != null)
				TextBox.Dispatcher.BeginInvoke(new Action(() => Keyboard.Focus(TextBox)));
		}

		/// <summary>
		/// Called when [detaching].
		/// </summary>
		protected override void OnDetaching()
		{
			base.OnDetaching();
			TextBox.TextChanged -= TextBoxOnTextChanged;
			TextBox.PreviewKeyDown -= TextBoxOnKeyDown;
			if (comboBox != null)
			{
				comboBox.DropDownOpened -= ComboBoxOnDropDownOpened;
			}
		}


		private void ComboBoxOnDropDownClosed(object sender, EventArgs eventArgs)
		{
			TextBox.Text = string.Empty;
		}

		private void TextBoxOnKeyDown(object sender, KeyEventArgs e)
		{
			if (e.Key == Key.Escape && !String.IsNullOrEmpty(TextBox.Text))
			{
				TextBox.Text = string.Empty;
				e.Handled = true;
			}
			if (e.Key == Key.Down && ItemsControl.Items.Count > 0)
			{
				var selector = ItemsControl as Selector;
				if (selector != null)
				{
					if (selector.SelectedIndex == -1)
						selector.SelectedIndex = 0;
					else
						selector.SelectedIndex++;
				}
				e.Handled = true;
			}
			if (e.Key == Key.Up && ItemsControl.Items.Count > 0)
			{
				var selector = ItemsControl as Selector;
				if (selector != null)
				{
					if (selector.SelectedIndex == -1)
						selector.SelectedIndex = ItemsControl.Items.Count - 1;
					else if (selector.SelectedIndex > 0)
						selector.SelectedIndex--;
				}
				e.Handled = true;
			}
		}

		private void TextBoxOnTextChanged(object sender, TextChangedEventArgs textChangedEventArgs)
		{
			try
			{
				if (!IsFilterEnabled || ItemsControl == null || ItemsControl.Items == null)
					return;

				if (!String.IsNullOrEmpty(TextBox.Text))
				{
					ItemsControl.Items.Filter = GetFilter();
				}
				else
				{
					ItemsControl.Items.Filter = oldFilter;
				}
			}
			catch (Exception e)
			{
				Console.WriteLine(e);
			}
		}

		private Predicate<object> GetFilter()
		{
			if (string.IsNullOrEmpty(PropertyNameToFilter))
			{
				PropertyNameToFilter = ItemsControl.DisplayMemberPath;
			}

			if (oldFilter != null)
				return o => oldFilter(o) && o != null && ExecuteFilter(o);
			return o => o != null && ExecuteFilter(o);
		}

		private bool ExecuteFilter(object o)
		{
			string valueString = o.ToString();
			string searchString = TextBox.Text.ToLowerInvariant();

			if (!String.IsNullOrEmpty(PropertyNameToFilter))
			{
				try
				{
					var propertyInfo = o.GetType().GetProperty(PropertyNameToFilter);
					if (propertyInfo != null)
					{
						object value = propertyInfo.GetValue(o, null);
						if (value != null)
							valueString = value.ToString();
					}
				}
				catch
				{
					PropertyNameToFilter = string.Empty;
				}
			}

			if (!IsCaseSensitive)
			{
				valueString = valueString.ToLowerInvariant();
				searchString = searchString.ToLowerInvariant();
			}

			return valueString.Contains(searchString);
		}
	}


}