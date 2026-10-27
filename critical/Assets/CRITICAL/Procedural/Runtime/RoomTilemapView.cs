using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Critical.Procedural
{
    /// <summary>
    /// North-oriented source tiles are rotated to build all four room sides.
    /// Order from inside to outside: floor, wall, stone finish, void.
    /// Door graphics are optional when a separate door prefab is used.
    /// </summary>
    public sealed class RoomTilemapView : MonoBehaviour
    {
        [Header("Existing Tilemaps")]
        public Tilemap ground, collisionLow, collisionHigh, decorationHigh, decorations;

        [Header("Floor")]
        public TileBase[] floorTiles;

        [Header("North-oriented wall sprites")]
        public TileBase northWall;
        public TileBase northWestWallCorner;

        [Header("North-oriented outer stone sprites")]
        public TileBase northFinish;
        public TileBase northWestFinishCorner;

        [Header("Optional two-piece north door (leave empty when using prefabs)")]
        public TileBase northDoorLeft;
        public TileBase northDoorRight;

        [Header("Void")]
        public TileBase voidTile;
        [Min(1)] public int outsideVoidPadding = 8;

        public Bounds WorldBounds { get; private set; }

        private struct RotationRequest
        {
            public Tilemap Map;
            public Vector3Int Position;
            public int Degrees;

            public RotationRequest(Tilemap map, Vector3Int position, int degrees)
            {
                Map = map;
                Position = position;
                Degrees = degrees;
            }
        }

        public void Build(RoomLayout layout)
        {
            if (layout == null) throw new ArgumentNullException(nameof(layout));
            if (ground == null || collisionLow == null || collisionHigh == null ||
                decorationHigh == null || decorations == null)
                throw new InvalidOperationException("Assign all five Tilemap layers.");
            if (floorTiles == null || floorTiles.Length == 0 ||
                Array.Exists(floorTiles, tile => tile == null))
                throw new InvalidOperationException("Assign at least one valid floor tile.");
            if (northWall == null || northFinish == null || voidTile == null)
                throw new InvalidOperationException("Assign northWall, northFinish and voidTile.");
            if ((northDoorLeft == null) != (northDoorRight == null))
                throw new InvalidOperationException("Assign both door halves or leave both empty.");

            ClearTiles();

            int margin = Mathf.Clamp(outsideVoidPadding, 1, 64) + 1;
            int minX = -margin;
            int minY = -margin;
            int width = layout.Width + margin * 2;
            int height = layout.Height + margin * 2;
            var bounds = new BoundsInt(minX, minY, 0, width, height, 1);

            var floor = new TileBase[width * height];
            var low = new TileBase[width * height];
            var high = new TileBase[width * height];
            var doors = new TileBase[width * height];
            var finish = new TileBase[width * height];
            var rotations = new List<RotationRequest>();

            for (int y = minY; y < minY + height; y++)
            {
                for (int x = minX; x < minX + width; x++)
                {
                    int index = (y - minY) * width + x - minX;
                    var position = new Vector3Int(x, y, 0);

                    if (layout.InBounds(x, y))
                    {
                        switch (layout[x, y])
                        {
                            case RoomCell.Floor:
                                floor[index] = FloorTile(layout.Seed, x, y);
                                break;

                            case RoomCell.Door:
                                floor[index] = FloorTile(layout.Seed, x, y);
                                TileBase doorTile;
                                int doorAngle;
                                if (TryDoorGraphic(layout, x, y, out doorTile, out doorAngle))
                                {
                                    doors[index] = doorTile;
                                    rotations.Add(new RotationRequest(decorationHigh, position, doorAngle));
                                }
                                break;

                            case RoomCell.Void:
                                low[index] = voidTile;
                                break;

                            case RoomCell.Wall:
                                int wallAngle;
                                TileBase wall = EdgeTile(x, y,
                                    0, 0, layout.Width - 1, layout.Height - 1,
                                    northWall, northWestWallCorner, out wallAngle);

                                // Preserve the original low/high collision split.
                                Tilemap target = y == 0 ? collisionHigh : collisionLow;
                                if (y == 0) high[index] = wall;
                                else low[index] = wall;
                                rotations.Add(new RotationRequest(target, position, wallAngle));
                                break;
                        }

                        continue;
                    }
                    bool isFinish = IsFinishRing(
                        x, y, layout.Width, layout.Height
                    );

                    bool isDoorOpening = isFinish &&
                        (
                            IsDoorContinuation(layout.Entry, x, y) ||
                            IsDoorContinuation(layout.Exit, x, y)
                        );

                    if (isDoorOpening)
                    {
                        // Void visual, mas sem colisão nesta célula.
                        // Não cria nenhum corredor.
                        finish[index] = voidTile;
                        continue;
                    }

                    // Fora da sala, tudo é void.
                    low[index] = voidTile;

                    // Apenas a primeira fileira externa recebe pedras.
                    if (isFinish)
                    {
                        int angle;

                        finish[index] = EdgeTile(
                            x, y,
                            -1, -1,
                            layout.Width, layout.Height,
                            northFinish,
                            northWestFinishCorner,
                            out angle
                        );

                        rotations.Add(new RotationRequest(
                            decorations, position, angle
                        ));
                    }
                }
            }

            ground.SetTilesBlock(bounds, floor);
            collisionLow.SetTilesBlock(bounds, low);
            collisionHigh.SetTilesBlock(bounds, high);
            decorationHigh.SetTilesBlock(bounds, doors);
            decorations.SetTilesBlock(bounds, finish);

            // Standard Tile assets may be locked against transform changes.
            // Unlock each generated tile cell before applying its rotation.
            foreach (RotationRequest rotation in rotations)
            {
                rotation.Map.SetTileFlags(rotation.Position, TileFlags.None);
                rotation.Map.SetTransformMatrix(rotation.Position,
                    Matrix4x4.Rotate(Quaternion.Euler(0f, 0f, rotation.Degrees)));
            }

            RefreshCollision();

            // The room bounds still describe the logical/playable room.
            Vector3 origin = ground.CellToWorld(Vector3Int.zero);
            var world = new Bounds(origin, Vector3.zero);
            world.Encapsulate(ground.CellToWorld(new Vector3Int(layout.Width, 0, 0)));
            world.Encapsulate(ground.CellToWorld(new Vector3Int(0, layout.Height, 0)));
            world.Encapsulate(ground.CellToWorld(new Vector3Int(layout.Width, layout.Height, 0)));
            WorldBounds = world;
        }

        // North=0, East=-90, South=180, West=+90.
        public static int DoorAngle(DoorDirection direction)
        {
            switch (direction)
            {
                case DoorDirection.North: return 0;
                case DoorDirection.East: return -90;
                case DoorDirection.South: return 180;
                case DoorDirection.West: return 90;
                default: throw new ArgumentOutOfRangeException(nameof(direction));
            }
        }

        public static Quaternion DoorRotation(DoorPlacement placement) =>
            Quaternion.Euler(0f, 0f, DoorAngle(placement.Direction));

        public Vector3 DoorCenter(DoorPlacement placement) =>
            (CellCenter(placement.Cell) + CellCenter(placement.SecondCell)) * 0.5f;

        public Vector3 CellCenter(Vector2Int cell)
        {
            if (ground == null)
                throw new InvalidOperationException("Assign ground before querying cell positions.");
            return ground.GetCellCenterWorld(new Vector3Int(cell.x, cell.y, 0));
        }

        public void Clear()
        {
            ClearTiles();
            RefreshCollision();
            WorldBounds = new Bounds(transform.position, Vector3.zero);
        }

        private TileBase FloorTile(int seed, int x, int y)
        {
            int hash = unchecked(seed ^ (x * 73856093) ^ (y * 19349663)) & int.MaxValue;
            return floorTiles[hash % floorTiles.Length];
        }

        private bool TryDoorGraphic(RoomLayout layout, int x, int y,
            out TileBase tile, out int angle)
        {
            tile = null;
            angle = 0;
            if (northDoorLeft == null) return false;

            if (TryDoorHalf(layout.Entry, x, y, out tile, out angle)) return true;
            return TryDoorHalf(layout.Exit, x, y, out tile, out angle);
        }

        private bool TryDoorHalf(DoorPlacement placement, int x, int y,
            out TileBase tile, out int angle)
        {
            tile = null;
            angle = DoorAngle(placement.Direction);

            if (placement.Cell.x == x && placement.Cell.y == y)
                tile = northDoorLeft;
            else if (placement.SecondCell.x == x && placement.SecondCell.y == y)
                tile = northDoorRight;

            return tile != null;
        }

        /// <summary>
        /// North straight and NW corner rotate around each cell's center.
        /// The same routine also paints the outer finishing ring.
        /// </summary>
        private static TileBase EdgeTile(int x, int y, int minX, int minY,
            int maxX, int maxY, TileBase northStraight, TileBase northWestCorner,
            out int angle)
        {
            bool corner = (x == minX || x == maxX) && (y == minY || y == maxY);

            if (y == maxY)
                angle = x == minX ? 0 : x == maxX ? -90 : 0;
            else if (x == maxX)
                angle = y == minY ? 180 : -90;
            else if (y == minY)
                angle = x == minX ? 90 : 180;
            else
                angle = 90;

            return corner && northWestCorner != null ? northWestCorner : northStraight;
        }

        private static bool IsFinishRing(int x, int y, int width, int height) =>
            x >= -1 && x <= width && y >= -1 && y <= height &&
            (x == -1 || x == width || y == -1 || y == height);

        private static bool IsDoorContinuation(DoorPlacement door, int x, int y)
        {
            Vector2Int outwards = DoorPlacement.Outward(door.Direction);
            return AlongOutwardRay(door.Cell, outwards, x, y) ||
                   AlongOutwardRay(door.SecondCell, outwards, x, y);
        }

        private static bool AlongOutwardRay(Vector2Int start, Vector2Int direction,
            int x, int y)
        {
            int dx = x - start.x;
            int dy = y - start.y;
            return direction.x != 0
                ? dy == 0 && dx * direction.x > 0
                : dx == 0 && dy * direction.y > 0;
        }

        private void ClearTiles()
        {
            if (ground != null) ground.ClearAllTiles();
            if (collisionLow != null) collisionLow.ClearAllTiles();
            if (collisionHigh != null) collisionHigh.ClearAllTiles();
            if (decorationHigh != null) decorationHigh.ClearAllTiles();
            if (decorations != null) decorations.ClearAllTiles();
        }

        private void RefreshCollision()
        {
            ProcessCollider(collisionLow);
            ProcessCollider(collisionHigh);
            Physics2D.SyncTransforms();
        }

        private static void ProcessCollider(Tilemap map)
        {
            if (map == null) return;
            TilemapCollider2D collider = map.GetComponent<TilemapCollider2D>();
            if (collider != null && collider.enabled)
                collider.ProcessTilemapChanges();
        }
    }
}
