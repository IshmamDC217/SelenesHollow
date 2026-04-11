namespace SelenesHollow;

// Real-time combat arena that exists in the overworld. The practice field is a
// rectangular area on the map. Selene moves freely with WASD inside it, presses
// Space to pause and pick a move from the command selector, then the projectile
// fires toward the enemy. The enemy moves around and periodically attacks.
// Dodge enemy shots by moving out of the way.
public class BattleArena
{
    public enum Phase { Intro, Combat, Paused, Victory, Outro, Done }

    // ----- Arena bounds (cell coords, converted to world px) -----
    // Practice field: above the eastern ruins
    private static readonly Point ArenaTopLeft  = new(35, 5);
    private static readonly Point ArenaBottomRight = new(42, 10);

    // ----- Stats -----
    private int _playerHP = 100, _playerMaxHP = 100;
    private int _playerMP = 40,  _playerMaxMP = 40;
    private int _enemyHP, _enemyMaxHP;
    private string _enemyName = "Hollow Shade";
    private int _enemyDamage = 8;
    private const float ENEMY_SPEED = 80f;
    private const float ENEMY_ATTACK_CD = 2.5f;

    // ----- Spectator spirits lining the arena borders (crowd) -----
    private record struct Spectator(Vector2 Offset, Color Tint, float Phase, float Speed);
    private Spectator[] _spectators;

    private void BuildSpectators()
    {
        var list = new List<Spectator>();
        var rng = new Random(99);
        Color[] palette =
        {
            new(255, 120, 120), new(120, 255, 160), new(255, 220, 80),
            new(200, 130, 255), new(140, 255, 255), new(255, 160, 220),
            new(255, 180, 140), new(120, 200, 255), new(255, 255, 130),
            new(180, 255, 180), new(255, 140, 180), new(160, 180, 255),
            new(255, 200, 120), new(200, 255, 200), new(255, 160, 100),
        };
        int w = _worldBounds.Width;
        int h = _worldBounds.Height;

        // North border
        for (int i = 0; i < 12; i++)
        {
            float x = (i / 11f) * w - 20;
            list.Add(new(new(x, -45 - rng.Next(30)), palette[rng.Next(palette.Length)],
                rng.NextSingle() * 6f, 1.6f + rng.NextSingle() * 0.8f));
        }
        // South border
        for (int i = 0; i < 12; i++)
        {
            float x = (i / 11f) * w - 20;
            list.Add(new(new(x, h + 15 + rng.Next(30)), palette[rng.Next(palette.Length)],
                rng.NextSingle() * 6f, 1.6f + rng.NextSingle() * 0.8f));
        }
        // West border
        for (int i = 0; i < 10; i++)
        {
            float y = (i / 9f) * h - 20;
            list.Add(new(new(-45 - rng.Next(30), y), palette[rng.Next(palette.Length)],
                rng.NextSingle() * 6f, 1.6f + rng.NextSingle() * 0.8f));
        }
        // East border
        for (int i = 0; i < 10; i++)
        {
            float y = (i / 9f) * h - 20;
            list.Add(new(new(w + 15 + rng.Next(30), y), palette[rng.Next(palette.Length)],
                rng.NextSingle() * 6f, 1.6f + rng.NextSingle() * 0.8f));
        }
        // Extra crowd (second row behind each side for density)
        for (int i = 0; i < 8; i++)
        {
            float x = (i / 7f) * w;
            list.Add(new(new(x, -80 - rng.Next(20)), palette[rng.Next(palette.Length)],
                rng.NextSingle() * 6f, 1.5f + rng.NextSingle() * 0.6f));
            list.Add(new(new(x, h + 45 + rng.Next(20)), palette[rng.Next(palette.Length)],
                rng.NextSingle() * 6f, 1.5f + rng.NextSingle() * 0.6f));
        }
        for (int i = 0; i < 6; i++)
        {
            float y = (i / 5f) * h;
            list.Add(new(new(-85 - rng.Next(20), y), palette[rng.Next(palette.Length)],
                rng.NextSingle() * 6f, 1.5f + rng.NextSingle() * 0.6f));
            list.Add(new(new(w + 50 + rng.Next(20), y), palette[rng.Next(palette.Length)],
                rng.NextSingle() * 6f, 1.5f + rng.NextSingle() * 0.6f));
        }
        _spectators = list.ToArray();
    }

    // Crowd commentary — triggered on first hit, big hits, low enemy HP
    private static readonly string[] FirstHitLines =
    {
        "Wow, she's good!", "Did you see that?!", "A fire wielder!",
    };
    private static readonly string[] BigHitLines =
    {
        "That was HUGE!", "Incredible!", "Feel the burn!",
    };
    private static readonly string[] LowHPLines =
    {
        "She's gonna win!", "Finish it!", "One more hit!",
    };
    private int _hitCount;
    private bool _firstHitCommented;
    private bool _lowHPCommented;

    // Crowd shout popup (separate from damage popups)
    private string _crowdText;
    private Vector2 _crowdPos;
    private float _crowdT;
    private Color _crowdColor;

    // Victory fade
    private float _victoryFade; // 0→1 black overlay during victory

    // ----- State -----
    private Phase _phase = Phase.Intro;
    private float _phaseT;
    private float _totalT;
    private float _enemyAtkTimer;
    private bool _shielded;
    private float _shieldTimer;

