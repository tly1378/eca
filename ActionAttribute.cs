using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace ECA
{
    [AttributeUsage(AttributeTargets.Method)]
    public class ActionAttribute : Attribute, IKeyed
    {
        const int BUFF_LENGTH = 20;
        static readonly object[] sArguments = new object[BUFF_LENGTH];

        public string Key { get; }

        public ActionAttribute(string key = null)
        {
            Key = key;
        }

        public string FunctionDesc { get; set; }

        /// <summary>
        /// 根据键值执行Action，参数可任意直接传入或包含在键值中。
        /// 直接传入：DoAction("YourFunction", "arg1", "arg2", "arg3");
        /// 随键传入：DoAction("YourFunction(arg1, arg2, arg3)");
        /// </summary>
        internal static object Invoke(object instance, string key, params object[] inputs)
        {
            // 参数合法性检查
            if (!ECA.Methods.TryGetValue(key, out MethodInfo method))
            {
                string msg = string.Format("[ECA] {0}调用失败，找不到引用。", key);
                throw new Exception(msg);
            }

            // 初始化
            ParameterInfo[] parameters = GetParameters(method);
            ParameterInfo lastParam = null;
            if (parameters.Length > 0)
            {
                lastParam = parameters[parameters.Length - 1];
            }
            Array.Clear(sArguments, 0, BUFF_LENGTH);
            int argumentLength;
            bool isParames = false;
            if (inputs == null)
            {
                argumentLength = 0;
            }
            else
            {
                argumentLength = inputs.Length;
                if (parameters.Length > 0)
                {
                    bool overLength = parameters.Length < inputs.Length;
                    bool paramArray = parameters.Length == inputs.Length && lastParam.ParameterType.IsSubclassOf(typeof(Array)) && !(inputs[inputs.Length - 1] is Array);
                    isParames = overLength || paramArray;
                }
            }

            // 根据参数类型转译
            Type parameterType = null;
            for (int i = 0; i < argumentLength; i++)
            {
                if (i >= parameters.Length - 1 && isParames)
                {
                    // 输入的参数数量超过函数的参数总数
                    if (i == parameters.Length - 1)
                    {
                        if (lastParam.ParameterType.IsArray)
                        {
                            ParameterInfo parameter = lastParam;
                            parameterType = parameter.ParameterType.GetElementType();
                        }
                        else
                        {
                            string msg = string.Format("[ECA] {0}调用失败，参数数量异常。", key);
                            throw new Exception(msg);
                        }
                    }
                }
                else
                {
                    // 输入的参数数量在参数数量内
                    ParameterInfo parameter = parameters[i];
                    parameterType = parameter.ParameterType;
                }

                Type inputType = inputs[i]?.GetType();
                if (inputType == null)
                {
                    sArguments[i] = null;
                }
                else if (inputType == parameterType || inputType.IsSubclassOf(parameterType))
                {
                    sArguments[i] = inputs[i];
                }
                else if (inputs[i] is string argument)
                {
                    sArguments[i] = StringToObject(parameterType, argument);
                }
                else
                {
                    sArguments[i] = inputs[i];
                }
            }

            // 修正参数长度
            int parameterLength = parameters.Length;
            // 参数数量过多：将多出的参数存为数组
            if (argumentLength > parameterLength || isParames)
            {
                int lastIndex = parameterLength - 1;
                var objectArray = sArguments.Skip(lastIndex).Take(argumentLength - lastIndex).ToArray(); // 待优化
                Array convertedArray = ConvertArray(objectArray, parameterType);
                sArguments[lastIndex] = convertedArray;
            }
            // 参数数量不足：缺少的参数用默认值。
            else if (argumentLength < parameterLength)
            {
                for (int i = argumentLength; i < parameterLength; i++)
                {
                    sArguments[i] = parameters[i].DefaultValue;
                }
            }

            object[] arguments = GetArgumentArray(parameterLength);
            Array.ConstrainedCopy(sArguments, 0, arguments, 0, parameterLength);
            return method.Invoke(method.IsStatic ? null : instance, arguments);
        }

        public static Array ConvertArray(object[] objectArray, Type targetType)
        {
            if (targetType == typeof(object))
            {
                return objectArray;
            }

            var targetArray = Array.CreateInstance(targetType, objectArray.Length);
            Array.Copy(objectArray, targetArray, objectArray.Length);
            return targetArray;
        }

        #region Parse 转译
        private static readonly Type[] PARSE_ARG = new Type[] { typeof(string) };
        private const string PARSE_NAME = nameof(int.Parse);
        private static readonly MethodInfo mNullGetter = typeof(ActionAttribute).GetMethod(nameof(NoChange));
        private static readonly Dictionary<Type, MethodInfo> mParseMethodCache = new Dictionary<Type, MethodInfo>();
        private static readonly Dictionary<Type, Func<string, object>> mTypeParser = new Dictionary<Type, Func<string, object>>
        {
            { typeof(string), arg => arg },
            { typeof(int), arg => int.Parse(arg) },
            { typeof(bool), arg => bool.Parse(arg) },
            { typeof(float), arg => float.Parse(arg) },
            { typeof(double), arg => double.Parse(arg) },
            { typeof(short), arg => short.Parse(arg) },
            { typeof(long), arg => long.Parse(arg) },
            { typeof(byte), arg => byte.Parse(arg) },
            { typeof(char), arg => char.Parse(arg) },
            { typeof(ulong), arg => ulong.Parse(arg) },
            { typeof(uint), arg => uint.Parse(arg) },
            { typeof(ushort), arg => ushort.Parse(arg) },
            { typeof(sbyte), arg => sbyte.Parse(arg) },
            { typeof(decimal), arg => decimal.Parse(arg) },
            { typeof(DateTime), arg => DateTime.Parse(arg) },
            { typeof(TimeSpan), arg => TimeSpan.Parse(arg) },
        };

        private static object StringToObject(Type targetType, string argument)
        {
            if (mTypeParser.TryGetValue(targetType, out var parser))
            {
                return parser(argument);
            }

            if (mParseMethodCache.TryGetValue(targetType, out var parse))
            {
                return parse.Invoke(null, new object[] { argument });
            }

            parse = targetType.GetMethod(PARSE_NAME, PARSE_ARG);
            if (parse != null)
            {
                mParseMethodCache[targetType] = parse;
            }
            else
            {
                parse = mNullGetter;
                mParseMethodCache[targetType] = parse;
                string msg = $"[ECA] {targetType}无法反序列化{argument}";
                throw new InvalidOperationException(msg);
            }
            return parse.Invoke(null, new object[] { argument });
        }

        private static object NoChange(string input)
        {
            return input;
        }
        #endregion

        #region 数组缓存
        private static object[][] arguments = Array.Empty<object[]>();

        /// <summary>
        /// 为避免频繁new参数数组，将参数数组缓存
        /// </summary>
        public static object[] GetArgumentArray(int count)
        {
            if (count >= arguments.Length)
            {
                int length = count < BUFF_LENGTH ? BUFF_LENGTH : count;
                arguments = new object[length][];
                for (int i = 0; i < length; i++)
                {
                    arguments[i] = new object[i];
                }
            }

            return arguments[count];
        }
        #endregion

        #region 参数缓存
        private readonly static Dictionary<MethodInfo, ParameterInfo[]> mParameterInfoDictionary = new Dictionary<MethodInfo, ParameterInfo[]>();

        private static ParameterInfo[] GetParameters(MethodInfo methodInfo)
        {
            if (mParameterInfoDictionary.TryGetValue(methodInfo, out var parameterInfos))
            {
                return parameterInfos;
            }
            else
            {
                parameterInfos = methodInfo.GetParameters();
                mParameterInfoDictionary[methodInfo] = parameterInfos;
                return parameterInfos;
            }
        }
        #endregion
    }
}