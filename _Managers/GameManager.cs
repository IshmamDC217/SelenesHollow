namespace SelenesHollow;

public class GameManager
{
    // Where Selene wakes up — on the path two cells south of the fountain.
    private static readonly Point StartCell = new(14, 11);

    // Idle musings: random thoughts after standing still this long.
    private const float IDLE_MUSING_DELAY = 22f;
    private static readonly string[] IdleMusings =
    {
        "...",
        "I should keep moving.",
        "It's so quiet here.",
        "I wonder if anyone's looking for me.",
        "The wind smells like rain.",
        "How long have I been wandering?",
        "Maybe I'll remember if I just stand still.",
    };

    private readonly Map _map;
    private Player _player;
    private IntroSequence _intro;
    private readonly AmbientDialog _ambient;
    private readonly DialogTriggers _triggers;
    private readonly OrbManager _orbs;
    private readonly HudOverlay _hud;
    private readonly ChaliceInteraction _chalice;
    private KeyboardState _lastKb;
    private float _idleTimer;
    private int _lastMusingIdx = -1;
    private readonly Random _musingRng = new();
    private bool _wasFocusOnPlayer;

    public GameManager()
    {
        _map = new Map();
        _ambient = new AmbientDialog();
        _triggers = new DialogTriggers(_ambient);
        _orbs = new OrbManager(_map, _ambient);
        _hud = new HudOverlay(_orbs);
        _chalice = new ChaliceInteraction(_map, _ambient);
        ResetRunState();
    }

    // F5 restart wipes the player + intro back to their initial state and
    // replays the opening sequence. The map itself is static so we keep it.
    private void ResetRunState()
    {
        _player = new Player(_map.GetCellFootPos(StartCell));
        _map.Player = _player;
        _intro = new IntroSequence(_map.FountainCenter);
        _triggers.Reset();
        _ambient.Clear();
        _orbs.Reset();
        _chalice.Reset();
        _idleTimer = 0f;
        _wasFocusOnPlayer = false;
    }

    public void Update()
    {
        InputManager.Update();

        var kb = Keyboard.GetState();
        if (kb.IsKeyDown(Keys.F5) && _lastKb.IsKeyUp(Keys.F5))
            ResetRunState();
        _lastKb = kb;

        _map.Update();
        // While the intro is playing the player is locked in place AND hidden
        // so only the sleeping sprite is visible at the fountain. Once the
        // intro crosses into its post-dismiss black hold, PlayerVisible flips
        // true so the player appears as the world fades back in.
        _player.IsHidden = !_intro.PlayerVisible;
        if (!_intro.Finished)
        {
            _intro.Update();
        }
        else
        {
            var (minFoot, maxFoot) = _map.GetWalkBounds();
            _player.Update(minFoot, maxFoot, _map.Obstacles);
            _triggers.Check(_player.FootPos);
            _orbs.Update(_player.FootPos);
            _chalice.Update(_player.FootPos);
            UpdateIdleMusings();
        }
        _ambient.Update();

        // ----- Camera follow -----
        // During the intro the camera centres on the fountain so the sleeping
        // Selene is framed; once the player becomes visible we hand off so
        // exploration follows her with a smooth lerp.
        bool focusOnPlayer = _intro.PlayerVisible;
        Vector2 focus = focusOnPlayer ? _player.FootPos : _map.FountainCenter;
        var viewport = Globals.GraphicsDevice.Viewport;
        var (mapMin, mapMax) = _map.GetPixelBounds();
        // Snap on the frame focus hands off (from fountain to player) — that
        // happens during the post-dismiss black hold so the user never sees the
        // jump, and the fade-in then begins already centred on Selene.
        if (focusOnPlayer && !_wasFocusOnPlayer)
            Globals.Camera.Snap(focus, mapMin, mapMax, viewport.Width, viewport.Height);
        else
            Globals.Camera.Follow(focus, mapMin, mapMax, viewport.Width, viewport.Height);
        _wasFocusOnPlayer = focusOnPlayer;
    }

    private void UpdateIdleMusings()
    {
        if (_player.IsMoving || _ambient.IsBusy)
        {
            _idleTimer = 0f;
            return;
        }
        _idleTimer += Globals.TotalSeconds;
        if (_idleTimer >= IDLE_MUSING_DELAY)
        {
            int idx;
            // Pick a different line than the one we just used so it doesn't
            // repeat on consecutive musings.
            do { idx = _musingRng.Next(IdleMusings.Length); }
            while (idx == _lastMusingIdx && IdleMusings.Length > 1);
            _lastMusingIdx = idx;
            _ambient.Show(IdleMusings[idx]);
            _idleTimer = 0f;
        }
    }

    public void DrawWorld()
    {
        _map.Draw();
        _orbs.Draw();
        _chalice.DrawWorld();
        if (!_intro.Finished) _intro.DrawWorld();
    }

    public void DrawUI()
    {
        if (!_intro.Finished) _intro.DrawUI();
        _hud.Draw(visible: _intro.Finished || _intro.PlayerVisible);
        _ambient.Draw();
    }
}
