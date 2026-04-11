namespace SelenesHollow;

// Square top-down grid à la Sea of Stars. 32x32 source tiles drawn at 3x for
// 96 px on-screen cells. The world is 50x30 = a spacious multi-screen area,
// with the camera following Selene as she explores. Decorations
// (trees, ruins, fountain, temple, shrine) come from a separate props atlas
// and are anchored at their base. Each one optionally registers a base AABB
// in _obstacles so the player can collide with it. Painter's algorithm sorts
// by row so Selene walks behind/in front of objects naturally.
public class Map
{
    private const int TILE_SRC = 32;
    private const int DRAW_SCALE = 3;
    public const int TILE_SCREEN = TILE_SRC * DRAW_SCALE;   // 96 px

    private readonly Point MAP_SIZE = new(50, 30);
    private readonly Vector2 MAP_OFFSET = new(16f, 16f);

    // Where the fountain prop is anchored. Used by IntroSequence to place the
    // sleeping Selene on the pedestal. Pushed south so the temple at row 7
    // and the fountain don't visually overlap.
    public static readonly Point FountainCell = new(24, 14);

    // ----- Terrain source rects -----
    private static readonly Rectangle GrassPlain = new(64,  96, 32, 32);
    private static readonly Rectangle GrassWeedy = new(640, 160, 32, 32);
    private static readonly Rectangle GrassDense = new(576, 224, 32, 32);
    private static readonly Rectangle PathClean  = new(800, 384, 32, 32);
    private static readonly Rectangle PathMossy  = new(832, 384, 32, 32);
    private static readonly Rectangle PathSandy  = new(864, 384, 32, 32);

    // ----- Prop source rects + foot anchors (in source pixels) -----
    private static readonly Rectangle BigTreeRect      = new(10,  2, 101, 182);
    private static readonly Vector2   BigTreeAnchor    = new(50f, 182f);
    private static readonly Rectangle BigTree2Rect     = new(138, 2, 101, 182);
    private static readonly Vector2   BigTree2Anchor   = new(50f, 182f);
    private static readonly Rectangle SmallTreeRect    = new(288, 13, 32, 47);
    private static readonly Vector2   SmallTreeAnchor  = new(16f, 47f);
    private static readonly Rectangle BareTreeRect     = new(320, 14, 26, 49);
    private static readonly Vector2   BareTreeAnchor   = new(13f, 49f);
    private static readonly Rectangle BushRect        = new(265, 65, 50, 30);
    private static readonly Vector2   BushAnchor      = new(25f, 30f);
    private static readonly Rectangle TinyTreeRect     = new(356, 67, 27, 29);
    private static readonly Vector2   TinyTreeAnchor   = new(13f, 29f);
    private static readonly Rectangle PillarRect       = new(263, 114, 53, 76);
    private static readonly Vector2   PillarAnchor     = new(26f, 76f);
    private static readonly Rectangle Pillar2Rect      = new(327, 129, 51, 60);
    private static readonly Vector2   Pillar2Anchor    = new(25f, 60f);
    private static readonly Rectangle TempleRect       = new(359, 194, 144, 189);
    private static readonly Vector2   TempleAnchor     = new(72f, 189f);
    private static readonly Rectangle BushColumnRect   = new(205, 209, 36, 71);
    private static readonly Vector2   BushColumnAnchor = new(18f, 71f);
    private static readonly Rectangle WallRect         = new(259, 235, 90, 48);
    private static readonly Vector2   WallAnchor       = new(45f, 48f);
    private static readonly Rectangle FountainRect     = new(83, 294, 123, 119);
    private static readonly Vector2   FountainAnchor   = new(61f, 119f);
    private static readonly Rectangle ChaliceRect      = new(16, 306, 29, 40);
    private static readonly Vector2   ChaliceAnchor    = new(14f, 40f);

