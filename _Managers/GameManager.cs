namespace SelenesHollow;

public class GameManager
{
    private enum GameState { Intro, TutorialBattle, Overworld }

    // Where Selene wakes up — on the path two cells south of the fountain.
    private static readonly Point StartCell = new(24, 16);
    // Where Selene teleports for the tutorial battle (edge of the practice field)
    private static readonly Point BattleStartCell = new(35, 8);

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

    private GameState _state;
    private readonly Map _map;
    private Player _player;
    private IntroSequence _intro;
    private BattleArena _arena;
    private readonly AmbientDialog _ambient;
    private readonly PortraitDialog _portrait;
    private Texture2D _spiritPortrait;
    private bool _postBattleDialogShown;
    // Wisp NPC in overworld (near practice field)
    private static readonly Point WispCell = new(34, 8);
    private Vector2 _wispWorldPos;
    private bool _wispNearby;
    private Texture2D _wispGlow;
    private readonly DialogTriggers _triggers;
    private readonly ChaliceInteraction _chalice;
    private readonly SpiritNPCs _spiritNPCs;
    private KeyboardState _lastKb;
    private float _idleTimer;
    private int _lastMusingIdx = -1;
    private readonly Random _musingRng = new();
    private bool _wasFocusOnPlayer;
    private bool _tutorialDone;

    public GameManager()
    {
        _map = new Map();
        _ambient = new AmbientDialog();
        _portrait = new PortraitDialog();
        _triggers = new DialogTriggers(_ambient);
        _chalice = new ChaliceInteraction(_map, _ambient);
        _spiritNPCs = new SpiritNPCs(_map, _ambient);
        _wispWorldPos = _map.GetCellFootPos(WispCell) + new Vector2(0, -55);
        _wispGlow = BuildGlow(48);
        MusicManager.Load();
        ResetRunState();
    }

    private void ResetRunState()
    {
        _state = GameState.Intro;
        _player = new Player(_map.GetCellFootPos(StartCell));
        _map.Player = _player;
        _intro = new IntroSequence(_map.FountainCenter, _portrait);
        _arena = null;
        _portrait.Clear();
        _triggers.Reset();
        _ambient.Clear();
        _chalice.Reset();
        _spiritNPCs.Reset();
        _spiritNPCs.Visible = false;
        _idleTimer = 0f;
        _wasFocusOnPlayer = false;
        _tutorialDone = false;
        _postBattleDialogShown = false;
        MusicManager.PlayIntro();
    }

    public void Update()
    {
        InputManager.Update();

        var kb = Keyboard.GetState();
        if (kb.IsKeyDown(Keys.F5) && _lastKb.IsKeyUp(Keys.F5))
            ResetRunState();
        _lastKb = kb;

        switch (_state)
        {
            case GameState.Intro:
                UpdateIntro();
                break;
            case GameState.TutorialBattle:
                UpdateBattle();
                break;
            case GameState.Overworld:
                UpdateOverworld();
                break;
        }

        _ambient.Update();
        _portrait.Update();
    }

    private void UpdateIntro()
    {
        _map.Update();
        _player.IsHidden = !_intro.PlayerVisible;
        _intro.Update();

        if (_intro.Finished && !_tutorialDone)
        {
            // Teleport player to the practice field and start battle
            _player.FootPos = _map.GetCellFootPos(BattleStartCell);
            _player.IsHidden = false;
            _arena = new BattleArena(_map);
            _state = GameState.TutorialBattle;
            MusicManager.PlayBattle();
        }
        else if (_intro.Finished && _tutorialDone)
        {
            _state = GameState.Overworld;
        }

        UpdateCamera();
    }

    private void UpdateBattle()
    {
        _map.Update();

        // Player moves freely but clamped to the arena — locked during melee
        if (_arena.CurrentPhase == BattleArena.Phase.Combat && !_arena.MeleeActive)
        {
            var (minFoot, maxFoot) = _map.GetWalkBounds();
            _player.Update(minFoot, maxFoot, _map.Obstacles);
            _player.FootPos = _arena.ClampPlayer(_player.FootPos);
        }

        _arena.Update(_player.FootPos);

        // Set IsHidden AFTER arena update so melee end is reflected immediately
        _player.IsHidden = _arena.MeleeActive;

        // Camera follows the player during battle
        var viewport = Globals.GraphicsDevice.Viewport;
        var (mapMin, mapMax) = _map.GetPixelBounds();
        Globals.Camera.Follow(_player.FootPos, mapMin, mapMax, viewport.Width, viewport.Height);

        if (_arena.IsFinished)
        {
            _tutorialDone = true;
            _state = GameState.Overworld;
            Globals.Camera.SetZoom(1f, 2f);
            _arena = null;
            _player.IsHidden = false;
            _spiritNPCs.Visible = true;
            MusicManager.PlayExplore();

            // Post-battle Wisp dialog (automatic, one-time)
            if (!_postBattleDialogShown)
            {
                _postBattleDialogShown = true;
                if (_spiritPortrait == null)
                    _spiritPortrait = _intro.SpiritPortraitTexture;

                Texture2D seleneTex = null;
                int sf = 1, sfw = 0, sfh = 0;
                try
                {
                    seleneTex = Globals.Content.Load<Texture2D>("selenetalking");
                    sfw = seleneTex.Width;
                    sfh = seleneTex.Height / 2;
                    sf = 2;
                }
                catch { }

                _portrait.ShowSequence(
                    new PortraitDialog.Line("Wisp", "Not bad! You really do know fire!",
                        _spiritPortrait),
                    new PortraitDialog.Line("Wisp", "The more you fight, the stronger you'll get.",
                        _spiritPortrait),
                    new PortraitDialog.Line("Selene", "I felt it... the fire came back, like muscle memory.",
                        seleneTex, sf, sfw, sfh),
                    new PortraitDialog.Line("Wisp", "Go explore the hollow. I'll be here if you want to train again!",
                        _spiritPortrait)
                );
            }
        }
    }

