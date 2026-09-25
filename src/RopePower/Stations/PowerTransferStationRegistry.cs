using System;
using System.Collections.Generic;

namespace RopePower.Stations
{
    /// <summary>All live stations, plus the order in which they finished construction.</summary>
    public class PowerTransferStationRegistry
    {
        private readonly List<PowerTransferStation> _stations = new List<PowerTransferStation>();
        private readonly List<PowerTransferStation> _finishedOrder = new List<PowerTransferStation>();

        public IReadOnlyList<PowerTransferStation> Stations => _stations;

        public event Action<PowerTransferStation> StationFinished;

        public void Add(PowerTransferStation station)
        {
            _stations.Add(station);
        }

        public void Remove(PowerTransferStation station)
        {
            _stations.Remove(station);
            _finishedOrder.Remove(station);
        }

        public void MarkFinished(PowerTransferStation station)
        {
            _finishedOrder.Remove(station);
            _finishedOrder.Add(station);
            StationFinished?.Invoke(station);
        }

        /// <summary>Most recently finished station first.</summary>
        public IEnumerable<PowerTransferStation> MostRecentlyFinished()
        {
            for (int i = _finishedOrder.Count - 1; i >= 0; i--)
            {
                yield return _finishedOrder[i];
            }
        }
    }
}
