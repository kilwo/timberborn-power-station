using System;
using HarmonyLib;
using Timberborn.ModManagerScene;

namespace RopePower
{
    /// <summary>Mod entry point (Timberborn.ModManagerScene.ModCodeStarter): applies the Harmony patches in Patches/.</summary>
    public class RopePowerModStarter : IModStarter
    {
        public void StartMod(IModEnvironment modEnvironment)
        {
            try
            {
                new Harmony("Elum.RopePower").PatchAll(typeof(RopePowerModStarter).Assembly);
                ModLog.Info("Harmony patches applied");
            }
            catch (Exception e)
            {
                ModLog.Error($"Harmony patching failed, ropes will not carry power: {e}");
            }
        }
    }
}
