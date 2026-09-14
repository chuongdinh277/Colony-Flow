using System.Collections.Generic;

namespace ColonyFlow
{
    public sealed class ColonyTile
    {
        private readonly ColonyTileData data;
        public int Id => data.id;
        public int ColorIndex => data.colorIndex;
        public int Count => data.count;
        public bool Hidden => data.hidden;
        public TileState State { get; private set; }
        public ColonyTileView View { get; set; }

        public ColonyTile(ColonyTileData data)
        {
            this.data = data;
            State = TileState.Locked;
        }

        public void RefreshAvailability(IReadOnlyDictionary<int, ColonyTile> allTiles)
        {
            if (State == TileState.InTray || State == TileState.Completed) return;
            foreach (int coveringId in data.coveredBy)
            {
                if (allTiles.TryGetValue(coveringId, out ColonyTile covering) && covering.State != TileState.InTray && covering.State != TileState.Completed)
                {
                    State = TileState.Locked;
                    View?.Refresh();
                    return;
                }
            }
            State = TileState.Available;
            View?.Refresh();
        }

        public void MoveToTray()
        {
            State = TileState.InTray;
            View?.Refresh();
        }

        public void SetColumnAvailability(bool available)
        {
            if (State == TileState.InTray || State == TileState.Completed) return;
            State = available ? TileState.Available : TileState.Locked;
            View?.Refresh();
        }

        public void Complete()
        {
            State = TileState.Completed;
            View?.Refresh();
        }
    }
}
