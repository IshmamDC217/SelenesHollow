namespace SelenesHollow;

// Square top-down grid à la Sea of Stars. 32x32 source tiles drawn at 3x for
// 96 px on-screen cells. The world is 30x20 = a couple of screens in each
// direction, with the camera following Selene as she explores. Decorations
// (trees, ruins, fountain, temple, chalice) come from a separate props atlas
// and are anchored at their base. Each one optionally registers a base AABB
// in _obstacles so the player can collide with it. Painter's algorithm sorts
// by row so Selene walks behind/in front of objects naturally.
public class Map
{
    private const int TILE_SRC = 32;
    private const int DRAW_SCALE = 3;
    public const int TILE_SCREEN = TILE_SRC * DRAW_SCALE;   // 96 px

    private readonly Point MAP_SIZE = new(30, 20);
    private readonly Vector2 MAP_OFFSET = new(16f, 16f);

    // Where the fountain prop is anchored. Used by IntroSequence to place the
    // sleeping Selene on the pedestal. Pushed south so the temple at row 4
    // and the fountain don't visually overlap.
    public static readonly Point FountainCell = new(14, 9);

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
    private static readonly Vector2 FountainBox    = new(300f, 170f);
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
        // Courtyard around the fountain (cell 14,9) plus paths radiating out
        // toward the western garden, eastern ruins and southern grove.
        var path = new HashSet<(int, int)>();
        for (int x = 12; x <= 16; x++)
            for (int y = 8; y <= 10; y++)
                path.Add((x, y));
        // South spine — main exploration trail
        for (int y = 9; y <= 18; y++) path.Add((14, y));
        // West branch into the garden
        for (int x = 4; x <= 14; x++) path.Add((x, 14));
        // East branch into the ruins
        for (int x = 14; x <= 26; x++) path.Add((x, 13));
        // Side path into the southern grove (chalice clearing)
        for (int y = 14; y <= 17; y++) path.Add((15, y));

        // Sandy variant marks the ruins approach
        var sandy = new HashSet<(int, int)>();
        for (int x = 22; x <= 26; x++) sandy.Add((x, 13));

