namespace SelenesHollow;

// Megaman Battle Network–style dialog box drawn at the bottom of the screen.
// Left 1/3 holds an animated or static portrait; right 2/3 holds a speaker
// name and typewriter-revealed text. Player presses Space/Enter to advance.
public class PortraitDialog
{
    // A single line of dialog with speaker info and optional portrait.
    public record struct Line(
        string Speaker,
        string Text,
        Texture2D Portrait,
        int FrameCount = 1,   // frames in the portrait sheet (1 = static)
        int FrameW = 0,       // width of one frame (0 = whole texture)
        int FrameH = 0        // height of one frame (0 = whole texture)
    );

    // ----- Layout constants -----
    private const int BOX_MARGIN_X = 50;
    private const int BOX_MARGIN_BOTTOM = 30;
    private const int BOX_HEIGHT = 190;
    private const int PAD = 16;
    private const int PORTRAIT_PAD = 10;
    private const int BORDER = 2;

    // ----- Timing -----
    private const float TYPE_SPEED = 0.035f;
    private const float PORTRAIT_FRAME_DURATION = 0.12f; // talking mouth cycle
    private const float FADE_DURATION = 0.25f;

    private readonly SpriteFont _font;
    private Line _current;
    private float _t;
    private float _portraitTimer;
    private int _portraitFrame;
    private bool _active;
    private float _fadeAlpha;       // 0→1 on show, 1→0 on dismiss
    private readonly Queue<Line> _queue = new();
    private KeyboardState _lastKb;

    public bool IsActive => _active || _queue.Count > 0;

    private int VisibleChars =>
        _current.Text == null ? 0 : Math.Min(_current.Text.Length, (int)(_t / TYPE_SPEED));

    private bool TextFullyShown => _current.Text != null && VisibleChars >= _current.Text.Length;

    public PortraitDialog()
    {
        _font = Globals.Content.Load<SpriteFont>("dialog");
    }

    public void Show(Line line)
    {
        _current = line;
        _t = 0f;
        _portraitTimer = 0f;
        _portraitFrame = 0;
        _active = true;
    }

    public void ShowSequence(params Line[] lines)
    {
        _queue.Clear();
        if (lines.Length == 0) return;
        Show(lines[0]);
        for (int i = 1; i < lines.Length; i++)
            _queue.Enqueue(lines[i]);
    }

    public void Clear()
    {
        _active = false;
        _queue.Clear();
    }

    public void Update()
    {
        // Fade in/out
        float fadeTarget = _active ? 1f : 0f;
        float fadeSpeed = 1f / FADE_DURATION * Globals.TotalSeconds;
        _fadeAlpha = _fadeAlpha < fadeTarget
            ? Math.Min(_fadeAlpha + fadeSpeed, 1f)
            : Math.Max(_fadeAlpha - fadeSpeed, 0f);

        if (!_active)
        {
            if (_queue.Count > 0) Show(_queue.Dequeue());
            return;
        }

        _t += Globals.TotalSeconds;

        // Animate portrait: cycle talking frames while text is typing,
        // snap to frame 0 (mouth closed) when text is fully shown.
        if (_current.FrameCount > 1)
        {
            if (!TextFullyShown)
            {
                _portraitTimer += Globals.TotalSeconds;
                if (_portraitTimer >= PORTRAIT_FRAME_DURATION)
                {
                    _portraitTimer -= PORTRAIT_FRAME_DURATION;
                    _portraitFrame = (_portraitFrame + 1) % _current.FrameCount;
                }
            }
            else
            {
                _portraitFrame = 0; // mouth closed
            }
        }

        // Space / Enter: if still typing → instant reveal; if done → advance
        var kb = Keyboard.GetState();
        bool advance = (kb.IsKeyDown(Keys.Space) || kb.IsKeyDown(Keys.Enter))
                       && _lastKb.IsKeyUp(Keys.Space) && _lastKb.IsKeyUp(Keys.Enter);
        if (advance)
        {
            if (!TextFullyShown)
            {
                // Skip typewriter — reveal all text instantly
                _t = _current.Text.Length * TYPE_SPEED + 1f;
            }
            else
            {
                _active = false;
                if (_queue.Count > 0) Show(_queue.Dequeue());
            }
        }
        _lastKb = kb;
    }

