using System;
using System.Reflection;
using HarmonyLib;
using PeterHan.PLib.Core;

namespace UtilLibs
{
    /// <summary>
    /// Reflection-based patch registration: resolves a method by name and parameter
    /// types and attaches Harmony prefix/postfix/transpiler patches, logging a skip
    /// instead of throwing when the target method cannot be found (e.g. the game
    /// build was updated and the method was renamed or removed).
    /// </summary>
    public static class PatchUtil
    {
        /// <summary>
        /// Tries to attach Harmony patches to a method resolved by reflection.
        /// </summary>
        /// <param name="harmony">The Harmony instance to register the patches with.</param>
        /// <param name="type">The type declaring the target method.</param>
        /// <param name="name">The exact name of the target method.</param>
        /// <param name="paramTypes">The parameter types of the target method (pass plain
        /// types; byref/out parameters are normalized by the lookup). Non-null array.</param>
        /// <param name="feature">A short human-readable feature name, used in log messages.</param>
        /// <param name="prefix">Optional Harmony prefix method.</param>
        /// <param name="postfix">Optional Harmony postfix method.</param>
        /// <param name="transpiler">Optional Harmony transpiler method.</param>
        /// <returns>
        /// <c>true</c> if the method was resolved and the patch registered;
        /// <c>false</c> if the method could not be resolved (an error is logged
        /// and the feature is skipped).
        /// </returns>
        public static bool TryPatch(Harmony harmony, Type type, string name, Type[] paramTypes, string feature, HarmonyMethod prefix = null, HarmonyMethod postfix = null, HarmonyMethod transpiler = null)
        {
            MethodInfo method = ReflectionUtil.FindMethod(type, name, paramTypes);

            string sig = BuildSignature(type, name, paramTypes);

            if (method == null)
            {
                PUtil.LogError("could not resolve {0} — {1} skipped (game build mismatch?)".F(sig, feature));
#if DEBUG
                PUtil.LogDebug("patch skipped: could not resolve {0}".F(sig));
#endif
                return false;
            }

            harmony.Patch(method, prefix: prefix, postfix: postfix, transpiler: transpiler);
#if DEBUG
            PUtil.LogDebug("{0} patch attached to {1}".F(feature, sig));
#endif
            return true;
        }

        /// <summary>
        /// Formats a signature string "<TypeName>.<name>(<arg1>, <arg2>, ...)".
        /// </summary>
        private static string BuildSignature(Type type, string name, Type[] paramTypes)
        {
            string args = string.Join(", ", Array.ConvertAll(paramTypes, FormatTypeName));
            return type.Name + "." + name + "(" + args + ")";
        }

        /// <summary>
        /// Formats a parameter type for display: plain <see cref="Type.Name"/> for
        /// non-generic types, "List&lt;...&gt;" style for generic instances.
        /// </summary>
        private static string FormatTypeName(Type t)
        {
            if (t == null)
            {
                return "object";
            }
            if (t.IsGenericType && !t.IsGenericTypeDefinition)
            {
                Type[] genericArgs = t.GetGenericArguments();
                string genericNames = string.Join(", ", Array.ConvertAll(genericArgs, FormatTypeName));
                // "List`1" -> "List"
                string baseName = t.GetGenericTypeDefinition().Name;
                int backtick = baseName.IndexOf('`');
                if (backtick > 0)
                {
                    baseName = baseName.Substring(0, backtick);
                }
                return baseName + "<" + genericNames + ">";
            }
            return t.Name;
        }
    }
}
