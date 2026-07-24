using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.Caching;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using FG.CheckoutAndBuild2.Common;

namespace FG.CheckoutAndBuild2.Extensions
{
	public static class CoreExtensions
	{
		static readonly object lockObj = new object();

	    public static TValue GetOrDefault<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key)
	    {
	        return dictionary.TryGetValue(key, out var obj) ? obj : default(TValue);
	    }

        public static TResult ReadFromAttribute<TResult, TAttribute>(this MemberInfo info, Func<TAttribute, TResult> readerFunc,
		TResult fallbackValue = default (TResult)) where TAttribute : Attribute
		{
			var attribute = info.GetCustomAttribute<TAttribute>();
			return attribute != null ? readerFunc(attribute) : fallbackValue;
		}
    
        public static string Replace(this string s, string[] valuesToReplace, string newValue)
		{
			return valuesToReplace.Aggregate(s, (current, s1) => current.Replace(s1, newValue));
		}

		public static string GetMessage(this Exception e)
		{
			var agg = e as AggregateException;
			if (agg != null)
				return agg.GetJoinedMessage();
			return e.Message;
		}

		public static IOrderedEnumerable<TSource> Order<TSource, TKey>(this IEnumerable<TSource> source, Func<TSource, TKey> keySelector, ListSortDirection direction)
		{
			if (direction == ListSortDirection.Ascending)
				return source.OrderBy(keySelector);
			return source.OrderByDescending(keySelector);
		}

	    public static void RemoveRange<T>(this IList<T> list, IEnumerable<T> items)
	    {
	        foreach (T item in items.ToList())
	            list.Remove(item);
	    }


        public static IEnumerable<T> AddRange<T>(this IEnumerable<T> enumerable, IEnumerable<T> items)
		{
			return enumerable.ToObservableCollection().AddRange(items);			
		}

		public static IEnumerable<T> Add<T>(this IEnumerable<T> enumerable, T item)
		{
			var res = enumerable.ToList();
			res.Add(item);
			return res;
		}

		internal static IDictionary<string, string> ResolvePlaceHolders(this IDictionary<string, string> collection)
		{
			var res = new Dictionary<string, string>();
			foreach (var v in collection)
			{
				string value = v.Value;
				if (!string.IsNullOrEmpty(value) && value.Contains("%"))
				{
					Regex regex = new Regex(@"\%(.*?)\%", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Multiline | RegexOptions.Singleline);
					MatchCollection matchCollection = regex.Matches(value);
					foreach (Match match in matchCollection)
					{
						//var keyValue = Environment.GetEnvironmentVariable(match.Value.Replace("%", string.Empty));
						var keyValue = collection[match.Value.Replace("%", string.Empty)];
						value = value.Replace(match.Value, keyValue);
					}
				}
				res.Add(v.Key, value);
			}
			if (res.Any(pair => pair.Value.Contains("%")))
			{
				var nres = res.ResolvePlaceHolders();
				return nres;
			}
			return res;
		}

		public static StringBuilder AppendLineWhen(this StringBuilder builder, string s, Func<string, bool> predicate )
		{
			if (predicate(s))
				builder.AppendLine(s);
			return builder;
		}

        public static StringBuilder AppendLinesWhen(this StringBuilder builder, IEnumerable<string> s, Func<string, bool> predicate)
        {
            foreach (var t in s)
                builder.AppendLineWhen(t, predicate);
            return builder;
        }

        public static StringBuilder AppendLineIfNotEmpty(this StringBuilder builder, string s)
		{
			return builder.AppendLineWhen(s, s1 => !string.IsNullOrWhiteSpace(s1));
		}

        public static StringBuilder AppendLinesIfNotEmpty(this StringBuilder builder, params string[] s)
        {
            foreach (var t in s)            
                builder.AppendLineIfNotEmpty(t);
            return builder;
        }

        public static StringBuilder AppendLines(this StringBuilder builder, IEnumerable<string> lines )
		{
			foreach (var line in lines)
			{
				builder.AppendLine(line);
			}
			return builder;
		}

		public static string GetUnionStringPart(string s1, string s2)
		{
			string res = "";
			string longStr;
			string smallStr;
			if (s1.Length > s2.Length)
			{
				longStr = s1;
				smallStr = s2;
			}
			else
			{
				longStr = s2;
				smallStr = s1;
			}
			int i = 0;
			foreach (char c in smallStr)
			{
				if (c == longStr[i])
					res += c;
				else
					break;
				i++;
			}
			return res;
		}

