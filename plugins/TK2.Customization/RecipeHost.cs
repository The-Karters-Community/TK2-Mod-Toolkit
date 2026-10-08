using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;

namespace TK2.Customization;

// Add a public IModRecipe class to Recipes/. It is discovered inside this same DLL.
public interface IModRecipe
{
    string Name { get; }
    bool ChangesGameplay { get; }
    void Configure(ConfigFile config);
    void Tick();
    void Restore();
}

internal static class RecipeHost
{
    private static readonly List<(IModRecipe Recipe, ConfigEntry<bool> Enabled)> Recipes = new();
    internal static void Install(Plugin plugin)
    {
        foreach (Type type in typeof(Plugin).Assembly.GetTypes())
        {
            if (type.IsAbstract || !typeof(IModRecipe).IsAssignableFrom(type)) continue;
            try
            {
                var recipe = (IModRecipe)Activator.CreateInstance(type)!;
                recipe.Configure(plugin.Config);
                Recipes.Add((recipe, plugin.Config.Bind("Recipe." + recipe.Name, "Enabled", false, "Enable this custom recipe.")));
            }
            catch (Exception ex) { plugin.Log.LogError($"Recipe {type.Name}: {ex}"); }
        }
    }

    internal static void Tick()
    {
        foreach (var entry in Recipes)
        {
            bool allowed = entry.Enabled.Value && (!entry.Recipe.ChangesGameplay || Plugin.OfflineLabAllowed);
            try
            {
                if (allowed)
                {
                    if (entry.Recipe.ChangesGameplay) Plugin.Instance!.SessionModified = true;
                    entry.Recipe.Tick();
                }
                else entry.Recipe.Restore();
            }
            catch (Exception ex)
            {
                entry.Enabled.Value = false;
                Plugin.Instance!.Log.LogError($"Recipe {entry.Recipe.Name} disabled: {ex}");
                try { entry.Recipe.Restore(); } catch { }
            }
        }
    }

    internal static void Restore()
    {
        foreach (var entry in Recipes) try { entry.Recipe.Restore(); } catch { }
        Recipes.Clear();
    }
}
