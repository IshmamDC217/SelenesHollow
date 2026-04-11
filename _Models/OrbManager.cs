namespace SelenesHollow;

// Collectible spirits (little Luma-like wisps) scattered around the map. Each
// spirit pulses and bobs on its own phase so they shimmer independently. Picked
// up when Selene's foot crosses within PICKUP_RANGE; a HUD counter tracks them.
public class OrbManager
{
    private const float PICKUP_RANGE = 56f;
    private const int   GLOW_TEX_SIZE = 64;

    private class Spirit
    {
        public Vector2 WorldPos;
        public bool Collected;
    }

    private readonly List<Spirit> _spirits;
    private readonly Texture2D _glow;
    private readonly AmbientDialog _dialog;
    private bool _allCollectedFired;

    public int Total => _spirits.Count;
    public int Collected => _spirits.Count(s => s.Collected);
    public bool AllCollected => Collected == Total;

    public OrbManager(Map map, AmbientDialog dialog)
    {
        _dialog = dialog;
        _glow = BuildGlowTexture(GLOW_TEX_SIZE);

        // Scattered across the biomes — each one rewards exploration of a
        // different corner of the world. Positioned at cell foot points then
        // floated up a bit so they hover above the ground.
        Vector2 P(int x, int y, float lift = 60f) =>
            map.GetCellFootPos(new Point(x, y)) + new Vector2(0f, -lift);
        _spirits = new List<Spirit>
        {
            new() { WorldPos = P( 3, 11) },   // western garden
            new() { WorldPos = P(27, 10) },   // eastern ruins
            new() { WorldPos = P( 8,  3) },   // forest fringe
            new() { WorldPos = P(22,  3) },   // forest fringe east
            new() { WorldPos = P(11, 17) },   // southern grove west
            new() { WorldPos = P(19, 17) },   // southern grove east
            new() { WorldPos = P(14,  4, 240f) }, // floating in front of the temple
        };
    }

    public void Reset()
    {
        foreach (var s in _spirits) s.Collected = false;
        _allCollectedFired = false;
    }

    public void Update(Vector2 playerFoot)
    {
        foreach (var s in _spirits)
        {
            if (s.Collected) continue;
            if (Vector2.DistanceSquared(s.WorldPos, playerFoot) < PICKUP_RANGE * PICKUP_RANGE)
            {
                s.Collected = true;
                _dialog.Show($"A lost spirit... ({Collected}/{Total})");
            }
        }
        if (AllCollected && !_allCollectedFired)
        {
            _allCollectedFired = true;
            _dialog.ShowSequence(
                "All seven spirits... they've all come back.",
                "I think I'm starting to remember why I came.");
        }
    }

    public void Draw()
    {
        var sb = Globals.SpriteBatch;
        var t = Globals.RunningSeconds;
        var origin = new Vector2(GLOW_TEX_SIZE / 2f, GLOW_TEX_SIZE / 2f);
        foreach (var s in _spirits)
        {
            if (s.Collected) continue;
            // Each spirit pulses on its own phase based on its position so a
            // group of nearby spirits visibly shimmer rather than blink in unison.
            float phase = t * 2.4f + s.WorldPos.X * 0.013f + s.WorldPos.Y * 0.017f;
            float pulse = 0.85f + 0.15f * (float)Math.Sin(phase);
            float bob   = 4f * (float)Math.Sin(phase * 0.7f);
            // Warm golden glow (Luma-style) instead of cold blue
            sb.Draw(_glow,
                s.WorldPos + new Vector2(0f, bob),
                null,
                new Color(255, 210, 100) * pulse,
                rotation: 0f,
                origin: origin,
                scale: pulse * 0.9f,
                effects: SpriteEffects.None,
                layerDepth: 0f);
            // Hot white core
            sb.Draw(_glow,
                s.WorldPos + new Vector2(0f, bob),
                null,
                Color.White * (0.7f * pulse),
                rotation: 0f,
                origin: origin,
                scale: pulse * 0.45f,
                effects: SpriteEffects.None,
                layerDepth: 0f);
        }
    }

    private static Texture2D BuildGlowTexture(int size)
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
            // Smooth quadratic falloff from 1 at centre to 0 at the edge
            float a = 1f - d * d;
            px[y * size + x] = new Color(255, 255, 255, (int)(a * 255));
        }
        tex.SetData(px);
        return tex;
    }
}