    private void UpdateOverworld()
    {
        _map.Update();
        _player.IsHidden = false;
        var (minFoot, maxFoot) = _map.GetWalkBounds();
        _player.Update(minFoot, maxFoot, _map.Obstacles);
        _triggers.Check(_player.FootPos);
        _chalice.Update(_player.FootPos);
        _spiritNPCs.Update(_player.FootPos);
        UpdateWispNPC();
        UpdateIdleMusings();
        UpdateCamera();
    }

    private bool _wispTrainPromptActive;
    private KeyboardState _wispLastKb;     // separate KB state for Wisp E detection

    private void UpdateWispNPC()
    {
        if (!_tutorialDone) return;
        if (_spiritPortrait == null)
            _spiritPortrait = _intro.SpiritPortraitTexture;

        var kb = Keyboard.GetState();
        bool ePressed = kb.IsKeyDown(Keys.E) && _wispLastKb.IsKeyUp(Keys.E);
        _wispLastKb = kb;

        _wispNearby = Vector2.Distance(_player.FootPos, _wispWorldPos + new Vector2(0, 55)) < 110f;

        if (!_wispNearby)
        {
            _wispTrainPromptActive = false;
            return;
        }

        if (_portrait.IsActive || _ambient.IsBusy) return;

        // First E near Wisp → show train prompt
        if (ePressed && !_wispTrainPromptActive)
        {
            _wispTrainPromptActive = true;
            return;
        }

        // Second E while prompt is showing → start battle
        if (_wispTrainPromptActive && ePressed)
        {
            _wispTrainPromptActive = false;
            _player.FootPos = _map.GetCellFootPos(BattleStartCell);
            _arena = new BattleArena(_map);
            _state = GameState.TutorialBattle;
            MusicManager.PlayBattle();
        }
    }

    private void UpdateCamera()
    {
        bool focusOnPlayer = _intro.PlayerVisible;
        Vector2 focus = focusOnPlayer ? _player.FootPos : _map.FountainCenter;
        var viewport = Globals.GraphicsDevice.Viewport;
        var (mapMin, mapMax) = _map.GetPixelBounds();
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

        // Battle arena elements (boundary, enemy, projectiles) in world space
        if (_state == GameState.TutorialBattle && _arena != null)
            _arena.DrawWorld(_player.FootPos);

        // Wisp NPC in overworld (circular golden glow near practice field)
        if (_tutorialDone && _state == GameState.Overworld)
        {
            var sb = Globals.SpriteBatch;
            var font = Globals.Content.Load<SpriteFont>("dialog");
            float t = Globals.RunningSeconds;
            float bob = 5f * MathF.Sin(t * 2.5f);
            float pulse = 0.75f + 0.25f * MathF.Sin(t * 3f);
            var pos = _wispWorldPos + new Vector2(0, bob);
            var glowO = new Vector2(24, 24);

            // Outer golden glow
            sb.Draw(_wispGlow, pos, null, new Color(255, 210, 80) * (0.55f * pulse),
                0f, glowO, pulse * 0.8f, SpriteEffects.None, 0f);
            // Inner white core
            sb.Draw(_wispGlow, pos, null, Color.White * (0.5f * pulse),
                0f, glowO, pulse * 0.35f, SpriteEffects.None, 0f);

            // Name + prompt above head (fades with distance)
            float dist = Vector2.Distance(_player.FootPos, _wispWorldPos + new Vector2(0, 55));
            if (dist < 150f)
            {
                float nameAlpha = 1f - (dist / 150f);
                nameAlpha *= nameAlpha;

                string label;
                if (_wispTrainPromptActive)
                    label = "Wisp   [E] Train";
                else if (_wispNearby && !_portrait.IsActive)
                    label = "Wisp   [E]";
                else
                    label = "Wisp";

                var nSz = font.MeasureString(label);
                var nPos = pos + new Vector2(0, -28);
                sb.DrawString(font, label, nPos + Vector2.One, Color.Black * (0.4f * nameAlpha),
                    0f, new Vector2(nSz.X / 2f, nSz.Y / 2f), 0.6f, SpriteEffects.None, 0f);
                sb.DrawString(font, label, nPos, Color.White * nameAlpha,
                    0f, new Vector2(nSz.X / 2f, nSz.Y / 2f), 0.6f, SpriteEffects.None, 0f);
            }
        }

        _spiritNPCs.Draw();
        _spiritNPCs.DrawPrompt();
        _chalice.DrawWorld();
        if (!_intro.Finished) _intro.DrawWorld();
    }

    public void DrawUI()
    {
        if (_state == GameState.TutorialBattle && _arena != null)
        {
            _arena.DrawUI();
            _portrait.Draw();
            return;
        }
        if (!_intro.Finished) _intro.DrawUI();
        _ambient.Draw();
        _portrait.Draw();
    }

    private static Texture2D BuildGlow(int size)
    {
        var tex = new Texture2D(Globals.GraphicsDevice, size, size);
        var px = new Color[size * size];
        float c = (size - 1) / 2f;
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float d = MathF.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c;
            if (d >= 1f) { px[y * size + x] = Color.Transparent; continue; }
            px[y * size + x] = new Color(255, 255, 255, (int)((1f - d * d) * 255));
        }
        tex.SetData(px);
        return tex;
    }
}
