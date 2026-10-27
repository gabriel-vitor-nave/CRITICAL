using System;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Critical.Procedural
{
    public sealed class RoomTilemapView : MonoBehaviour
    {
        public Tilemap ground, collisionLow, collisionHigh, decorationHigh, decorations;
        public TileBase[] floorTiles;
        public TileBase northWall, southWall, westWall, eastWall;
        public TileBase northWestCorner, northEastCorner, southWestCorner, southEastCorner;
        public TileBase voidTile;

        public Bounds WorldBounds { get; private set; }

        public void Build(RoomLayout layout)
        {
            if (layout == null) throw new ArgumentNullException(nameof(layout));
            if (ground == null || collisionLow == null || collisionHigh == null || decorationHigh == null || decorations == null)
                throw new InvalidOperationException("RoomTilemapView requires the five configured Tilemap layers.");
            if (floorTiles == null || floorTiles.Length == 0 || Array.Exists(floorTiles, tile => tile == null))
                throw new InvalidOperationException("RoomTilemapView requires valid floor tiles.");
            if (northWall == null || southWall == null || westWall == null || eastWall == null || voidTile == null)
                throw new InvalidOperationException("RoomTilemapView requires wall and collidable void tiles.");

            ClearTiles();
            var bounds = new BoundsInt(0, 0, 0, layout.Width, layout.Height, 1);
            var floor = new TileBase[layout.Cells.Length];
            var low = new TileBase[layout.Cells.Length];
            var high = new TileBase[layout.Cells.Length];
            var upperDecoration = new TileBase[layout.Cells.Length];
            for (int y = 0; y < layout.Height; y++)
                for (int x = 0; x < layout.Width; x++)
                {
                    int index = y * layout.Width + x;
                    switch (layout[x, y])
                    {
                        case RoomCell.Floor:
                        case RoomCell.Door:
                            int variant = unchecked(layout.Seed ^ (x * 73856093) ^ (y * 19349663)) & int.MaxValue;
                            floor[index] = floorTiles[variant % floorTiles.Length];
                            break;
                        case RoomCell.Void:
                            low[index] = voidTile;
                            break;
                        case RoomCell.Wall:
                            TileBase wall = WallTile(x, y, layout.Width, layout.Height);
                            if (y == 0) high[index] = wall;
                            else low[index] = wall;
                            break;
                    }
                }
            ground.SetTilesBlock(bounds, floor);
            collisionLow.SetTilesBlock(bounds, low);
            collisionHigh.SetTilesBlock(bounds, high);
            decorationHigh.SetTilesBlock(bounds, upperDecoration);
            RefreshCollision();

            Vector3 origin = ground.CellToWorld(Vector3Int.zero);
            var world = new Bounds(origin, Vector3.zero);
            world.Encapsulate(ground.CellToWorld(new Vector3Int(layout.Width, 0, 0)));
            world.Encapsulate(ground.CellToWorld(new Vector3Int(0, layout.Height, 0)));
            world.Encapsulate(ground.CellToWorld(new Vector3Int(layout.Width, layout.Height, 0)));
            WorldBounds = world;
        }

        public void Clear()
        {
            ClearTiles();
            RefreshCollision();
            WorldBounds = new Bounds(transform.position, Vector3.zero);
        }

        public Vector3 CellCenter(Vector2Int cell)
        {
            if (ground == null) throw new InvalidOperationException("Assign the ground Tilemap before querying cell positions.");
            return ground.GetCellCenterWorld(new Vector3Int(cell.x, cell.y, 0));
        }

        public Vector3 DoorCenter(DoorPlacement placement) =>
            (CellCenter(placement.Cell) + CellCenter(placement.InnerCell)) * 0.5f;

        private TileBase WallTile(int x, int y, int width, int height)
        {
            if (y == 0)
                return x == 0 ? southWestCorner != null ? southWestCorner : southWall
                    : x == width - 1 ? southEastCorner != null ? southEastCorner : southWall : southWall;
            if (y == height - 1)
                return x == 0 ? northWestCorner != null ? northWestCorner : northWall
                    : x == width - 1 ? northEastCorner != null ? northEastCorner : northWall : northWall;
            return x == 0 ? westWall : eastWall;
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
            if (collider != null && collider.enabled) collider.ProcessTilemapChanges();
        }
    }
}
