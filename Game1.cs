namespace SelenesHollow;

public class Game1 : Game
{
    private readonly GraphicsDeviceManager _graphics;
    private SpriteBatch _spriteBatch;
    private GameManager _gameManager;

    public Game1()
    {
        _graphics = new GraphicsDeviceManager(this);
        Content.RootDirectory = "Content";
        IsMouseVisible = true;
    }

    protected override void Initialize()
    {
        _graphics.PreferredBackBufferWidth = 1280;
        _graphics.PreferredBackBufferHeight = 800;
        _graphics.ApplyChanges();

        Window.Title = "Selene's Hollow";

        Globals.Content = Content;
        Globals.GraphicsDevice = GraphicsDevice;
        Globals.Pixel = new Texture2D(GraphicsDevice, 1, 1);
        Globals.Pixel.SetData(new[] { Color.White });
        Globals.Camera = new Camera();

        _gameManager = new();

        base.Initialize();
    }

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);
        Globals.SpriteBatch = _spriteBatch;
    }

    protected override void Update(GameTime gameTime)
    {
        if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed || Keyboard.GetState().IsKeyDown(Keys.Escape))
            Exit();

        Globals.Update(gameTime);
        _gameManager.Update();

        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(Color.Black);

        // World pass — camera-transformed so the map scrolls under the player.
        // PointClamp keeps pixel-art crisp when scaled up (no bilinear blur).
        _spriteBatch.Begin(samplerState: SamplerState.PointClamp,
                           transformMatrix: Globals.Camera.Transform);
        _gameManager.DrawWorld();
        _spriteBatch.End();

        // UI pass — screen-space, on top of the world (intro overlays, dialog).
        _spriteBatch.Begin(samplerState: SamplerState.PointClamp);
        _gameManager.DrawUI();
        _spriteBatch.End();

        base.Draw(gameTime);
    }
}
