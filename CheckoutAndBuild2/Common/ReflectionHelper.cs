﻿using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Text;
using PropertyAttributes = System.Reflection.PropertyAttributes;

namespace FG.CheckoutAndBuild2.Common
{
	/// <summary>
	/// ReflectionHelper
	/// </summary>
	public static class ReflectionHelper
	{
		private static readonly ConcurrentDictionary<Type, IEnumerable<Type>> interfaceTypeCache = new ConcurrentDictionary<Type, IEnumerable<Type>>();

		/// <summary>
		/// PublicBindingFlags
		/// </summary>
		public const BindingFlags PublicBindingFlags = BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public | BindingFlags.GetField | BindingFlags.GetProperty;

		/// <summary>
		/// Instanz für interface erzeugen
		/// </summary>
		/// <typeparam name="TInterface">The type of the interface.</typeparam>
		/// <param name="coverUpAbstractMembers">Wenn true werden Abstrakte basis properties überdeckt</param>
		/// <returns></returns>
		public static TInterface CreateInstanceFromInterfaceOrAbstractType<TInterface>(bool coverUpAbstractMembers)
			where TInterface : class
		{
			return CreateInstanceFromInterfaceOrAbstractType(typeof(TInterface), coverUpAbstractMembers) as TInterface;
		}

		/// <summary>
		/// Instanz erzeugen
		/// </summary>
		public static T CreateInstance<T>() where T : class
		{
			return CreateInstance<T>(true, false);
		}

		/// <summary>
		/// Instanz erzeugen
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="allowInterfacesAndAbstractClasses">if set to <c>true</c> [allow interfaces and abstract classes].</param>
		/// <param name="coverUpAbstractMembers">Wenn true werden Abstrakte basis properties überdeckt</param>
		/// <returns></returns>
		public static T CreateInstance<T>(bool allowInterfacesAndAbstractClasses,
			bool coverUpAbstractMembers) where T : class
		{
			return CreateInstance(typeof(T), allowInterfacesAndAbstractClasses, coverUpAbstractMembers) as T;
		}

		/// <summary>
		/// Returns the default Value
		/// </summary>
		public static object GetDefaultValue(Type t)
		{
			if (t.IsValueType)
			{
				return Activator.CreateInstance(t);
			}
			return null;
		}

		/// <summary>
		/// PropertyInfo auch für parenttypes zurückgeben
		/// </summary>
		public static PropertyInfo[] GetPropertiesRecursive(this Type type, BindingFlags flags, Func<Type, bool> continueCondition = null)
		{
			if (continueCondition == null)
				continueCondition = t => t != typeof(object);

			var infos = new List<PropertyInfo>();
			Type typeToSearch = type;
			do
			{
				infos.AddRange(typeToSearch.GetProperties(flags));
				typeToSearch = typeToSearch.BaseType;
			} while (continueCondition(typeToSearch) && typeToSearch != null);
			return infos.ToArray();
		}

		/// <summary>
		/// Gets method that will be invoked the event is raised.
		/// </summary>
		/// <param name="obj">Object that contains the event.</param>
		/// <param name="eventName">Event Name.</param>
		/// <returns></returns>
		public static MethodInfo GetEventInvoker(object obj, string eventName)
		{
			// --- Begin parameters checking code -----------------------------
			Debug.Assert(obj != null);
			Debug.Assert(!string.IsNullOrEmpty(eventName));
			// --- End parameters checking code -------------------------------

			// prepare current processing type
			Type currentType = obj.GetType();

			// try to get special event decleration
			while (true)
			{
				FieldInfo fieldInfo = currentType.GetField(eventName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.GetField);

				if (fieldInfo == null)
				{
					if (currentType.BaseType != null)
					{
						// move deeper
						currentType = currentType.BaseType;
						continue;
					}

					return null;
				}

				// found
				return ((MulticastDelegate)fieldInfo.GetValue(obj)).Method;
			}
		}

		/// <summary>
		/// PropertyInfo auch für parenttypes zurückgeben
		/// </summary>
		public static FieldInfo[] GetFieldsRecursive(this Type type, BindingFlags flags, Func<Type, bool> continueCondition = null)
		{
			if (continueCondition == null)
				continueCondition = t => t != typeof(object);

			var infos = new List<FieldInfo>();
			Type typeToSearch = type;
			do
			{
				infos.AddRange(typeToSearch.GetFields(flags));
				typeToSearch = typeToSearch.BaseType;
			} while (continueCondition(typeToSearch) && typeToSearch != null);
			return infos.ToArray();
		}