    // ----- Enemy -----
    private Vector2 _enemyPos;
    private Vector2 _enemyTarget;     // AI wander target
    private float _wanderTimer;
    private float _enemyFlash;

    // ----- Projectiles -----
    private record struct Projectile(Vector2 Pos, Vector2 Vel, bool IsPlayer, string MoveName, int Damage, float Life);
    private readonly List<Projectile> _projectiles = new();

    // ----- Damage popups -----
    private record struct Popup(string Text, Vector2 Pos, float T);
    private readonly List<Popup> _popups = new();

    // ----- Melee attacks -----
    private const float MELEE_RANGE = 120f;
    private const float MELEE_FRAME_DUR = 0.025f;   // snappy fast animations
    private const int MELEE_FRAME_SIZE = 256;
    private const int MELEE_FRAMES_PER_ROW = 5;
    // Per-attack frame counts set in the MeleeAttack record below
    // Melee sheets have smaller art within the same 256x256 frames.
    // Bump scale slightly and adjust origin to their foot position so
    // Selene's on-screen body matches her idle/run size.
    private const float MELEE_SCALE = 0.58f;
    private static readonly Vector2 MeleeOrigin = new(128f, 202f);

    private record struct MeleeAttack(string Name, int Damage, Texture2D Sheet, float Cooldown, int FrameCount);
    private MeleeAttack[] _meleeAttacks;
    private float[] _meleeCooldowns;
    private int _activeMelee = -1;       // -1 = not attacking
    private int _meleeFrame;
    private float _meleeFrameTimer;
    private bool _meleeHit;              // did this swing already connect?

    private bool _facingRight = true;   // tracks Selene's facing based on movement

    public bool MeleeActive => _activeMelee >= 0;
    public Texture2D MeleeSheet => _activeMelee >= 0 ? _meleeAttacks[_activeMelee].Sheet : null;
    public int MeleeFrameIndex => _meleeFrame;

    // ----- Visual -----
    private readonly CommandWheel _wheel;
    private readonly SpriteFont _font;
    private readonly Texture2D _glow;
    private readonly Texture2D _enemyTex;
    private readonly Map _map;
    private KeyboardState _lastKb;

    // World-space bounds
    private Rectangle _worldBounds;

    public bool IsFinished => _phase == Phase.Done;
    public Phase CurrentPhase => _phase;

    // The arena needs to constrain the player — expose bounds so GameManager can clamp
    public Vector2 ClampPlayer(Vector2 foot)
    {
        return new Vector2(
            Math.Clamp(foot.X, _worldBounds.Left + 30, _worldBounds.Right - 30),
            Math.Clamp(foot.Y, _worldBounds.Top + 30, _worldBounds.Bottom - 10));
    }

    public BattleArena(Map map)
    {
        _map = map;
        _wheel = new CommandWheel();
        _font = Globals.Content.Load<SpriteFont>("dialog");
        _glow = BuildGlow(48);
        _enemyTex = BuildTrainingSpirit(72);

        // Only punch available at start (sword/greatsword unlocked at temple)
        var punchTex = Globals.Content.Load<Texture2D>("selene_punch");
        _meleeAttacks = new MeleeAttack[]
        {
            new("Fire Punch", 30, punchTex, 0.5f, 10),
        };
        _meleeCooldowns = new float[_meleeAttacks.Length];

        // Convert cell bounds to world pixels
        var tl = map.GetCellFootPos(ArenaTopLeft);
        var br = map.GetCellFootPos(ArenaBottomRight);
        _worldBounds = new Rectangle(
            (int)(tl.X - Map.TILE_SCREEN / 2), (int)(tl.Y - Map.TILE_SCREEN),
            (int)(br.X - tl.X + Map.TILE_SCREEN), (int)(br.Y - tl.Y + Map.TILE_SCREEN / 2));

        // Enemy starts at center-right of the arena
        _enemyPos = new Vector2(_worldBounds.Center.X + 100, _worldBounds.Center.Y);
        _enemyTarget = _enemyPos;

        _enemyHP = _enemyMaxHP = 80;
        _enemyAtkTimer = 1.5f;
        BuildSpectators();
    }

    // Time scale: 1.0 = normal, 0.3 = slow-mo when selecting moves
    public float TimeScale { get; private set; } = 1f;

    private Vector2 _lastPlayerFoot;

