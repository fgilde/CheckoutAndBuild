using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Design;
using System.Linq;
using System.Reflection;
using CheckoutAndBuild2.Contracts.Settings;
using FG.CheckoutAndBuild2.Extensions;

namespace FG.CheckoutAndBuild2.Common
{

	/// <summary>
	/// CustomClass
	/// </summary>	
	[TypeConverter(typeof(ExpandableObjectConverter))]
	public class CustomClass : Component, ICustomTypeDescriptor
	{
		private readonly string name;

		//Private members
		private readonly PropertyDescriptorCollection propertyCollection;
		private int maxLength;
		private readonly string toString;
		/// <summary>
		/// MaxLength
		/// </summary>
		public int MaxLength
		{
			get
			{
				return maxLength;
			}
			set
			{
				if (value > maxLength)
					maxLength = value;
			}
		}

		/// <summary>
		/// Returns a <see cref="T:System.String"/> containing the name of the <see cref="T:System.ComponentModel.Component"/>, if any. This method should not be overridden.
		/// </summary>
		/// <returns>
		/// A <see cref="T:System.String"/> containing the name of the <see cref="T:System.ComponentModel.Component"/>, if any, or null if the <see cref="T:System.ComponentModel.Component"/> is unnamed.
		/// </returns>
		public override string ToString()
		{
			if (!string.IsNullOrEmpty(toString))
				return toString;
			return base.ToString();
		}

		/// <summary>
		/// Constructor of CustomClass which initializes the new PropertyDescriptorCollection.
		/// </summary>
		public CustomClass(string name, params object[] objects)			
		{
			this.name = name;

			propertyCollection = new PropertyDescriptorCollection(new PropertyDescriptor[] { });
			foreach (var obj in objects)
			{
				if (string.IsNullOrEmpty(toString))
					toString = obj.ToString();
				foreach (PropertyInfo propertyInfo in obj.GetType().GetProperties())
				{
					try
					{
						AddProperty(propertyInfo, obj);
					}
					catch (Exception e)
					{
						Trace.TraceError(e.Message);
					}
				}
			}		
		}


		/// <summary>
		/// Occurs when [property changed].
		/// </summary>
		public event PropertyChangedEventHandler PropertyChanged;

		/// <summary>
		/// Notifies the property changed.
		/// </summary>
		/// <param name="propertyName">Name of the property.</param>
		public void NotifyPropertyChanged(string propertyName)
		{
			var handler = PropertyChanged;
		    handler?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}

		/// <summary>
		/// Adds the property.
		/// </summary>
		public PropertyDescriptor AddProperty(string propName, object propValue, string propDesc, string propCat, Type propType)
		{
			return AddProperty(propName, propValue, propDesc, propCat, propType, false, false);
		}

		/// <summary>
		/// Adds the property.
		/// </summary>
		public void RemoveProperty(string propName)
		{
			var prop = propertyCollection.Find(propName, true);
			propertyCollection.Remove(prop);
		}

		/// <summary>
		/// Adds the property.
		/// </summary>
		public void RemoveProperty(DynamicProperty prop)
		{
			propertyCollection.Remove(prop);
		}


		/// <summary>
		/// Adds the property.
		/// </summary>
		public PropertyDescriptor AddProperty<T>(string propName, object propValue, string propDesc, string propCat, Type propType) where T : UITypeEditor
		{
			var property = new DynamicProperty(propName, propValue, propDesc, propCat, propType, false, false, true, new EditorAttribute(typeof(T), typeof(UITypeEditor)));

			int index = propertyCollection.Add(property);

			MaxLength = propName.Length;
			MaxLength = propValue.ToString().Length;

			return propertyCollection[index];
		}


		/// <summary>
		/// Adds a property into the CustomClass.
		/// </summary>
		public PropertyDescriptor AddProperty(PropertyInfo propertyInfo, object owner)
		{
			return AddProperty(propertyInfo, propertyInfo.GetValue(owner, new object[] {}), o => propertyInfo.SetValue(owner, o));
		}