		/// <summary>
		/// Eine instance erzeugen
		/// </summary>
		public static object CreateInstance(Type t)
		{
			return CreateInstance(t, true, false);
		}

		/// <summary>
		/// Eine instance erzeugen
		/// </summary>
		/// <param name="t">The t.</param>
		/// <param name="allowInterfacesAndAbstractClasses">Abstrakte klassen oder Interface instanzen erstellen</param>
		/// <param name="coverUpAbstractMembers">Wenn true werden Abstrakte basis properties überdeckt</param>
		/// <returns></returns>
		public static object CreateInstance(Type t, bool allowInterfacesAndAbstractClasses,
			bool coverUpAbstractMembers)
		{
			if (typeof(Guid) == t)
				return Guid.NewGuid();
			object result = null;
			try
			{
				if (allowInterfacesAndAbstractClasses && (t.IsInterface || t.IsArray || t.IsAbstract))
				{
					result = CreateInstanceFromInterfaceOrAbstractType(t, coverUpAbstractMembers);
				}
				else if (t.GetConstructors().Any(info => !info.GetParameters().Any()))
				{
					result = Activator.CreateInstance(t);
				}
			}
			catch (Exception)
			{
				result = null;
			}

			if (result == null)
			{
				List<object> parameters = new List<object>();
				var constructorInfo = t.GetConstructors().OrderBy(info => info.GetParameters().Count()).FirstOrDefault();
				if (constructorInfo != null)
				{
					parameters.AddRange(constructorInfo.GetParameters().Select(parameterInfo => CreateInstance(parameterInfo.ParameterType)));
					result = constructorInfo.Invoke(parameters.ToArray());
				}
			}
			return result;
		}

		/// <summary>
		/// Findet den typen, der das angegebene interface implementiert
		/// </summary>
		public static Type FindImplementingType(Type interfaceType)
		{
			Func<Type, IEnumerable<Type>> func = it => (from assembly in AppDomain.CurrentDomain.GetAssemblies()
														where assembly.GetName().Version > new Version(0, 0, 0, 0)
														from referencedAssembly in assembly.GetReferencedAssemblies()
														where assembly == it.Assembly || referencedAssembly.FullName == it.Assembly.GetName().FullName
														select assembly).SelectMany(assembly => assembly.GetTypes())
				.Where(type => type.ImplementsInterface(it))
				.ToList();

			IEnumerable<Type> implementingTypes = interfaceTypeCache.GetOrAdd(interfaceType, func).ToList();
			return implementingTypes.FirstOrDefault(
				type => type.Name.Equals(interfaceType.Name.Substring(1), StringComparison.InvariantCultureIgnoreCase))
				   ?? implementingTypes.FirstOrDefault();
		}

		/// <summary>
		/// Instanz für interface erzeugen
		/// </summary>
		/// <param name="interfaceType">Typ</param>
		/// <param name="coverUpAbstractMembers">Wenn true werden Abstrakte basis properties überdeckt</param>
		public static object CreateInstanceFromInterfaceOrAbstractType(Type interfaceType, bool coverUpAbstractMembers)
		{
			if (typeof(IEnumerable).IsAssignableFrom(interfaceType))
			{
				if (interfaceType == typeof(IEnumerable) || (interfaceType.IsGenericType && interfaceType.GetGenericTypeDefinition() == typeof(IEnumerable<>)) ||
					(interfaceType.IsGenericType && interfaceType.GetGenericTypeDefinition() == typeof(IDictionary<,>)) ||
					(interfaceType.IsGenericType && interfaceType.GetGenericTypeDefinition() == typeof(Dictionary<,>)) || interfaceType.IsArray)
				{
					return CreateList(interfaceType);
				}
			}

			if (!interfaceType.IsInterface && !interfaceType.IsAbstract)
				throw new NotSupportedException("Only interfaces are supported");

			var existingType = FindImplementingType(interfaceType);
			if (existingType != null)
			{
				return CreateInstance(existingType);
			}


