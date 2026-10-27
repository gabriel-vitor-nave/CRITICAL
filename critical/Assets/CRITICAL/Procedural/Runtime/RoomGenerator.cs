using System;

using System.Collections.Generic;

using UnityEngine;

namespace Critical.Procedural

{

    public enum RoomCell { Floor, Void, Wall, Door }

    public enum DoorDirection { North, East, South, West }

    [Serializable]

    public struct DoorPlacement

    {

        public Vector2Int Cell;

        public DoorDirection Direction;

        public DoorPlacement(Vector2Int cell, DoorDirection direction)

        {

            Cell = cell;

            Direction = direction;

        }

        // Both door tiles lie side by side on the same wall.
        // Cell is the first north-oriented door piece, rotated by Direction.
        public Vector2Int SecondCell => Cell + Tangent(Direction);

        // Floor approach, not part of the door footprint.
        public Vector2Int InnerCell => Cell - Outward(Direction);

        public static Vector2Int Tangent(DoorDirection direction)
        {
            switch (direction)
            {
                case DoorDirection.North: return Vector2Int.right;
                case DoorDirection.East: return Vector2Int.down;
                case DoorDirection.South: return Vector2Int.left;
                case DoorDirection.West: return Vector2Int.up;
                default: throw new ArgumentOutOfRangeException(nameof(direction));
            }
        }

        public static Vector2Int Outward(DoorDirection direction)

        {

            switch (direction)

            {

                case DoorDirection.North: return Vector2Int.up;

                case DoorDirection.East: return Vector2Int.right;

                case DoorDirection.South: return Vector2Int.down;

                case DoorDirection.West: return Vector2Int.left;

                default: throw new ArgumentOutOfRangeException(nameof(direction));

            }

        }

    }

    [Serializable]

    public struct RoomGenerationProfile

    {

        public int MinWidth, MaxWidth, MinHeight, MaxHeight;

        public float VoidFraction;

        public int VoidRegions, WalkLength;

    }

    [Serializable]

    public class RoomGenerationSettings

    {

        public const int MinimumWidth = 12;

        public const int MinimumHeight = 10;

        public const int MaximumDimension = 96;

        [Min(MinimumWidth)] public int minWidth = 14;

        [Min(MinimumWidth)] public int maxWidth = 28;

        [Min(MinimumHeight)] public int minHeight = 10;

        [Min(MinimumHeight)] public int maxHeight = 20;

        [Range(0f, 0.45f)] public float minVoidFraction = 0.04f;

        [Range(0f, 0.45f)] public float maxVoidFraction = 0.22f;

        [Range(0f, 1f)] public float dimensionInfluence = 0.75f;

        [Range(0f, 1f)] public float voidInfluence = 1f;

        [Range(0f, 1f)] public float complexityInfluence = 1f;

        [Range(1, 24)] public int minVoidRegions = 1;

        [Range(1, 24)] public int maxVoidRegions = 7;

        [Range(1, 12)] public int minWalkLength = 2;

        [Range(1, 12)] public int maxWalkLength = 7;
// Numeric settings are normalized without mutating the Inspector values.
// Invalid rolls and non-finite floating-point settings fail explicitly.
        public RoomGenerationProfile GetProfile(int roll)

        {

            if (roll < 1 || roll > 20) throw new ArgumentOutOfRangeException(nameof(roll), "D20 must be between 1 and 20.");

            int w0 = Clamp(minWidth, MinimumWidth, MaximumDimension);

            int w1 = Clamp(maxWidth, w0, MaximumDimension);

            int h0 = Clamp(minHeight, MinimumHeight, MaximumDimension);

            int h1 = Clamp(maxHeight, h0, MaximumDimension);

            float sizeInfluence = FiniteClamp(dimensionInfluence, 0f, 1f);

            float risk = (roll - 1) / 19f;

            float lower = risk * sizeInfluence * 0.55f;

            float upper = 1f - sizeInfluence + risk * sizeInfluence;

            float lowVoid = FiniteClamp(minVoidFraction, 0f, 0.45f);

            float highVoid = FiniteClamp(maxVoidFraction, lowVoid, 0.45f);

            float shapeRisk = risk * FiniteClamp(complexityInfluence, 0f, 1f);

            int regions0 = Clamp(minVoidRegions, 1, 24);

            int regions1 = Clamp(maxVoidRegions, regions0, 24);

            int walk0 = Clamp(minWalkLength, 1, 12);

            int walk1 = Clamp(maxWalkLength, walk0, 12);

            return new RoomGenerationProfile

            {

                MinWidth = w0 + Rounded((w1 - w0) * lower),

                MaxWidth = w0 + Rounded((w1 - w0) * upper),

                MinHeight = h0 + Rounded((h1 - h0) * lower),

                MaxHeight = h0 + Rounded((h1 - h0) * upper),

                VoidFraction = lowVoid + (highVoid - lowVoid) * risk * FiniteClamp(voidInfluence, 0f, 1f),

                VoidRegions = regions0 + Rounded((regions1 - regions0) * shapeRisk),

                WalkLength = walk0 + Rounded((walk1 - walk0) * shapeRisk)

            };

        }

