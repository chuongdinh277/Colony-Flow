using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace ColonyFlow
{
    /// <summary>
    /// Represents the game layout as 2D coordinate matrices:
    /// 1. Picture + Yellow Border (-1) 2D array.
    /// 2. Cave Entrance (4 adjacent cells for ants to enter/jump into the nest).
    /// 3. Tray slot positions.
    /// 4. Colony box positions starting from bottom-left (0, 0).
    /// 5. Unified gameplay 2D grid.
    /// </summary>
    public sealed class BoardGrid2D
    {
        public struct CaveEntranceInfo
        {
            public int Index; // 0..3 (left to right)
            public Vector2Int GridCoordinate;
            public Vector2Int UnifiedCoordinate;
            public Vector3 WorldPosition;
        }

        public struct TraySlotInfo
        {
            public int Index;
            public Vector2Int GridCoordinate;
            public Vector3 LocalPosition;
            public Vector3 WorldPosition;
        }

        public struct ColonyBoxInfo
        {
            public Vector2Int Coordinate; // (0, 0) is the bottom-left box!
            public int TileId;
            public int ColorIndex;
            public Color Color;
            public int Count;
            public TileState State;
            public Vector3 WorldPosition;
        }

        // Value conventions in grids:
        public const int CellBorder = -1;
        public const int CellTraySlot = -2;
        public const int CellCaveEntrance = -3;
        public const int CellEmptyMargin = 0;
        // Picture pixels are stored as (colorIndex + 1), so Color 0 = 1, Color 1 = 2, etc.

        public int GridWidth { get; private set; }
        public int GridHeight { get; private set; }
        public int HMargin { get; private set; }
        public int VMargin { get; private set; }
        public int[,] PictureBorderGrid { get; private set; }

        public List<CaveEntranceInfo> CaveEntranceCells { get; } = new();
        public List<TraySlotInfo> TraySlots { get; } = new();
        public List<ColonyBoxInfo> ColonyBoxes { get; } = new();
        public ColonyBoxInfo[,] ColonyBoxGrid { get; private set; }
        public int BoxColumns { get; private set; }
        public int BoxRows { get; private set; }

        // Unified 2D array representing the entire game vertical layout
        public int[,] UnifiedGrid { get; private set; }
        public int UnifiedWidth { get; private set; }
        public int UnifiedHeight { get; private set; }

        public void Build(LevelData level, int hMargin = 3, int vMargin = 3)
        {
            if (level == null) return;

            HMargin = hMargin;
            VMargin = vMargin;
            GridWidth = level.width + 2 * HMargin;
            GridHeight = level.height + 2 * VMargin;

            // 1. Build Picture + Border 2D Array
            PictureBorderGrid = new int[GridWidth, GridHeight];

            for (int x = 0; x < GridWidth; x++)
            {
                PictureBorderGrid[x, 0] = CellBorder;
                PictureBorderGrid[x, GridHeight - 1] = CellBorder;
            }
            for (int y = 0; y < GridHeight; y++)
            {
                PictureBorderGrid[0, y] = CellBorder;
                PictureBorderGrid[GridWidth - 1, y] = CellBorder;
            }

            foreach (PixelData pixel in level.pixels)
            {
                int gx = pixel.position.x + HMargin;
                int gy = pixel.position.y + VMargin;
                if (gx >= 0 && gx < GridWidth && gy >= 0 && gy < GridHeight)
                {
                    PictureBorderGrid[gx, gy] = pixel.colorIndex + 1;
                }
            }

            // 2. Cave Entrance (4 adjacent cells right below picture bottom border)
            CaveEntranceCells.Clear();
            int centerGx = GridWidth / 2;
            Vector3 entranceWorldBase = new(0f, -1.8f, 0f);
            for (int i = 0; i < 4; i++)
            {
                int entranceGx = centerGx - 2 + i;
                float xOffset = (i - 1.5f) * level.cellSize;
                CaveEntranceCells.Add(new CaveEntranceInfo
                {
                    Index = i,
                    GridCoordinate = new Vector2Int(entranceGx, -1),
                    WorldPosition = entranceWorldBase + new Vector3(xOffset, 0f, 0f)
                });
            }

            // 3. Tray Slots
            TraySlots.Clear();
            int capacity = level.trayCapacity > 0 ? level.trayCapacity : 5;
            for (int i = 0; i < capacity; i++)
            {
                int slotGx = centerGx - capacity / 2 + i;
                TraySlots.Add(new TraySlotInfo
                {
                    Index = i,
                    GridCoordinate = new Vector2Int(slotGx, -3),
                    LocalPosition = new Vector3((i - (capacity - 1) * 0.5f) * 0.92f, 0.12f, 0f),
                    WorldPosition = Vector3.zero
                });
            }

            // 4. Colony Boxes from LevelData (4 columns by default, starting from bottom-left (0,0))
            ColonyBoxes.Clear();
            const int maxColumns = 4;
            int columnCount = Mathf.Min(maxColumns, Mathf.Max(1, level.colonyTiles.Count));
            BoxColumns = columnCount;
            var columns = new List<ColonyTileData>[columnCount];
            for (int i = 0; i < columnCount; i++) columns[i] = new List<ColonyTileData>();

            for (int i = 0; i < level.colonyTiles.Count; i++)
            {
                int col = i % columnCount;
                columns[col].Add(level.colonyTiles[i]);
            }

            int maxRows = 0;
            for (int i = 0; i < columnCount; i++)
                if (columns[i].Count > maxRows) maxRows = columns[i].Count;
            BoxRows = maxRows;

            ColonyBoxGrid = new ColonyBoxInfo[BoxColumns, BoxRows];

            for (int col = 0; col < BoxColumns; col++)
            {
                int colCount = columns[col].Count;
                for (int row = 0; row < colCount; row++)
                {
                    // Bottom-left is row 0 -> index in column is colCount - 1 - row
                    int indexInCol = colCount - 1 - row;
                    ColonyTileData data = columns[col][indexInCol];
                    var info = new ColonyBoxInfo
                    {
                        Coordinate = new Vector2Int(col, row),
                        TileId = data.id,
                        ColorIndex = data.colorIndex,
                        Color = level.palette != null ? level.palette.GetColor(data.colorIndex) : Color.white,
                        Count = data.count,
                        State = TileState.Available,
                        WorldPosition = Vector3.zero
                    };
                    ColonyBoxes.Add(info);
                    ColonyBoxGrid[col, row] = info;
                }
            }

            BuildUnifiedGrid();
        }

        public void Build(PixelBoard board, BorderPath border, ColonyTray tray, ColonyTileBoard tileBoard, LevelData level)
        {
            if (board == null || border == null) return;

            HMargin = border.HorizontalMarginCells;
            VMargin = border.VerticalMarginCells;
            GridWidth = board.Width + 2 * HMargin;
            GridHeight = board.Height + 2 * VMargin;

            // 1. Build Picture + Border 2D Array
            PictureBorderGrid = new int[GridWidth, GridHeight];

            // Mark border edges as -1
            for (int x = 0; x < GridWidth; x++)
            {
                PictureBorderGrid[x, 0] = CellBorder;
                PictureBorderGrid[x, GridHeight - 1] = CellBorder;
            }
            for (int y = 0; y < GridHeight; y++)
            {
                PictureBorderGrid[0, y] = CellBorder;
                PictureBorderGrid[GridWidth - 1, y] = CellBorder;
            }

            // Fill pixels inside border
            foreach (PixelCell cell in board.LiveCells)
            {
                int gx = cell.Position.x + HMargin;
                int gy = cell.Position.y + VMargin;
                if (gx >= 0 && gx < GridWidth && gy >= 0 && gy < GridHeight)
                {
                    PictureBorderGrid[gx, gy] = cell.ColorIndex + 1;
                }
            }

            // 2. Cave Entrance (4 adjacent cells right below picture bottom border)
            CaveEntranceCells.Clear();
            int centerGx = GridWidth / 2;
            Vector3 entranceCenter = border.EntranceWorldPosition;
            float step = board.CellSize;
            for (int i = 0; i < 4; i++)
            {
                int entranceGx = centerGx - 2 + i;
                float xOffset = (i - 1.5f) * step;
                CaveEntranceCells.Add(new CaveEntranceInfo
                {
                    Index = i,
                    GridCoordinate = new Vector2Int(entranceGx, -1),
                    WorldPosition = entranceCenter + new Vector3(xOffset, 0f, 0f)
                });
            }

            // 3. Tray Slots Coordinates
            TraySlots.Clear();
            if (tray != null && tray.Slots != null)
            {
                int capacity = tray.Slots.Count;
                for (int i = 0; i < capacity; i++)
                {
                    ColonySlot slot = tray.Slots[i];
                    int slotGx = centerGx - capacity / 2 + i;
                    Vector2Int gridCoord = new(slotGx, -3); // below cave entrance
                    TraySlots.Add(new TraySlotInfo
                    {
                        Index = i,
                        GridCoordinate = gridCoord,
                        LocalPosition = slot != null ? slot.transform.localPosition : Vector3.zero,
                        WorldPosition = slot != null ? slot.transform.position : Vector3.zero
                    });
                }
            }

            // 4. Colony Boxes (starting from bottom-left (0, 0))
            ColonyBoxes.Clear();
            if (tileBoard != null)
            {
                BoxColumns = tileBoard.ColumnCount;
                BoxRows = tileBoard.MaxRowCount;
                ColonyBoxGrid = new ColonyBoxInfo[BoxColumns, BoxRows];

                for (int col = 0; col < BoxColumns; col++)
                {
                    for (int row = 0; row < BoxRows; row++)
                    {
                        ColonyTile tile = tileBoard.GetTileAtBottomLeftCoordinate(col, row);
                        if (tile != null)
                        {
                            var info = new ColonyBoxInfo
                            {
                                Coordinate = new Vector2Int(col, row),
                                TileId = tile.Id,
                                ColorIndex = tile.ColorIndex,
                                Color = level != null && level.palette != null ? level.palette.GetColor(tile.ColorIndex) : Color.white,
                                Count = tile.Count,
                                State = tile.State,
                                WorldPosition = tile.View != null ? tile.View.transform.position : Vector3.zero
                            };
                            ColonyBoxes.Add(info);
                            ColonyBoxGrid[col, row] = info;
                        }
                    }
                }
            }

            // 5. Build Unified 2D Grid
            BuildUnifiedGrid();
        }

        private void BuildUnifiedGrid()
        {
            int boxAreaHeight = Mathf.Max(1, BoxRows);
            int trayRowOffset = boxAreaHeight + 1;
            int caveRowOffset = trayRowOffset + 1; // 1 row above tray slots
            int pictureAreaOffset = caveRowOffset + 2; // gap then picture bottom border

            UnifiedWidth = Mathf.Max(GridWidth, Mathf.Max(BoxColumns, TraySlots.Count + 2));
            UnifiedHeight = pictureAreaOffset + GridHeight;
            UnifiedGrid = new int[UnifiedWidth, UnifiedHeight];

            int pictureXOffset = (UnifiedWidth - GridWidth) / 2;
            int boxXOffset = (UnifiedWidth - BoxColumns) / 2;
            int trayXOffset = (UnifiedWidth - TraySlots.Count) / 2;
            int caveXOffset = UnifiedWidth / 2 - 2;

            // Map Picture + Border
            for (int x = 0; x < GridWidth; x++)
            {
                for (int y = 0; y < GridHeight; y++)
                {
                    UnifiedGrid[x + pictureXOffset, y + pictureAreaOffset] = PictureBorderGrid[x, y];
                }
            }

            // Map Cave Entrance (4 adjacent cells = CellCaveEntrance / -3)
            for (int i = 0; i < 4; i++)
            {
                int cx = caveXOffset + i;
                UnifiedGrid[cx, caveRowOffset] = CellCaveEntrance;
                if (i < CaveEntranceCells.Count)
                {
                    var info = CaveEntranceCells[i];
                    info.UnifiedCoordinate = new Vector2Int(cx, caveRowOffset);
                    CaveEntranceCells[i] = info;
                }
            }

            // Map Tray Slots (value = -2 for Tray Slot)
            for (int i = 0; i < TraySlots.Count; i++)
            {
                UnifiedGrid[trayXOffset + i, trayRowOffset] = CellTraySlot;
            }

            // Map Colony Boxes (value = count, starting from bottom-left (0,0))
            for (int col = 0; col < BoxColumns; col++)
            {
                for (int row = 0; row < BoxRows; row++)
                {
                    if (ColonyBoxGrid != null && ColonyBoxGrid[col, row].Count > 0)
                    {
                        UnifiedGrid[boxXOffset + col, row] = ColonyBoxGrid[col, row].Count;
                    }
                }
            }
        }

        /// <summary>
        /// Logs the 2D grid representation cleanly to Unity Console.
        /// </summary>
        public void LogDebug()
        {
            var sb = new StringBuilder();

            sb.AppendLine("==================================================================================");
            sb.AppendLine("🎮 [COLONY FLOW] 2D BOARD GRID DEBUG");
            sb.AppendLine("==================================================================================");

            // SECTION 1: Picture & Yellow Border Matrix
            sb.AppendLine($"\n🖼️ [1] MA TRẬN ẢNH & ĐƯỜNG BIÊN MÀU VÀNG (Size: {GridWidth}x{GridHeight}, Margin: H={HMargin}, V={VMargin})");
            sb.AppendLine("    • Đường biên màu vàng = -1");
            sb.AppendLine("    • Khoảng trống viền cho kiến đi = 0 (hiển thị '.')");
            sb.AppendLine("    • Pixel ảnh = Mã màu C0, C1, ... (Gốc (0,0) ở DƯỚI CÙNG BÊN TRÁI)");
            sb.AppendLine("----------------------------------------------------------------------------------");

            // Print matrix from top row (GridHeight - 1) down to bottom row (0)
            for (int y = GridHeight - 1; y >= 0; y--)
            {
                sb.Append($"Y={y:D2} | ");
                for (int x = 0; x < GridWidth; x++)
                {
                    int val = PictureBorderGrid[x, y];
                    if (val == CellBorder)
                    {
                        sb.Append(" -1 ");
                    }
                    else if (val == CellEmptyMargin)
                    {
                        sb.Append("  . ");
                    }
                    else
                    {
                        int colorIdx = val - 1;
                        sb.Append($" C{colorIdx} ");
                    }
                }
                sb.AppendLine();
            }

            sb.Append("       ");
            for (int x = 0; x < GridWidth; x++) sb.Append($" X={x:D2}");
            sb.AppendLine();

            // SECTION 2: Cave Entrance (4 adjacent cells)
            sb.AppendLine("\n----------------------------------------------------------------------------------");
            sb.AppendLine("🕳️ [2] VÙNG CỬA HANG (4 Ô ĐỨNG CẠNH NHAU ĐỂ KIẾN NHẢY VÀO)");
            sb.AppendLine("    • Vị trí: Ở giữa bàn cờ, nằm ngay trên hàng khay và phía dưới tranh");
            sb.AppendLine("    • Ký hiệu trong Unified Grid: [ H]");
            sb.AppendLine("----------------------------------------------------------------------------------");
            for (int i = 0; i < CaveEntranceCells.Count; i++)
            {
                CaveEntranceInfo cell = CaveEntranceCells[i];
                sb.AppendLine($"  ▶ Ô Cửa Hang [{i}]: Toạ độ Grid={cell.GridCoordinate}, Unified={cell.UnifiedCoordinate}, WorldPos={cell.WorldPosition}");
            }

            // SECTION 3: Tray Slots
            sb.AppendLine("\n----------------------------------------------------------------------------------");
            sb.AppendLine($"📥 [3] VỊ TRÍ CÁC KHAY XẾP BOX KIẾN (Tổng cộng: {TraySlots.Count} khay)");
            sb.AppendLine("----------------------------------------------------------------------------------");
            for (int i = 0; i < TraySlots.Count; i++)
            {
                TraySlotInfo slot = TraySlots[i];
                sb.AppendLine($"  ▶ Khay [{i}]: Toạ độ Grid={slot.GridCoordinate}, LocalPos={slot.LocalPosition}, WorldPos={slot.WorldPosition}");
            }

            // SECTION 4: Colony Boxes (from bottom-left (0, 0))
            sb.AppendLine("\n----------------------------------------------------------------------------------");
            sb.AppendLine($"📦 [4] VỊ TRÍ TỪNG BOX KIẾN (Lưới {BoxColumns} cột x {BoxRows} hàng)");
            sb.AppendLine("    ★ BẮT ĐẦU TỪ BOX DƯỚI CÙNG BÊN TRÁI LÀ TOẠ ĐỘ (0, 0) ★");
            sb.AppendLine("----------------------------------------------------------------------------------");

            for (int row = BoxRows - 1; row >= 0; row--)
            {
                string rowName = row switch
                {
                    0 => "Hàng 0 (Dưới cùng)",
                    1 => "Hàng 1 (Ở giữa)   ",
                    2 => "Hàng 2 (Trên cùng) ",
                    _ => $"Hàng {row}            "
                };
                sb.Append($"  {rowName} | ");
                for (int col = 0; col < BoxColumns; col++)
                {
                    if (ColonyBoxGrid != null)
                    {
                        ColonyBoxInfo box = ColonyBoxGrid[col, row];
                        if (box.Count > 0)
                        {
                            sb.Append($"[({col},{row}) C{box.ColorIndex}:x{box.Count,2}] ");
                        }
                        else
                        {
                            sb.Append($"[({col},{row}) Empty ] ");
                        }
                    }
                }
                sb.AppendLine();
            }

            sb.AppendLine("\n  Chi tiết từng Box theo toạ độ (col, row):");
            foreach (ColonyBoxInfo box in ColonyBoxes)
            {
                sb.AppendLine($"    • Box toạ độ ({box.Coordinate.x}, {box.Coordinate.y}): ID={box.TileId}, Màu C{box.ColorIndex}, Số lượng={box.Count} kiến, WorldPos={box.WorldPosition}");
            }

            // SECTION 5: Full Unified Grid
            sb.AppendLine("\n----------------------------------------------------------------------------------");
            sb.AppendLine($"🗺️ [5] MA TRẬN TỔNG HỢP TOÀN BỘ GAMEPLAY (Unified Grid {UnifiedWidth}x{UnifiedHeight})");
            sb.AppendLine("    [-1] = Viền vàng | [C#] = Pixel tranh | [ H] = Cửa hang (4 ô) | [ K] = Khay | [Số] = Số lượng Box kiến");
            sb.AppendLine("----------------------------------------------------------------------------------");

            for (int y = UnifiedHeight - 1; y >= 0; y--)
            {
                sb.Append($"Y={y:D2} | ");
                for (int x = 0; x < UnifiedWidth; x++)
                {
                    int val = UnifiedGrid[x, y];
                    if (val == CellBorder) sb.Append(" -1 ");
                    else if (val == CellCaveEntrance) sb.Append(" [H]");
                    else if (val == CellTraySlot) sb.Append(" [K]");
                    else if (val == CellEmptyMargin) sb.Append("  . ");
                    else if (val > 0 && y < BoxRows) sb.Append($"{val,3} ");
                    else sb.Append($" C{val - 1} ");
                }
                sb.AppendLine();
            }

            sb.AppendLine("==================================================================================");

            Debug.Log(sb.ToString());
        }
    }
}