			string name = Assembly.GetCallingAssembly().GetName().Name;
			AssemblyName assemblyName = new AssemblyName { Name = name + ".Dynamic." + interfaceType.Name };
			AssemblyBuilder asmBuilder = AppDomain.CurrentDomain.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);

			var modBuilder = asmBuilder.DefineDynamicModule(asmBuilder.GetName().Name, false);

			TypeBuilder typeBuilder = CreateTypeBuilder(modBuilder, "DynamicType_" + interfaceType.Name, interfaceType);
			if (interfaceType.IsInterface)
				typeBuilder.AddInterfaceImplementation(interfaceType);

			if (interfaceType.IsInterface || coverUpAbstractMembers)
			{
				foreach (var prop in interfaceType.GetProperties(PublicBindingFlags))
				{
					BuildProperty(typeBuilder, prop.Name, prop.PropertyType);
				}

				foreach (var field in interfaceType.GetFields(PublicBindingFlags))
				{
					BuildField(typeBuilder, field.Name, field.FieldType);
				}

				foreach (var method in interfaceType.GetMethods(PublicBindingFlags))
				{
					BuildEmptyMethod(typeBuilder, method.Name, method.ReturnType);
				}
			}

			Type type = typeBuilder.CreateType();
			return Activator.CreateInstance(type);
		}

		/// <summary>
		/// Enthaltene typen zurückgeben
		/// </summary>
		public static Type[] GetDeclaringTypes(Type t)
		{
			if (t.IsGenericType)
				return t.GetGenericArguments();
			if (t.IsArray)
				return new[] { t.GetElementType() };
			return new[] { typeof(object) };
		}

		private static object CreateList(Type interfaceType)
		{
			Type typeToCreate = typeof(ArrayList);
			if (interfaceType.IsGenericType || interfaceType.IsArray)
			{
				typeToCreate = typeof(List<>);
				if (typeof(IDictionary).IsAssignableFrom(interfaceType) ||
					(interfaceType.IsGenericType && interfaceType.GetGenericTypeDefinition() == typeof(IDictionary<,>)) ||
					(interfaceType.IsGenericType && interfaceType.GetGenericTypeDefinition() == typeof(Dictionary<,>)))
				{
					typeToCreate = typeof(Dictionary<,>);
				}
				typeToCreate = typeToCreate.MakeGenericType(GetDeclaringTypes(interfaceType));
			}
			return Activator.CreateInstance(typeToCreate);
		}

		#region Private Helper for typebuilder

		private static void BuildEmptyMethod(TypeBuilder typeBuilder, string name, Type type)
		{
			const MethodAttributes getSetAttr = MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName |
												MethodAttributes.Virtual;

			MethodBuilder getter = typeBuilder.DefineMethod(name, getSetAttr, type, Type.EmptyTypes);

			ILGenerator getIL = getter.GetILGenerator();
			getIL.Emit(OpCodes.Ldarg_0);
			getIL.Emit(OpCodes.Ret);
		}

		private static void BuildField(TypeBuilder typeBuilder, string name, Type type)
		{
			typeBuilder.DefineField(name, type, FieldAttributes.Public);
		}

		private static void BuildProperty(TypeBuilder typeBuilder, string name, Type type)
		{
			FieldBuilder field = typeBuilder.DefineField("m" + name, type, FieldAttributes.Private);
			PropertyBuilder propertyBuilder = typeBuilder.DefineProperty(name, PropertyAttributes.None, type, null);

			const MethodAttributes getSetAttr = MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName |
												MethodAttributes.Virtual;

			MethodBuilder getter = typeBuilder.DefineMethod("get_" + name, getSetAttr, type, Type.EmptyTypes);

			ILGenerator getIL = getter.GetILGenerator();
			getIL.Emit(OpCodes.Ldarg_0);
			getIL.Emit(OpCodes.Ldfld, field);
			getIL.Emit(OpCodes.Ret);

			MethodBuilder setter = typeBuilder.DefineMethod("set_" + name, getSetAttr, null, new[] { type });

			ILGenerator setIL = setter.GetILGenerator();
			setIL.Emit(OpCodes.Ldarg_0);
			setIL.Emit(OpCodes.Ldarg_1);
			setIL.Emit(OpCodes.Stfld, field);
			setIL.Emit(OpCodes.Ret);


			propertyBuilder.SetGetMethod(getter);
			propertyBuilder.SetSetMethod(setter);
		}