    public void Update(Vector2 playerFoot)
    {
        // Track facing direction from movement
        float dx = playerFoot.X - _lastPlayerFoot.X;
        if (dx > 0.5f) _facingRight = true;
        else if (dx < -0.5f) _facingRight = false;
        _lastPlayerFoot = playerFoot;

        // Slow-mo when command menu is open instead of full pause
        float rawDt = Globals.TotalSeconds;
        TimeScale = _phase == Phase.Paused ? 0.15f : 1f;
        float dt = rawDt * TimeScale;
        _totalT += dt;
        _phaseT += dt;

        // Update popups
        for (int i = _popups.Count - 1; i >= 0; i--)
        {
            var p = _popups[i];
            _popups[i] = p with { T = p.T + dt };
            if (p.T > 1.5f) _popups.RemoveAt(i);
        }

        _enemyFlash = Math.Max(0f, _enemyFlash - dt * 5f);
        if (_shieldTimer > 0) { _shieldTimer -= dt; if (_shieldTimer <= 0) _shielded = false; }

        var kb = Keyboard.GetState();
        bool spacePressed = kb.IsKeyDown(Keys.Space) && _lastKb.IsKeyUp(Keys.Space);

        switch (_phase)
        {
            case Phase.Intro:
                // Zoom in for battle drama
                Globals.Camera.SetZoom(1.4f, 2f);
                if (_phaseT >= 0.8f) AdvanceTo(Phase.Combat);
                break;

            case Phase.Combat:
                UpdateCombat(playerFoot, dt, kb, spacePressed);
                break;

            case Phase.Paused:
            {
                bool shiftHeld = kb.IsKeyDown(Keys.LeftShift) || kb.IsKeyDown(Keys.RightShift);
                _wheel.Update();
                if (_wheel.SelectedMove != null)
                {
                    var move = _wheel.SelectedMove.Value;
                    _wheel.Visible = false;
                    _wheel.ResetSelection();
                    if (move.IsMelee)
                    {
                        if (_activeMelee < 0 && _meleeCooldowns[0] <= 0 && _playerMP >= move.ManaCost)
                        {
                            _playerMP -= move.ManaCost;
                            _activeMelee = 0;
                            _meleeFrame = 0;
                            _meleeFrameTimer = 0f;
                            _meleeHit = false;
                            _meleeCooldowns[0] = _meleeAttacks[0].Cooldown;
                        }
                    }
                    else
                        ExecuteMove(move, playerFoot);
                    AdvanceTo(Phase.Combat);
                }
                // Release Shift = back to combat
                if (!shiftHeld)
                {
                    _wheel.Visible = false;
                    _wheel.ResetSelection();
                    AdvanceTo(Phase.Combat);
                }
                break;
            }

            case Phase.Victory:
                Globals.Camera.SetZoom(1f, 1.5f);
                // Fade to black over 2 seconds after a 1s hold
                _victoryFade = _phaseT > 1f ? Math.Clamp((_phaseT - 1f) / 1.5f, 0f, 1f) : 0f;
                if (_phaseT >= 3f) AdvanceTo(Phase.Outro);
                break;

            case Phase.Outro:
                if (_phaseT >= 1f) AdvanceTo(Phase.Done);
                break;
        }
        _lastKb = kb;
    }

