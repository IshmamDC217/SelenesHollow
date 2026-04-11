namespace SelenesHollow;

// Scripted opening sequence, expanded with a spirit guide ("Wisp") encounter.
//
// Phase flow:
//   BLACK_HOLD → FADE_IN → SETTLE_HOLD → TYPEWRITER ("Where am I?") →
//   WAIT_DISMISS → SPIRIT_ENTER (Wisp floats in) → SPIRIT_DIALOG (MMBN boxes) →
//   SPIRIT_EXIT → POST_FADE_OUT → POST_BLACK → POST_FADE_IN → FINISHED
public class IntroSequence
{
    private enum Phase
    {
        BlackHold,
        FadeIn,
        SettleHold,
        Typewriter,
        WaitDismiss,
        SpiritEnter,
        SpiritDialog,
        SpiritExit,
        PostFadeOut,
        PostBlack,
        PostFadeIn,
        Finished,
    }

    // ----- Phase timings (seconds) -----
    private const float BLACK_HOLD    = 1.0f;
    private const float FADE_IN       = 2.0f;
    private const float SETTLE_HOLD   = 0.6f;
    private const float TYPE_SPEED    = 0.05f;
    private const float SPIRIT_ENTER_DUR = 1.6f;
    private const float SPIRIT_EXIT_DUR  = 0.8f;
    private const float POST_FADE_OUT = 0.8f;
    private const float POST_BLACK    = 0.5f;
    private const float POST_FADE_IN  = 1.0f;

    private const string DialogText = "Where am I?";
    private const int LETTERBOX_HEIGHT = 90;
    private const int DIALOG_PAD = 24;

    // ----- Sleeping Selene -----
    private static readonly Vector2 SleepOrigin = new(529f, 524f);
    private const float SLEEP_SCALE = 0.17f;

    // ----- Spirit world-space settings -----
    private const float SPIRIT_SIZE = 38f;          // drawn diameter on screen
    private const float SPIRIT_BOB_SPEED = 3.2f;
    private const float SPIRIT_BOB_AMP = 6f;

    // ----- Watching spirits (different-colored glows around the fountain) -----
    private record struct WatcherSpirit(Vector2 Offset, Color Tint, float Phase, float Speed, float Amp);
    private static readonly WatcherSpirit[] Watchers =
    {
        new(new(-180f,  -80f), new Color(255, 120, 120), 0.0f, 1.8f, 8f),  // red
        new(new( 200f,  -60f), new Color(120, 255, 160), 1.2f, 2.1f, 7f),  // green
        new(new(-140f,   50f), new Color(120, 160, 255), 2.5f, 1.6f, 9f),  // blue
        new(new( 160f,   80f), new Color(255, 220,  80), 0.8f, 2.4f, 6f),  // gold
        new(new(-250f,  -20f), new Color(200, 130, 255), 3.1f, 1.9f, 8f),  // purple
        new(new( 270f,   20f), new Color(255, 180, 140), 1.7f, 2.0f, 7f),  // peach
        new(new(  -60f, 120f), new Color(140, 255, 255), 0.4f, 2.3f, 6f),  // cyan
        new(new(  80f, -130f), new Color(255, 160, 220), 2.0f, 1.7f, 9f),  // pink
    };

    // ----- State -----
    private Phase _phase = Phase.BlackHold;
    private float _phaseT;          // time within current phase
    private float _totalT;          // total elapsed (for typewriter timing)
    private KeyboardState _lastKb;

    private readonly SpriteFont _font;
    private readonly Texture2D _sleepTex;
    private readonly Vector2 _sleepWorldPos;

    // Spirit textures (procedural)
    private readonly Texture2D _spiritGlow;
    private readonly Texture2D _spiritPortrait;
    private Vector2 _spiritWorldPos;
    private Vector2 _spiritStartPos;
    private Vector2 _spiritEndPos;

