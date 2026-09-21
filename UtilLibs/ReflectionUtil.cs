using System;
using System.Reflection;

namespace UtilLibs
{
    public static class ReflectionUtil
    {
        /// <summary>
        /// Scans the declared methods of <paramref name="type"/> (public and nonpublic,
        /// instance and static, declared-only — not inherited) and returns the first
        /// method whose name exactly matches <paramref name="name"/> and whose
        /// parameter count exactly matches <paramref name="underlyingTypes"/>. Each
        /// parameter's underlying type is compared — a byref parameter (T& / out T) is
        /// normalized to its element type T before the comparison — so the caller may
        /// pass plain parameter types. Returns <c>null</c> when no method matches;
        /// callers are expected to log a skip rather than crash.
        /// </summary>
        public static MethodInfo FindMethod(Type type, string name, params Type[] underlyingTypes)
        {
            foreach (MethodInfo m in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            {
                if (m.Name != name)
                {
                    continue;
                }
                ParameterInfo[] ps = m.GetParameters();
                if (ps.Length != underlyingTypes.Length)
                {
                    continue;
                }
                bool match = true;
                for (int i = 0; i < ps.Length; i++)
                {
                    Type pt = ps[i].ParameterType;
                    if (pt.IsByRef)
                    {
                        pt = pt.GetElementType();
                    }
                    if (pt != underlyingTypes[i])
                    {
                        match = false;
                        break;
                    }
                }
                if (match)
                {
                    return m;
                }
            }
            return null;
        }
    }
}