    // ----- Per-prop collision footprint (width, height) in screen pixels -----
    // Centred horizontally on the prop's foot pos, with the bottom of the box
    // sitting on the foot pos itself. Tuned by eye for each prop type.
    private static readonly Vector2 BigTreeBox     = new( 60f, 30f);
    private static readonly Vector2 SmallTreeBox   = new( 50f, 22f);
    private static readonly Vector2 BareTreeBox    = new( 40f, 22f);
    private static readonly Vector2 BushBox        = new(130f, 30f);
    private static readonly Vector2 TinyTreeBox    = new( 45f, 20f);
    private static readonly Vector2 PillarBox      = new( 70f, 50f);
    private static readonly Vector2 TempleBox      = new(320f, 90f);
    private static readonly Vector2 BushColumnBox  = new( 90f, 70f);
    private static readonly Vector2 WallBox        = new(240f, 70f);
    private static readonly Vector2 FountainBox    = new(160f, 100f);
    // Chalice is interactable so we don't block walking right up to it.

    private readonly Tile[,] _tiles;
    private readonly List<(int sortKey, Tile tile)> _decorations = new();
    private readonly List<Rectangle> _obstacles = new();

    public Point Size => MAP_SIZE;
    public Player Player { get; set; }
    public IReadOnlyList<Rectangle> Obstacles => _obstacles;

    public Vector2 FountainCenter
    {
        get
        {
            var foot = GetCellFootPos(FountainCell);
            return new Vector2(foot.X + 5f, foot.Y - 220f);
        }
    }

    public Map()
    {
        _tiles = new Tile[MAP_SIZE.X, MAP_SIZE.Y];
        var terrain = Globals.Content.Load<Texture2D>("terrain");
        var props   = Globals.Content.Load<Texture2D>("props");

        // ----- Path layout -----
        // Organic winding paths connecting the temple, courtyard, garden,
        // ruins, practice field, and southern shrine across the bigger map.
        var path = new HashSet<(int, int)>();

        // Courtyard around the fountain (cells 22-26, 13-15)
        for (int x = 22; x <= 26; x++)
            for (int y = 13; y <= 15; y++)
                path.Add((x, y));

        // Clear side corridors around the fountain (east side bypass)
        for (int y = 12; y <= 16; y++) { path.Add((26, y)); path.Add((22, y)); }

        // North approach to temple — straight then slight wind
        for (int y = 8; y <= 13; y++) path.Add((24, y));
        path.Add((23, 8)); path.Add((25, 10)); path.Add((23, 12));

        // South spine — from courtyard down to shrine
        for (int y = 15; y <= 18; y++) path.Add((24, y));
        path.Add((23, 18)); path.Add((23, 19)); path.Add((24, 19));
        path.Add((24, 20)); path.Add((25, 20)); path.Add((25, 21));
        path.Add((24, 21)); path.Add((24, 22)); path.Add((24, 23));
        path.Add((23, 23)); path.Add((23, 24)); path.Add((24, 24));
        path.Add((24, 25)); path.Add((24, 26));

        // West branch — curves from courtyard through the garden
        path.Add((22, 14)); path.Add((21, 14)); path.Add((20, 15));
        path.Add((19, 15)); path.Add((18, 16)); path.Add((17, 16));
        path.Add((16, 17)); path.Add((15, 17)); path.Add((14, 18));
        path.Add((13, 18)); path.Add((12, 18)); path.Add((11, 19));
        path.Add((10, 19)); path.Add((9, 20)); path.Add((8, 20));
        path.Add((7, 20)); path.Add((6, 20)); path.Add((5, 19));
        // Garden loop
        path.Add((6, 19)); path.Add((7, 19)); path.Add((8, 19));
        path.Add((8, 18)); path.Add((9, 18));

        // East branch — winding from courtyard through ruins
        path.Add((26, 14)); path.Add((27, 14)); path.Add((28, 15));
        path.Add((29, 15)); path.Add((30, 16)); path.Add((31, 16));
        path.Add((32, 17)); path.Add((33, 17)); path.Add((34, 17));
        path.Add((35, 17)); path.Add((36, 16)); path.Add((37, 16));
        path.Add((38, 17)); path.Add((39, 17));
        // Ruins loop
        path.Add((36, 17)); path.Add((37, 17)); path.Add((38, 16));
        path.Add((35, 16)); path.Add((34, 16));

        // Path to practice field — connects east branch up to (35, 10)
        path.Add((35, 15)); path.Add((35, 14)); path.Add((36, 13));
        path.Add((36, 12)); path.Add((35, 11)); path.Add((35, 10));

        // Practice field — rectangular sandy area (cells 35-42, 5-10)
        for (int x = 35; x <= 42; x++)
            for (int y = 5; y <= 10; y++)
                path.Add((x, y));

        // Sandy variant: practice field + eastern ruins approach
        var sandy = new HashSet<(int, int)>();
        for (int x = 35; x <= 42; x++)
            for (int y = 5; y <= 10; y++)
                sandy.Add((x, y));
        for (int x = 33; x <= 40; x++)
        {
            sandy.Add((x, 16));
            sandy.Add((x, 17));
        }

        // Dense grass marks the forest band along the top of the map (rows 0-3)
        var dense = new HashSet<(int, int)>();
        for (int x = 0; x < MAP_SIZE.X; x++)
            for (int y = 0; y <= 3; y++)
                dense.Add((x, y));

        // Build a set of grass cells that border a path cell (transition zone)
        var pathEdge = new HashSet<(int, int)>();
        foreach (var (px, py) in path)
        {
            for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                if (dx == 0 && dy == 0) continue;
                var nb = (px + dx, py + dy);
                if (!path.Contains(nb) && nb.Item1 >= 0 && nb.Item1 < MAP_SIZE.X
                                       && nb.Item2 >= 0 && nb.Item2 < MAP_SIZE.Y)
                    pathEdge.Add(nb);
            }
        }