    // Portrait dialog (owned by GameManager, ref'd here for the spirit convo)
    private readonly PortraitDialog _dialog;
    private bool _dialogQueued;

    // Selene's talking portrait (loaded if available)
    private readonly Texture2D _seleneTalking;
    private readonly int _seleneTalkFrameW;
    private readonly int _seleneTalkFrameH;

    // ----- Public flags -----
    public bool PlayerVisible =>
        _phase == Phase.PostFadeIn || _phase == Phase.Finished ||
        (_phase == Phase.PostBlack);

    public bool Finished => _phase == Phase.Finished;
    public Texture2D SpiritPortraitTexture => _spiritPortrait;

    public IntroSequence(Vector2 sleepWorldPos, PortraitDialog dialog)
    {
        _font = Globals.Content.Load<SpriteFont>("dialog");
        _sleepTex = Globals.Content.Load<Texture2D>("selene_sleep");
        _sleepWorldPos = sleepWorldPos;
        _dialog = dialog;

        // Try to load Selene's talking portrait
        try
        {
            _seleneTalking = Globals.Content.Load<Texture2D>("selenetalking");
            // 2 frames stacked vertically: top = mouth closed, bottom = mouth open
            _seleneTalkFrameW = _seleneTalking.Width;
            _seleneTalkFrameH = _seleneTalking.Height / 2;
        }
        catch
        {
            _seleneTalking = null;
        }

        // Procedural spirit glow + portrait
        _spiritGlow = BuildSpiritGlow(64);
        _spiritPortrait = BuildSpiritPortrait(128);

        // Spirit entry positions (relative to sleeping Selene)
        _spiritEndPos = sleepWorldPos + new Vector2(90f, -10f);
        _spiritStartPos = _spiritEndPos + new Vector2(0f, -250f);
        _spiritWorldPos = _spiritStartPos;
    }

    // ----- Computed helpers -----

    private float SceneAlpha
    {
        get
        {
            return _phase switch
            {
                Phase.BlackHold => 0f,
                Phase.FadeIn => Math.Clamp(_phaseT / FADE_IN, 0f, 1f),
                Phase.PostFadeOut => 1f - Math.Clamp(_phaseT / POST_FADE_OUT, 0f, 1f),
                Phase.PostBlack => 0f,
                Phase.PostFadeIn => Math.Clamp(_phaseT / POST_FADE_IN, 0f, 1f),
                Phase.Finished => 1f,
                _ => 1f,
            };
        }
    }

    private float BarPresence
    {
        get
        {
            if (PlayerVisible) return 0f;
            if (_phase == Phase.BlackHold) return 0f;
            float inP = _phase == Phase.FadeIn
                ? Math.Clamp(_phaseT / FADE_IN, 0f, 1f)
                : 1f;
            if (_phase == Phase.PostFadeOut)
                inP *= 1f - Math.Clamp(_phaseT / POST_FADE_OUT, 0f, 1f);
            return inP;
        }
    }

    private float TypewriterStart => BLACK_HOLD + FADE_IN + SETTLE_HOLD;

    private int VisibleChars
    {
        get
        {
            float since = _totalT - TypewriterStart;
            if (since <= 0f) return 0;
            return Math.Min((int)(since / TYPE_SPEED), DialogText.Length);
        }
    }

    private bool TextFullyShown => VisibleChars >= DialogText.Length;

    // Spirit visibility: visible from SpiritEnter through SpiritExit
    private bool SpiritVisible =>
        _phase is Phase.SpiritEnter or Phase.SpiritDialog or Phase.SpiritExit;

    private float SpiritAlpha
    {
        get
        {
            if (_phase == Phase.SpiritEnter)
                return Math.Clamp(_phaseT / (SPIRIT_ENTER_DUR * 0.5f), 0f, 1f);
            if (_phase == Phase.SpiritExit)
                return 1f - Math.Clamp(_phaseT / SPIRIT_EXIT_DUR, 0f, 1f);
            return SpiritVisible ? 1f : 0f;
        }
    }

