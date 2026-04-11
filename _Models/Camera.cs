namespace SelenesHollow;

// Top-down 2D camera with zoom and screen shake support.
public class Camera
{
    public Vector2 Position { get; private set; }
    private bool _initialized;

    // Zoom: 1.0 = normal, >1 = zoomed in
    public float Zoom { get; set; } = 1f;
    private float _targetZoom = 1f;
    private float _zoomSpeed = 3f;

    // Screen shake
    private float _shakeIntensity;
    private float _shakeDuration;
    private float _shakeTimer;
    private readonly Random _shakeRng = new();

    public void Snap(Vector2 focus, Vector2 mapMin, Vector2 mapMax, int viewW, int viewH)
    {
        Position = ClampedTopLeft(focus, mapMin, mapMax, viewW, viewH);
        _initialized = true;
    }

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

        // Animate zoom toward target
        if (Math.Abs(Zoom - _targetZoom) > 0.001f)
        {
            float zk = 1f - (float)Math.Exp(-_zoomSpeed * dt);
            Zoom = MathHelper.Lerp(Zoom, _targetZoom, zk);
        }

        // Update shake
        if (_shakeTimer > 0)
            _shakeTimer -= dt;
    }

    public void SetZoom(float target, float speed = 3f)
    {
        _targetZoom = target;
        _zoomSpeed = speed;
    }

    public void Shake(float intensity = 5f, float duration = 0.3f)
    {
        _shakeIntensity = intensity;
        _shakeDuration = duration;
        _shakeTimer = duration;
    }

    private Vector2 ClampedTopLeft(Vector2 focus, Vector2 mapMin, Vector2 mapMax,
                                   int viewW, int viewH)
    {
        // Adjust view size for zoom
        float zw = viewW / Zoom;
        float zh = viewH / Zoom;
        var pos = focus - new Vector2(zw / 2f, zh / 2f);
        float maxX = Math.Max(mapMin.X, mapMax.X - zw);
        float maxY = Math.Max(mapMin.Y, mapMax.Y - zh);
        pos.X = MathHelper.Clamp(pos.X, mapMin.X, maxX);
        pos.Y = MathHelper.Clamp(pos.Y, mapMin.Y, maxY);
        return pos;
    }

    public Matrix Transform
    {
        get
        {
            float sx = 0f, sy = 0f;
            if (_shakeTimer > 0)
            {
                float t = _shakeTimer / _shakeDuration;
                float intensity = _shakeIntensity * t;
                sx = (_shakeRng.NextSingle() * 2f - 1f) * intensity;
                sy = (_shakeRng.NextSingle() * 2f - 1f) * intensity;
            }
            return Matrix.CreateTranslation(-(int)Math.Round(Position.X) + sx,
                                            -(int)Math.Round(Position.Y) + sy, 0)
                 * Matrix.CreateScale(Zoom, Zoom, 1f);
        }
    }
}