        private static int Rounded(float value) => (int)Math.Round(value, MidpointRounding.AwayFromZero);

        private static int Clamp(int value, int low, int high) => Math.Max(low, Math.Min(high, value));

        private static float FiniteClamp(float value, float low, float high)

        {

            if (float.IsNaN(value) || float.IsInfinity(value)) throw new ArgumentException("Generation settings must contain finite numbers.");

            return Math.Max(low, Math.Min(high, value));

        }

    }

    public sealed class RoomLayout

    {

        public int Width { get; }

        public int Height { get; }

        public int Seed { get; }

        public int Roll { get; }

        public RoomCell[] Cells { get; }

        public DoorPlacement Entry { get; }

        public DoorPlacement Exit { get; }

        internal RoomLayout(int width, int height, int seed, int roll, DoorPlacement entry, DoorPlacement exit)

        {

            Width = width;

            Height = height;

            Seed = seed;

            Roll = roll;

            Entry = entry;

            Exit = exit;

            Cells = new RoomCell[width * height];

        }

        public RoomCell this[int x, int y]

        {

            get => Cells[Index(x, y)];

            internal set => Cells[Index(x, y)] = value;

        }

        public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;

        public bool IsWalkable(int x, int y) => InBounds(x, y) && (this[x, y] == RoomCell.Floor || this[x, y] == RoomCell.Door);

        public IEnumerable<Vector2Int> DoorFootprint(DoorPlacement door)

        {

            yield return door.Cell;

            yield return door.SecondCell;

        }

        private int Index(int x, int y)

        {

            if (!InBounds(x, y)) throw new ArgumentOutOfRangeException("cell", "Cell is outside this room.");

            return y * Width + x;

        }

    }

    public static class RoomGenerator

    {

        private static readonly Vector2Int[] Steps = { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };

        public static RoomLayout Generate(RoomGenerationSettings settings, int seed, int roll, DoorDirection entryDirection)

        {

            if (settings == null) throw new ArgumentNullException(nameof(settings));

            if ((int)entryDirection < 0 || (int)entryDirection > 3) throw new ArgumentOutOfRangeException(nameof(entryDirection));

            RoomGenerationProfile profile = settings.GetProfile(roll);

            var random = new System.Random(seed);

            int width = random.Next(profile.MinWidth, profile.MaxWidth + 1);

            int height = random.Next(profile.MinHeight, profile.MaxHeight + 1);

            var exitDirection = (DoorDirection)(((int)entryDirection + random.Next(1, 4)) % 4);

            var layout = new RoomLayout(width, height, seed, roll,

                PlaceDoor(random, width, height, entryDirection), PlaceDoor(random, width, height, exitDirection));

            for (int y = 0; y < height; y++)

                for (int x = 0; x < width; x++)

                    layout[x, y] = IsInterior(layout, x, y) ? RoomCell.Floor : RoomCell.Wall;

            var reserved = new bool[width * height];

            ReserveLanding(layout, reserved, layout.Entry);

            ReserveLanding(layout, reserved, layout.Exit);

            Corridor(layout, reserved, layout.Entry.InnerCell, layout.Exit.InnerCell, random.Next(2) == 0);

            AddVoidPatches(layout, reserved, random, profile);

            MarkDoor(layout, layout.Entry);

            MarkDoor(layout, layout.Exit);

            RepairConnectivity(layout, reserved);

            return layout;

        }

        private static DoorPlacement PlaceDoor(System.Random random, int width, int height, DoorDirection direction)

        {

            switch (direction)

            {

                case DoorDirection.North: return new DoorPlacement(new Vector2Int(random.Next(1, width - 2), height - 1), direction);

                case DoorDirection.East: return new DoorPlacement(new Vector2Int(width - 1, random.Next(1, height - 2) + 1), direction);

                case DoorDirection.South: return new DoorPlacement(new Vector2Int(random.Next(1, width - 2) + 1, 0), direction);

                case DoorDirection.West: return new DoorPlacement(new Vector2Int(0, random.Next(1, height - 2)), direction);
                default: throw new ArgumentOutOfRangeException(nameof(direction));

            }

        }

        private static bool IsInterior(RoomLayout layout, int x, int y) => x > 0 && y > 0 && x < layout.Width - 1 && y < layout.Height - 1;

        private static void ReserveLanding(RoomLayout layout, bool[] reserved, DoorPlacement door)

        {

            Vector2Int inward = -DoorPlacement.Outward(door.Direction);

            for (int depth = 1; depth <= 3; depth++)
            {
                Reserve(layout, reserved, door.Cell + inward * depth);
                Reserve(layout, reserved, door.SecondCell + inward * depth);
            }

        }

        private static void Reserve(RoomLayout layout, bool[] reserved, Vector2Int cell)