    private void UpdateCombat(Vector2 playerFoot, float dt, KeyboardState kb, bool spacePressed)
    {
        // ----- Melee cooldowns -----
        for (int i = 0; i < _meleeCooldowns.Length; i++)
            if (_meleeCooldowns[i] > 0) _meleeCooldowns[i] -= dt;

        // ----- Melee animation update -----
        if (_activeMelee >= 0)
        {
            _meleeFrameTimer += dt;
            if (_meleeFrameTimer >= MELEE_FRAME_DUR)
            {
                _meleeFrameTimer -= MELEE_FRAME_DUR;
                _meleeFrame++;

                // Check hit mid-swing (around frame 6-12) — must be in facing direction
                if (!_meleeHit && _meleeFrame >= 6 && _meleeFrame <= 12)
                {
                    bool enemyInFront = _facingRight
                        ? _enemyPos.X >= playerFoot.X - 20
                        : _enemyPos.X <= playerFoot.X + 20;
                    if (enemyInFront && Vector2.Distance(playerFoot, _enemyPos) < MELEE_RANGE)
                    {
                        _meleeHit = true;
                        int dmg = _meleeAttacks[_activeMelee].Damage;
                        _enemyHP = Math.Max(0, _enemyHP - dmg);
                        _enemyFlash = 1f;
                        _popups.Add(new Popup($"-{dmg}", _enemyPos + new Vector2(0, -80), 0f));
                        Globals.Camera.Shake(dmg > 25 ? 8f : 5f, 0.3f);
                        var knockDir = _enemyPos - playerFoot;
                        if (knockDir.LengthSquared() > 0) knockDir.Normalize();
                        _enemyPos += knockDir * 30f;
                        _hitCount++;
                        TriggerCrowdReaction(dmg);
                        if (_enemyHP <= 0) AdvanceTo(Phase.Victory);
                    }
                }

                if (_meleeFrame >= _meleeAttacks[_activeMelee].FrameCount)
                    _activeMelee = -1; // animation done
            }
        }

        // ----- Quick-fire with I/O/K/L/P -----
        for (int i = 0; i < _wheel.MoveCount && i < CommandWheel.QuickKeys.Length; i++)
        {
            var key = CommandWheel.QuickKeys[i];
            if (kb.IsKeyDown(key) && _lastKb.IsKeyUp(key))
            {
                var move = _wheel.GetMove(i);
                if (move.IsMelee)
                {
                    // Fire Punch uses melee animation
                    if (_activeMelee < 0 && _meleeCooldowns[0] <= 0 && _playerMP >= move.ManaCost)
                    {
                        _playerMP -= move.ManaCost;
                        _activeMelee = 0;
                        _meleeFrame = 0;
                        _meleeFrameTimer = 0f;
                        _meleeHit = false;
                        _meleeCooldowns[0] = _meleeAttacks[0].Cooldown;
                    }
                    else if (_playerMP < move.ManaCost)
                        _popups.Add(new Popup("No MP!", playerFoot + new Vector2(0, -90), 0f));
                }
                else
                {
                    ExecuteMove(move, playerFoot);
                }
                break;
            }
        }

        // Hold Shift = slow-mo + open command selector
        bool shiftHeld = kb.IsKeyDown(Keys.LeftShift) || kb.IsKeyDown(Keys.RightShift);
        if (shiftHeld && _activeMelee < 0)
        {
            _wheel.Visible = true;
            AdvanceTo(Phase.Paused);
            return;
        }

        // ----- Enemy AI -----
        // Wander around the arena
        _wanderTimer -= dt;
        if (_wanderTimer <= 0f)
        {
            _wanderTimer = 1.5f + (float)new Random().NextDouble() * 2f;
            _enemyTarget = new Vector2(
                _worldBounds.Left + 60 + (float)new Random().NextDouble() * (_worldBounds.Width - 120),
                _worldBounds.Top + 40 + (float)new Random().NextDouble() * (_worldBounds.Height - 80));
        }
        var toTarget = _enemyTarget - _enemyPos;
        if (toTarget.LengthSquared() > 4f)
        {
            toTarget.Normalize();
            _enemyPos += toTarget * ENEMY_SPEED * dt;
        }
        // Keep enemy in bounds
        _enemyPos = new Vector2(
            Math.Clamp(_enemyPos.X, _worldBounds.Left + 30, _worldBounds.Right - 30),
            Math.Clamp(_enemyPos.Y, _worldBounds.Top + 30, _worldBounds.Bottom - 10));

        // Enemy attack on cooldown
        _enemyAtkTimer -= dt;
        if (_enemyAtkTimer <= 0f)
        {
            _enemyAtkTimer = ENEMY_ATTACK_CD;
            var dir = playerFoot - _enemyPos;
            if (dir.LengthSquared() > 0) dir.Normalize();
            _projectiles.Add(new Projectile(_enemyPos + new Vector2(0, -30), dir * 280f,
                false, "Spirit Shot", _enemyDamage, 0f));
        }

        // ----- Update projectiles -----
        for (int i = _projectiles.Count - 1; i >= 0; i--)
        {
            var p = _projectiles[i];
            p = p with { Pos = p.Pos + p.Vel * dt, Life = p.Life + dt };
            _projectiles[i] = p;

            // Remove if out of arena or too old
            if (p.Life > 3f || !_worldBounds.Contains(p.Pos.ToPoint()))
            {
                _projectiles.RemoveAt(i);
                continue;
            }

            // Hit detection
            if (p.IsPlayer)
            {
                if (Vector2.Distance(p.Pos, _enemyPos + new Vector2(0, -30)) < 45f)
                {
                    _enemyHP = Math.Max(0, _enemyHP - p.Damage);
                    _enemyFlash = 1f;
                    _popups.Add(new Popup($"-{p.Damage}", _enemyPos + new Vector2(0, -80), 0f));
                    _projectiles.RemoveAt(i);
                    // Screen shake on hit — bigger for bigger damage
                    Globals.Camera.Shake(p.Damage > 20 ? 7f : 4f, 0.25f);
                    // Knockback enemy away from projectile
                    var knockDir = _enemyPos - p.Pos;
                    if (knockDir.LengthSquared() > 0) knockDir.Normalize();
                    _enemyPos += knockDir * 25f;
                    _hitCount++;
                    TriggerCrowdReaction(p.Damage);
                    if (_enemyHP <= 0) AdvanceTo(Phase.Victory);
                }
            }
            else
            {
                if (Vector2.Distance(p.Pos, playerFoot + new Vector2(0, -30)) < 35f)
                {
                    int dmg = _shielded ? p.Damage / 3 : p.Damage;
                    _playerHP = Math.Max(0, _playerHP - dmg);
                    string label = _shielded ? $"-{dmg} blocked" : $"-{dmg}";
                    _popups.Add(new Popup(label, playerFoot + new Vector2(0, -90), 0f));
                    _projectiles.RemoveAt(i);
                    // Screen shake when player is hit
                    Globals.Camera.Shake(_shielded ? 3f : 6f, 0.3f);
                }
            }
        }
    }

    private void ExecuteMove(CommandWheel.Move move, Vector2 playerFoot)
    {
        if (move.MpRestore > 0)
        {
            _playerMP = Math.Min(_playerMaxMP, _playerMP + move.MpRestore);
            _popups.Add(new Popup($"+{move.MpRestore} MP", playerFoot + new Vector2(0, -90), 0f));
            return;
        }
        if (move.Name == "Flame Shield")
        {
            if (_playerMP >= move.ManaCost)
            {
                _playerMP -= move.ManaCost;
                _shielded = true;
                _shieldTimer = 3f;
                _popups.Add(new Popup("SHIELD!", playerFoot + new Vector2(0, -90), 0f));
            }
            else
                _popups.Add(new Popup("No MP!", playerFoot + new Vector2(0, -90), 0f));
            return;
        }
        if (_playerMP < move.ManaCost)
        {
            _popups.Add(new Popup("No MP!", playerFoot + new Vector2(0, -90), 0f));
            return;
        }
        _playerMP -= move.ManaCost;

        // Fire projectile toward enemy
        var dir = _enemyPos - playerFoot;
        if (dir.LengthSquared() > 0) dir.Normalize();

        float speed = move.Name == "Fire Punch" ? 450f : move.Name == "Ember Burst" ? 300f : 350f;

        if (move.Name == "Ember Burst")
        {
            // Fan of 5 sparks
            for (int i = -2; i <= 2; i++)
            {
                float angle = MathF.Atan2(dir.Y, dir.X) + i * 0.2f;
                var vel = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * speed;
                _projectiles.Add(new Projectile(playerFoot + new Vector2(0, -40), vel,
                    true, move.Name, move.Damage / 3, 0f));
            }
        }
        else
        {
            _projectiles.Add(new Projectile(playerFoot + new Vector2(0, -40), dir * speed,
                true, move.Name, move.Damage, 0f));
        }
    }

