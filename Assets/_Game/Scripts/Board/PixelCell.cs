using UnityEngine;

namespace ColonyFlow
{
    public sealed class PixelCell
    {
        public Vector2Int Position { get; }
        public int ColorIndex { get; }
        public bool IsDestroyed { get; private set; }
        public AntAgent ReservedBy { get; private set; }
        public PixelCellView View { get; set; }

        public bool IsReserved => ReservedBy != null;

        public PixelCell(Vector2Int position, int colorIndex)
        {
            Position = position;
            ColorIndex = colorIndex;
        }

        public bool TryReserve(AntAgent ant)
        {
            if (IsDestroyed || ReservedBy != null || ant == null) return false;
            ReservedBy = ant;
            return true;
        }

        public void Release(AntAgent ant)
        {
            if (ReservedBy == ant) ReservedBy = null;
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