        var rng = new Random(1337);
        for (int y = 0; y < MAP_SIZE.Y; y++)
        {
            for (int x = 0; x < MAP_SIZE.X; x++)
            {
                Rectangle src;
                if (path.Contains((x, y)))
                    src = sandy.Contains((x, y))
                        ? PathSandy
                        : (rng.Next(4) == 0 ? PathMossy : PathClean);
                else if (dense.Contains((x, y)))
                    src = (rng.Next(3) == 0) ? GrassWeedy : GrassDense;
                else if (pathEdge.Contains((x, y)))
                    // Transition zone: always weedy grass for softer blend
                    src = GrassWeedy;
                else
                    src = (rng.Next(5) == 0) ? GrassWeedy : GrassPlain;
                _tiles[x, y] = new Tile(terrain, src, CellTopLeft(x, y), Vector2.Zero);
            }
        }

        // ----- Decorations -----
        // Centerpiece: temple at top of the central axis, fountain south of it
        AddProp(props, TempleRect, TempleAnchor, 24, 7, new Vector2(530f, 150f), scale: 5f);
        AddProp(props, FountainRect, FountainAnchor, 24, 14, FountainBox);

        // Courtyard pillars removed — they blocked fountain walkways

        // Additional pillars along the approach to the temple
        AddProp(props, Pillar2Rect, Pillar2Anchor, 22, 10, PillarBox);
        AddProp(props, Pillar2Rect, Pillar2Anchor, 26, 10, PillarBox);
        AddProp(props, Pillar2Rect, Pillar2Anchor, 21, 11, PillarBox);
        AddProp(props, Pillar2Rect, Pillar2Anchor, 27, 11, PillarBox);

        // Trees flanking the courtyard (make it feel like a clearing)
        AddProp(props, BigTreeRect, BigTreeAnchor, 20, 13, BigTreeBox);
        AddProp(props, BigTreeRect, BigTreeAnchor, 28, 13, BigTreeBox);
        AddProp(props, BigTree2Rect, BigTree2Anchor, 20, 16, BigTreeBox);
        AddProp(props, BigTree2Rect, BigTree2Anchor, 28, 16, BigTreeBox);