        // Dense grass marks the forest band along the top of the map
        var dense = new HashSet<(int, int)>();
        for (int x = 0; x < MAP_SIZE.X; x++)
            for (int y = 0; y <= 2; y++)
                dense.Add((x, y));

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
                else
                    src = (rng.Next(5) == 0) ? GrassWeedy : GrassPlain;
                _tiles[x, y] = new Tile(terrain, src, CellTopLeft(x, y), Vector2.Zero);
            }
        }

        // ----- Decorations -----
        // Centerpiece: temple at top of the central axis, fountain south of it
        AddProp(props, TempleRect, TempleAnchor, 14, 4, TempleBox);
        AddProp(props, FountainRect, FountainAnchor, 14, 9, FountainBox);

        // Pillars at courtyard corners
        AddProp(props, PillarRect, PillarAnchor, 12,  8, PillarBox);
        AddProp(props, PillarRect, PillarAnchor, 16,  8, PillarBox);
        AddProp(props, PillarRect, PillarAnchor, 12, 10, PillarBox);
        AddProp(props, PillarRect, PillarAnchor, 16, 10, PillarBox);

        // ----- Northern forest band (rows 0-2) -----
        var bigTrees = new (int, int)[]
        {
            (1, 2), (3, 1), (5, 2), (7, 1), (9, 2),
            (11, 1), (13, 2), (16, 2), (18, 1), (20, 2),
            (22, 1), (24, 2), (26, 1), (28, 2),
        };
        foreach (var c in bigTrees) AddProp(props, BigTreeRect, BigTreeAnchor, c.Item1, c.Item2, BigTreeBox);
        AddProp(props, BigTree2Rect, BigTree2Anchor,  4, 0, BigTreeBox);
        AddProp(props, BigTree2Rect, BigTree2Anchor, 17, 0, BigTreeBox);
        AddProp(props, BigTree2Rect, BigTree2Anchor, 27, 0, BigTreeBox);

        var smallTrees = new (int, int)[]
        {
            (2, 3), (6, 3), (10, 3), (15, 3), (19, 3), (23, 3), (27, 3),
        };
        foreach (var c in smallTrees) AddProp(props, SmallTreeRect, SmallTreeAnchor, c.Item1, c.Item2, SmallTreeBox);
        AddProp(props, BareTreeRect, BareTreeAnchor,  8, 3, BareTreeBox);
        AddProp(props, BareTreeRect, BareTreeAnchor, 21, 3, BareTreeBox);

        // ----- Western garden (cols 0-6, rows 8-16) -----
        var bushes = new (int, int)[]
        {
            (1, 8), (3, 9), (5, 8), (1, 10), (3, 11), (5, 11),
            (2, 13), (1, 15), (4, 15), (6, 15), (3, 16),
        };
        foreach (var c in bushes) AddProp(props, BushRect, BushAnchor, c.Item1, c.Item2, BushBox);
        var tiny = new (int, int)[]
        {
            (2, 9), (4, 10), (1, 12), (5, 13), (2, 16),
        };
        foreach (var c in tiny) AddProp(props, TinyTreeRect, TinyTreeAnchor, c.Item1, c.Item2, TinyTreeBox);
        AddProp(props, BushColumnRect, BushColumnAnchor, 6,  9, BushColumnBox);
        AddProp(props, BushColumnRect, BushColumnAnchor, 6, 13, BushColumnBox);
        AddProp(props, BushColumnRect, BushColumnAnchor, 0, 11, BushColumnBox);

        // ----- Eastern ruins (cols 21-29, rows 8-15) -----
        var ruinsPillars = new (int, int)[]
        {
            (22, 9), (24, 8), (26, 9), (28, 10), (23, 11),
            (25, 14), (27, 15), (29, 14), (22, 15),
        };
        foreach (var c in ruinsPillars) AddProp(props, Pillar2Rect, Pillar2Anchor, c.Item1, c.Item2, PillarBox);
        var walls = new (int, int)[]
        {
            (24, 11), (26, 12), (23, 14), (25, 12), (28, 14),
        };
        foreach (var c in walls) AddProp(props, WallRect, WallAnchor, c.Item1, c.Item2, WallBox);
        AddProp(props, BareTreeRect, BareTreeAnchor, 21, 10, BareTreeBox);
        AddProp(props, BareTreeRect, BareTreeAnchor, 29,  9, BareTreeBox);

        // ----- Southern grove (rows 15-19) -----
        var grove = new (int, int)[]
        {
            (8, 16), (10, 17), (12, 18), (16, 18), (18, 16),
            (20, 17), (22, 18), (6, 18),
        };
        foreach (var c in grove) AddProp(props, SmallTreeRect, SmallTreeAnchor, c.Item1, c.Item2, SmallTreeBox);
        AddProp(props, TinyTreeRect, TinyTreeAnchor, 11, 19, TinyTreeBox);
        AddProp(props, TinyTreeRect, TinyTreeAnchor, 17, 19, TinyTreeBox);
        AddProp(props, BushRect, BushAnchor, 12, 19, BushBox);

        // The chalice — interactable, no collision
        AddProp(props, ChaliceRect, ChaliceAnchor, 14, 17, Vector2.Zero);

        // East/west edge garden touches so the map borders feel intentional
        AddProp(props, BushRect, BushAnchor, 27, 6, BushBox);
        AddProp(props, BushRect, BushAnchor, 28, 7, BushBox);
        AddProp(props, BushRect, BushAnchor, 29, 5, BushBox);
        AddProp(props, BushRect, BushAnchor,  0, 6, BushBox);
        AddProp(props, BushRect, BushAnchor,  0, 8, BushBox);

        _decorations.Sort((a, b) => a.sortKey.CompareTo(b.sortKey));
    }

    // Adds a decoration tile and (optionally) a collision rectangle. Pass
    // Vector2.Zero for collideSize to skip collision (e.g. the chalice).
    private void AddProp(Texture2D tex, Rectangle src, Vector2 anchor,
                         int x, int y, Vector2 collideSize)
    {
        var foot = GetCellFootPos(new Point(x, y));
        _decorations.Add((y, new Tile(tex, src, foot, anchor)));
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
