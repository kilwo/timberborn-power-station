using Timberborn.SingletonSystem;

namespace RopePower
{
    /// <summary>Phase 0 smoke test: proves the configurator was discovered and the Game context loaded it.</summary>
    public class RopePowerLoadMarker : ILoadableSingleton
    {
        public void Load()
        {
            ModLog.Info("loaded");
        }
    }
}