    // ----- Update -----
    public void Update()
    {
        float dt = Globals.TotalSeconds;
        _totalT += dt;
        _phaseT += dt;

        var kb = Keyboard.GetState();
        bool advance = (kb.IsKeyDown(Keys.Space) || kb.IsKeyDown(Keys.Enter))
                       && _lastKb.IsKeyUp(Keys.Space) && _lastKb.IsKeyUp(Keys.Enter);
        _lastKb = kb;

        switch (_phase)
        {
            case Phase.BlackHold:
                if (_phaseT >= BLACK_HOLD) Advance(Phase.FadeIn);
                break;

            case Phase.FadeIn:
                if (_phaseT >= FADE_IN) Advance(Phase.SettleHold);
                break;

            case Phase.SettleHold:
                if (_phaseT >= SETTLE_HOLD) Advance(Phase.Typewriter);
                break;

            case Phase.Typewriter:
                if (advance && !TextFullyShown)
                    _totalT = TypewriterStart + DialogText.Length * TYPE_SPEED + 1f;
                if (TextFullyShown) Advance(Phase.WaitDismiss);
                break;

            case Phase.WaitDismiss:
                if (advance) Advance(Phase.SpiritEnter);
                break;

            case Phase.SpiritEnter:
            {
                float p = Math.Clamp(_phaseT / SPIRIT_ENTER_DUR, 0f, 1f);
                // Ease-out cubic
                float ease = 1f - (1f - p) * (1f - p) * (1f - p);
                _spiritWorldPos = Vector2.Lerp(_spiritStartPos, _spiritEndPos, ease);
                if (_phaseT >= SPIRIT_ENTER_DUR)
                    Advance(Phase.SpiritDialog);
                break;
            }

            case Phase.SpiritDialog:
                if (!_dialogQueued)
                {
                    QueueSpiritDialog();
                    _dialogQueued = true;
                }
                // Stay in this phase until the portrait dialog finishes
                if (!_dialog.IsActive)
                    Advance(Phase.SpiritExit);
                break;

            case Phase.SpiritExit:
            {
                float p = Math.Clamp(_phaseT / SPIRIT_EXIT_DUR, 0f, 1f);
                // Shrink + float up
                _spiritWorldPos = Vector2.Lerp(
                    _spiritEndPos,
                    _spiritEndPos + new Vector2(0f, -60f), p);
                if (_phaseT >= SPIRIT_EXIT_DUR)
                    Advance(Phase.PostFadeOut);
                break;
            }

            case Phase.PostFadeOut:
                if (_phaseT >= POST_FADE_OUT) Advance(Phase.PostBlack);
                break;

            case Phase.PostBlack:
                if (_phaseT >= POST_BLACK) Advance(Phase.PostFadeIn);
                break;

            case Phase.PostFadeIn:
                if (_phaseT >= POST_FADE_IN) Advance(Phase.Finished);
                break;
        }

        // Bob the spirit gently while visible
        if (SpiritVisible)
        {
            float bob = SPIRIT_BOB_AMP * (float)Math.Sin(_totalT * SPIRIT_BOB_SPEED);
            _spiritWorldPos.Y += bob * dt;
        }
    }

    private void Advance(Phase next)
    {
        _phase = next;
        _phaseT = 0f;
    }

