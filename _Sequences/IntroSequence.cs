namespace SelenesHollow;

// A scripted opening sequence: hold-on-black, fade in to the world, drop in
// letterbox bars and a typewriter dialog ("Where am I?"), wait for the player
// to acknowledge, then retract the bars and hand control back to GameManager.
public class IntroSequence
{
    // ----- Phase timings (seconds) -----
    private const float BLACK_HOLD       = 1.0f;   // pure black at the start
    private const float FADE_IN          = 2.0f;   // black -> scene
    private const float SETTLE_HOLD      = 0.6f;   // breathe before dialog
    private const float TYPE_SPEED       = 0.05f;  // seconds per character
    // After Space is pressed: fade everything to black, hold, then fade the
    // gameplay (with the player visible) up from black.
    private const float POST_FADE_OUT    = 0.8f;
    private const float POST_BLACK       = 0.5f;
    private const float POST_FADE_IN     = 1.0f;

    private const string DialogText = "Where am I?";
    private const int LETTERBOX_HEIGHT = 90;
    private const int DIALOG_PAD       = 24;

    private float _t;
    private float _dismissT;       // time elapsed since dismissal
    private bool _dismissTriggered;
    private KeyboardState _lastKb;
    private readonly SpriteFont _font;
    private readonly Texture2D _sleepTex;
    private readonly Vector2 _sleepWorldPos;
    // Centre of the visible figure inside selene_sleep.png (bbox 251..806 / 381..667).
    private static readonly Vector2 SleepOrigin = new(529f, 524f);
    // Match the in-game player size: standing player is ~93 px tall on
    // screen, so the lying figure (556 src px long) should be ~93 px wide.
    private const float SLEEP_SCALE = 0.17f;

    // True from the moment we enter the post-dismiss fade-IN — at that point
    // gameplay (with the standing player) is what should be drawn under the
    // fading black overlay, not the sleeping sprite.
    public bool PlayerVisible => _dismissTriggered && _dismissT >= POST_FADE_OUT + POST_BLACK;
    // True once the post-dismiss fade-in completes — intro stops drawing and
    // GameManager starts updating the player.
    public bool Finished => _dismissTriggered && _dismissT >= POST_FADE_OUT + POST_BLACK + POST_FADE_IN;

    public IntroSequence(Vector2 sleepWorldPos)
    {
        _font = Globals.Content.Load<SpriteFont>("dialog");
        _sleepTex = Globals.Content.Load<Texture2D>("selene_sleep");
        _sleepWorldPos = sleepWorldPos;
    }

    // ----- Phase helpers -----
    // 0 = full black, 1 = fully visible. Drives both the sleep/world tint
    // during the intro and the second fade-in to gameplay after dismissal.
    private float SceneAlpha
    {
        get
        {
            if (!_dismissTriggered)
            {
                if (_t <= BLACK_HOLD) return 0f;
                return Math.Clamp((_t - BLACK_HOLD) / FADE_IN, 0f, 1f);
            }
            if (_dismissT < POST_FADE_OUT)
                return 1f - _dismissT / POST_FADE_OUT;        // fade to black
            if (_dismissT < POST_FADE_OUT + POST_BLACK)
                return 0f;                                     // hold black
            return Math.Clamp(
                (_dismissT - POST_FADE_OUT - POST_BLACK) / POST_FADE_IN, 0f, 1f); // fade in
        }
    }

    // 0 = bars hidden, 1 = bars fully visible. Bars and dialog vanish entirely
    // once we cross into the post-dismiss black/fade-in — this is gameplay now.
    private float BarPresence
    {
        get
        {
            if (PlayerVisible) return 0f;
            float inProgress = Math.Clamp((_t - BLACK_HOLD) / FADE_IN, 0f, 1f);
            float p = inProgress;
            if (_dismissTriggered)
                p *= 1f - Math.Clamp(_dismissT / POST_FADE_OUT, 0f, 1f);
            return p;
        }
    }

    private float DialogStartTime => BLACK_HOLD + FADE_IN + SETTLE_HOLD;

    private int VisibleChars
    {
        get
        {
            float since = _t - DialogStartTime;
            if (since <= 0f) return 0;
            int n = (int)(since / TYPE_SPEED);
            return Math.Min(n, DialogText.Length);
        }
    }