    public void Draw()
    {
        if (_fadeAlpha <= 0f) return;

        var sb = Globals.SpriteBatch;
        var screen = Globals.GraphicsDevice.Viewport.Bounds;
        float a = _fadeAlpha;

        // ----- Box dimensions -----
        int boxW = screen.Width - BOX_MARGIN_X * 2;
        int boxH = BOX_HEIGHT;
        int boxX = BOX_MARGIN_X;
        int boxY = screen.Height - boxH - BOX_MARGIN_BOTTOM;

        // Background panel
        sb.Draw(Globals.Pixel, new Rectangle(boxX, boxY, boxW, boxH),
                new Color(8, 8, 18) * (0.93f * a));
        // Outer border
        DrawBorder(sb, boxX, boxY, boxW, boxH, Color.White * (0.55f * a));

        // ----- Portrait area (left side, square) -----
        int portraitAreaW = boxH;
        int pX = boxX + PORTRAIT_PAD;
        int pY = boxY + PORTRAIT_PAD;
        int pSize = boxH - PORTRAIT_PAD * 2;

        // Portrait frame border + dark fill
        DrawBorder(sb, pX - BORDER, pY - BORDER, pSize + BORDER * 2, pSize + BORDER * 2,
                   new Color(100, 160, 220) * (0.5f * a));
        sb.Draw(Globals.Pixel, new Rectangle(pX, pY, pSize, pSize),
                new Color(4, 4, 12) * (0.95f * a));

        // Draw portrait texture
        if (_current.Portrait != null)
        {
            int fw = _current.FrameW > 0 ? _current.FrameW : _current.Portrait.Width;
            int fh = _current.FrameH > 0 ? _current.FrameH : _current.Portrait.Height;
            int cols = _current.Portrait.Width / Math.Max(fw, 1);
            int col = cols > 0 ? _portraitFrame % cols : 0;
            int row = cols > 0 ? _portraitFrame / cols : 0;
            var src = new Rectangle(col * fw, row * fh, fw, fh);

            // Scale to fit portrait box, centered
            float scale = Math.Min((float)pSize / fw, (float)pSize / fh);
            int drawW = (int)(fw * scale);
            int drawH = (int)(fh * scale);
            int drawX = pX + (pSize - drawW) / 2;
            int drawY = pY + (pSize - drawH) / 2;

            sb.Draw(_current.Portrait, new Rectangle(drawX, drawY, drawW, drawH),
                    src, Color.White * a);
        }

        // ----- Divider line -----
        int divX = boxX + portraitAreaW;
        sb.Draw(Globals.Pixel,
                new Rectangle(divX, boxY + PAD, BORDER, boxH - PAD * 2),
                new Color(100, 160, 220) * (0.35f * a));

        // ----- Text area (right 2/3) -----
        int textX = divX + PAD + 4;
        int textMaxW = boxX + boxW - textX - PAD;

        // Speaker name (accent colour)
        float nameH = _font.MeasureString("A").Y;
        Color nameColor = _current.Speaker == "Wisp"
            ? new Color(255, 220, 100)    // warm gold for the spirit
            : new Color(140, 200, 255);   // cool blue for Selene
        sb.DrawString(_font, _current.Speaker,
                      new Vector2(textX, boxY + PAD), nameColor * a);

        // Dialog text with word wrap
        int chars = VisibleChars;
        if (_current.Text != null && chars > 0)
        {
            string visible = _current.Text.Substring(0, chars);
            DrawWrapped(sb, visible,
                        new Vector2(textX, boxY + PAD + nameH + 6),
                        textMaxW, Color.White * a);
        }

        // Continue arrow (▼) once text is done
        if (TextFullyShown)
        {
            float pulse = 0.5f + 0.5f * (float)Math.Sin(Globals.RunningSeconds * 4.5f);
            string arrow = ">";
            var arrowSize = _font.MeasureString(arrow);
            sb.DrawString(_font, arrow,
                new Vector2(boxX + boxW - PAD - arrowSize.X,
                            boxY + boxH - PAD - arrowSize.Y),
                Color.White * (0.35f + 0.45f * pulse) * a);
        }
    }

    // ----- Helpers -----

    private static void DrawBorder(SpriteBatch sb, int x, int y, int w, int h, Color c)
    {
        sb.Draw(Globals.Pixel, new Rectangle(x, y, w, BORDER), c);
        sb.Draw(Globals.Pixel, new Rectangle(x, y + h - BORDER, w, BORDER), c);
        sb.Draw(Globals.Pixel, new Rectangle(x, y, BORDER, h), c);
        sb.Draw(Globals.Pixel, new Rectangle(x + w - BORDER, y, BORDER, h), c);
    }

    private void DrawWrapped(SpriteBatch sb, string text, Vector2 pos, int maxW, Color color)
    {
        float x = pos.X;
        float y = pos.Y;
        float lineH = _font.MeasureString("A").Y + 2f;
        string[] words = text.Split(' ');
        foreach (var word in words)
        {
            if (word.Length == 0) continue;
            string token = word + " ";
            var size = _font.MeasureString(token);
            if (x + size.X > pos.X + maxW && x > pos.X)
            {
                x = pos.X;
                y += lineH;
            }
            sb.DrawString(_font, token, new Vector2(x, y), color);
            x += size.X;
        }
    }
}