		/// <summary>
		/// Adds a property into the CustomClass.
		/// </summary>
		public PropertyDescriptor AddProperty(PropertyInfo propertyInfo, object value, Action<object> onValueSet)
		{
			object propValue = value;

			//AddProperty(property.Name, value, desc, property.ReadFromAttribute<string, CategoryAttribute>(attribute => attribute.Category), property.PropertyType);


			DynamicProperty property = new DynamicProperty(propertyInfo, propValue, onValueSet);

			int index = propertyCollection.Add(property);

			MaxLength = propertyInfo.Name.Length;
			if (propValue != null)
				MaxLength = propValue.ToString().Length;

			return propertyCollection[index];
		}

		/// <summary>
		/// Adds a property into the CustomClass.
		/// </summary>
		public PropertyDescriptor AddProperty(string propName, object propValue, string propDesc, string propCat, Type propType, bool isReadOnly, bool isExpandable)
		{
			DynamicProperty property;
			if (propValue != null && (!(propValue is int) && !(propValue is bool) && !(propValue is double) && !(propValue is float) && !(propValue is Color)))
			{
				property = new DynamicProperty(propName, propValue, propDesc, propCat, propType, isReadOnly, isExpandable, true, new EditorAttribute(typeof(PropertyGridTypeEditor), typeof(UITypeEditor)));
			}
			else
			{
				property = new DynamicProperty(propName, propValue, propDesc, propCat, propType, isReadOnly, isExpandable, true);
			}

			int index = propertyCollection.Add(property);

			MaxLength = propName.Length;
			if (propValue != null)
				MaxLength = propValue.ToString().Length;

			return propertyCollection[index];
		}

		/// <summary>
		/// Gets the <see cref="CustomClass.DynamicProperty"/> at the specified index.
		/// </summary>
		/// <value></value>
		public DynamicProperty this[int index] => (DynamicProperty)propertyCollection[index];

	    //Overloaded Indexer for this class - returns a DynamicProperty by name.
		/// <summary>
		/// Gets the <see cref="CustomClass.DynamicProperty"/> with the specified name.
		/// </summary>
		/// <value></value>
		public DynamicProperty this[string _name] => (DynamicProperty)propertyCollection[_name];

	    /// <summary>
		///
		/// </summary>
		/// <returns></returns>
		public string GetClassName()
		{
			if(!string.IsNullOrEmpty(name))
				return name;
			return (TypeDescriptor.GetClassName(this, true));
		}

		/// <summary>
		///
		/// </summary>
		/// <returns></returns>
		public AttributeCollection GetAttributes()
		{
			return (TypeDescriptor.GetAttributes(this, true));
		}

		/// <summary>
		///
		/// </summary>
		/// <returns></returns>
		public string GetComponentName()
		{
			return ToString();
			//return (TypeDescriptor.GetComponentName(this, true));
		}

		/// <summary>
		///
		/// </summary>
		/// <returns></returns>
		public TypeConverter GetConverter()
		{
			return (TypeDescriptor.GetConverter(this, true));
		}

		/// <summary>
		///
		/// </summary>
		/// <returns></returns>
		public EventDescriptor GetDefaultEvent()
		{
			return (TypeDescriptor.GetDefaultEvent(this, true));
		}

		/// <summary>
		///
		/// </summary>
		/// <returns></returns>
		public PropertyDescriptor GetDefaultProperty()
		{
			PropertyDescriptorCollection props = GetAllProperties();

			if (props.Count > 0)
				return (props[0]);
			return (null);
		}

		/// <summary>
		///
		/// </summary>
		/// <param name="editorBaseType"></param>
		/// <returns></returns>
		public object GetEditor(Type editorBaseType)
		{
			return (TypeDescriptor.GetEditor(this, editorBaseType, true));
		}

