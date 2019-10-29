using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Reflection;

namespace FG.CheckoutAndBuild2.Common
{
    public class ExposedObject : DynamicObject
    {
        private readonly object objectInstance;
        private readonly Type objectType;
        private readonly Dictionary<string, Dictionary<int, List<MethodInfo>>> instanceMethods;
        private readonly Dictionary<string, Dictionary<int, List<MethodInfo>>> genInstanceMethods;

        private ExposedObject(object obj)
        {
            objectInstance = obj;
            objectType = obj.GetType();

            instanceMethods =
                objectType
                    .GetMethods(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance)
                    .Where(m => !m.IsGenericMethod)
                    .GroupBy(m => m.Name)
                    .ToDictionary(
                        p => p.Key,
                        p => p.GroupBy(r => r.GetParameters().Length).ToDictionary(r => r.Key, r => r.ToList()));

            genInstanceMethods =
                objectType
                    .GetMethods(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance)
                    .Where(m => m.IsGenericMethod)
                    .GroupBy(m => m.Name)
                    .ToDictionary(
                        p => p.Key,
                        p => p.GroupBy(r => r.GetParameters().Length).ToDictionary(r => r.Key, r => r.ToList()));
        }

        public object Object { get { return objectInstance; } }

		public static dynamic New<T>(params object[] parameters)
        {
            return New(typeof(T),parameters);
        }

		public static dynamic New(Type type, params object[] parameters)
        {
            return new ExposedObject(Create(type,parameters));
        }

        private static object Create(Type type, params object[] parameters)
        {
			ConstructorInfo constructorInfo = GetConstructorInfo(type, parameters);
            return constructorInfo.Invoke(parameters);
        }

        private static ConstructorInfo GetConstructorInfo(Type type, params object[] parameters)
        {
            ConstructorInfo[] constructorInfo = type.GetConstructors(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
            if (constructorInfo.Any())
            {
            	foreach (ConstructorInfo info in constructorInfo)
            	{
					if (info.GetParameters().Count() == parameters.Count())
						return info;
            	}
				return constructorInfo[0];
            }
            //throw new MissingMemberException(type.FullName, string.Format(".ctor({0})", string.Join(", ", Array.ConvertAll(args, t => t.FullName))));
            throw new MissingMemberException(type.FullName);
        }

        public static dynamic From(object obj)
        {
            return new ExposedObject(obj);
        }

        public static T Cast<T>(ExposedObject t)
        {
            return (T)t.objectInstance;
        }

        public override bool TryInvokeMember(InvokeMemberBinder binder, object[] args, out object result)
        {
            // Get type args of the call
            Type[] typeArgs = ExposedObjectHelper.GetTypeArgs(binder);
            if (typeArgs != null && typeArgs.Length == 0) typeArgs = null;


            // Try to call a non-generic instance method
            if (typeArgs == null
                    && instanceMethods.ContainsKey(binder.Name)
                    && instanceMethods[binder.Name].ContainsKey(args.Length)
                    && ExposedObjectHelper.InvokeBestMethod(args, objectInstance, instanceMethods[binder.Name][args.Length], out result))
            {
                return true;
            }

            
            // Try to call a generic instance method
            if (instanceMethods.ContainsKey(binder.Name)
                    && instanceMethods[binder.Name].ContainsKey(args.Length))
            {
	            List<MethodInfo> methods = null;

	            try
	            {
		            methods = (from method in genInstanceMethods[binder.Name][args.Length] 
		                       where method.GetGenericArguments().Length == typeArgs.Length 
		                       select method.MakeGenericMethod(typeArgs)).ToList();
	            }
	            catch (Exception)
	            {
					// Nicht generisch sondern overload
					methods = new List<MethodInfo>();
					foreach (List<MethodInfo> methodInfos in instanceMethods.Where(pair => pair.Key == binder.Name).Select(pair => pair.Value).SelectMany(dictionary => dictionary.Values))
		            {
			            methods.AddRange(methodInfos);
		            }
	            }

            	if (ExposedObjectHelper.InvokeBestMethod(args, objectInstance, methods, out result))
                {
                    return true;
                }
            }

            result = null;
            return false;
        }

        public override bool TrySetMember(SetMemberBinder binder, object value)
        {
            var propertyInfo = objectType.GetProperty(
                binder.Name,
                BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);

            if (propertyInfo != null)
            {
                propertyInfo.SetValue(objectInstance, value, null);
                return true;
            }

            var fieldInfo = objectType.GetField(
                binder.Name,
                BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);

            if (fieldInfo != null)
            {
                fieldInfo.SetValue(objectInstance, value);
                return true;
            }

            return false;
        }

        public override bool TryGetMember(GetMemberBinder binder, out object result)
        {
            var propertyInfo = objectInstance.GetType().GetProperty(
                binder.Name,
                BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);

            if (propertyInfo != null)
            {
                result = propertyInfo.GetValue(objectInstance, null);
                return true;
            }

            var fieldInfo = objectInstance.GetType().GetField(
                binder.Name,
                BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);

            if (fieldInfo != null)
            {
                result = fieldInfo.GetValue(objectInstance);
                return true;
            }

            result = null;
            return false;
        }

        public override bool TryConvert(ConvertBinder binder, out object result)
        {
            result = objectInstance;
            return true;
        }
    }

}