		private static TypeBuilder CreateTypeBuilder(ModuleBuilder modBuilder, string typeName, Type interfaceType)
		{
			Type parentType = typeof(object);
			if (!interfaceType.IsInterface)
				parentType = interfaceType;
			TypeBuilder typeBuilder = modBuilder.DefineType(typeName,
				TypeAttributes.Public |
				TypeAttributes.Class |
				TypeAttributes.AutoClass |
				TypeAttributes.AnsiClass |
				TypeAttributes.BeforeFieldInit |
				TypeAttributes.AutoLayout,
				parentType, interfaceType.IsInterface ? new[] { interfaceType } : new Type[0]);

			return typeBuilder;
		}


		#endregion


		/// <summary>
		/// Simple Implicit or Explicit cast
		/// </summary>
		public static T SimpleCast<T>(object o)
		{
			var res = SimpleCast(o, typeof(T));
			if (res != null)
				return (T)res;
			return default(T);
		}

		/// <summary>
		/// Simple Implicit or Explicit cast
		/// </summary>
		public static object SimpleCast(object o, Type targetType)
		{
			MethodInfo mi = o.GetType().GetMethods((BindingFlags.Public | BindingFlags.Static)).
				FirstOrDefault(info => (info.Name == "op_Implicit" || info.Name == "op_Explicit")
										&& targetType.IsAssignableFrom(info.ReturnType) && info.GetParameters().Count() == 1
										&& o.GetType().IsAssignableFrom(info.GetParameters()[0].ParameterType))
							??
							targetType.GetMethods((BindingFlags.Public | BindingFlags.Static)).
							FirstOrDefault(info => (info.Name == "op_Implicit" || info.Name == "op_Explicit")
													&& targetType.IsAssignableFrom(info.ReturnType) && info.GetParameters().Count() == 1
													&& o.GetType().IsAssignableFrom(info.GetParameters()[0].ParameterType));

			if (mi != null)
			{
				var invoke = mi.Invoke(null, new[] { o });
				return invoke;
			}

			if (targetType.IsInstanceOfType(o))
			{
				var typeConverter = TypeDescriptor.GetConverter(targetType);
				if (typeConverter.CanConvertTo(targetType))
					return typeConverter.ConvertTo(o, targetType);
				typeConverter = TypeDescriptor.GetConverter(o.GetType());
				if (typeConverter.CanConvertFrom(o.GetType()))
					return typeConverter.ConvertFrom(o);
			}
			return null;
		}

		/// <summary>
		/// Aus einem Typen ein Dictionary erzeugen
		/// </summary>
		public static IDictionary<string, object> ToDictionary(this Type type, Func<Type, bool> condition = null)
		{
			if (condition == null)
				condition = t => true;
			return type.GetFields()
				.Where(fi => condition(fi.FieldType))
				.ToDictionary(fi => fi.Name, fi => fi.GetValue(null));
		}

		/// <summary>
		/// Gibt alle Properties mit wert zurück
		/// </summary>
		/// <param name="objectValue">Objekt</param>
		/// <param name="except">Alle Properties ausser diesen</param>
		/// <returns></returns>
		[DebuggerStepThrough]
		public static Dictionary<string, object> GetProperties(object objectValue, params string[] except)
		{
			var result = new Dictionary<string, object>();
			BindingFlags bindingInfo = BindingFlags.Public | BindingFlags.Static;
			Type objectType = objectValue as Type;
			object objectInstance = null;
			if (objectType == null)
			{
				bindingInfo = BindingFlags.Public | BindingFlags.Instance;
				objectType = objectValue.GetType();
				objectInstance = objectValue;
			}

			foreach (var propertyInfo in from info in objectType.GetProperties(bindingInfo)
										 where !except.Contains(info.Name)
										 select info)
			{
				try
				{
					var value = propertyInfo.GetValue(objectInstance, new object[0]);
					result.Add(propertyInfo.Name, value);
				}
				catch { }
			}
			return result;
		}

