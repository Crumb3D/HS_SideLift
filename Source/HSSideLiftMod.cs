using System;
using System.IO;
using System.Reflection;
using HarmonyLib;

public class HSSideLiftMod : IModApi
{
    public static string ModPath;
    public static string UserDataPath;

    public void InitMod(Mod _modInstance)
    {
        if (!HSGameVersion.AllowLoad("[HSSideLift]"))
            return;
        ModPath = _modInstance.Path;
        try
        {
            UserDataPath = Path.Combine(GameIO.GetUserGameDataDir(), "HSSideLift");
            Directory.CreateDirectory(UserDataPath);
            HSSideLiftConfiguration.EvacuateRuntimeFilesFromModFolder();
        }
        catch (Exception e)
        {
            HSSideLiftDebug.Error("Could not move lift save out of the mod folder", e);
        }
        HSSideLiftDebug.Info("Init v1.0.14 - one owner per panel; admin: hssidelift");
        HSSideLiftNet.RegisterPackage();
        ModEvents.GameStartDone.RegisterHandler(OnGameStartDone);
        ModEvents.WorldShuttingDown.RegisterHandler(OnWorldShuttingDown);
        ModEvents.PlayerSpawnedInWorld.RegisterHandler(HSSideLiftNet.OnPlayerSpawned);
        try
        {
            new Harmony("HSSideLift").PatchAll(Assembly.GetExecutingAssembly());
        }
        catch (Exception e)
        {
            HSSideLiftDebug.Error("Harmony patch failed (lift doors will not lock)", e);
        }
    }

    static void OnGameStartDone(ref ModEvents.SGameStartDoneData data)
    {
        try
        {
            HSSideLiftSettings.Load();
            HSSideLiftConfiguration.Load();
            HSSideLiftController.EnsureCreated();
        }
        catch (Exception e)
        {
            HSSideLiftDebug.Error("Start failed", e);
        }
    }

    static void OnWorldShuttingDown(ref ModEvents.SWorldShuttingDownData data)
    {
        try
        {
            HSSideLiftController.OnWorldShuttingDown();
        }
        catch (Exception e)
        {
            HSSideLiftDebug.Error("Shutdown handling failed", e);
        }
    }
}