    private bool TextFullyShown => VisibleChars >= DialogText.Length;

    public void Update()
    {
        _t += Globals.TotalSeconds;

        var kb = Keyboard.GetState();
        bool advance = (kb.IsKeyDown(Keys.Space) || kb.IsKeyDown(Keys.Enter))
                       && _lastKb.IsKeyUp(Keys.Space) && _lastKb.IsKeyUp(Keys.Enter);
        if (TextFullyShown && advance && !_dismissTriggered)
            _dismissTriggered = true;

        if (_dismissTriggered)
            _dismissT += Globals.TotalSeconds;

        _lastKb = kb;
    }

    // ----- World-space pass: just the sleeping Selene at the fountain -----
    public void DrawWorld()
    {
        if (PlayerVisible) return;
        Globals.SpriteBatch.Draw(
            _sleepTex,
            _sleepWorldPos,
            null,
            Color.White * SceneAlpha,
            rotation: 0f,
            origin: SleepOrigin,
            scale: SLEEP_SCALE,
            effects: SpriteEffects.None,
            layerDepth: 0f);
    }

    // ----- Screen-space pass: black overlay, letterbox bars, dialog -----
    public void DrawUI()
    {
        var sb = Globals.SpriteBatch;
        var screen = Globals.GraphicsDevice.Viewport.Bounds;

        // ----- Black overlay (full at t=0, fades out as scene reveals) -----
        float blackAlpha = 1f - SceneAlpha;
        if (blackAlpha > 0f)
            sb.Draw(Globals.Pixel, screen, Color.Black * blackAlpha);

        // ----- Letterbox bars sliding in/out from top and bottom -----
        float bar = BarPresence;
        if (bar > 0f)
        {
            int h = (int)(LETTERBOX_HEIGHT * bar);
            sb.Draw(Globals.Pixel, new Rectangle(0, 0, screen.Width, h), Color.Black);
            sb.Draw(Globals.Pixel, new Rectangle(0, screen.Height - h, screen.Width, h), Color.Black);
        }

        // ----- Dialog typewriter inside the top bar -----
        if (_t >= DialogStartTime && !PlayerVisible)
        {
            int chars = VisibleChars;
            string visible = DialogText.Substring(0, chars);
            var fullSize = _font.MeasureString(DialogText);

            // Dialog box behind the text — soft dark panel
            int boxW = (int)fullSize.X + DIALOG_PAD * 2;
            int boxH = (int)fullSize.Y + DIALOG_PAD;
            int boxX = (screen.Width - boxW) / 2;
            int boxY = LETTERBOX_HEIGHT / 2 - boxH / 2;
            float boxAlpha = _dismissTriggered
                ? 1f - Math.Clamp(_dismissT / POST_FADE_OUT, 0f, 1f)
                : 1f;
            sb.Draw(Globals.Pixel, new Rectangle(boxX, boxY, boxW, boxH), new Color(15, 15, 25) * (0.85f * boxAlpha));
            // Top + bottom border lines for the panel
            sb.Draw(Globals.Pixel, new Rectangle(boxX, boxY, boxW, 2), Color.White * (0.6f * boxAlpha));
            sb.Draw(Globals.Pixel, new Rectangle(boxX, boxY + boxH - 2, boxW, 2), Color.White * (0.6f * boxAlpha));

            var textPos = new Vector2(boxX + DIALOG_PAD, boxY + DIALOG_PAD / 2);
            sb.DrawString(_font, visible, textPos, Color.White * boxAlpha);

            // Continue hint (pulses softly) once text is fully revealed —
            // sits centred inside the bottom letterbox bar.
            if (TextFullyShown && !_dismissTriggered)
            {
                float pulse = 0.5f + 0.5f * (float)Math.Sin(_t * 4f);
                string hint = "[Space]";
                var hintSize = _font.MeasureString(hint);
                var hintPos = new Vector2(
                    (screen.Width - hintSize.X) / 2f,
                    screen.Height - LETTERBOX_HEIGHT / 2f - hintSize.Y / 2f);
                sb.DrawString(_font, hint, hintPos, Color.White * (0.4f + 0.4f * pulse));
            }
        }
    }
}