		/// <summary>
		/// Gibt die signatur der methode zurück
		/// </summary>
		public static string GetSignature(this MethodInfo method, bool callable = false)
		{
			var firstParam = true;
			var sigBuilder = new StringBuilder();
			if (!callable)
			{
				if (method.IsPublic)
					sigBuilder.Append("public ");
				else if (method.IsPrivate)
					sigBuilder.Append("private ");
				else if (method.IsAssembly)
					sigBuilder.Append("internal ");
				if (method.IsFamily)
					sigBuilder.Append("protected ");
				if (method.IsStatic)
					sigBuilder.Append("static ");
				sigBuilder.Append(TypeName(method.ReturnType));
				sigBuilder.Append(' ');
			}
			sigBuilder.Append(method.Name);

			// Add method generics
			if (method.IsGenericMethod)
			{
				sigBuilder.Append("<");
				foreach (var g in method.GetGenericArguments())
				{
					if (firstParam)
						firstParam = false;
					else
						sigBuilder.Append(", ");
					sigBuilder.Append(TypeName(g));
				}
				sigBuilder.Append(">");
			}
			sigBuilder.Append("(");
			firstParam = true;
			var secondParam = false;
			foreach (var param in method.GetParameters())
			{
				if (firstParam)
				{
					firstParam = false;
					if (method.IsDefined(typeof(ExtensionAttribute), false))
					{
						if (callable)
						{
							secondParam = true;
							continue;
						}
						sigBuilder.Append("this ");
					}
				}
				else if (secondParam)
					secondParam = false;
				else
					sigBuilder.Append(", ");
				if (param.ParameterType.IsByRef)
					sigBuilder.Append("ref ");
				else if (param.IsOut)
					sigBuilder.Append("out ");
				if (!callable)
				{
					sigBuilder.Append(TypeName(param.ParameterType));
					sigBuilder.Append(' ');
				}
				sigBuilder.Append(param.Name);
			}
			sigBuilder.Append(")");
			return sigBuilder.ToString();
		}

		/// <summary>
		/// Prüft ob ein bestimmter Typ ein bestimmtes interface implementiert
		/// </summary>
		public static bool ImplementsInterface(this Type type, Type interfaceType)
		{
			Type[] array = type.FindInterfaces(
				(typeObj, criteriaObj) => typeObj.Equals((Type)criteriaObj),
				interfaceType
			);
			return array.Length > 0;
		}

		/// <summary>
		/// Get full type name with full namespace names
		/// </summary>
		/// <param name="type">Type. May be generic or nullable</param>
		/// <returns>Full type name, fully qualified namespaces</returns>
		public static string TypeName(Type type)
		{
			var nullableType = Nullable.GetUnderlyingType(type);
			if (nullableType != null)
				return nullableType.Name + "?";

			if (!type.IsGenericType)
				switch (type.Name)
				{
					case "String": return "string";
					case "Int32": return "int";
					case "Decimal": return "decimal";
					case "Object": return "object";
					case "Void": return "void";
					default:
						{
							return String.IsNullOrWhiteSpace(type.FullName) ? type.Name : type.FullName;
						}
				}

			var sb = new StringBuilder(type.Name.Substring(0,
			type.Name.IndexOf('`'))
			);
			sb.Append('<');
			var first = true;
			foreach (var t in type.GetGenericArguments())
			{
				if (!first)
					sb.Append(',');
				sb.Append(TypeName(t));
				first = false;
			}
			sb.Append('>');
			return sb.ToString();
		}



		/// <summary>
		/// Gibt den wert einer Property eines Objektes zurück
		/// </summary>
		public static object GetValue(object obj, string property)
		{
			var dataRowView = obj as DataRowView;
			if (dataRowView != null)
			{
				return dataRowView[property];
			}
			if (obj == null)
				return null;
			if (String.IsNullOrEmpty(property))
				return obj;
			var propertyInfo = obj.GetType().GetProperty(property);
			if (propertyInfo == null)
				return obj;
			var result = propertyInfo.GetValue(obj, new object[] { });
			return result ?? obj;
		}

		/// <summary>
		/// Gibt die Methode zurück, von der der Aufruf der Methode, die "GetCallingMethod" aufgerufen hat kam
		/// </summary>
		public static MethodBase GetCallingMethod(int skip = 0)
		{
			var st = new StackTrace(2 + skip, true);

			try
			{
				StackFrame sf = st.GetFrame(0);
				if (sf != null)
					return sf.GetMethod();
			}
			catch (Exception)
			{
				return MethodBase.GetCurrentMethod();
			}

			return MethodBase.GetCurrentMethod();
		}
	}
}