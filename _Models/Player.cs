namespace SelenesHollow;

public enum AnimState
{
    Idle,      // walk sheet — gentle in-place loop
    RunUp,     // iso_run_up — moving north
    RunDown,   // iso_run_down — moving south
    RunLeft,   // run sheet, horizontally flipped
    RunRight,  // run sheet
    Jump,      // jump sheet, plays once
}

// Selene the player character. Each of her sheets is a 5x5 grid of 256x256
// frames. We pick a sheet based on movement state and cycle through its frames
// to animate. Movement is continuous in pixel space — held keys produce smooth
// running, not grid-snap teleports.
public class Player
{
    private const int FRAME_SIZE = 256;
    private const int FRAMES_PER_ROW = 5;
    private const int TOTAL_FRAMES = 25;
    private const float FRAME_DURATION = 0.06f;     // ~16fps animation
    private const float IDLE_FRAME_DURATION = 0.14f;   // ping-pong idle bob
    // Idle reuses settled poses from the jump sheet: row 4 cols 4-5 and
    // row 5 cols 1-3 (1-indexed) = frames 18, 19, 20, 21, 22. Each frame has
    // its body at a slightly different y in the source, so we override the
    // origin Y per frame to keep her feet planted instead of floating.
    // Ping-pong: 0→1→2→3→2→1→0→1→... so the loop is smooth
    private static readonly int[] IdleFrames = { 18, 19, 20, 21, 20, 19 };
    private static readonly float[] IdleOriginY = { 203f, 194f, 192f, 193f, 192f, 194f };
    private const float RUN_SPEED = 260f;           // pixels per second
    private const float DRAW_SCALE = 0.5f;          // smaller character per request
    // Character bbox center (horizontal) and feet (vertical) measured from
    // frame (0,0) of iso_run_down: x bbox 78..178 (center 128), y feet ~221.
    private static readonly Vector2 SpriteOrigin = new(128f, 221f);

    private readonly Texture2D _walk;
    private readonly Texture2D _shadow;
    private readonly Texture2D _runIsoDown;
    private readonly Texture2D _runIsoUp;
    private readonly Texture2D _run;
    private readonly Texture2D _jump;

    private AnimState _state = AnimState.Idle;
    private int _frameIndex;
    private float _frameTimer;
    private KeyboardState _lastKb;
    private bool _facingLeft;       // tracks last horizontal direction for idle mirroring

    public Vector2 FootPos { get; set; }
    public bool IsHidden { get; set; }
    public bool IsMoving { get; private set; }

    public Player(Vector2 startFootPos)
    {
        _walk       = Globals.Content.Load<Texture2D>("selene_walk");
        _shadow     = BuildShadowTexture(64, 24);
        _runIsoDown = Globals.Content.Load<Texture2D>("selene_run_down");
        _runIsoUp   = Globals.Content.Load<Texture2D>("selene_run_up");
        _run        = Globals.Content.Load<Texture2D>("selene_run");
        _jump       = Globals.Content.Load<Texture2D>("selene_jump");
        FootPos = startFootPos;
    }

    // Player collision footprint, expressed as offsets from FootPos.
    // Selene's feet are at the bottom-centre of this rect.
    private const int BBOX_W = 28;
    private const int BBOX_H = 16;

    private static bool CollidesAt(Vector2 footPos, IReadOnlyList<Rectangle> obstacles)
    {
        var r = new Rectangle(
            (int)(footPos.X - BBOX_W / 2f),
            (int)(footPos.Y - BBOX_H),
            BBOX_W, BBOX_H);
        for (int i = 0; i < obstacles.Count; i++)
            if (obstacles[i].Intersects(r)) return true;
        return false;
    }

