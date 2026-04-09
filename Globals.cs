using Microsoft.Xna.Framework.Content;

namespace SelenesHollow;

public static class Globals
{
    public static float TotalSeconds { get; set; }   // delta this frame
    public static float RunningSeconds { get; set; } // accumulated since start
    public static ContentManager Content { get; set; }
    public static SpriteBatch SpriteBatch { get; set; }
    public static GraphicsDevice GraphicsDevice { get; set; }
    // 1x1 white texture used to draw solid-colour rects (overlays, dialog bg).
    public static Texture2D Pixel { get; set; }
    public static Camera Camera { get; set; }

    public static void Update(GameTime gt)
    {
        TotalSeconds = (float)gt.ElapsedGameTime.TotalSeconds;
        RunningSeconds += TotalSeconds;
    }
}
