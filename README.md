通过简单的添加一个属性，让函数自动注册到字典中。

需要配合ECAMap生成器使用，自动生成索引代码。

目前还没有实现存Delegate的索引，存的还是MethodInfo，因此有一定额外性能开销。

待有相关需求后，再改为生成Delegate索引表提高性能。

### 性能测试结果：
![image](https://github.com/tly1378/eca/assets/63791112/005473dc-da24-4884-a6f9-9885e5afa130)


### 性能测试代码
```cs
using System.Diagnostics;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;

namespace ConsoleApp
{
    public class Program
    {
        private int test;

        public void TestMethod()
        {
            test ++;
        }

        public static void Main()
        {
            Program instance = new();
            Action methodDelegate = instance.TestMethod;
            MethodInfo? methodInfo = typeof(Program).GetMethod("TestMethod");

            if (methodInfo != null)
            {
                int iterations = 10000000;

                // 直接调用
                Stopwatch stopwatch = new();
                stopwatch.Start();
                for (int i = 0; i < iterations; i++)
                {
                    instance.TestMethod();
                }
                stopwatch.Stop();
                Console.WriteLine("Direct Call: " + stopwatch.ElapsedMilliseconds + " ms");

                // 委托调用
                stopwatch.Restart();
                for (int i = 0; i < iterations; i++)
                {
                    methodDelegate();
                }
                stopwatch.Stop();
                Console.WriteLine("Delegate.Invoke: " + stopwatch.ElapsedMilliseconds + " ms");

                // DynamicMethod 调用
                DynamicMethod dynamicMethod = new("InvokeMethod", null, [typeof(Program)], typeof(Program));
                ILGenerator il = dynamicMethod.GetILGenerator();
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Call, methodInfo);
                il.Emit(OpCodes.Ret);
                var action = (Action<Program>)dynamicMethod.CreateDelegate(typeof(Action<Program>));
                stopwatch.Restart();
                for (int i = 0; i < iterations; i++)
                {
                    action(instance);
                }
                stopwatch.Stop();
                Console.WriteLine("DynamicMethod: " + stopwatch.ElapsedMilliseconds + " ms");

                // 表达式树调用
                var methodCall = Expression.Call(Expression.Constant(instance), methodInfo);
                var lambda = Expression.Lambda<Action>(methodCall);
                var compiled = lambda.Compile();
                stopwatch.Restart();
                for (int i = 0; i < iterations; i++)
                {
                    compiled();
                }
                stopwatch.Stop();
                Console.WriteLine("Expression Tree: " + stopwatch.ElapsedMilliseconds + " ms");

                // 反射调用
                stopwatch.Restart();
                for (int i = 0; i < iterations; i++)
                {
                    methodInfo.Invoke(instance, null);
                }
                stopwatch.Stop();
                Console.WriteLine("Reflection: " + stopwatch.ElapsedMilliseconds + " ms");
            }

            Console.WriteLine("Number of traversals: " + instance.test);
        }
    }
}
```
