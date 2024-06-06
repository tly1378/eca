using System;
using System.Collections.Generic;
using System.Reflection;

namespace ECA
{
    public partial class ECA
    {
        internal static Dictionary<string, MethodInfo> Methods;

        public static object Invoke(object instance, string key, params object[] objects)
        {
            if (objects == null || objects.Length == 0)
            {
                (key, objects) = ToArguments(key);
            }

            return ActionAttribute.Invoke(instance, key, objects);
        }

        public static void DoAction(object instance, string key, params object[] objects)
        {
            Invoke(instance, key, objects);
        }

        public static bool DoChecker(object instance, string key, params object[] objects)
        {
            if (Invoke(instance, key, objects) is bool result)
            {
                return result;
            }
            else
            {
                throw new Exception($"[ECA][DoChecker] The type returned by the Checker \"{key}\" is not bool");
            }
        }

        private static readonly char[] SPLIT_CHARS = new char[] { '(', ')' };

        private static (string key, object[] arguments) ToArguments(string input)
        {
            string[] inputs = input.Split(SPLIT_CHARS, StringSplitOptions.RemoveEmptyEntries);
            if (inputs.Length > 1)
            {
                return (inputs[0], inputs[1].Split(','));
            }
            else
            {
                return (inputs[0], null);
            }
        }

        public static MethodInfo GetMethod(string methodName)
        {
            if (Methods == null)
            {
                throw new Exception("ECA.Method not initialized!");
            }

            if (!Methods.TryGetValue(methodName, out var methodInfo))
            {
                throw new Exception($"Method[{methodName}] not found!!");
            }

            return methodInfo;
        }

        public static void SetMethods(Dictionary<string, MethodInfo> methods)
        {
            Methods = methods;
        }

        public static bool TryGetFuncDesc(MethodInfo methodInfo, out string desc)
        {
            foreach (var attribute in methodInfo.GetCustomAttributes())
            {
                var type = attribute.GetType();
                if (type.Name == nameof(ActionAttribute))
                {
                    desc = (string)type.GetProperty(nameof(ActionAttribute.FunctionDesc)).GetValue(attribute);
                    return true;
                }
            }
            desc = string.Empty;
            return false;
        }
    }
}