    private void AdvanceTo(Phase p) { _phase = p; _phaseT = 0f; }

    private readonly Random _crowdRng = new();

    private void TriggerCrowdReaction(int dmg)
    {
        // Pick a random spectator position for the shout
        var spec = _spectators[_crowdRng.Next(_spectators.Length)];
        var shoutPos = new Vector2(_worldBounds.X, _worldBounds.Y) + spec.Offset + new Vector2(0, -25);

        if (!_firstHitCommented && _hitCount >= 1)
        {
            _firstHitCommented = true;
            _crowdText = FirstHitLines[_crowdRng.Next(FirstHitLines.Length)];
            _crowdPos = shoutPos;
            _crowdT = 0f;
            _crowdColor = spec.Tint;
            return;
        }

        if (!_lowHPCommented && _enemyHP > 0 && (float)_enemyHP / _enemyMaxHP < 0.25f)
        {
            _lowHPCommented = true;
            _crowdText = LowHPLines[_crowdRng.Next(LowHPLines.Length)];
            _crowdPos = shoutPos;
            _crowdT = 0f;
            _crowdColor = spec.Tint;
            return;
        }

        // Random chance for big hit commentary
        if (dmg >= 20 && _crowdRng.Next(3) == 0)
        {
            _crowdText = BigHitLines[_crowdRng.Next(BigHitLines.Length)];
            _crowdPos = shoutPos;
            _crowdT = 0f;
            _crowdColor = spec.Tint;
        }
    }

    // ==================== DRAWING (world space) ====================