        // Trees between temple and courtyard
        AddProp(props, SmallTreeRect, SmallTreeAnchor, 22,  8, SmallTreeBox);
        AddProp(props, SmallTreeRect, SmallTreeAnchor, 26,  8, SmallTreeBox);
        AddProp(props, BareTreeRect, BareTreeAnchor, 21,  9, BareTreeBox);
        AddProp(props, BareTreeRect, BareTreeAnchor, 27,  9, BareTreeBox);

        // Scattered trees in the mid-map open areas
        AddProp(props, BigTreeRect, BigTreeAnchor,  18, 10, BigTreeBox);
        AddProp(props, BigTree2Rect, BigTree2Anchor, 30, 10, BigTreeBox);
        AddProp(props, SmallTreeRect, SmallTreeAnchor, 19, 12, SmallTreeBox);
        AddProp(props, SmallTreeRect, SmallTreeAnchor, 29, 12, SmallTreeBox);
        AddProp(props, BushRect, BushAnchor, 17,  8, BushBox);
        AddProp(props, BushRect, BushAnchor, 31,  8, BushBox);

        // ----- Northern forest band (rows 0-3) — big trees every ~3 cols across 50-wide map -----
        var bigTrees = new (int, int)[]
        {
            ( 1, 2), ( 3, 1), ( 5, 2), ( 7, 1), ( 9, 2),
            (11, 1), (13, 2), (15, 1), (17, 2), (19, 1),
            (21, 2), (23, 1), (25, 2), (27, 1), (29, 2),
            (31, 1), (33, 2), (35, 1), (37, 2), (39, 1),
            (41, 2), (43, 1), (45, 2), (47, 1), (49, 2),
        };
        foreach (var c in bigTrees) AddProp(props, BigTreeRect, BigTreeAnchor, c.Item1, c.Item2, BigTreeBox);
        // BigTree2 variants for variety
        AddProp(props, BigTree2Rect, BigTree2Anchor,  4, 0, BigTreeBox);
        AddProp(props, BigTree2Rect, BigTree2Anchor, 12, 0, BigTreeBox);
        AddProp(props, BigTree2Rect, BigTree2Anchor, 24, 0, BigTreeBox);
        AddProp(props, BigTree2Rect, BigTree2Anchor, 36, 0, BigTreeBox);
        AddProp(props, BigTree2Rect, BigTree2Anchor, 46, 0, BigTreeBox);

        // Small trees at row 4 scattered
        var smallTrees = new (int, int)[]
        {
            (2, 4), (6, 4), (10, 4), (14, 4), (18, 4),
            (22, 4), (28, 4), (32, 4), (38, 4), (44, 4), (48, 4),
        };
        foreach (var c in smallTrees) AddProp(props, SmallTreeRect, SmallTreeAnchor, c.Item1, c.Item2, SmallTreeBox);
        AddProp(props, BareTreeRect, BareTreeAnchor,  8, 4, BareTreeBox);
        AddProp(props, BareTreeRect, BareTreeAnchor, 16, 4, BareTreeBox);
        AddProp(props, BareTreeRect, BareTreeAnchor, 34, 4, BareTreeBox);
        AddProp(props, BareTreeRect, BareTreeAnchor, 42, 4, BareTreeBox);