    private void QueueSpiritDialog()
    {
        var selenePortrait = _seleneTalking;
        int seleneFrames = _seleneTalking != null ? 2 : 1;
        int seleneFW = _seleneTalkFrameW;
        int seleneFH = _seleneTalkFrameH;
        // If selenetalking.png isn't available, fall back to sleep texture
        if (selenePortrait == null)
        {
            selenePortrait = _sleepTex;
            seleneFrames = 1;
            seleneFW = 0;
            seleneFH = 0;
        }

        _dialog.ShowSequence(
            new PortraitDialog.Line("Wisp", "Oh! You're awake!",
                _spiritPortrait),

            new PortraitDialog.Line("Selene", "Who... what are you?",
                selenePortrait, seleneFrames, seleneFW, seleneFH),

            new PortraitDialog.Line("Wisp", "I'm Wisp! I've been here a long, long time... waiting for someone like you.",
                _spiritPortrait),

            new PortraitDialog.Line("Selene", "I don't remember anything. Where is this place?",
                selenePortrait, seleneFrames, seleneFW, seleneFH),

            new PortraitDialog.Line("Wisp", "This is the Spirit World. It's beautiful, but... not everything here is friendly.",
                _spiritPortrait),

            new PortraitDialog.Line("Wisp", "To survive here, you need to be able to wield one of the elements. Can you do anything like that?",
                _spiritPortrait),

            new PortraitDialog.Line("Selene", "Actually... yes. I can wield fire. I know a few fire moves.",
                selenePortrait, seleneFrames, seleneFW, seleneFH),

            new PortraitDialog.Line("Wisp", "Wait, really?! You can use fire? That's amazing!",
                _spiritPortrait),

            new PortraitDialog.Line("Selene", "My family comes from a long line of fire wielders. We descend from a noble clan that once used fire in battle.",
                selenePortrait, seleneFrames, seleneFW, seleneFH),

            new PortraitDialog.Line("Selene", "But that was a long time ago. Nowadays people just use their elements for everyday things... cooking, keeping the house warm, lighting the fireplace.",
                selenePortrait, seleneFrames, seleneFW, seleneFH),

            new PortraitDialog.Line("Wisp", "Cooking and fireplaces? You have FIRE and you use it for CHORES?",
                _spiritPortrait),

            new PortraitDialog.Line("Selene", "That's just how things are now. Nobody really fights with elements anymore.",
                selenePortrait, seleneFrames, seleneFW, seleneFH),

            new PortraitDialog.Line("Wisp", "Well, things are different here. You're going to need those fire moves for more than warming soup.",
                _spiritPortrait),

            new PortraitDialog.Line("Wisp", "How about a little test? I want to see what you can actually do!",
                _spiritPortrait),

            new PortraitDialog.Line("Selene", "A test? Already?",
                selenePortrait, seleneFrames, seleneFW, seleneFH),

            new PortraitDialog.Line("Wisp", "No better time than now! Come on, it'll be fun. Probably.",
                _spiritPortrait)
        );
    }

