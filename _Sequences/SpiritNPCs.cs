namespace SelenesHollow;

// Colorful spirits scattered around the overworld after the tutorial battle.
// Each one bobs and glows at its position. When Selene is close, an [E] prompt
// appears; pressing E shows a one-line dialog via AmbientDialog. One-shot per
// spirit (resets on F5).
public class SpiritNPCs
{
    private const float INTERACT_RANGE = 90f;

    private const float NAME_RANGE = 140f;
    private record struct Spirit(Vector2 WorldPos, Color Tint, string Name, string Line);

    private readonly List<Spirit> _spirits;
    private readonly HashSet<int> _talked = new();
    private readonly AmbientDialog _dialog;
    private readonly Texture2D _glow;
    private readonly SpriteFont _font;
    private int _nearIdx = -1;
    private KeyboardState _lastKb;
    private Vector2 _playerPos;        // cached for name rendering

    public bool Visible { get; set; }

    public SpiritNPCs(Map map, AmbientDialog dialog)
    {
        _dialog = dialog;
        _glow = BuildGlow(48);
        _font = Globals.Content.Load<SpriteFont>("dialog");

        Vector2 P(int x, int y, float lift = 55f) =>
            map.GetCellFootPos(new Point(x, y)) + new Vector2(0f, -lift);

        _spirits = new List<Spirit>
        {
            new(P( 6, 16), new Color(255, 120, 120), "Ember",   "I've been here longer than the trees..."),
            new(P(40, 14), new Color(120, 255, 160), "Moss",    "The ruins weren't always broken, you know."),
            new(P(12,  6), new Color(120, 160, 255), "Lume",    "You remind me of someone. I can't remember who."),
            new(P(15,  6), new Color(255, 220,  80), "Sol",     "The temple hums when no one is watching."),
            new(P(18, 24), new Color(200, 130, 255), "Dusk",    "Fire... it's been so long since I felt warmth."),
            new(P(30, 22), new Color(255, 180, 140), "Coral",   "Don't go too far south. Not yet."),
            new(P( 4, 12), new Color(140, 255, 255), "Fern",    "The garden grows by itself. Nobody plants anything."),
            new(P(42, 20), new Color(255, 160, 220), "Petal",   "We watched you sleep. We hoped you'd wake up."),
        };
    }

    public void Reset()
    {
        _talked.Clear();
        _nearIdx = -1;
    }

    public void Update(Vector2 playerFoot)
    {
        if (!Visible) return;
        _playerPos = playerFoot;

        _nearIdx = -1;
        for (int i = 0; i < _spirits.Count; i++)
        {
            if (_talked.Contains(i)) continue;
            if (Vector2.Distance(playerFoot, _spirits[i].WorldPos + new Vector2(0, 55)) < INTERACT_RANGE)
            {
                _nearIdx = i;
                break;
            }
        }

        var kb = Keyboard.GetState();
        bool ePressed = kb.IsKeyDown(Keys.E) && _lastKb.IsKeyUp(Keys.E);
        if (_nearIdx >= 0 && ePressed && !_dialog.IsBusy)
        {
            _dialog.Show(_spirits[_nearIdx].Line);
            _talked.Add(_nearIdx);
        }
        _lastKb = kb;
    }

    public void Draw()
    {
        if (!Visible) return;
        var sb = Globals.SpriteBatch;
        var t = Globals.RunningSeconds;
        var origin = new Vector2(24f, 24f);

        for (int i = 0; i < _spirits.Count; i++)
        {
            var s = _spirits[i];
            float phase = t * 2.2f + i * 1.7f;
            float pulse = 0.75f + 0.25f * MathF.Sin(phase);
            float bob = 5f * MathF.Sin(phase * 0.8f);
            float alpha = _talked.Contains(i) ? 0.3f : 1f;

            // Outer colored glow
            sb.Draw(_glow, s.WorldPos + new Vector2(0, bob), null,
                s.Tint * (pulse * 0.6f * alpha),
                0f, origin, pulse * 0.9f, SpriteEffects.None, 0f);
            // Inner white core
            sb.Draw(_glow, s.WorldPos + new Vector2(0, bob), null,
                Color.White * (pulse * 0.5f * alpha),
                0f, origin, pulse * 0.4f, SpriteEffects.None, 0f);

            // Name + [E] above head when player is nearby
            float dist = Vector2.Distance(_playerPos, s.WorldPos + new Vector2(0, 55));
            if (dist < NAME_RANGE)
            {
                float nameAlpha = 1f - (dist / NAME_RANGE);
                nameAlpha *= nameAlpha;
                bool canTalk = i == _nearIdx && !_talked.Contains(i);
                string label = canTalk ? $"{s.Name}   [E]" : s.Name;
                var namePos = s.WorldPos + new Vector2(0, bob - 28);
                var nameSize = _font.MeasureString(label);
                sb.DrawString(_font, label, namePos + Vector2.One,
                    Color.Black * (0.4f * nameAlpha), 0f,
                    new Vector2(nameSize.X / 2f, nameSize.Y / 2f),
                    0.6f, SpriteEffects.None, 0f);
                sb.DrawString(_font, label, namePos,
                    Color.White * nameAlpha, 0f,
                    new Vector2(nameSize.X / 2f, nameSize.Y / 2f),
                    0.6f, SpriteEffects.None, 0f);
            }
        }
    }

    // [E] is now drawn inline with the name above the spirit
    public void DrawPrompt() { }

    private static Texture2D BuildGlow(int size)
    {
        var tex = new Texture2D(Globals.GraphicsDevice, size, size);
        var px = new Color[size * size];
        float c = (size - 1) / 2f;
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float d = MathF.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c;
            if (d >= 1f) { px[y * size + x] = Color.Transparent; continue; }
            px[y * size + x] = new Color(255, 255, 255, (int)((1f - d * d) * 255));
        }
        tex.SetData(px);
        return tex;
    }
}
