using System.Reflection;
using System.Runtime.Loader;

if (args.Length != 2) throw new ArgumentException("Usage: RuntimeHooks <built-plugin.dll> <game-directory>");
var pluginPath = Path.GetFullPath(args[0]);
var game = Path.GetFullPath(args[1]);
var directories = new[] { Path.GetDirectoryName(pluginPath)!, Path.Combine(game, "BepInEx", "interop"), Path.Combine(game, "BepInEx", "core") };
AssemblyLoadContext.Default.Resolving += (_, name) => {
    foreach (var directory in directories) {
        var candidate = Path.Combine(directory, name.Name + ".dll");
        if (File.Exists(candidate)) return AssemblyLoadContext.Default.LoadFromAssemblyPath(candidate);
    }
    return null;
};
var assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(pluginPath);
const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
int count = 0;
foreach (var feature in new[] { "AirShiftFeature", "MineTrapFeature" }) {
    var type = assembly.GetType("TK2.Customization." + feature, true)!;
    var hooks = (Array)type.GetMethod("Hooks", flags)!.Invoke(null, null)!;
    foreach (var hook in hooks) {
        var spec = hook!.GetType();
        var target = (MethodInfo)spec.GetMethod("Resolve", flags)!.Invoke(hook, null)!;
        var parameters = target.GetParameters();
        foreach (var kind in new[] { "Prefix", "Postfix", "Finalizer" }) {
            var name = (string?)spec.GetProperty(kind)!.GetValue(hook);
            if (name == null) continue;
            var handler = type.GetMethod(name, flags) ?? throw new Exception($"Missing handler {feature}.{name}");
            foreach (var parameter in handler.GetParameters()) {
                var n = parameter.Name!;
                Type expected;
                if (n == "__instance") expected = target.DeclaringType!;
                else if (n == "__result") expected = target.ReturnType;
                else if (n.StartsWith("__") && int.TryParse(n.AsSpan(2), out int index)) expected = parameters[index].ParameterType;
                else if (n == "__state") continue;
                else throw new Exception($"Unverified Harmony parameter {feature}.{name}.{n}");
                var actual = parameter.ParameterType;
                if (actual.IsByRef) actual = actual.GetElementType()!;
                if (expected.IsByRef) expected = expected.GetElementType()!;
                if (actual != expected) throw new Exception($"Hook mismatch: {feature}.{name}.{n}: {actual} != {expected}");
            }
        }
        Console.WriteLine($"PASS {feature}: {target}");
        count++;
    }
}
Console.WriteLine($"{count} actual runtime hook signatures and all Harmony argument types verified. No game methods executed.");