        // ----- Western garden (cols 3-14, rows 14-24) -----
        var bushes = new (int, int)[]
        {
            ( 4, 15), ( 6, 14), ( 8, 15), ( 4, 17), ( 7, 17),
            (10, 16), ( 3, 19), ( 5, 20), ( 9, 21), (11, 20),
            ( 6, 22), ( 3, 23), ( 8, 23), (12, 22), (10, 24),
        };
        foreach (var c in bushes) AddProp(props, BushRect, BushAnchor, c.Item1, c.Item2, BushBox);
        var tiny = new (int, int)[]
        {
            ( 5, 15), ( 9, 16), ( 4, 18), (11, 18), ( 7, 19),
            ( 3, 21), (10, 22), ( 6, 24),
        };
        foreach (var c in tiny) AddProp(props, TinyTreeRect, TinyTreeAnchor, c.Item1, c.Item2, TinyTreeBox);
        AddProp(props, BushColumnRect, BushColumnAnchor,  7, 15, BushColumnBox);
        AddProp(props, BushColumnRect, BushColumnAnchor, 12, 19, BushColumnBox);
        AddProp(props, BushColumnRect, BushColumnAnchor,  3, 16, BushColumnBox);
        AddProp(props, SmallTreeRect, SmallTreeAnchor,  5, 17, SmallTreeBox);
        AddProp(props, SmallTreeRect, SmallTreeAnchor, 11, 21, SmallTreeBox);

        // ----- Practice field corner pillars (cells 35-42, 5-10) -----
        AddProp(props, PillarRect, PillarAnchor, 35,  5, PillarBox);
        AddProp(props, PillarRect, PillarAnchor, 42,  5, PillarBox);
        AddProp(props, PillarRect, PillarAnchor, 35, 10, PillarBox);
        AddProp(props, PillarRect, PillarAnchor, 42, 10, PillarBox);

        // ----- Eastern ruins (cols 33-47, rows 12-22) -----
        var ruinsPillars = new (int, int)[]
        {
            (34, 13), (37, 12), (40, 13), (43, 14), (36, 15),
            (39, 18), (42, 17), (45, 16), (44, 20), (34, 20),
            (38, 21), (46, 19), (33, 18),
        };
        foreach (var c in ruinsPillars) AddProp(props, Pillar2Rect, Pillar2Anchor, c.Item1, c.Item2, PillarBox);
        var walls = new (int, int)[]
        {
            (35, 14), (38, 15), (41, 16), (43, 19), (36, 20),
            (40, 21), (44, 17),
        };
        foreach (var c in walls) AddProp(props, WallRect, WallAnchor, c.Item1, c.Item2, WallBox);
        AddProp(props, BareTreeRect, BareTreeAnchor, 33, 14, BareTreeBox);
        AddProp(props, BareTreeRect, BareTreeAnchor, 47, 13, BareTreeBox);
        AddProp(props, BareTreeRect, BareTreeAnchor, 45, 21, BareTreeBox);
        AddProp(props, BareTreeRect, BareTreeAnchor, 33, 22, BareTreeBox);

        // ----- Southern grove (rows 23-28) -----
        var grove = new (int, int)[]
        {
            (14, 24), (17, 23), (20, 25), (22, 27), (26, 27),
            (29, 25), (32, 24), (19, 27), (28, 26),
        };
        foreach (var c in grove) AddProp(props, SmallTreeRect, SmallTreeAnchor, c.Item1, c.Item2, SmallTreeBox);
        AddProp(props, TinyTreeRect, TinyTreeAnchor, 16, 25, TinyTreeBox);
        AddProp(props, TinyTreeRect, TinyTreeAnchor, 31, 25, TinyTreeBox);
        AddProp(props, TinyTreeRect, TinyTreeAnchor, 21, 28, TinyTreeBox);
        AddProp(props, TinyTreeRect, TinyTreeAnchor, 27, 28, TinyTreeBox);
        AddProp(props, BushRect, BushAnchor, 18, 26, BushBox);
        AddProp(props, BushRect, BushAnchor, 30, 26, BushBox);

        // Spirit shrine — broken pillar with mystical presence
        AddProp(props, Pillar2Rect, Pillar2Anchor, 24, 26, Vector2.Zero);

        // ----- WEST border wall (cols 0-2, dense tree line, rows 5-27) -----
        for (int y = 5; y <= 27; y += 2)
            AddProp(props, BigTreeRect, BigTreeAnchor, 0, y, BigTreeBox);
        for (int y = 6; y <= 26; y += 2)
            AddProp(props, BigTree2Rect, BigTree2Anchor, 1, y, BigTreeBox);
        for (int y = 7; y <= 25; y += 3)
            AddProp(props, BigTreeRect, BigTreeAnchor, 2, y, BigTreeBox);

