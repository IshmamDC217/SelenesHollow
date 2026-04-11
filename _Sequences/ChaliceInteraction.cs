namespace SelenesHollow;

// Watches the player's distance from the spirit shrine. When she's close,
// draws a floating "[E]" prompt above it (in world space so it scrolls with
// the camera) and on E-press fires a multi-line monologue through AmbientDialog.
// Fires once per game (cleared on F5 by ResetRunState).
public class ChaliceInteraction
{
    private const float RANGE = 110f;
    // Cell coordinates of the chalice in Map.cs
    private static readonly Point ChaliceCell = new(24, 26);

    private readonly AmbientDialog _dialog;
    private readonly Vector2 _chaliceFootPos;
    private readonly SpriteFont _font;
    private bool _consumed;
    private KeyboardState _lastKb;
    private bool _inRange;

    public ChaliceInteraction(Map map, AmbientDialog dialog)
    {
        _dialog = dialog;
        _chaliceFootPos = map.GetCellFootPos(ChaliceCell);
        _font = Globals.Content.Load<SpriteFont>("dialog");
    }

    public void Reset()
    {
        _consumed = false;
        _inRange = false;
    }

    public void Update(Vector2 playerFoot)
    {
        _inRange = !_consumed && Vector2.Distance(playerFoot, _chaliceFootPos) < RANGE;

        var kb = Keyboard.GetState();
        bool ePressed = kb.IsKeyDown(Keys.E) && _lastKb.IsKeyUp(Keys.E);
        if (_inRange && ePressed && !_dialog.IsBusy)
        {
            _consumed = true;
            _dialog.ShowSequence(
                "This shrine... it's pulsing with energy.",
                "Visions... a tower, a name I almost remember...",
                "\"Selene,\" the wind says. \"Come home.\"");
        }
        _lastKb = kb;
    }

    // Drawn in WORLD space so the [E] prompt sits over the chalice and
    // scrolls with the camera.
    public void DrawWorld()
    {
        if (!_inRange) return;
        // Float the prompt above the chalice with a gentle bob
        float bob = 4f * (float)Math.Sin(Globals.RunningSeconds * 4f);
        var pos = _chaliceFootPos + new Vector2(0f, -130f + bob);
        const string label = "[E]";
        var size = _font.MeasureString(label);
        var origin = new Vector2(size.X / 2f, size.Y / 2f);
        // Soft drop shadow then bright text on top
        Globals.SpriteBatch.DrawString(_font, label, pos + new Vector2(2, 2),
                                        Color.Black * 0.7f, 0f, origin, 1f, SpriteEffects.None, 0f);
        Globals.SpriteBatch.DrawString(_font, label, pos,
                                        new Color(220, 240, 255), 0f, origin, 1f, SpriteEffects.None, 0f);
    }
}
