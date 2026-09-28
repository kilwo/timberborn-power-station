using UnityEngine;

namespace CablePowerTransfer.UI
{
    /// <summary>
    /// Same values as the vanilla Configurations/ZiplineSystemColors.blueprint.json (its spec type is internal,
    /// so we can't read it), keeping the cable tool visually consistent with the zipline tool.
    /// </summary>
    public static class CableColors
    {
        public static readonly Color Origin = new Color(0.06127626f, 0.4811321f, 0.3761681f, 1f);
        public static readonly Color Connectable = new Color(0f, 0.5f, 0.06862748f, 1f);
        public static readonly Color NotConnectable = new Color(0.6f, 0f, 0f, 1f);
    }
}