        // ----- EAST border wall (cols 47-49, dense tree line, rows 5-27) -----
        for (int y = 5; y <= 27; y += 2)
            AddProp(props, BigTreeRect, BigTreeAnchor, 49, y, BigTreeBox);
        for (int y = 6; y <= 26; y += 2)
            AddProp(props, BigTree2Rect, BigTree2Anchor, 48, y, BigTreeBox);
        for (int y = 7; y <= 25; y += 3)
            AddProp(props, BigTreeRect, BigTreeAnchor, 47, y, BigTreeBox);

        // ----- SOUTH border (rows 28-29, trees on sides, opening at cols 21-27) -----
        for (int x = 0; x <= 19; x += 2)
            AddProp(props, BigTreeRect, BigTreeAnchor, x, 29, BigTreeBox);
        for (int x = 28; x <= 49; x += 2)
            AddProp(props, BigTree2Rect, BigTree2Anchor, x, 29, BigTreeBox);
        AddProp(props, BushRect, BushAnchor, 20, 29, BushBox);
        AddProp(props, BushRect, BushAnchor, 27, 29, BushBox);

        _decorations.Sort((a, b) => a.sortKey.CompareTo(b.sortKey));
    }

    // Adds a decoration tile and (optionally) a collision rectangle. Pass
    // Vector2.Zero for collideSize to skip collision (e.g. the chalice).
    private void AddProp(Texture2D tex, Rectangle src, Vector2 anchor,
                         int x, int y, Vector2 collideSize, float scale = 3f)
    {
        var foot = GetCellFootPos(new Point(x, y));
        _decorations.Add((y, new Tile(tex, src, foot, anchor, scale)));
        if (collideSize == Vector2.Zero) return;
        _obstacles.Add(new Rectangle(
            (int)(foot.X - collideSize.X / 2f),
            (int)(foot.Y - collideSize.Y),
            (int)collideSize.X,
            (int)collideSize.Y));
    }

    private Vector2 CellTopLeft(int mapX, int mapY)
        => new(MAP_OFFSET.X + mapX * TILE_SCREEN, MAP_OFFSET.Y + mapY * TILE_SCREEN);

    public Vector2 GetCellFootPos(Point cell)
    {
        var tl = CellTopLeft(cell.X, cell.Y);
        return new Vector2(tl.X + TILE_SCREEN / 2f, tl.Y + TILE_SCREEN);
    }

    public (Vector2 min, Vector2 max) GetWalkBounds()
    {
        var min = GetCellFootPos(new Point(0, 0));
        var max = GetCellFootPos(new Point(MAP_SIZE.X - 1, MAP_SIZE.Y - 1));
        return (min, max);
    }

    public (Vector2 min, Vector2 max) GetPixelBounds()
        => (Vector2.Zero,
            new Vector2(MAP_OFFSET.X * 2 + MAP_SIZE.X * TILE_SCREEN,
                        MAP_OFFSET.Y * 2 + MAP_SIZE.Y * TILE_SCREEN));

    public void Update() { }

    public void Draw()
    {
        for (int y = 0; y < MAP_SIZE.Y; y++)
            for (int x = 0; x < MAP_SIZE.X; x++)
                _tiles[x, y].Draw();

        int playerKey = Player == null
            ? int.MaxValue
            : (int)((Player.FootPos.Y - MAP_OFFSET.Y) / TILE_SCREEN);
        bool playerDrawn = Player == null;
        foreach (var (sortKey, tile) in _decorations)
        {
            if (!playerDrawn && sortKey > playerKey)
            {
                Player.Draw(Player.FootPos);
                playerDrawn = true;
            }
            tile.Draw();
        }
        if (!playerDrawn)
            Player.Draw(Player.FootPos);
    }
}
