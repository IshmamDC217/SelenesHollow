namespace SelenesHollow;

// A drawable sprite from the tilesheet. Each Tile carries its own source rect
// AND its own origin so we can mix sprites of different sizes (cube tiles,
// trees, rocks, etc.) in the same scene. Origin is the source-pixel point
// that lands at the screen position — for ground cubes it's the diamond
// top-left bbox corner; for taller props it's the corresponding point on
// the prop's BASE diamond, so the prop appears to sit on the cube top.
public class Tile(Texture2D texture, Rectangle sourceRect, Vector2 position, Vector2 origin, float scale = 3f)
{
    private readonly Texture2D _texture = texture;
    private readonly Rectangle _sourceRect = sourceRect;
    private readonly Vector2 _position = position;
    private readonly Vector2 _origin = origin;
    private readonly float _scale = scale;
    private bool _keyboardSelected;
    private bool _mouseSelected;

    public void KeyboardSelect() => _keyboardSelected = true;
    public void KeyboardDeselect() => _keyboardSelected = false;
    public void MouseSelect() => _mouseSelected = true;
    public void MouseDeselect() => _mouseSelected = false;

    public void Draw()
    {
        var color = Color.White;
        if (_keyboardSelected) color = Color.Red;
        if (_mouseSelected) color = Color.Green;
        Globals.SpriteBatch.Draw(
            _texture,
            _position,
            _sourceRect,
            color,
            rotation: 0f,
            origin: _origin,
            scale: _scale,
            effects: SpriteEffects.None,
            layerDepth: 0f);
    }
}
