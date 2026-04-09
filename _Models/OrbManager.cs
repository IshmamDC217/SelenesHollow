namespace SelenesHollow;

// Collectible glowing orbs scattered around the map. Each orb pulses (size +
// alpha modulated by a sine wave keyed off RunningSeconds and the orb's own
// position so they don't pulse in sync), is picked up when Selene's foot
// crosses within PICKUP_RANGE, and reports how many remain.
public class OrbManager
{
    private const float PICKUP_RANGE = 56f;
    private const int   GLOW_TEX_SIZE = 64;

    private class Orb
    {
        public Vector2 WorldPos;
        public bool Collected;
    }

    private readonly List<Orb> _orbs;
    private readonly Texture2D _glow;
    private readonly AmbientDialog _dialog;
    private bool _allCollectedFired;

    public int Total => _orbs.Count;
    public int Collected => _orbs.Count(o => o.Collected);
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
        _orbs = new List<Orb>
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
        foreach (var o in _orbs) o.Collected = false;
        _allCollectedFired = false;
    }

    public void Update(Vector2 playerFoot)
    {
        foreach (var o in _orbs)
        {
            if (o.Collected) continue;
            if (Vector2.DistanceSquared(o.WorldPos, playerFoot) < PICKUP_RANGE * PICKUP_RANGE)
            {
                o.Collected = true;
                _dialog.Show($"An orb of memory... ({Collected}/{Total})");
            }
        }
        if (AllCollected && !_allCollectedFired)
        {
            _allCollectedFired = true;
            _dialog.ShowSequence(
                "All seven... they're all here.",
                "I think I'm starting to remember why I came.");
        }
    }

    public void Draw()
    {
        var sb = Globals.SpriteBatch;
        var t = Globals.RunningSeconds;
        var origin = new Vector2(GLOW_TEX_SIZE / 2f, GLOW_TEX_SIZE / 2f);
        foreach (var o in _orbs)
        {
            if (o.Collected) continue;
            // Each orb pulses on its own phase based on its position so a
            // group of nearby orbs visibly shimmer rather than blink in unison.
            float phase = t * 2.4f + o.WorldPos.X * 0.013f + o.WorldPos.Y * 0.017f;
            float pulse = 0.85f + 0.15f * (float)Math.Sin(phase);
            float bob   = 4f * (float)Math.Sin(phase * 0.7f);
            sb.Draw(_glow,
                o.WorldPos + new Vector2(0f, bob),
                null,
                new Color(140, 200, 255) * pulse,
                rotation: 0f,
                origin: origin,
                scale: pulse * 0.9f,
                effects: SpriteEffects.None,
                layerDepth: 0f);
            // Hot core in the middle
            sb.Draw(_glow,
                o.WorldPos + new Vector2(0f, bob),
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