    // ----- World-space drawing: sleeping Selene + spirits -----
    public void DrawWorld()
    {
        if (PlayerVisible) return;

        float sa = SceneAlpha;

        // Watching spirits — colorful glows floating around the fountain
        if (_phase < Phase.PostFadeOut && sa > 0f)
        {
            var glowOrigin = new Vector2(32f, 32f);
            foreach (var w in Watchers)
            {
                float bobX = w.Amp * 0.6f * (float)Math.Sin(_totalT * w.Speed * 0.7f + w.Phase + 1f);
                float bobY = w.Amp * (float)Math.Sin(_totalT * w.Speed + w.Phase);
                var pos = _sleepWorldPos + w.Offset + new Vector2(bobX, bobY);
                float pulse = 0.7f + 0.3f * (float)Math.Sin(_totalT * w.Speed * 1.3f + w.Phase);

                // Outer colored glow
                Globals.SpriteBatch.Draw(_spiritGlow, pos, null,
                    w.Tint * (sa * 0.5f * pulse),
                    0f, glowOrigin, pulse * 0.7f,
                    SpriteEffects.None, 0f);
                // Inner white core
                Globals.SpriteBatch.Draw(_spiritGlow, pos, null,
                    Color.White * (sa * 0.4f * pulse),
                    0f, glowOrigin, pulse * 0.3f,
                    SpriteEffects.None, 0f);
            }
        }

        // Sleeping Selene on the fountain
        if (_phase < Phase.PostFadeOut)
        {
            Globals.SpriteBatch.Draw(
                _sleepTex,
                _sleepWorldPos,
                null,
                Color.White * sa,
                rotation: 0f,
                origin: SleepOrigin,
                scale: SLEEP_SCALE,
                effects: SpriteEffects.None,
                layerDepth: 0f);
        }

        // Wisp — glowing orb in world space
        if (SpiritVisible)
        {
            float wa = SpiritAlpha * sa;
            if (wa <= 0f) return;
            float pulse = 0.85f + 0.15f * (float)Math.Sin(_totalT * 3.5f);
            var origin = new Vector2(32f, 32f);

            Globals.SpriteBatch.Draw(_spiritGlow, _spiritWorldPos, null,
                new Color(255, 210, 80) * (wa * pulse),
                0f, origin, pulse * (SPIRIT_SIZE / 64f) * 1.5f,
                SpriteEffects.None, 0f);
            Globals.SpriteBatch.Draw(_spiritGlow, _spiritWorldPos, null,
                Color.White * (wa * 0.85f * pulse),
                0f, origin, pulse * (SPIRIT_SIZE / 64f) * 0.7f,
                SpriteEffects.None, 0f);
        }
    }

    // ----- Screen-space drawing: black overlay, letterbox, initial dialog -----
    public void DrawUI()
    {
        var sb = Globals.SpriteBatch;
        var screen = Globals.GraphicsDevice.Viewport.Bounds;

        // ----- Full-screen black overlay -----
        float blackAlpha = 1f - SceneAlpha;
        if (blackAlpha > 0f)
            sb.Draw(Globals.Pixel, screen, Color.Black * blackAlpha);

        // ----- Letterbox bars -----
        float bar = BarPresence;
        if (bar > 0f)
        {
            int h = (int)(LETTERBOX_HEIGHT * bar);
            sb.Draw(Globals.Pixel, new Rectangle(0, 0, screen.Width, h), Color.Black);
            sb.Draw(Globals.Pixel,
                    new Rectangle(0, screen.Height - h, screen.Width, h), Color.Black);
        }

        // ----- "Where am I?" typewriter (only during Typewriter / WaitDismiss) -----
        if ((_phase == Phase.Typewriter || _phase == Phase.WaitDismiss) &&
            _totalT >= TypewriterStart)
        {
            int chars = VisibleChars;
            string visible = DialogText.Substring(0, chars);
            var fullSize = _font.MeasureString(DialogText);

            int boxW = (int)fullSize.X + DIALOG_PAD * 2;
            int boxH = (int)fullSize.Y + DIALOG_PAD;
            int boxX = (screen.Width - boxW) / 2;
            int boxY = LETTERBOX_HEIGHT / 2 - boxH / 2;

            sb.Draw(Globals.Pixel, new Rectangle(boxX, boxY, boxW, boxH),
                    new Color(15, 15, 25) * 0.85f);
            sb.Draw(Globals.Pixel, new Rectangle(boxX, boxY, boxW, 2),
                    Color.White * 0.6f);
            sb.Draw(Globals.Pixel, new Rectangle(boxX, boxY + boxH - 2, boxW, 2),
                    Color.White * 0.6f);

            var textPos = new Vector2(boxX + DIALOG_PAD, boxY + DIALOG_PAD / 2);
            sb.DrawString(_font, visible, textPos, Color.White);

            // [Space] hint once text is done
            if (TextFullyShown)
            {
                float pulse = 0.5f + 0.5f * (float)Math.Sin(_totalT * 4f);
                string hint = "[Space]";
                var hintSize = _font.MeasureString(hint);
                var hintPos = new Vector2(
                    (screen.Width - hintSize.X) / 2f,
                    screen.Height - LETTERBOX_HEIGHT / 2f - hintSize.Y / 2f);
                sb.DrawString(_font, hint, hintPos,
                              Color.White * (0.4f + 0.4f * pulse));
            }
        }
    }

