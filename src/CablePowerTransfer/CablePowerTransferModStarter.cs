using System;
using HarmonyLib;
using Timberborn.ModManagerScene;

namespace CablePowerTransfer
{
    /// <summary>Mod entry point (Timberborn.ModManagerScene.ModCodeStarter): applies the Harmony patches in Patches/.</summary>
    public class CablePowerTransferModStarter : IModStarter
    {
        public void StartMod(IModEnvironment modEnvironment)
        {
            try
            {
                new Harmony("Elum.CablePowerTransfer").PatchAll(typeof(CablePowerTransferModStarter).Assembly);
                ModLog.Info("Harmony patches applied");
            }
            catch (Exception e)
            {
                ModLog.Error($"Harmony patching failed, cables will not carry power: {e}");
            }
        }
    }
}