    public void DrawWorld(Vector2 playerFoot)
    {
        var sb = Globals.SpriteBatch;

        // Arena boundary glow (subtle pulsing rectangle)
        float pulse = 0.15f + 0.05f * MathF.Sin(_totalT * 2f);
        Color borderCol = new Color(100, 200, 255) * pulse;
        int bw = 3;
        sb.Draw(Globals.Pixel, new Rectangle(_worldBounds.X, _worldBounds.Y, _worldBounds.Width, bw), borderCol);
        sb.Draw(Globals.Pixel, new Rectangle(_worldBounds.X, _worldBounds.Bottom - bw, _worldBounds.Width, bw), borderCol);
        sb.Draw(Globals.Pixel, new Rectangle(_worldBounds.X, _worldBounds.Y, bw, _worldBounds.Height), borderCol);
        sb.Draw(Globals.Pixel, new Rectangle(_worldBounds.Right - bw, _worldBounds.Y, bw, _worldBounds.Height), borderCol);

        // ----- Spectator crowd lining arena borders -----
        var specOrigin = new Vector2(24, 24);
        foreach (var s in _spectators)
        {
            float sp = s.Phase + _totalT * s.Speed;
            float sPulse = 0.6f + 0.4f * MathF.Sin(sp);
            float sBob = 6f * MathF.Sin(sp * 0.7f);
            float sWave = 3f * MathF.Sin(sp * 1.5f); // lateral sway like cheering
            var sPos = new Vector2(_worldBounds.X, _worldBounds.Y) + s.Offset
                       + new Vector2(sWave, sBob);

            // Outer glow
            sb.Draw(_glow, sPos, null, s.Tint * (0.45f * sPulse),
                0f, specOrigin, sPulse * 0.6f, SpriteEffects.None, 0f);
            // Core
            sb.Draw(_glow, sPos, null, Color.White * (0.35f * sPulse),
                0f, specOrigin, sPulse * 0.25f, SpriteEffects.None, 0f);
        }

        // Enemy
        if (_enemyHP > 0 || _phase != Phase.Victory)
        {
            float ea = (_phase == Phase.Victory) ? Math.Max(0, 1f - _phaseT / 1.5f) : 1f;
            float bob = 5f * MathF.Sin(_totalT * 2.5f);
            Color tint = _enemyFlash > 0
                ? Color.Lerp(Color.White, Color.Red, _enemyFlash * 0.7f) : Color.White;

            // Shadow
            var glowOrigin = new Vector2(24, 24);
            sb.Draw(_glow, _enemyPos + new Vector2(0, 2), null, new Color(0, 0, 0) * (0.25f * ea),
                0f, glowOrigin, new Vector2(1.8f, 0.5f), SpriteEffects.None, 0f);

            var origin = new Vector2(_enemyTex.Width / 2f, _enemyTex.Height);
            sb.Draw(_enemyTex, _enemyPos + new Vector2(0, bob - 15), null, tint * ea,
                0f, origin, 1.3f, SpriteEffects.None, 0f);

            // Enemy HP bar moved to bottom-center (Elden Ring style) in DrawHud
        }

        // Projectiles
        var pOrigin = new Vector2(24, 24);
        foreach (var p in _projectiles)
        {
            float pPulse = 0.9f + 0.2f * MathF.Sin(_totalT * 20f + p.Life * 10f);
            if (p.IsPlayer)
            {
                Color outer, inner;
                float outerScale, innerScale;
                switch (p.MoveName)
                {
                    case "Fireball":
                        outer = new Color(255, 130, 30); inner = new Color(255, 240, 160);
                        outerScale = pPulse * 1.4f; innerScale = pPulse * 0.6f;
                        break;
                    case "Fire Punch":
                        outer = new Color(240, 50, 30); inner = new Color(255, 200, 120);
                        outerScale = pPulse * 1.1f; innerScale = pPulse * 0.4f;
                        break;
                    case "Ember Burst":
                        outer = new Color(255, 180, 60); inner = new Color(255, 240, 180);
                        outerScale = pPulse * 0.7f; innerScale = pPulse * 0.3f;
                        break;
                    default:
                        outer = new Color(255, 140, 40); inner = Color.White;
                        outerScale = pPulse; innerScale = pPulse * 0.4f;
                        break;
                }
                sb.Draw(_glow, p.Pos, null, outer * 0.85f, 0f, pOrigin, outerScale, SpriteEffects.None, 0f);
                sb.Draw(_glow, p.Pos, null, inner * 0.9f, 0f, pOrigin, innerScale, SpriteEffects.None, 0f);
            }
            else
            {
                sb.Draw(_glow, p.Pos, null, new Color(100, 200, 220) * 0.8f, 0f, pOrigin, pPulse * 0.9f, SpriteEffects.None, 0f);
                sb.Draw(_glow, p.Pos, null, new Color(220, 255, 255) * 0.9f, 0f, pOrigin, pPulse * 0.4f, SpriteEffects.None, 0f);
            }
        }

        // Shield visual around player
        if (_shielded)
        {
            sb.Draw(_glow, playerFoot + new Vector2(0, -45), null,
                new Color(255, 210, 70) * (0.2f + 0.1f * MathF.Sin(_totalT * 5f)),
                0f, pOrigin, 3f, SpriteEffects.None, 0f);
        }

        // Melee attack animation (drawn over the player)
        if (_activeMelee >= 0)
        {
            // Drop shadow stays during melee
            sb.Draw(_glow, playerFoot + new Vector2(0, 2), null,
                new Color(0, 0, 0) * 0.35f, 0f, pOrigin,
                new Vector2(2.2f, 0.6f), SpriteEffects.None, 0f);

            var sheet = _meleeAttacks[_activeMelee].Sheet;
            int col = _meleeFrame % MELEE_FRAMES_PER_ROW;
            int row = _meleeFrame / MELEE_FRAMES_PER_ROW;
            var src = new Rectangle(col * MELEE_FRAME_SIZE, row * MELEE_FRAME_SIZE,
                                    MELEE_FRAME_SIZE, MELEE_FRAME_SIZE);
            // Face the direction Selene is moving/facing
            var flip = _facingRight ? SpriteEffects.None : SpriteEffects.FlipHorizontally;
            sb.Draw(sheet, playerFoot, src, Color.White,
                    0f, MeleeOrigin, MELEE_SCALE, flip, 0f);
        }
    }

    // ==================== UI OVERLAY ====================

    public void DrawUI()
    {
        var sb = Globals.SpriteBatch;
        var screen = Globals.GraphicsDevice.Viewport.Bounds;

        // HP/MP panel (bottom-left)
        DrawHud(sb, screen);

        // Move hotbar (bottom-center)
        if (_phase == Phase.Combat)
            DrawHotbar(sb, screen);

        // Command selector when paused
        if (_phase == Phase.Paused && _wheel.Visible)
        {
            // Dim overlay
            sb.Draw(Globals.Pixel, screen, Color.Black * 0.3f);
            _wheel.Draw(new Vector2(screen.Width / 2f - 130f, screen.Height / 2f - 150f));
        }

        // Popups
        foreach (var p in _popups)
        {
            float a = Math.Clamp(1f - p.T / 1.5f, 0f, 1f);
            var pos = p.Pos + new Vector2(0, -p.T * 35f);
            var sz = _font.MeasureString(p.Text);
            sb.DrawString(_font, p.Text, pos + Vector2.One, Color.Black * (a * 0.5f),
                0f, sz / 2f, 1f, SpriteEffects.None, 0f);
            sb.DrawString(_font, p.Text, pos, Color.White * a,
                0f, sz / 2f, 1f, SpriteEffects.None, 0f);
        }

        // Crowd shout (world-space text from spectators)
        if (_crowdText != null)
        {
            _crowdT += Globals.TotalSeconds;
            if (_crowdT > 2f) _crowdText = null;
            else
            {
                float ca = Math.Clamp(1f - _crowdT / 2f, 0f, 1f);
                float rise = _crowdT * 25f;
                string ct = _crowdText;
                var csz = _font.MeasureString(ct);
                // Draw in screen center area so it's always visible
                var cpos = new Vector2(screen.Width / 2f, 80 - rise);
                sb.DrawString(_font, ct, cpos + Vector2.One, Color.Black * (ca * 0.5f),
                    0f, csz / 2f, 1f, SpriteEffects.None, 0f);
                sb.DrawString(_font, ct, cpos, _crowdColor * ca,
                    0f, csz / 2f, 1f, SpriteEffects.None, 0f);
            }
        }

        // Victory text + fade to black
        if (_phase == Phase.Victory)
        {
            float a = Math.Clamp(_phaseT / 0.5f, 0f, 1f);
            string txt = "VICTORY!";
            var sz = _font.MeasureString(txt);
            // Pulsing golden text
            float vPulse = 1f + 0.1f * MathF.Sin(_phaseT * 6f);
            sb.DrawString(_font, txt, new Vector2(screen.Width / 2f, screen.Height / 2f - 60) + new Vector2(2, 2),
                Color.Black * (a * 0.5f), 0f, sz / 2f, 2f * vPulse, SpriteEffects.None, 0f);
            sb.DrawString(_font, txt, new Vector2(screen.Width / 2f, screen.Height / 2f - 60),
                new Color(255, 220, 80) * a, 0f, sz / 2f, 2f * vPulse, SpriteEffects.None, 0f);

            // Fade to black
            if (_victoryFade > 0f)
                sb.Draw(Globals.Pixel, screen, Color.Black * _victoryFade);
        }
    }