		/// <summary>
		///
		/// </summary>
		/// <param name="attributes"></param>
		/// <returns></returns>
		public EventDescriptorCollection GetEvents(Attribute[] attributes)
		{
			return (TypeDescriptor.GetEvents(this, attributes, true));
		}

		/// <summary>
		///
		/// </summary>
		/// <returns></returns>
		public EventDescriptorCollection GetEvents()
		{
			return (TypeDescriptor.GetEvents(this, true));
		}

		/// <summary>
		///
		/// </summary>
		/// <param name="attributes"></param>
		/// <returns></returns>
		public PropertyDescriptorCollection GetProperties(Attribute[] attributes)
		{
			return (GetAllProperties());
		}

		/// <summary>
		///
		/// </summary>
		/// <returns></returns>
		public PropertyDescriptorCollection GetProperties()
		{
			return (GetAllProperties());
		}

		/// <summary>
		///
		/// </summary>
		/// <param name="pd"></param>
		/// <returns></returns>
		public object GetPropertyOwner(PropertyDescriptor pd)
		{
			return (this);
		}


		/// <summary>
		///	Helper method to return the PropertyDescriptorCollection or our Dynamic Properties
		/// </summary>
		/// <returns></returns>
		private PropertyDescriptorCollection GetAllProperties()
		{
			return propertyCollection;
		}


		/// <summary>
		///	This is the Property class this will be dynamically added to the class at runtime.
		///	These classes are returned in the PropertyDescriptorCollection of the GetAllProperties
		///	method of the custom class.
		/// </summary>
		/// <returns></returns>
		public class DynamicProperty : PropertyDescriptor
		{
		    private object propValue;
			private readonly Action<object> onSetValue;

		    public DynamicProperty(string pName, object pValue, string pDesc, string pCat, Type pType, bool readOnly, bool expandable, bool isBrowsable, params Attribute[] attrs)
				: base(pName, attrs)
			{
				PropertyName = pName;
				propValue = pValue;
				Description = pDesc;
				Category = pCat;
				PropertyType = pType;
				IsReadOnly = readOnly;
				IsExpandable = expandable;
				IsBrowsable = isBrowsable;
			}

			public DynamicProperty(PropertyInfo propertyInfo, object pValue, Action<object> onSetValue = null)
				: base(propertyInfo.Name, propertyInfo.GetCustomAttributes().ToArray())
			{
				PropertyName = propertyInfo.ReadFromAttribute<string, DisplayNameAttribute>(attribute => attribute.DisplayName, propertyInfo.ReadFromAttribute<string, SettingsPropertyAttribute>(attribute => attribute.Name, propertyInfo.Name));
				propValue = pValue;
				this.onSetValue = onSetValue;
				Description = propertyInfo.ReadFromAttribute<string, DescriptionAttribute>(attribute => attribute.Description, propertyInfo.ReadFromAttribute<string, SettingsPropertyAttribute>(attribute => attribute.Description)); 
				Category = propertyInfo.ReadFromAttribute<string, CategoryAttribute>(attribute => attribute.Category);
			
				PropertyType = propertyInfo.PropertyType;
				IsReadOnly = !propertyInfo.CanWrite;
				IsExpandable = true;
				IsBrowsable = true;
			}

			//   public IEnumerable<string> PossibleValues { get; set; }

			/// <summary>
			/// Gets the name of the member.
			/// </summary>
			/// <value></value>
			/// <returns>The name of the member.</returns>
			public override string Name => PropertyName;

		    /// <summary>
			/// Name
			/// </summary>
			public string PropertyName { get; }

		    /// <summary>
			/// IsExpandable
			/// </summary>
			public bool IsExpandable { get; }

		    /// <summary>
			/// When overridden in a derived class, gets the type of the component this property is bound to.
			/// </summary>
			/// <value></value>
			/// <returns>A <see cref="T:System.Type"/> that represents the type of component this property is bound to. When the <see cref="M:System.ComponentModel.PropertyDescriptor.GetValue(System.Object)"/> or <see cref="M:System.ComponentModel.PropertyDescriptor.SetValue(System.Object,System.Object)"/> methods are invoked, the object specified might be an instance of this type.</returns>
			public override Type ComponentType => propValue?.GetType();

