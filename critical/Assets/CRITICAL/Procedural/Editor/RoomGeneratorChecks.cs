using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Critical.Procedural
{
    public static class RoomGeneratorChecks
    {
        [Serializable]
        private class Report
        {
            public bool passed;
            public string startedAtUtc;
            public double elapsedMilliseconds;
            public int cases;
            public int deterministicReplays;
            public int failures;
            public int uniqueLayouts;
            public float averageAreaAtOne;
            public float averageAreaAtTwenty;
            public float averageVoidAtOne;
            public float averageVoidAtTwenty;
            public string[] details;
        }

        [MenuItem("Tools/CRITICAL/Procedural/Run Generation Checks")]
        public static void Run()
        {
            var report = new Report { startedAtUtc = DateTime.UtcNow.ToString("O") };
            var timer = Stopwatch.StartNew();
            var details = new List<string>();
            var unique = new HashSet<ulong>();
            var settings = new RoomGenerationSettings();
            try
            {
                CheckProfiles(settings);
                ExpectThrows<ArgumentNullException>(() => RoomGenerator.Generate(null, 0, 1, DoorDirection.North));
                ExpectThrows<ArgumentOutOfRangeException>(() => RoomGenerator.Generate(settings, 0, 0, DoorDirection.North));
                ExpectThrows<ArgumentOutOfRangeException>(() => RoomGenerator.Generate(settings, 0, 21, DoorDirection.North));
                ExpectThrows<ArgumentOutOfRangeException>(() => RoomGenerator.Generate(settings, 0, 1, (DoorDirection)99));
                var invalidFloat = new RoomGenerationSettings { minVoidFraction = float.NaN };
                ExpectThrows<ArgumentException>(() => RoomGenerator.Generate(invalidFloat, 0, 1, DoorDirection.North));

                for (int seed = 0; seed < 100; seed++)
                    for (int roll = 1; roll <= 20; roll++)
                    {
                        report.cases++;
                        try
                        {
                            var direction = (DoorDirection)((seed + roll) % 4);
                            RoomLayout room = RoomGenerator.Generate(settings, seed, roll, direction);
                            Validate(room, settings.GetProfile(roll), direction);
                            ulong hash = TopologyHash(room);
                            RoomLayout replay = RoomGenerator.Generate(settings, seed, roll, direction);
                            Require(hash == TopologyHash(replay), "Same seed/settings/roll must reproduce the same room.");
                            report.deterministicReplays++;
                            unique.Add(hash);
                            float voidFraction = Count(room, RoomCell.Void) / (float)((room.Width - 2) * (room.Height - 2));
                            if (roll == 1)
                            {
                                report.averageAreaAtOne += room.Width * room.Height / 100f;
                                report.averageVoidAtOne += voidFraction / 100f;
                            }
                            if (roll == 20)
                            {
                                report.averageAreaAtTwenty += room.Width * room.Height / 100f;
                                report.averageVoidAtTwenty += voidFraction / 100f;
                            }
                        }
                        catch (Exception error)
                        {
                            report.failures++;
                            if (details.Count < 20) details.Add("seed=" + seed + ", roll=" + roll + ": " + error.Message);
                        }
                    }

                report.uniqueLayouts = unique.Count;
                Require(report.uniqueLayouts > 1000, "Different seeds/rolls should vary topology, not only metadata.");
                Require(report.averageAreaAtTwenty > report.averageAreaAtOne * 1.2f, "High D20 must create statistically larger rooms with default settings.");
                Require(report.averageVoidAtTwenty > report.averageVoidAtOne + 0.02f, "High D20 must create statistically more void with default settings.");

                var bounded = new RoomGenerationSettings
                {
                    minWidth = -40, maxWidth = -1, minHeight = 400, maxHeight = 2,
                    minVoidFraction = -5f, maxVoidFraction = 8f,
                    minVoidRegions = -20, maxVoidRegions = 200, minWalkLength = -2, maxWalkLength = 200
                };
                Validate(RoomGenerator.Generate(bounded, int.MinValue, 20, DoorDirection.West), bounded.GetProfile(20), DoorDirection.West);
                Validate(RoomGenerator.Generate(settings, int.MaxValue, 20, DoorDirection.South), settings.GetProfile(20), DoorDirection.South);
                report.passed = report.failures == 0;
            }
            catch (Exception error)
            {
                report.failures++;
                details.Add(error.ToString());
            }
            finally
            {
                report.elapsedMilliseconds = timer.Elapsed.TotalMilliseconds;
                report.details = details.ToArray();
                const string folder = "Library/CRITICAL";
                Directory.CreateDirectory(folder);
                File.WriteAllText(folder + "/GenerationChecks.json", JsonUtility.ToJson(report, true));
            }

            string summary = "CRITICAL Generation Checks: " + (report.passed ? "PASS" : "FAIL")
                + " — " + report.cases + " cases, " + report.deterministicReplays + " replays, " + report.failures
                + " failures. Report: Library/CRITICAL/GenerationChecks.json";
            if (report.passed) Debug.Log(summary);
            else Debug.LogError(summary + "\n" + string.Join("\n", report.details));
        }

        private static void CheckProfiles(RoomGenerationSettings settings)
        {
            RoomGenerationProfile previous = settings.GetProfile(1);
            for (int roll = 2; roll <= 20; roll++)
            {
                RoomGenerationProfile current = settings.GetProfile(roll);
                Require(current.MinWidth >= previous.MinWidth && current.MaxWidth >= previous.MaxWidth
                    && current.MinHeight >= previous.MinHeight && current.MaxHeight >= previous.MaxHeight,
                    "Dimension profile bounds must be monotonic with D20.");
                Require(current.VoidFraction >= previous.VoidFraction && current.VoidRegions >= previous.VoidRegions
                    && current.WalkLength >= previous.WalkLength, "Void/complexity profiles must be monotonic with D20.");
                previous = current;
            }
            var flat = new RoomGenerationSettings { dimensionInfluence = 0f, voidInfluence = 0f, complexityInfluence = 0f };
            Require(JsonUtility.ToJson(flat.GetProfile(1)) == JsonUtility.ToJson(flat.GetProfile(20)), "Zero influences must remove D20 effects from the profile.");
        }

        private static void Validate(RoomLayout room, RoomGenerationProfile profile, DoorDirection expectedEntry)
        {
            Require(room.Width >= profile.MinWidth && room.Width <= profile.MaxWidth && room.Height >= profile.MinHeight
                && room.Height <= profile.MaxHeight, "Dimensions must stay within the roll profile.");
            Require(room.Width >= 12 && room.Height >= 10 && room.Width <= 96 && room.Height <= 96, "Dimension safety bounds must hold.");
            Require(room.Cells.Length == room.Width * room.Height, "Cell count must match dimensions.");
            Require(room.Entry.Direction == expectedEntry && room.Exit.Direction != expectedEntry, "Entry and exit must occupy different walls.");
            ValidateDoor(room, room.Entry);
            ValidateDoor(room, room.Exit);
            Require(Count(room, RoomCell.Door) == 4, "Exactly two two-cell door footprints must exist.");

            var expectedDoors = new HashSet<Vector2Int>
            {
                room.Entry.Cell, room.Entry.InnerCell, room.Exit.Cell, room.Exit.InnerCell
            };
            for (int y = 0; y < room.Height; y++)
                for (int x = 0; x < room.Width; x++)
                {
                    RoomCell value = room[x, y];
                    bool boundary = x == 0 || y == 0 || x == room.Width - 1 || y == room.Height - 1;
                    if (boundary && !expectedDoors.Contains(new Vector2Int(x, y)))
                        Require(value == RoomCell.Wall, "Boundary must be sealed outside the two doors.");
                    if (value == RoomCell.Door)
                        Require(expectedDoors.Contains(new Vector2Int(x, y)), "Unexpected extra door cell.");
                }

            // Independent BFS intentionally reads cell values directly instead of generator helpers.
            var seen = new bool[room.Cells.Length];
            var queue = new Queue<Vector2Int>();
            queue.Enqueue(room.Entry.Cell);
            seen[room.Entry.Cell.y * room.Width + room.Entry.Cell.x] = true;
            Vector2Int[] steps = { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };
            while (queue.Count > 0)
            {
                Vector2Int current = queue.Dequeue();
                foreach (Vector2Int step in steps)
                {
                    Vector2Int next = current + step;
                    if (next.x < 0 || next.y < 0 || next.x >= room.Width || next.y >= room.Height) continue;
                    int index = next.y * room.Width + next.x;
                    if (seen[index] || (room.Cells[index] != RoomCell.Floor && room.Cells[index] != RoomCell.Door)) continue;
                    seen[index] = true;
                    queue.Enqueue(next);
                }
            }
            for (int i = 0; i < room.Cells.Length; i++)
                if (room.Cells[i] == RoomCell.Floor || room.Cells[i] == RoomCell.Door)
                    Require(seen[i], "Every floor and door cell must be reachable from the entry.");
            Require(Count(room, RoomCell.Floor) > 0, "Room must contain usable floor.");
            Require(!room.IsWalkable(-1, 0) && !room.IsWalkable(room.Width, 0), "Out-of-bounds cells cannot be walkable.");
        }

        private static void ValidateDoor(RoomLayout room, DoorPlacement door)
        {
            Vector2Int cell = door.Cell;
            Vector2Int inner = door.InnerCell;
            Require(room[cell.x, cell.y] == RoomCell.Door && room[inner.x, inner.y] == RoomCell.Door, "Door footprint cells must be marked Door.");
            bool vertical = door.Direction == DoorDirection.North || door.Direction == DoorDirection.South;
            Require(vertical ? cell.x == inner.x && Math.Abs(cell.y - inner.y) == 1
                : cell.y == inner.y && Math.Abs(cell.x - inner.x) == 1, "Door footprint orientation/size must be N/S 1x2 or E/W 2x1.");
            if (vertical)
            {
                Require(cell.x >= 2 && cell.x <= room.Width - 3, "N/S door cannot occupy or touch a corner.");
                Require(cell.y == (door.Direction == DoorDirection.North ? room.Height - 1 : 0), "N/S door must lie on its matching wall.");
            }
            else
            {
                Require(cell.y >= 2 && cell.y <= room.Height - 3, "E/W door cannot occupy or touch a corner.");
                Require(cell.x == (door.Direction == DoorDirection.East ? room.Width - 1 : 0), "E/W door must lie on its matching wall.");
            }
            Vector2Int inward = inner - cell;
            for (int depth = 2; depth <= 3; depth++)
            {
                Vector2Int landing = cell + inward * depth;
                Require(room[landing.x, landing.y] == RoomCell.Floor, "Door must keep a two-cell floor approach landing.");
            }
        }

        private static int Count(RoomLayout room, RoomCell value)
        {
            int count = 0;
            foreach (RoomCell cell in room.Cells) if (cell == value) count++;
            return count;
        }

        private static ulong TopologyHash(RoomLayout room)
        {
            ulong hash = 14695981039346656037UL;
            unchecked
            {
                hash = (hash ^ (uint)room.Width) * 1099511628211UL;
                hash = (hash ^ (uint)room.Height) * 1099511628211UL;
                hash = (hash ^ (uint)room.Entry.Direction) * 1099511628211UL;
                hash = (hash ^ (uint)room.Exit.Direction) * 1099511628211UL;
                foreach (RoomCell cell in room.Cells) hash = (hash ^ (uint)cell) * 1099511628211UL;
            }
            return hash;
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private static void ExpectThrows<T>(Action action) where T : Exception
        {
            try { action(); }
            catch (T) { return; }
            throw new InvalidOperationException("Expected " + typeof(T).Name + ".");
        }
    }
}