		public static IEnumerable<string> GetLines(this string s)
		{
			using (var reader = new StringReader(s))
			{
				string line = reader.ReadLine();
				while (line != null)
				{
					yield return line;
					line = reader.ReadLine();
				}
			}
		}

		public static PropertyChangedEventHandler OnChange<T>(this INotifyPropertyChanged propertyChangedObject,
			Expression<Func<T>> action, Action<T> callback,
			bool ignoreExceptions = false)
		{
			var handler = GetPropertyChangedEventHandler(action, callback, ignoreExceptions);
			propertyChangedObject.PropertyChanged += handler;
			return handler;
		}

		public static PropertyChangedEventHandler GetPropertyChangedEventHandler<T>(Expression<Func<T>> action, Action<T> callback,
			bool ignoreExceptions)
		{
			PropertyChangedEventHandler handler = (sender, args) =>
			{
				if (args != null && args.PropertyName != null && action != null && args.PropertyName == GetMemberName(action))
				{
					var func = action.Compile();
					if (callback != null)
					{
						if (ignoreExceptions)
							Check.TryCatch<Exception>(() => callback(func()));
						else
							callback(func());
					}
				}
			};
			return handler;
		}


		public static ObservableCollection<T> ToObservableCollection<T>(this IEnumerable<T> list)
		{
			return new ObservableCollection<T>(list);
		}

		public static IDictionary<TKey, TValue> MergeWith<TKey, TValue>(this IDictionary<TKey, TValue> collection, params IDictionary<TKey, TValue>[] collections)
		{
			foreach (var value in collections.SelectMany(dictionary => dictionary))
			{
				collection.AddOrUpdate(value.Key, value.Value);
			}
			return collection;
		}

		public static void AddOrUpdate<TKey, TValue>(this IDictionary<TKey, TValue> collection, TKey key, TValue value)
		{
			if (collection.ContainsKey(key))
				collection[key] = value;
			else
				collection.Add(key, value);
		}

		public static ICollection<T> AddRange<T>(this ICollection<T> collection, IEnumerable<T> toAdd, bool cancelIfExists = false)
		{
			foreach (T item in toAdd)
			{
				if(!cancelIfExists || !collection.Contains(item))
				{
					var item1 = item;
					Check.TryCatch<Exception>(() => collection.Add(item1));
				}
			}
			return collection;
		}

		public static Task<T> IgnoreCancellation<T>(this Task<T> task, CancellationToken cancellationToken = default (CancellationToken))
		{
			return task.ContinueWith(ts => ts.IsCanceled ? default(T) : ts.Result, cancellationToken);
		}

		public static void UpdateCollection<T>(this ICollection<T> collection, IEnumerable<T> toAdd)
		{
			collection.Clear();
			collection.AddRange(toAdd);
		}

		public static int IndexOf<T>(this T[] array, T obj)
		{
			return Array.IndexOf(array, obj);
		}

		public static ConcurrentBag<T> Remove<T>(this ConcurrentBag<T> bag, T value)
		{			
			if (value != null && bag.Contains(value))
			{
				lock (lockObj)
				{
					return new ConcurrentBag<T>(bag.Where(arg => !arg.Equals(value)).ToList());
				}
			}
			return bag;
		}

			/// <summary>
		/// Adds a cache entry into the cache using the specified key and a Valuefactory and an absolute expiration value
		/// </summary>
		/// <param name="cache">The cache.</param>
		/// <param name="key">A unique identifier for the cache entry to add.</param>
		/// <param name="valueFactory">The value factory.</param>
		/// <param name="absoluteExpiration">The fixed date and time at which the cache entry will expire.</param>
		/// <param name="regionName">A named region in the cache to which a cache entry can be added. Do not pass a value for this parameter. This parameter is null by default, because the MemoryCache class does not implement regions.</param>
		/// <returns>If a cache entry with the same key exists, the existing cache entry; otherwise, null.</returns>
		public static T AddOrGetExisting<T>(this ObjectCache cache, string key, Func<T> valueFactory,
			DateTimeOffset absoluteExpiration = default(DateTimeOffset), string regionName = null)
		{ 
			if (absoluteExpiration == default(DateTimeOffset))
				absoluteExpiration = ObjectCache.InfiniteAbsoluteExpiration;

			Lazy<T> newValue = new Lazy<T>(valueFactory);
			Lazy<T> value = (Lazy<T>)cache.AddOrGetExisting(key, newValue, absoluteExpiration, regionName);
			return (value ?? newValue).Value;
		}