		    /// <summary>
			/// Gets the name of the category to which the member belongs, as specified in the <see cref="T:System.ComponentModel.CategoryAttribute"/>.
			/// </summary>
			/// <value></value>
			/// <returns>The name of the category to which the member belongs. If there is no <see cref="T:System.ComponentModel.CategoryAttribute"/>, the category name is set to the default category, Misc.</returns>
			/// <PermissionSet>
			/// 	<IPermission class="System.Security.Permissions.SecurityPermission, mscorlib, Version=2.0.3600.0, Culture=neutral, PublicKeyToken=b77a5c561934e089" version="1" Flags="UnmanagedCode, ControlEvidence"/>
			/// </PermissionSet>
			public override string Category { get; }

		    /// <summary>
			/// Gets a value indicating whether the member is browsable, as specified in the <see cref="T:System.ComponentModel.BrowsableAttribute"/>.
			/// </summary>
			/// <value></value>
			/// <returns>true if the member is browsable; otherwise, false. If there is no <see cref="T:System.ComponentModel.BrowsableAttribute"/>, the property value is set to the default, which is true.</returns>
			public override bool IsBrowsable { get; }

		    /// <summary>
			/// When overridden in a derived class, gets a value indicating whether this property is read-only.
			/// </summary>
			/// <value></value>
			/// <returns>true if the property is read-only; otherwise, false.</returns>
			public override bool IsReadOnly { get; }

		    /// <summary>
			/// When overridden in a derived class, gets the type of the property.
			/// </summary>
			/// <value></value>
			/// <returns>A <see cref="T:System.Type"/> that represents the type of the property.</returns>
			public override Type PropertyType { get; }

		    /// <summary>
			/// When overridden in a derived class, returns whether resetting an object changes its value.
			/// </summary>
			/// <param name="component">The component to test for reset capability.</param>
			/// <returns>
			/// true if resetting the component changes its value; otherwise, false.
			/// </returns>
			public override bool CanResetValue(object component)
			{
				return true;
			}

			/// <summary>
			/// When overridden in a derived class, gets the current value of the property on a component.
			/// </summary>
			/// <param name="component">The component with the property for which to retrieve the value.</param>
			/// <returns>
			/// The value of a property for a given component.
			/// </returns>
			public override object GetValue(object component)
			{				
				return propValue;
			}

			/// <summary>
			/// When overridden in a derived class, sets the value of the component to a different value.
			/// </summary>
			/// <param name="component">The component with the property value that is to be set.</param>
			/// <param name="value">The new value.</param>
			public override void SetValue(object component, object value)
			{
				propValue = value;
			    onSetValue?.Invoke(value);
			}

			/// <summary>
			/// When overridden in a derived class, resets the value for this property of the component to the default value.
			/// </summary>
			/// <param name="component">The component with the property value that is to be reset to the default value.</param>
			public override void ResetValue(object component)
			{
				propValue = null;
			}

			/// <summary>
			/// When overridden in a derived class, determines a value indicating whether the value of this property needs to be persisted.
			/// </summary>
			/// <param name="component">The component with the property to be examined for persistence.</param>
			/// <returns>
			/// true if the property should be persisted; otherwise, false.
			/// </returns>
			public override bool ShouldSerializeValue(object component)
			{
				return false;
			}

			/// <summary>
			/// Gets the description of the member, as specified in the <see cref="T:System.ComponentModel.DescriptionAttribute"/>.
			/// </summary>
			/// <value></value>
			/// <returns>The description of the member. If there is no <see cref="T:System.ComponentModel.DescriptionAttribute"/>, the property value is set to the default, which is an empty string ("").</returns>
			public override string Description { get; }
		}

	}
}