    private void DrawHud(SpriteBatch sb, Rectangle screen)
    {
        // ===== PLAYER HP/MP — top-left =====
        int px = 20, py = 20;
        int barW = 200, barH = 14, panelW = barW + 30, panelH = 95;

        sb.Draw(Globals.Pixel, new Rectangle(px, py, panelW, panelH), new Color(10, 10, 22) * 0.88f);
        DrawBorder(sb, px, py, panelW, panelH, new Color(60, 65, 80) * 0.5f);

        int x = px + 15, y = py + 10;

        sb.DrawString(_font, "HP", new Vector2(x, y), new Color(255, 120, 90), 0f, Vector2.Zero, 0.7f, SpriteEffects.None, 0f);
        string hpNum = $"{_playerHP} / {_playerMaxHP}";
        sb.DrawString(_font, hpNum, new Vector2(x + barW - _font.MeasureString(hpNum).X * 0.6f, y),
            Color.White * 0.8f, 0f, Vector2.Zero, 0.6f, SpriteEffects.None, 0f);
        int by = y + 20;
        sb.Draw(Globals.Pixel, new Rectangle(x, by, barW, barH), new Color(35, 15, 15) * 0.9f);
        float hpR = (float)_playerHP / _playerMaxHP;
        for (int bx = 0; bx < (int)(barW * hpR); bx++)
        {
            var c = Color.Lerp(new Color(190, 45, 40), new Color(245, 110, 55), (float)bx / barW);
            sb.Draw(Globals.Pixel, new Rectangle(x + bx, by, 1, barH), c);
        }
        DrawBorder(sb, x, by, barW, barH, new Color(100, 70, 70) * 0.4f);

        y = by + barH + 8;
        sb.DrawString(_font, "MP", new Vector2(x, y), new Color(80, 150, 255), 0f, Vector2.Zero, 0.7f, SpriteEffects.None, 0f);
        string mpNum = $"{_playerMP} / {_playerMaxMP}";
        sb.DrawString(_font, mpNum, new Vector2(x + barW - _font.MeasureString(mpNum).X * 0.6f, y),
            Color.White * 0.8f, 0f, Vector2.Zero, 0.6f, SpriteEffects.None, 0f);
        by = y + 20;
        sb.Draw(Globals.Pixel, new Rectangle(x, by, barW, barH), new Color(15, 15, 35) * 0.9f);
        float mpR = (float)_playerMP / _playerMaxMP;
        for (int bx = 0; bx < (int)(barW * mpR); bx++)
        {
            var c = Color.Lerp(new Color(40, 80, 190), new Color(80, 160, 245), (float)bx / barW);
            sb.Draw(Globals.Pixel, new Rectangle(x + bx, by, 1, barH), c);
        }
        DrawBorder(sb, x, by, barW, barH, new Color(70, 70, 100) * 0.4f);

        // ===== BOSS HP — bottom-center (Elden Ring style) =====
        if (_enemyHP > 0 || _phase == Phase.Victory)
        {
            int bossBarW = 500, bossBarH = 16;
            int bossX = (screen.Width - bossBarW) / 2;
            int bossY = screen.Height - 80;

            // Name above bar (left-aligned)
            sb.DrawString(_font, _enemyName, new Vector2(bossX, bossY - 22),
                Color.White * 0.9f, 0f, Vector2.Zero, 0.75f, SpriteEffects.None, 0f);

            // Bar background
            sb.Draw(Globals.Pixel, new Rectangle(bossX - 2, bossY - 2, bossBarW + 4, bossBarH + 4),
                new Color(10, 10, 20) * 0.9f);

            // Bar fill
            float bossR = (float)_enemyHP / _enemyMaxHP;
            for (int bx = 0; bx < (int)(bossBarW * bossR); bx++)
            {
                float bt = (float)bx / bossBarW;
                var c = bossR > 0.5f
                    ? Color.Lerp(new Color(200, 60, 50), new Color(220, 160, 50), bt)
                    : Color.Lerp(new Color(180, 30, 30), new Color(200, 80, 40), bt);
                sb.Draw(Globals.Pixel, new Rectangle(bossX + bx, bossY, 1, bossBarH), c);
            }
            DrawBorder(sb, bossX, bossY, bossBarW, bossBarH, new Color(120, 100, 80) * 0.5f);

            // HP text right-aligned
            string bossHP = $"{_enemyHP} / {_enemyMaxHP}";
            var bhpSz = _font.MeasureString(bossHP);
            sb.DrawString(_font, bossHP, new Vector2(bossX + bossBarW - bhpSz.X * 0.55f, bossY - 22),
                Color.White * 0.6f, 0f, Vector2.Zero, 0.55f, SpriteEffects.None, 0f);
        }
    }