		public static IEnumerable<T> Apply<T>(this IEnumerable<T> enumerable, Action<T> action)
		{
			foreach (var item in enumerable)
			{
				action(item);
			}
			return enumerable;
		}

		/// <summary>
		/// int zu Guid
		/// </summary>
		public static Guid ToGuid(this int value)
		{
			byte[] bytes = new byte[16];
			BitConverter.GetBytes(value).CopyTo(bytes, 0);
			return new Guid(bytes);
		}

		/// <summary>
		/// int zu Guid
		/// </summary>
		public static Guid ToGuid(this long value)
		{
			byte[] bytes = new byte[16];
			BitConverter.GetBytes(value).CopyTo(bytes, 0);
			return new Guid(bytes);
		}

		/// <summary>
		/// Guid zu int
		/// </summary>
		public static int ToInt(this Guid value)
		{
			return BitConverter.ToInt32(value.ToByteArray(), 0);
		}

		/// <summary>
		/// Guid zu int
		/// </summary>
		public static long ToInt64(this Guid value)
		{
			return BitConverter.ToInt64(value.ToByteArray(), 0);
		}

		/// <summary>
		/// Gibt die message aller fehler zurück
		/// </summary>
		public static string GetJoinedMessage(this AggregateException aggregateException)
		{
			return string.Join(Environment.NewLine + "- ", aggregateException.GetMessages());
		}

		/// <summary>
		/// Gibt die messages der fehler zurück
		/// </summary>
		public static IEnumerable<string> GetMessages(this AggregateException aggregateException)
		{
			return aggregateException.Unwrap().Select(exception => exception.Message);
		}

		/// <summary>
		/// Entpackt eine AggregateException
		/// </summary>
		/// <param name="aggregateException"></param>
		/// <returns></returns>
		public static IEnumerable<Exception> Unwrap(this AggregateException aggregateException)
		{
			var result = new List<Exception>();
			foreach (Exception exception in aggregateException.InnerExceptions)
			{
				if(exception is AggregateException)
					result.AddRange(((AggregateException)exception).Unwrap());
				else
					result.Add(exception);
			}
			return result;
		} 

		/// <summary>
		/// Gibt an ob eine Zahl zwischen 2 bereichen ist
		/// </summary>
		public static bool Between(this int value, int left, int right)
		{
			return (value >= Math.Min(left, right) && value <= Math.Max(left, right));
		}


