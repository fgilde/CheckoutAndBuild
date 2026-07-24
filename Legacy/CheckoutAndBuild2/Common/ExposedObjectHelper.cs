using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Reflection;

namespace FG.CheckoutAndBuild2.Common
{
    internal class ExposedObjectHelper
    {
        private static readonly Type CsharpInvokePropertyType =
            typeof(Microsoft.CSharp.RuntimeBinder.RuntimeBinderException)
                .Assembly
                .GetType("Microsoft.CSharp.RuntimeBinder.ICSharpInvokeOrInvokeMemberBinder");

        internal static bool InvokeBestMethod(object[] args, object target, List<MethodInfo> instanceMethods, out object result)
        {
            if (instanceMethods.Count == 1)
            {
                // Just one matching instance method - call it
                if (TryInvoke(instanceMethods[0], target, args, out result))
                {
                    return true;
                }
            }
            else if (instanceMethods.Count > 1)
            {
                // Find a method with best matching parameters
                MethodInfo best = null;
                Type[] bestParams = null;
                Type[] actualParams = args.Select(p => p == null ? typeof(object) : p.GetType()).ToArray();

                Func<Type[], Type[], bool> isAssignableFrom = (a, b) => !a.Where((t, i) => !t.IsAssignableFrom(b[i])).Any();


                foreach (var method in instanceMethods.Where(m => m.GetParameters().Length == args.Length))
                {
                    Type[] mParams = method.GetParameters().Select(x => x.ParameterType).ToArray();
                    if (isAssignableFrom(mParams, actualParams))
                    {
                        if (best == null || isAssignableFrom(bestParams, mParams))
                        {
                            best = method;
                            bestParams = mParams;
                        }
                    }
                }

                if (best != null && TryInvoke(best, target, args, out result))
                {
                    return true;
                }

	            if (best == null)
					return TryInvoke(instanceMethods[1], target, args, out result);
            }

            result = null;
            return false;
        }

        internal static bool TryInvoke(MethodInfo methodInfo, object target, object[] args, out object result)
        {
            try
            {
                result = methodInfo.Invoke(target, args);
                return true;
            }
            catch (TargetInvocationException) { }
            catch (TargetParameterCountException) { }

            result = null;
            return false;

        }

        internal static Type[] GetTypeArgs(InvokeMemberBinder binder)
        {
            if (CsharpInvokePropertyType.IsInstanceOfType(binder))
            {
                PropertyInfo typeArgsProperty = CsharpInvokePropertyType.GetProperty("TypeArguments");
                return ((IEnumerable<Type>)typeArgsProperty.GetValue(binder, null)).ToArray();
            }
            return null;
        }

    }
}


