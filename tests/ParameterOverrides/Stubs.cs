namespace UnityEngine
{
    public class Component
    {
        public int Id, IdReads;
        public bool ForbidIdRead;
        public int GetInstanceID() { IdReads++; if (ForbidIdRead) throw new Exception("Unexpected native interop call"); return Id; }
    }
}
namespace BepInEx.Configuration
{
    public sealed class ConfigEntry<T>(T initial) { public T Value = initial; }
    public sealed class ConfigFile
    {
        public readonly Dictionary<string, object> Entries = new();
        public ConfigEntry<T> Bind<T>(string section, string key, T initial, object description)
        { var entry = new ConfigEntry<T>(initial); Entries[section+"/"+key]=entry; return entry; }
        public ConfigEntry<T> Entry<T>(string key) => (ConfigEntry<T>)Entries[key];
    }
}
public sealed class Ant_Player { }
public sealed class PixelEasyCharMoveKartController { public Ant_Player? parentPlayer; }
namespace TK2.Customization
{
    internal static partial class LegacyMK
    {
        private sealed class TestPlugin { internal readonly BepInEx.Configuration.ConfigFile Config = new(); }
        private static readonly TestPlugin _plugin = new();
        internal static BepInEx.Configuration.ConfigFile TestConfig => _plugin.Config;
        private static BepInEx.Configuration.ConfigEntry<bool> Toggle(string section, string text) => _plugin.Config.Bind(section,"Enabled",false,text);
        private static BepInEx.Configuration.ConfigEntry<float> Number(string section,string key,float value,float min,float max,string text) => _plugin.Config.Bind(section,key,value,text);
        private static bool Allowed(Ant_Player? player) => player != null;
        private static void Fault(BepInEx.Configuration.ConfigEntry<bool> entry,Exception error) => throw error;
        private static void Capability(string name,Action action) => action();
        private static void Hook(Type type,string method,Type[] args,string hook,bool prefix) { }
    }
}