    // ----- Procedural spirit textures -----

    // Small world-space glow (same radial falloff as OrbManager)
    private static Texture2D BuildSpiritGlow(int size)
    {
        var tex = new Texture2D(Globals.GraphicsDevice, size, size);
        var px = new Color[size * size];
        float c = (size - 1) / 2f;
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float dx = (x - c) / c;
            float dy = (y - c) / c;
            float d = (float)Math.Sqrt(dx * dx + dy * dy);
            if (d >= 1f) { px[y * size + x] = Color.Transparent; continue; }
            float a = 1f - d * d;
            px[y * size + x] = new Color(255, 255, 255, (int)(a * 255));
        }
        tex.SetData(px);
        return tex;
    }

    // Larger portrait texture for the dialog box — warm glowing circle with
    // simple "face": two dark oval eyes and a small smile.
    private static Texture2D BuildSpiritPortrait(int size)
    {
        var tex = new Texture2D(Globals.GraphicsDevice, size, size);
        var px = new Color[size * size];
        float c = (size - 1) / 2f;
        float r = c * 0.85f; // body radius

        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float dx = x - c;
            float dy = y - c;
            float dist = (float)Math.Sqrt(dx * dx + dy * dy);

            // Background: transparent
            if (dist > r * 1.3f)
            {
                px[y * size + x] = Color.Transparent;
                continue;
            }

            // Outer glow ring
            if (dist > r)
            {
                float glow = 1f - (dist - r) / (r * 0.3f);
                px[y * size + x] = new Color(255, 220, 100, (int)(glow * 120));
                continue;
            }

            // Body: warm golden gradient
            float t = dist / r;
            int br = (int)(255 - t * 40);     // 255 → 215
            int bg = (int)(225 - t * 60);     // 225 → 165
            int bb = (int)(120 - t * 60);     // 120 → 60
            var bodyColor = new Color(br, bg, bb, 255);

            // Eyes: two dark ovals
            float eyeY = c - r * 0.12f;
            float eyeSpacing = r * 0.3f;
            float eyeRx = r * 0.1f;
            float eyeRy = r * 0.14f;
            float leDx = (x - (c - eyeSpacing)) / eyeRx;
            float leDy = (y - eyeY) / eyeRy;
            float reDx = (x - (c + eyeSpacing)) / eyeRx;
            float reDy = (y - eyeY) / eyeRy;
            bool inLeftEye = leDx * leDx + leDy * leDy <= 1f;
            bool inRightEye = reDx * reDx + reDy * reDy <= 1f;

            if (inLeftEye || inRightEye)
            {
                // Eye: dark with a white highlight dot
                float edx = inLeftEye ? leDx : reDx;
                float edy = inLeftEye ? leDy : reDy;
                // Small highlight in upper-left of each eye
                float hx = edx + 0.35f;
                float hy = edy + 0.45f;
                if (hx * hx + hy * hy < 0.25f)
                    px[y * size + x] = new Color(255, 255, 255, 255);
                else
                    px[y * size + x] = new Color(30, 20, 15, 255);
                continue;
            }

            // Mouth: small upward curve (smile)
            float mouthY = c + r * 0.22f;
            float mouthW = r * 0.25f;
            float mxNorm = (x - c) / mouthW;
            if (Math.Abs(mxNorm) <= 1f)
            {
                float curveY = mouthY - mxNorm * mxNorm * r * 0.08f;
                if (Math.Abs(y - curveY) < 1.5f)
                {
                    px[y * size + x] = new Color(60, 30, 20, 255);
                    continue;
                }
            }

            px[y * size + x] = bodyColor;
        }
        tex.SetData(px);
        return tex;
    }
}
