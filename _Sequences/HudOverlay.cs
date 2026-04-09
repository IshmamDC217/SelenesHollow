namespace SelenesHollow;

// Tiny in-screen overlay: orb count in the top-right corner. Hides itself
// while the intro is running so it doesn't compete with the title beat.
public class HudOverlay
{
    private readonly SpriteFont _font;
    private readonly OrbManager _orbs;

    public HudOverlay(OrbManager orbs)
    {
        _orbs = orbs;
        _font = Globals.Content.Load<SpriteFont>("dialog");
    }

    public void Draw(bool visible)
    {
        if (!visible) return;
        var sb = Globals.SpriteBatch;
        var screen = Globals.GraphicsDevice.Viewport.Bounds;

        string text = $"Orbs  {_orbs.Collected} / {_orbs.Total}";
        var size = _font.MeasureString(text);
        const int pad = 16;
        int boxW = (int)size.X + pad * 2;
        int boxH = (int)size.Y + pad;
        int boxX = screen.Width - boxW - 20;
        int boxY = 20;

        sb.Draw(Globals.Pixel, new Rectangle(boxX, boxY, boxW, boxH),
                new Color(15, 15, 25) * 0.8f);
        sb.Draw(Globals.Pixel, new Rectangle(boxX, boxY, boxW, 2),
                Color.White * 0.5f);
        sb.Draw(Globals.Pixel, new Rectangle(boxX, boxY + boxH - 2, boxW, 2),
                Color.White * 0.5f);

        sb.DrawString(_font, text, new Vector2(boxX + pad, boxY + pad / 2),
                      new Color(180, 220, 255));
    }
}
