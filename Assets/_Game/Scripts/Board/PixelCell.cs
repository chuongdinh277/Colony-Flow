using UnityEngine;

namespace ColonyFlow
{
    public sealed class PixelCell
    {
        public Vector2Int Position { get; }
        public int ColorIndex { get; }
        public bool IsDestroyed { get; private set; }
        public object ReservedBy { get; private set; }
        public PixelCellView View { get; set; }

        public bool IsReserved => ReservedBy != null;

        public PixelCell(Vector2Int position, int colorIndex)
        {
            Position = position;
            ColorIndex = colorIndex;
        }

        public bool TryReserve(object owner)
        {
            if (IsDestroyed || ReservedBy != null || owner == null) return false;
            ReservedBy = owner;
            return true;
        }

        public bool TryTransferReservation(object currentOwner, object nextOwner)
        {
            if (IsDestroyed || ReservedBy != currentOwner || nextOwner == null) return false;
            ReservedBy = nextOwner;
            return true;
        }

        public void Release(object owner)
        {
            if (ReservedBy == owner) ReservedBy = null;
        }

        public PixelCellView Collect()
        {
            IsDestroyed = true;
            ReservedBy = null;
            PixelCellView collectedView = View;
            View = null;
            return collectedView;
        }
    }
}
