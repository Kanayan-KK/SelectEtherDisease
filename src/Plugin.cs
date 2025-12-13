using System.Reflection;
using System.Runtime.CompilerServices;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;

namespace SelectEtherDisease;

public static class ModInfo
{
    public const string Guid = "Elin.SelectEtherDisease";
    public const string Name = "Select Ether Disease";
    public const string Version = "1.0.1";
}

[BepInPlugin(ModInfo.Guid, ModInfo.Name, ModInfo.Version)]
internal class Plugin : BaseUnityPlugin
{
    internal static Plugin? Instance;
    internal ConfigEntry<bool>? EnableForPlayer;
    internal ConfigEntry<bool>? EnableForMember;
    internal ConfigEntry<bool>? EnableForOther;

    private void Awake()
    {
        Instance = this;
        EnableForPlayer = Config.Bind("General", "EnableForPlayer", true, "Enable selection for Player.");
        EnableForMember = Config.Bind("General", "EnableForMember", true, "Enable selection for Party Members.");
        EnableForOther = Config.Bind("General", "EnableForOther", false, "Enable selection for Others (NPCs,Enemies).");
        Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), ModInfo.Guid);
    }

    internal static void LogDebug(object message, [CallerMemberName] string caller = "")
    {
        Instance?.Logger.LogDebug($"[{caller}] {message}");
    }

    internal static void LogInfo(object message)
    {
        Instance?.Logger.LogInfo(message);
    }

    internal static void LogError(object message)
    {
        Instance?.Logger.LogError(message);
    }
}