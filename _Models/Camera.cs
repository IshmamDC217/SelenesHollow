namespace SelenesHollow;

// Top-down 2D camera. Holds a top-left world position and produces a
// translation matrix that SpriteBatch.Begin uses to render the world.
// Follow() centres the camera on a focus point and clamps so the view never
// shows past the map edges. Position is rounded to whole pixels to avoid
// sub-pixel jitter on the tiled background as Selene moves.
public class Camera
{
    public Vector2 Position { get; private set; }
    private bool _initialized;

    // Snap to a focus point with no smoothing — used at start-up so we don't
    // see the camera "fly" toward the focus on the first frame.
    public void Snap(Vector2 focus, Vector2 mapMin, Vector2 mapMax, int viewW, int viewH)
    {
        Position = ClampedTopLeft(focus, mapMin, mapMax, viewW, viewH);
        _initialized = true;
    }

    // Smooth follow — exponential ease toward the target, frame-rate
    // independent. lerpRate ~= "responsiveness" (higher = snappier).
    public void Follow(Vector2 focus, Vector2 mapMin, Vector2 mapMax,
                       int viewW, int viewH, float lerpRate = 7f)
    {
        var target = ClampedTopLeft(focus, mapMin, mapMax, viewW, viewH);
        if (!_initialized)
        {
            Position = target;
            _initialized = true;
            return;
        }
        float dt = Globals.TotalSeconds;
        float k = 1f - (float)Math.Exp(-lerpRate * dt);
        Position = Vector2.Lerp(Position, target, k);
    }

    private static Vector2 ClampedTopLeft(Vector2 focus, Vector2 mapMin, Vector2 mapMax,
                                          int viewW, int viewH)
    {
        var pos = focus - new Vector2(viewW / 2f, viewH / 2f);
        float maxX = Math.Max(mapMin.X, mapMax.X - viewW);
        float maxY = Math.Max(mapMin.Y, mapMax.Y - viewH);
        pos.X = MathHelper.Clamp(pos.X, mapMin.X, maxX);
        pos.Y = MathHelper.Clamp(pos.Y, mapMin.Y, maxY);
        return pos;
    }

    public Matrix Transform =>
        Matrix.CreateTranslation(-(int)Math.Round(Position.X), -(int)Math.Round(Position.Y), 0);
}