    public void Update(Vector2 minFoot, Vector2 maxFoot, IReadOnlyList<Rectangle> obstacles)
    {
        var kb = Keyboard.GetState();
        var dt = Globals.TotalSeconds;
        // ----- Read movement vector -----
        Vector2 dir = Vector2.Zero;
        if (kb.IsKeyDown(Keys.W)) dir.Y -= 1f;
        if (kb.IsKeyDown(Keys.S)) dir.Y += 1f;
        if (kb.IsKeyDown(Keys.A)) dir.X -= 1f;
        if (kb.IsKeyDown(Keys.D)) dir.X += 1f;
        bool moving = dir != Vector2.Zero;
        if (moving && dir.LengthSquared() > 1f) dir.Normalize();

        // ----- State selection (jump removed — Space is used for battle/dialog) -----
        AnimState newState;
        if (!moving)
            newState = AnimState.Idle;
        else if (Math.Abs(dir.Y) > Math.Abs(dir.X))
            newState = dir.Y < 0 ? AnimState.RunUp : AnimState.RunDown;
        else
            newState = dir.X < 0 ? AnimState.RunLeft : AnimState.RunRight;

        // Track last horizontal direction for idle mirroring
        if (dir.X < 0) _facingLeft = true;
        else if (dir.X > 0) _facingLeft = false;

        if (newState != _state)
        {
            _state = newState;
            _frameIndex = 0;
            _frameTimer = 0f;
        }

        // ----- Frame timing -----
        // Idle uses a slower 2-frame bob; everything else cycles all 25 frames.
        float duration = _state == AnimState.Idle ? IDLE_FRAME_DURATION : FRAME_DURATION;
        _frameTimer += dt;
        if (_frameTimer >= duration)
        {
            _frameTimer -= duration;
            _frameIndex++;
            if (_state == AnimState.Idle)
            {
                // Cycle through IdleFrames; _frameIndex stores the position
                // within that small array, not the global frame number.
                _frameIndex %= IdleFrames.Length;
            }
            else
            {
                _frameIndex %= TOTAL_FRAMES;
            }
        }

        // ----- Continuous movement -----
        // Don't drift while jumping in place.
        IsMoving = moving;
        if (IsMoving)
        {
            // Try X and Y as separate steps so that bumping into a wall on
            // one axis still allows sliding along it on the other axis.
            float stepX = dir.X * RUN_SPEED * dt;
            float stepY = dir.Y * RUN_SPEED * dt;

            var attempt = new Vector2(
                Math.Clamp(FootPos.X + stepX, minFoot.X, maxFoot.X),
                FootPos.Y);
            if (!CollidesAt(attempt, obstacles))
                FootPos = attempt;

            attempt = new Vector2(
                FootPos.X,
                Math.Clamp(FootPos.Y + stepY, minFoot.Y, maxFoot.Y));
            if (!CollidesAt(attempt, obstacles))
                FootPos = attempt;
        }

        _lastKb = kb;
    }

    // Soft elliptical drop-shadow built at startup. Black with alpha falloff
    // toward the edge so it looks like a feathered blob, no asset required.
    private static Texture2D BuildShadowTexture(int w, int h)
    {
        var tex = new Texture2D(Globals.GraphicsDevice, w, h);
        var pixels = new Color[w * h];
        float cx = (w - 1) / 2f;
        float cy = (h - 1) / 2f;
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            float dx = (x - cx) / cx;
            float dy = (y - cy) / cy;
            float d = dx * dx + dy * dy;       // 0 at centre, 1 at ellipse edge
            if (d >= 1f) { pixels[y * w + x] = Color.Transparent; continue; }
            // Feather: full alpha in inner 60%, falling off to 0 at the edge.
            float a = d <= 0.6f ? 1f : 1f - (d - 0.6f) / 0.4f;
            pixels[y * w + x] = new Color(0, 0, 0, (int)(a * 110));
        }
        tex.SetData(pixels);
        return tex;
    }

    public void Draw(Vector2 footScreenPos)
    {
        if (IsHidden) return;
        // Drop shadow under her feet — drawn first so the sprite covers most of it.
        var shadowOrigin = new Vector2(_shadow.Width / 2f, _shadow.Height / 2f);
        Globals.SpriteBatch.Draw(
            _shadow,
            footScreenPos + new Vector2(0f, -4f),
            null,
            Color.White,
            rotation: 0f,
            origin: shadowOrigin,
            scale: 1f,
            effects: SpriteEffects.None,
            layerDepth: 0f);

        var (sheet, flip) = _state switch
        {
            AnimState.Idle     => (_jump, _facingLeft ? SpriteEffects.FlipHorizontally : SpriteEffects.None),
            AnimState.RunUp    => (_runIsoUp,   SpriteEffects.None),
            AnimState.RunDown  => (_runIsoDown, SpriteEffects.None),
            AnimState.RunLeft  => (_run,        SpriteEffects.FlipHorizontally),
            AnimState.RunRight => (_run,        SpriteEffects.None),
            AnimState.Jump     => (_jump,       SpriteEffects.None),
            _                  => (_runIsoDown, SpriteEffects.None),
        };
        // For idle, _frameIndex is an index into IdleFrames; map it to the
        // actual jump-sheet frame number before computing src rect.
        int globalFrame = _state == AnimState.Idle ? IdleFrames[_frameIndex] : _frameIndex;
        int col = globalFrame % FRAMES_PER_ROW;
        int row = globalFrame / FRAMES_PER_ROW;
        var src = new Rectangle(col * FRAME_SIZE, row * FRAME_SIZE, FRAME_SIZE, FRAME_SIZE);
        var origin = _state == AnimState.Idle
            ? new Vector2(SpriteOrigin.X, IdleOriginY[_frameIndex])
            : SpriteOrigin;
        Globals.SpriteBatch.Draw(
            sheet,
            footScreenPos,
            src,
            Color.White,
            rotation: 0f,
            origin: origin,
            scale: DRAW_SCALE,
            effects: flip,
            layerDepth: 0f);
    }
}
