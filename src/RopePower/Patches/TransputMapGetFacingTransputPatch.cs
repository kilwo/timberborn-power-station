// Game method: Timberborn.MechanicalSystem.TransputMap.GetFacingTransput(Transput transput) : Transput
// Written against Timberborn 1.1.2.4 (Timberborn.MechanicalSystem.dll). See docs/game-api-notes.md §1.
//
// Vanilla returns the adjacent transput that faces the given one. MechanicalGraphManager.AddNode uses the result to
// connect transputs and join mechanical graphs, and MechanicalGraphReorganizer later follows those connections.
// For a Power Transfer Station rope slot (which vanilla never connects, it faces into the station itself) we return
// the linked partner's paired slot instead, so the two stations join one network through the game's own code.

using System;
using HarmonyLib;
using RopePower.Stations;
using Timberborn.Coordinates;
using Timberborn.MechanicalSystem;

namespace RopePower.Patches
{
    [HarmonyPatch(typeof(TransputMap), nameof(TransputMap.GetFacingTransput))]
    internal static class TransputMapGetFacingTransputPatch
    {
        private static bool _errorLogged;

        private static void Postfix(Transput transput, ref Transput __result)
        {
            // Cheap filters first: vanilla found a neighbour, or this can't be a rope slot (slots face Bottom).
            if (__result != null || transput == null || transput.BaseDirection != Direction3D.Bottom)
            {
                return;
            }
            try
            {
                // ParentNode is null for the throwaway transputs MechanicalNodeSelfMarkerDrawer builds from specs.
                PowerTransferStation station = transput.ParentNode == null
                    ? null
                    : transput.ParentNode.GetComponent<PowerTransferStation>();
                if (station != null)
                {
                    __result = station.GetRopePartnerTransput(transput);
                }
            }
            catch (Exception e)
            {
                // Fail safe: never break the game's graph code; worst case the rope carries no power.
                if (!_errorLogged)
                {
                    _errorLogged = true;
                    ModLog.Error($"GetFacingTransput patch failed, ropes may not carry power: {e}");
                }
            }
        }
    }
}
