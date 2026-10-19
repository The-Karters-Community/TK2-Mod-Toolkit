using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace TK2.Customization;

// Resolve the complete feature before installing any patch. Tests use these same bindings
// against the installed interop DLL; IL2CPP dump spellings are not runtime signatures.
internal sealed record FeatureHook(Type Target, string Name, Type[] Parameters,
    string? Prefix = null, string? Postfix = null, string? Finalizer = null)
{
    internal MethodInfo Resolve() => AccessTools.DeclaredMethod(Target, Name, Parameters)
        ?? throw new MissingMethodException($"{Target.FullName}.{Name}({string.Join<Type>(", ", Parameters)})");

    internal static void Install(Harmony harmony, Type handlers, FeatureHook[] hooks)
    {
        var resolved = new List<(MethodInfo Target, HarmonyMethod? Prefix, HarmonyMethod? Postfix, HarmonyMethod? Finalizer)>();
        HarmonyMethod? Handler(string? name) => name == null ? null : new HarmonyMethod(
            AccessTools.DeclaredMethod(handlers, name) ?? throw new MissingMethodException(handlers.FullName, name));
        foreach (var hook in hooks)
            resolved.Add((hook.Resolve(), Handler(hook.Prefix), Handler(hook.Postfix), Handler(hook.Finalizer)));
        try {
            foreach (var hook in resolved) harmony.Patch(hook.Target, prefix: hook.Prefix, postfix: hook.Postfix, finalizer: hook.Finalizer);
        } catch {
            foreach (var hook in resolved)
                foreach (var handler in new[] { hook.Prefix, hook.Postfix, hook.Finalizer })
                    if (handler != null) harmony.Unpatch(hook.Target, handler.method);
            throw;
        }
    }
}
