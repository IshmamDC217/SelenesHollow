namespace SelenesHollow;

// Clean shadcn/Radix-style command selector. Vertical card list with
// accent-colored selection highlight. W/S to navigate, Space to confirm.
public class CommandWheel
{
    public record struct Move(string Name, string Desc, int Damage, int ManaCost, int MpRestore, bool IsMelee);

    private static readonly Move[] Moves =
    {
        new("Fire Punch",   "Close-range fire strike",     30, 12, 0, true),
        new("Fireball",     "Hurl a ball of flame",        25, 10, 0, false),
        new("Ember Burst",  "Sparks, low cost",            15,  5, 0, false),
        new("Flame Shield", "Block and reduce damage",      0,  8, 0, false),
        new("Focus",        "Concentrate to restore MP",    0,  0, 4, false),
    };

    // Quick-fire keys: I, O, K, L, P
    public static readonly Keys[] QuickKeys = { Keys.I, Keys.O, Keys.K, Keys.L, Keys.P };
    public static readonly string[] QuickLabels = { "I", "O", "K", "L", "P" };

    private static readonly Color[] AccentColors =
    {
        new(230,  70,  50),   // Fire Punch — red
        new(255, 140,  40),   // Fireball — orange
        new(255, 180, 100),   // Ember Burst — amber
        new(255, 210,  70),   // Flame Shield — gold
        new( 80, 160, 255),   // Focus — blue
    };

    // Layout
    private const int CARD_W = 240;
    private const int CARD_H = 48;
    private const int CARD_GAP = 4;
    private const int PAD = 12;
    private const int BORDER = 2;

    private readonly SpriteFont _font;
    private int _selected;
    private KeyboardState _lastKb;
    private bool _confirmed;
    private bool _firstFrame;  // skip input on the frame the menu opens

    public bool Visible
    {
        get => _visible;
        set { _visible = value; if (value) _firstFrame = true; }
    }
    private bool _visible;

    public Move? SelectedMove => _confirmed ? Moves[_selected] : null;
    public int MoveCount => Moves.Length;
    public Move GetMove(int i) => Moves[i];
    public void ResetSelection() { _confirmed = false; }

    public CommandWheel()
    {
        _font = Globals.Content.Load<SpriteFont>("dialog");
    }

    public void Update()
    {
        if (!Visible) return;
        _confirmed = false;

        var kb = Keyboard.GetState();

        // Skip input on the first frame the menu opens so a held Space
        // from the "open" press doesn't immediately confirm.
        if (_firstFrame)
        {
            _firstFrame = false;
            _lastKb = kb;
            return;
        }

        bool up    = (kb.IsKeyDown(Keys.W) || kb.IsKeyDown(Keys.Up))
                     && _lastKb.IsKeyUp(Keys.W) && _lastKb.IsKeyUp(Keys.Up);
        bool down  = (kb.IsKeyDown(Keys.S) || kb.IsKeyDown(Keys.Down))
                     && _lastKb.IsKeyUp(Keys.S) && _lastKb.IsKeyUp(Keys.Down);
        bool confirm = kb.IsKeyDown(Keys.Space) && _lastKb.IsKeyUp(Keys.Space);

        if (up)   _selected = (_selected - 1 + Moves.Length) % Moves.Length;
        if (down) _selected = (_selected + 1) % Moves.Length;
        if (confirm) _confirmed = true;

        _lastKb = kb;
    }

    public void Draw(Vector2 topLeft, float scale = 1f)
    {
        if (scale <= 0.01f) return;
        var sb = Globals.SpriteBatch;

        int totalH = Moves.Length * (CARD_H + CARD_GAP) - CARD_GAP + PAD * 2;
        int totalW = CARD_W + PAD * 2;
        int px = (int)topLeft.X;
        int py = (int)topLeft.Y;

        // Panel backdrop (dark, slight transparency)
        sb.Draw(Globals.Pixel,
            new Rectangle(px, py, totalW, totalH),
            new Color(12, 12, 22) * (0.92f * scale));
        // Outer border
        DrawBorder(sb, px, py, totalW, totalH, new Color(60, 65, 80) * (0.7f * scale));

        // Title
        // (none — clean and minimal)

        int cardX = px + PAD;
        int cardY = py + PAD;

        for (int i = 0; i < Moves.Length; i++)
        {
            bool sel = i == _selected;
            var accent = AccentColors[i];
            float itemAlpha = scale * (sel ? 1f : 0.65f);

            // Card background
            Color bgColor = sel
                ? new Color(accent.R / 8, accent.G / 8, accent.B / 8)
                : new Color(18, 18, 28);
            sb.Draw(Globals.Pixel,
                new Rectangle(cardX, cardY, CARD_W, CARD_H),
                bgColor * (0.95f * scale));

            // Selection accent — left stripe
            if (sel)
            {
                sb.Draw(Globals.Pixel,
                    new Rectangle(cardX, cardY, 3, CARD_H),
                    accent * scale);
                // Subtle glow behind selected card
                sb.Draw(Globals.Pixel,
                    new Rectangle(cardX, cardY, CARD_W, CARD_H),
                    accent * (0.08f * scale));
            }

            // Card border (subtle)
            Color borderCol = sel ? accent * (0.5f * scale) : new Color(45, 48, 60) * (0.5f * scale);
            DrawBorder(sb, cardX, cardY, CARD_W, CARD_H, borderCol);

            // Move name (left side)
            string name = Moves[i].Name;
            var namePos = new Vector2(cardX + 14, cardY + 6);
            Color nameCol = sel ? accent : Color.White * 0.7f;
            sb.DrawString(_font, name, namePos, nameCol * itemAlpha,
                0f, Vector2.Zero, 0.85f, SpriteEffects.None, 0f);

            // Cost / info (right-aligned, smaller, muted)
            string info = Moves[i].MpRestore > 0
                ? $"+{Moves[i].MpRestore} MP"
                : Moves[i].Damage > 0
                    ? $"{Moves[i].Damage} DMG - {Moves[i].ManaCost} MP"
                    : $"DEF - {Moves[i].ManaCost} MP";
            var infoSize = _font.MeasureString(info);
            var infoPos = new Vector2(cardX + CARD_W - 14 - infoSize.X * 0.65f, cardY + 26);
            sb.DrawString(_font, info, infoPos,
                (sel ? Color.White * 0.8f : Color.White * 0.35f) * scale,
                0f, Vector2.Zero, 0.65f, SpriteEffects.None, 0f);

            cardY += CARD_H + CARD_GAP;
        }

        // Navigation hint at bottom
        string hint = "W/S  select   Space  confirm";
        var hintSize = _font.MeasureString(hint);
        sb.DrawString(_font, hint,
            new Vector2(px + totalW / 2f - hintSize.X * 0.28f, py + totalH + 6),
            Color.White * (0.3f * scale), 0f, Vector2.Zero, 0.55f, SpriteEffects.None, 0f);
    }

    private static void DrawBorder(SpriteBatch sb, int x, int y, int w, int h, Color c)
    {
        sb.Draw(Globals.Pixel, new Rectangle(x, y, w, 1), c);
        sb.Draw(Globals.Pixel, new Rectangle(x, y + h - 1, w, 1), c);
        sb.Draw(Globals.Pixel, new Rectangle(x, y, 1, h), c);
        sb.Draw(Globals.Pixel, new Rectangle(x + w - 1, y, 1, h), c);
    }
}
