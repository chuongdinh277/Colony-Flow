using System;
using UnityEngine;

namespace ColonyFlow
{
    public enum GameState
    {
        None,
        Loading,
        MainMenu,
        Playing,
        Paused,
        Victory,
        Failed,
        Pause = Paused,
        Lose = Failed
    }
    public enum AntState { Inactive, ToBorder, OnBorder, ToPixel, Attacking, Returning, Completed }
    public enum ColonyState { Waiting, Active, Blocked, Completed }
    public enum TileState { Locked, Available, InTray, Completed }

    [Serializable]
    public struct PaletteColor
    {
        public string id;
        public Color color;

        public PaletteColor(string id, Color color)
        {
            this.id = id;
            this.color = color;
        }
    }

    public static class GridDirections
    {
        public static readonly Vector2Int[] Four =
        {
            Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left
        };
    }
}
