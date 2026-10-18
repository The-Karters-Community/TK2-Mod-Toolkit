namespace TK2.Customization;

public sealed partial class Plugin
{
    // A fast-fall native-build check must not disable unrelated recipes or camera effects.
    // OnlineProtection is installed before any modules; gameplay remains offline only.
    internal static bool OfflineLabAllowed => Instance != null &&
        !Ant_CurrentGameConfiguration.IsOnlineGame_InRoom_WithInternet;
}
