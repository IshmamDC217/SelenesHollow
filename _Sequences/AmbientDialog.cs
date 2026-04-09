namespace SelenesHollow;

// A short on-screen line of self-talk that fades in/out at the top of the
// screen. Triggered by DialogTriggers when Selene enters certain cells. Reuses
// the same dark panel + typewriter style as the intro dialog so the game has
// a single visual voice for Selene's thoughts.
public class AmbientDialog
{
    private const float FADE_IN       = 0.3f;
    private const float FADE_OUT      = 0.6f;
    private const float SHOW_DURATION = 4.5f;
    private const float TYPE_SPEED    = 0.04f;
    private const int   DIALOG_PAD    = 24;
    private const int   TOP_MARGIN    = 24;

    private readonly SpriteFont _font;
    private string _line;
    private float _t;
    private readonly Queue<string> _queue = new();

    public AmbientDialog()
    {
        _font = Globals.Content.Load<SpriteFont>("dialog");
    }

    public void Show(string line)
    {
        _line = line;
        _t = 0f;
    }

    // Queue a sequence of lines that play one after another (each line stays
    // up for SHOW_DURATION before the next one auto-starts).
    public void ShowSequence(params string[] lines)
    {
        _queue.Clear();
        if (lines.Length == 0) return;
        Show(lines[0]);
        for (int i = 1; i < lines.Length; i++) _queue.Enqueue(lines[i]);
    }

    public bool IsBusy => _line != null || _queue.Count > 0;

    public void Clear() { _line = null; _queue.Clear(); }

    public void Update()
    {
        if (_line == null)
        {
            if (_queue.Count > 0) Show(_queue.Dequeue());
            return;
        }
        _t += Globals.TotalSeconds;
        if (_t >= SHOW_DURATION)
        {
            _line = null;
            if (_queue.Count > 0) Show(_queue.Dequeue());
        }
    }

    public void Draw()
    {
        if (_line == null) return;
        var sb = Globals.SpriteBatch;
        var screen = Globals.GraphicsDevice.Viewport.Bounds;

        // Envelope: fade in, hold, fade out
        float alpha;
        if (_t < FADE_IN) alpha = _t / FADE_IN;
        else if (_t > SHOW_DURATION - FADE_OUT) alpha = (SHOW_DURATION - _t) / FADE_OUT;
        else alpha = 1f;
        alpha = Math.Clamp(alpha, 0f, 1f);

        int chars = Math.Min(_line.Length, (int)(_t / TYPE_SPEED));
        string visible = _line.Substring(0, chars);
        var fullSize = _font.MeasureString(_line);

        int boxW = (int)fullSize.X + DIALOG_PAD * 2;
        int boxH = (int)fullSize.Y + DIALOG_PAD;
        int boxX = (screen.Width - boxW) / 2;
        int boxY = TOP_MARGIN;

        sb.Draw(Globals.Pixel, new Rectangle(boxX, boxY, boxW, boxH),
                new Color(15, 15, 25) * (0.85f * alpha));
        sb.Draw(Globals.Pixel, new Rectangle(boxX, boxY, boxW, 2),
                Color.White * (0.6f * alpha));
        sb.Draw(Globals.Pixel, new Rectangle(boxX, boxY + boxH - 2, boxW, 2),
                Color.White * (0.6f * alpha));

        var textPos = new Vector2(boxX + DIALOG_PAD, boxY + DIALOG_PAD / 2);
        sb.DrawString(_font, visible, textPos, Color.White * alpha);
    }
}