		/// <summary>
		/// Prüft ob es sich um einen nullable typ handelt z.b double?
		/// </summary>
		/// <param name="type"></param>
		/// <returns></returns>
		public static bool IsNullableType(this Type type)
		{
			return type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>);
		}

		/// <summary>
		/// Prüft, ob ein Typ (null)-Werte annehmen kann oder nicht
		/// </summary>
		/// <param name="type">Typ, der geprüft werden soll</param>
		/// <returns>Kann (null)-Werte annehmen (true) oder nicht (false)</returns>
		public static bool IsNullable(this Type type)
		{
			Check.NotNull(() => type);

			if (!type.IsValueType)
			{
				// kein Wertetyp, daher kann der Typ (null)-Werte annehmen
				return true;
			}

			return IsNullableType(type);
		}
		/// <summary>
		/// Gibt alle Atrribute des members zurück
		/// </summary>
		public static IEnumerable<T> GetAttributes<T>(this MemberInfo member, bool inherit)
		{
			return Attribute.GetCustomAttributes(member, inherit).OfType<T>();
		}

		/// <summary>
		/// Gets the member info represented by an expression.
		/// </summary>
		public static MemberInfo GetMemberInfo(this Expression expression)
		{
			if (expression is LambdaExpression)
			{
				var lambda = (LambdaExpression) expression;

				MemberExpression memberExpression;
				if (lambda.Body is UnaryExpression)
				{
					var unaryExpression = (UnaryExpression) lambda.Body;
					memberExpression = (MemberExpression) unaryExpression.Operand;
				}
				else memberExpression = (MemberExpression) lambda.Body;

				return memberExpression.Member;
			}
			return null;
		}

		/// <summary>
		/// Gets a property by name, ignoring case and searching all interfaces.
		/// </summary>
		public static PropertyInfo GetPropertyCaseInsensitive(this Type type, string propertyName)
		{
			var typeList = new List<Type> { type };

			if (type.IsInterface)
				typeList.AddRange(type.GetInterfaces());

			return typeList
				.Select(interfaceType => interfaceType.GetProperty(propertyName, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance))
				.FirstOrDefault(property => property != null);
		}

		

		/// <summary> 
		/// Helper method to get member name with compile time verification to avoid typo. 
		/// </summary> 
		/// <param name="expr">The lambda expression usually in the form of o => o.member.</param> 
		/// <returns>The name of the property.</returns> 
		[SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode", Justification = "Not used in all design time assemblies.")]
		public static string GetMemberName<T>(this Expression<Func<T>> expr)
		{
			Expression body = expr.Body;
			if (body is MemberExpression || body is UnaryExpression)
			{
				MemberExpression memberExpression = body as MemberExpression ?? (MemberExpression)((UnaryExpression)body).Operand;
				return memberExpression.Member.Name;
			}
			//MemberInfo memberInfo = GetMemberInfo(expr);

			return expr.ToString();
		}


		/// <summary>
		///     Helper method to get member name with compile time verification to avoid typo.
		/// </summary>
		/// <param name="expr">The lambda expression usually in the form of o => o.member.</param>
		/// <returns>The name of the property.</returns>
		[SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode", Justification = "Not used in all design time assemblies.")]
		public static string GetMemberName<T, TResult>(this Expression<Func<T, TResult>> expr)
		{
			Expression body = expr.Body;
			if (body is MemberExpression || body is UnaryExpression)
			{
				MemberExpression memberExpression = body as MemberExpression ?? (MemberExpression)((UnaryExpression)body).Operand;
				return memberExpression.Member.Name;
			}
			//MemberInfo memberInfo = GetMemberInfo(expr);

			return expr.ToString();
		}

		/// <summary>
		/// Gets the property info.
		/// </summary>
		/// <param name="expr">The expr.</param>
		/// <returns></returns>
		public static PropertyInfo GetPropertyInfo(this Expression<Func<object>> expr)
		{
			Expression body = expr.Body;
			MemberExpression memberExpression = body as MemberExpression ?? (MemberExpression)((UnaryExpression)body).Operand;
			return (PropertyInfo)memberExpression.Member;
		}


		/// <summary>
		/// FirstCharToLower
		/// </summary>
		public static string ToLower(this string input, bool firstCharOnly)
		{
			if (firstCharOnly)
			{
				string temp = input.Substring(0, 1);
				return temp.ToLower() + input.Remove(0, 1);
			}
			return input.ToLower();
		}

		/// <summary>
		/// FirstCharToUpper
		/// </summary>
		public static string ToUpper(this string input, bool firstCharOnly)
		{
			if (firstCharOnly)
			{
				string temp = input.Substring(0, 1);
				return temp.ToUpper() + input.Remove(0, 1);
			}
			return input.ToUpper();
		}

		/// <summary>
		/// Gibt an ob entweder ein zeichen, oder alle Zeichen aus <paramref name="chars"/> im String <paramref name="s"/> enthalten sind
		/// </summary>
		public static bool Contains(this string s, ContainsType type, params char[] chars)
		{
			if (type == ContainsType.Any)
				return chars.Any(s.Contains);
			return chars.All(s.Contains);
		}

		/// <summary>
		/// Gibt an ob entweder ein zeichen, oder alle Zeichen aus <paramref name="values"/> im String <paramref name="s"/> enthalten sind
		/// </summary>
		public static bool Contains(this string s, params string[] values)
		{
			return s.Contains(ContainsType.Any, values);
		}

		/// <summary>
		/// Gibt an ob entweder ein zeichen, oder alle Zeichen aus <paramref name="values"/> im String <paramref name="s"/> enthalten sind
		/// </summary>
		public static bool Contains(this string s, ContainsType type, params string[] values)
		{
			if (type == ContainsType.Any)
				return values.Any(s.Contains);
			return values.All(s.Contains);
		}

		/// <summary>
		/// Gibt an ob entweder ein zeichen, oder alle Zeichen aus <paramref name="chars"/> im String <paramref name="s"/> enthalten sind
		/// </summary>
		public static bool Contains(this string s, params char[] chars)
		{
			return s.Contains(ContainsType.Any, chars);
		}
	}

	/// <summary>
	/// Option ob Contains true ist , wenn der string alle oder ein zeichen des arrays enthält
	/// </summary>
	public enum ContainsType
	{
		/// <summary>
		/// Ein zeichen muss enthalten sein
		/// </summary>
		Any,

		/// <summary>
		/// Alle zeichen müssen enthalten sein
		/// </summary>
		All,
	}


}