        {

            if (!IsInterior(layout, cell.x, cell.y)) return;

            layout[cell.x, cell.y] = RoomCell.Floor;

            reserved[cell.y * layout.Width + cell.x] = true;

        }

        private static void Corridor(RoomLayout layout, bool[] reserved, Vector2Int from, Vector2Int to, bool horizontalFirst)

        {

            Vector2Int current = from;

            for (int axis = 0; axis < 2; axis++)

            {

                bool horizontal = (axis == 0) == horizontalFirst;

                while (horizontal ? current.x != to.x : current.y != to.y)

                {

                    Widen(layout, reserved, current, horizontal);

                    current += horizontal

                        ? new Vector2Int(Math.Sign(to.x - current.x), 0)

                        : new Vector2Int(0, Math.Sign(to.y - current.y));

                }

                Widen(layout, reserved, current, horizontal);

            }

        }

        private static void Widen(RoomLayout layout, bool[] reserved, Vector2Int cell, bool horizontal)

        {

            Reserve(layout, reserved, cell);

            Vector2Int adjacent = cell + (horizontal ? Vector2Int.up : Vector2Int.right);

            if (!IsInterior(layout, adjacent.x, adjacent.y)) adjacent = cell + (horizontal ? Vector2Int.down : Vector2Int.left);

            Reserve(layout, reserved, adjacent);

        }

        private static void AddVoidPatches(RoomLayout layout, bool[] reserved, System.Random random, RoomGenerationProfile profile)

        {

            int budget = (int)((layout.Width - 2) * (layout.Height - 2) * profile.VoidFraction);

            int remaining = budget;

            int attemptsLeft = layout.Cells.Length * 8;

            for (int region = 0; region < profile.VoidRegions && remaining > 0; region++)

            {

                int regionBudget = (remaining + profile.VoidRegions - region - 1) / (profile.VoidRegions - region);

                Vector2Int center = RandomInterior(layout, random);

                Vector2Int current = center;

                int missed = 0;

                while (regionBudget > 0 && attemptsLeft-- > 0)

                {

                    int index = current.y * layout.Width + current.x;

                    if (!reserved[index] && layout.Cells[index] == RoomCell.Floor)

                    {

                        layout.Cells[index] = RoomCell.Void;

                        remaining--;

                        regionBudget--;

                        missed = 0;

                    }

                    else missed++;

                    current += Steps[random.Next(Steps.Length)];

                    if (!IsInterior(layout, current.x, current.y) || Math.Abs(current.x - center.x) + Math.Abs(current.y - center.y) > profile.WalkLength)

                        current = center;

                    if (missed >= 12)

                    {

                        center = RandomInterior(layout, random);

                        current = center;

                        missed = 0;

                    }

                }

            }

        }

        private static Vector2Int RandomInterior(RoomLayout layout, System.Random random) =>

            new Vector2Int(random.Next(1, layout.Width - 1), random.Next(1, layout.Height - 1));

        private static void MarkDoor(RoomLayout layout, DoorPlacement door)

        {

            layout[door.Cell.x, door.Cell.y] = RoomCell.Door;

            layout[door.SecondCell.x, door.SecondCell.y] = RoomCell.Door;

        }

        private static void RepairConnectivity(RoomLayout layout, bool[] reserved)

        {
// At most eight repair floods keep generation bounded for extreme Inspector settings.
            for (int pass = 0; pass <= 8; pass++)

            {

                bool[] reachable = Flood(layout);

                int missing = -1;

                for (int i = 0; i < layout.Cells.Length; i++)

                    if (layout.Cells[i] == RoomCell.Floor && !reachable[i]) { missing = i; break; }

                if (missing < 0) return;

                if (pass == 8)

                {

                    for (int i = 0; i < layout.Cells.Length; i++)

                        if (layout.Cells[i] == RoomCell.Floor && !reachable[i]) layout.Cells[i] = RoomCell.Void;

                    return;

                }

                Corridor(layout, reserved, new Vector2Int(missing % layout.Width, missing / layout.Width), layout.Entry.InnerCell, pass % 2 == 0);
// Repair never changes the two reserved door footprints.
                MarkDoor(layout, layout.Entry);

                MarkDoor(layout, layout.Exit);

            }

        }

        private static bool[] Flood(RoomLayout layout)

        {

            var visited = new bool[layout.Cells.Length];

            var queue = new Queue<Vector2Int>();

            queue.Enqueue(layout.Entry.Cell);

            visited[layout.Entry.Cell.y * layout.Width + layout.Entry.Cell.x] = true;

            while (queue.Count > 0)

            {

                Vector2Int cell = queue.Dequeue();

                foreach (Vector2Int step in Steps)

                {

                    Vector2Int next = cell + step;

                    if (!layout.IsWalkable(next.x, next.y)) continue;

                    int index = next.y * layout.Width + next.x;

                    if (visited[index]) continue;

                    visited[index] = true;

                    queue.Enqueue(next);

                }

            }

            return visited;

        }

    }

}
