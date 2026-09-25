using UnityEngine;

namespace RopePower
{
    /// <summary>Writes to Player.log with the consistent [RopePower] prefix.</summary>
    public static class ModLog
    {
        private const string Prefix = "[RopePower] ";

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
