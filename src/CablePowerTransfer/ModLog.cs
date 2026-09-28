using UnityEngine;

namespace CablePowerTransfer
{
    /// <summary>Writes to Player.log with the consistent [CablePowerTransfer] prefix.</summary>
    public static class ModLog
    {
        private const string Prefix = "[CablePowerTransfer] ";

        public static void Info(string message)
        {
            Debug.Log(Prefix + message);
        }

        public static void Warn(string message)
        {
            Debug.LogWarning(Prefix + message);
        }

        public static void Error(string message)
        {
            Debug.LogError(Prefix + message);
        }
    }
}