    private void DrawHotbar(SpriteBatch sb, Rectangle screen)
    {
        // Unified hotbar with I/O/K/L/P keys
        int hy = screen.Height - 40;
        int totalW = _wheel.MoveCount * 70 + 10;
        int hx = (screen.Width - totalW) / 2;

        sb.Draw(Globals.Pixel, new Rectangle(hx - 5, hy - 5, totalW + 10, 35), new Color(10, 10, 22) * 0.8f);

        for (int i = 0; i < _wheel.MoveCount && i < CommandWheel.QuickLabels.Length; i++)
        {
            var move = _wheel.GetMove(i);
            var pos = new Vector2(hx + i * 70 + 5, hy);
            bool onCd = move.IsMelee && _meleeCooldowns.Length > 0 && _meleeCooldowns[0] > 0;
            Color keyCol = onCd ? Color.Gray * 0.5f : new Color(255, 200, 100);
            sb.DrawString(_font, CommandWheel.QuickLabels[i], pos, keyCol, 0f, Vector2.Zero, 0.7f, SpriteEffects.None, 0f);
            string abbr = move.Name.Length > 6 ? move.Name.Substring(0, 6) : move.Name;
            sb.DrawString(_font, abbr, pos + new Vector2(14, 2), Color.White * (onCd ? 0.3f : 0.5f),
                0f, Vector2.Zero, 0.45f, SpriteEffects.None, 0f);
        }

        // Shift hint
        string hint = "[Shift] hold for menu";
        var hsz = _font.MeasureString(hint);
        sb.DrawString(_font, hint, new Vector2(screen.Width / 2f - hsz.X * 0.25f, hy - 22),
            Color.White * 0.35f, 0f, Vector2.Zero, 0.5f, SpriteEffects.None, 0f);
    }

    private static void DrawBorder(SpriteBatch sb, int x, int y, int w, int h, Color c)
    {
        sb.Draw(Globals.Pixel, new Rectangle(x, y, w, 1), c);
        sb.Draw(Globals.Pixel, new Rectangle(x, y + h - 1, w, 1), c);
        sb.Draw(Globals.Pixel, new Rectangle(x, y, 1, h), c);
        sb.Draw(Globals.Pixel, new Rectangle(x + w - 1, y, 1, h), c);
    }

    // ==================== PROCEDURAL TEXTURES ====================

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

    private static Texture2D BuildTrainingSpirit(int size)
    {
        var tex = new Texture2D(Globals.GraphicsDevice, size, size);
        var px = new Color[size * size];
        float cx = (size - 1) / 2f, cy = size * 0.5f, bodyR = size * 0.4f;
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float dx = x - cx, dy = y - cy;
            float dist = MathF.Sqrt(dx * dx + dy * dy);
            if (dist > bodyR * 1.3f) { px[y * size + x] = Color.Transparent; continue; }
            if (dist > bodyR) { px[y * size + x] = new Color(100, 210, 230, (int)((1f - (dist - bodyR) / (bodyR * 0.3f)) * 120)); continue; }
            float t = dist / bodyR;
            int r = (int)(140 + (1f - t) * 80), g = (int)(220 + (1f - t) * 35), b = (int)(230 + (1f - t) * 25);
            float eyeY = cy - bodyR * 0.15f, eyeSpacing = bodyR * 0.28f, eyeR = bodyR * 0.12f;
            float le = MathF.Sqrt((x - (cx - eyeSpacing)) * (x - (cx - eyeSpacing)) + (y - eyeY) * (y - eyeY));
            float re = MathF.Sqrt((x - (cx + eyeSpacing)) * (x - (cx + eyeSpacing)) + (y - eyeY) * (y - eyeY));
            if (le < eyeR || re < eyeR)
            {
                float hx = (Math.Min(le, re) == le ? x - (cx - eyeSpacing) : x - (cx + eyeSpacing)) / eyeR + 0.35f;
                float hy = (y - eyeY) / eyeR + 0.45f;
                px[y * size + x] = hx * hx + hy * hy < 0.25f ? new Color(255, 255, 255, 255) : new Color(25, 35, 50, 255);
                continue;
            }
            float mY = cy + bodyR * 0.2f, mW = bodyR * 0.22f, mn = (x - cx) / mW;
            if (Math.Abs(mn) <= 1f && Math.Abs(y - (mY - mn * mn * bodyR * 0.08f)) < 1.2f)
            { px[y * size + x] = new Color(40, 60, 80, 255); continue; }
            px[y * size + x] = new Color(Math.Clamp(r, 0, 255), Math.Clamp(g, 0, 255), Math.Clamp(b, 0, 255), 255);
        }
        tex.SetData(px);
        return tex;
    }
}
