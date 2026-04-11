namespace SelenesHollow;

public class BattleManager
{
    public enum Phase
    {
        FadeIn,
        PlayerTurn,     // waiting for player to press Space to open wheel
        WheelOpen,      // wheel is up, A/D + Space to pick
        PlayerAttack,   // fire animation
        EnemyTurn,      // brief pause
        EnemyAttack,    // enemy attack animation
        Victory,
        Defeat,
        FadeOut,
        Done,
    }

    // ----- Stats -----
    private int _playerHP = 100, _playerMaxHP = 100;
    private int _playerMP = 40,  _playerMaxMP = 40;
    private bool _shielded;
    private int _enemyHP, _enemyMaxHP;
    private string _enemyName;
    private int _enemyDamage = 12;

    // ----- Positions — characters stand ON the ground -----
    private const int GROUND_Y = 590;                              // where grass starts
    private readonly Vector2 _playerPos = new(260f, GROUND_Y + 35); // feet well into grass
    private readonly Vector2 _enemyPos  = new(980f, GROUND_Y + 15); // spirit hovers just above grass

    // ----- State -----
    private Phase _phase = Phase.FadeIn;
    private float _phaseT;
    private float _totalT;

    // Attack anim
    private CommandWheel.Move _currentAttack;
    private Vector2 _projectilePos;
    private float _attackProgress;
    private int _pendingDamage;

    // Enemy attack
    private Vector2 _enemyProjPos;
    private float _enemyAtkProgress;

    // Hit flash / shake
    private float _enemyFlash, _playerFlash;
    private float _enemyShake, _playerShake;

    // Damage popup
    private string _popupText;
    private Vector2 _popupPos;
    private float _popupT;

    // Wheel open/close animation
    private float _wheelScale;         // 0→1 open, 1→0 close
    private bool _wheelClosing;
    private bool _waitForSpaceRelease; // prevent held-Space from rapid-firing

    // ----- Assets -----
    private readonly CommandWheel _wheel;
    private readonly SpriteFont _font;
    private readonly Texture2D _fireTex;
    private readonly Texture2D _enemyTex;
    private readonly Texture2D _terrain;
    private readonly Texture2D _props;
    private readonly PortraitDialog _dialog;
    private KeyboardState _lastKb;

    // Player sprite
    private readonly Texture2D _playerSheet;
    private int _playerFrame;
    private float _playerFrameTimer;
    private const float IDLE_FRAME_DUR = 0.1125f;
    private static readonly int[] IdleFrames = { 18, 19, 20, 21, 22 };
    private static readonly float[] IdleOriginY = { 203f, 194f, 192f, 193f, 197f };
    private const int FRAME_SIZE = 256;
    private const int FRAMES_PER_ROW = 5;
    private static readonly Vector2 SpriteOrigin = new(128f, 221f);
    private const float PLAYER_SCALE = 0.55f;

    // Background prop rects
    private static readonly Rectangle GrassPlain = new(64, 96, 32, 32);
    private static readonly Rectangle GrassWeedy = new(640, 160, 32, 32);
    private static readonly Rectangle GrassDense = new(576, 224, 32, 32);
    private static readonly Rectangle BigTreeSrc    = new(10, 2, 101, 182);
    private static readonly Vector2   BigTreeAnchor  = new(50f, 182f);
    private static readonly Rectangle BigTree2Src    = new(138, 2, 101, 182);
    private static readonly Vector2   BigTree2Anchor  = new(50f, 182f);
    private static readonly Rectangle SmallTreeSrc   = new(288, 13, 32, 47);
    private static readonly Vector2   SmallTreeAnchor = new(16f, 47f);
    private static readonly Rectangle BushSrc        = new(265, 65, 50, 30);
    private static readonly Vector2   BushAnchor     = new(25f, 30f);

    public bool IsFinished => _phase == Phase.Done;

    public BattleManager(PortraitDialog dialog, bool isTutorial = true)
    {
        _dialog = dialog;
        _wheel = new CommandWheel();
        _font = Globals.Content.Load<SpriteFont>("dialog");
        _playerSheet = Globals.Content.Load<Texture2D>("selene_jump");
        _terrain = Globals.Content.Load<Texture2D>("terrain");
        _props = Globals.Content.Load<Texture2D>("props");
        _fireTex = BuildFireTexture(48);
        _enemyTex = BuildTrainingSpiritTexture(72);

        if (isTutorial)
        {
            _enemyName = "Training Spirit";
            _enemyHP = _enemyMaxHP = 80;
            _enemyDamage = 10;
        }
    }

    public void Update()
    {
        float dt = Globals.TotalSeconds;
        _totalT += dt;
        _phaseT += dt;

        // Player idle anim
        _playerFrameTimer += dt;
        if (_playerFrameTimer >= IDLE_FRAME_DUR)
        {
            _playerFrameTimer -= IDLE_FRAME_DUR;
            _playerFrame = (_playerFrame + 1) % IdleFrames.Length;
        }

        // Popup
        if (_popupText != null) _popupT += dt;
        if (_popupT > 1.5f) _popupText = null;

        // Flash/shake decay
        _enemyFlash  = Math.Max(0f, _enemyFlash  - dt * 5f);
        _playerFlash = Math.Max(0f, _playerFlash - dt * 5f);
        _enemyShake  = Math.Max(0f, _enemyShake  - dt * 4f);
        _playerShake = Math.Max(0f, _playerShake - dt * 4f);

        // Wheel scale animation
        if (_phase == Phase.WheelOpen && !_wheelClosing)
            _wheelScale = Math.Min(1f, _wheelScale + dt * 5f);
        if (_wheelClosing)
        {
            _wheelScale = Math.Max(0f, _wheelScale - dt * 6f);
            if (_wheelScale <= 0f) _wheelClosing = false;
        }

        var kb = Keyboard.GetState();

        // Guard: if Space is held from a previous action, wait for release
        if (_waitForSpaceRelease)
        {
            if (kb.IsKeyUp(Keys.Space)) _waitForSpaceRelease = false;
        }
        bool spacePressed = !_waitForSpaceRelease
                            && kb.IsKeyDown(Keys.Space) && _lastKb.IsKeyUp(Keys.Space);

        switch (_phase)
        {
            case Phase.FadeIn:
                if (_phaseT >= 1.0f)
                {
                    _waitForSpaceRelease = kb.IsKeyDown(Keys.Space);
                    Advance(Phase.PlayerTurn);
                }
                break;

            case Phase.PlayerTurn:
                // Press Space to open command selector
                if (spacePressed)
                {
                    _wheelScale = 0f;
                    _wheelClosing = false;
                    _waitForSpaceRelease = true;
                    _wheel.Visible = true;
                    Advance(Phase.WheelOpen);
                }
                break;

            case Phase.WheelOpen:
                _wheel.Update();
                if (_wheel.SelectedMove != null)
                {
                    _currentAttack = _wheel.SelectedMove.Value;
                    _wheel.Visible = false;
                    _wheel.ResetSelection();
                    _wheelClosing = true;

                    if (_currentAttack.MpRestore > 0)
                    {
                        _shielded = false;
                        _playerMP = Math.Min(_playerMaxMP, _playerMP + _currentAttack.MpRestore);
                        ShowPopup($"+{_currentAttack.MpRestore} MP", _playerPos + new Vector2(0, -130));
                        Advance(Phase.EnemyTurn);
                    }
                    else if (_currentAttack.Name == "Flame Shield")
                    {
                        _shielded = true;
                        _playerMP = Math.Max(0, _playerMP - _currentAttack.ManaCost);
                        ShowPopup("SHIELD UP!", _playerPos + new Vector2(0, -130));
                        Advance(Phase.EnemyTurn);
                    }
                    else if (_playerMP >= _currentAttack.ManaCost)
                    {
                        _shielded = false;
                        _playerMP = Math.Max(0, _playerMP - _currentAttack.ManaCost);
                        _pendingDamage = _currentAttack.Damage;
                        _projectilePos = _playerPos + new Vector2(50f, -60f);
                        _attackProgress = 0f;
                        Advance(Phase.PlayerAttack);
                    }
                    else
                    {
                        ShowPopup("Not enough MP!", _playerPos + new Vector2(0, -130));
                        _wheel.ResetSelection();
                        _wheel.Visible = true;
                        _wheelClosing = false;
                    }
                }
                break;

            case Phase.PlayerAttack:
            {
                _attackProgress += dt * 2.2f;
                Vector2 start = _playerPos + new Vector2(50f, -60f);
                Vector2 end = _enemyPos + new Vector2(-30f, -45f);
                float t = Math.Clamp(_attackProgress, 0f, 1f);
                float arc = -120f * t * (1f - t);
                _projectilePos = Vector2.Lerp(start, end, t) + new Vector2(0f, arc);

                if (_attackProgress >= 1f)
                {
                    _enemyHP = Math.Max(0, _enemyHP - _pendingDamage);
                    _enemyFlash = 1f;
                    _enemyShake = 1f;
                    ShowPopup($"-{_pendingDamage}", _enemyPos + new Vector2(0, -100));
                    Advance(_enemyHP <= 0 ? Phase.Victory : Phase.EnemyTurn);
                }
                break;
            }

            case Phase.EnemyTurn:
                if (_phaseT >= 0.8f)
                {
                    _enemyProjPos = _enemyPos + new Vector2(-30f, -45f);
                    _enemyAtkProgress = 0f;
                    Advance(Phase.EnemyAttack);
                }
                break;

            case Phase.EnemyAttack:
            {
                _enemyAtkProgress += dt * 2.5f;
                Vector2 start = _enemyPos + new Vector2(-30f, -45f);
                Vector2 end = _playerPos + new Vector2(20f, -50f);
                float t = Math.Clamp(_enemyAtkProgress, 0f, 1f);
                float arc = -80f * t * (1f - t);
                _enemyProjPos = Vector2.Lerp(start, end, t) + new Vector2(0f, arc);

                if (_enemyAtkProgress >= 1f)
                {
                    int dmg = _shielded ? _enemyDamage / 3 : _enemyDamage;
                    _playerHP = Math.Max(0, _playerHP - dmg);
                    _playerFlash = 1f;
                    _playerShake = 1f;
                    _shielded = false;
                    ShowPopup($"-{dmg}", _playerPos + new Vector2(0, -130));
                    if (_playerHP <= 0)
                        Advance(Phase.Defeat);
                    else
                    {
                        _waitForSpaceRelease = kb.IsKeyDown(Keys.Space);
                        Advance(Phase.PlayerTurn);
                    }
                }
                break;
            }

            case Phase.Victory:
                if (_phaseT >= 2.5f) Advance(Phase.FadeOut);
                break;
            case Phase.Defeat:
                if (_phaseT >= 2.5f) Advance(Phase.FadeOut);
                break;
            case Phase.FadeOut:
                if (_phaseT >= 1.0f) Advance(Phase.Done);
                break;
        }
        _lastKb = kb;
    }

    private void Advance(Phase next) { _phase = next; _phaseT = 0f; }

    private void ShowPopup(string text, Vector2 pos)
    { _popupText = text; _popupPos = pos; _popupT = 0f; }

    public void DrawWorld() { }

    public void DrawUI()
    {
        var sb = Globals.SpriteBatch;
        var screen = Globals.GraphicsDevice.Viewport.Bounds;

        float alpha = _phase == Phase.FadeIn  ? Math.Clamp(_phaseT / 1.0f, 0f, 1f)
                    : _phase == Phase.FadeOut ? 1f - Math.Clamp(_phaseT / 1.0f, 0f, 1f)
                    : 1f;

        DrawBackground(sb, screen, alpha);
        DrawEnemy(sb, alpha);
        DrawPlayer(sb, alpha);

        if (_phase == Phase.PlayerAttack && _attackProgress < 1f)
            DrawFireProjectile(sb, _projectilePos, alpha);
        if (_phase == Phase.EnemyAttack && _enemyAtkProgress < 1f)
            DrawEnemyProjectile(sb, _enemyProjPos, alpha);

        DrawHud(sb, screen, alpha);

        // "Press [Space]" prompt during PlayerTurn
        if (_phase == Phase.PlayerTurn)
        {
            float pulse = 0.5f + 0.5f * MathF.Sin(_totalT * 3.5f);
            string hint = "[Space] to act";
            var sz = _font.MeasureString(hint);
            var pos = _playerPos + new Vector2(0, -130);
            sb.DrawString(_font, hint, pos + Vector2.One, Color.Black * (0.4f * pulse * alpha),
                0f, new Vector2(sz.X / 2f, sz.Y / 2f), 0.9f, SpriteEffects.None, 0f);
            sb.DrawString(_font, hint, pos, Color.White * (pulse * alpha),
                0f, new Vector2(sz.X / 2f, sz.Y / 2f), 0.9f, SpriteEffects.None, 0f);
        }

        // Command selector (shadcn-style card list, next to Selene)
        if (_wheel.Visible || _wheelScale > 0.01f)
        {
            var selectorPos = new Vector2(_playerPos.X + 80f, _playerPos.Y - 290f);
            _wheel.Draw(selectorPos, _wheelScale);
        }

        DrawPopup(sb, alpha);
        DrawVictoryDefeat(sb, screen, alpha);

        if (alpha < 1f)
            sb.Draw(Globals.Pixel, screen, Color.Black * (1f - alpha));
    }

    // ==================== BACKGROUND ====================

    private void DrawBackground(SpriteBatch sb, Rectangle screen, float alpha)
    {
        // ---- Sky gradient: blue top → light cyan at horizon ----
        for (int y = 0; y < GROUND_Y; y++)
        {
            float t = (float)y / GROUND_Y;
            var c = new Color(
                (int)(55 + t * 110),
                (int)(115 + t * 110),
                (int)(215 + t * 35));
            sb.Draw(Globals.Pixel, new Rectangle(0, y, screen.Width, 1), c * alpha);
        }

        // ---- Ground tiles FIRST (so trees overlap them) ----
        int tileSize = 96;
        var rng = new Random(77);
        for (int y = GROUND_Y; y < screen.Height; y += tileSize)
        for (int x = -tileSize; x < screen.Width + tileSize; x += tileSize)
        {
            int pick = rng.Next(6);
            var src = pick == 0 ? GrassWeedy : pick == 1 ? GrassDense : GrassPlain;
            sb.Draw(_terrain, new Vector2(x, y), src, Color.White * alpha,
                    0f, Vector2.Zero, 3f, SpriteEffects.None, 0f);
        }

        // ---- Far trees: smaller, faded, rooted BELOW ground line ----
        // The anchor is at the tree base, so placing at GROUND_Y+30 means
        // the trunk disappears into the grass while the canopy is above.
        Color farTint = new Color(150, 180, 150) * (0.6f * alpha);
        for (int x = 30; x < screen.Width; x += 100)
            sb.Draw(_props, new Vector2(x, GROUND_Y + 40), SmallTreeSrc, farTint,
                    0f, SmallTreeAnchor, 2.2f, SpriteEffects.None, 0f);

        // ---- Main trees: big, rooted well into the grass ----
        Color treeTint = Color.White * (0.85f * alpha);
        float treeScale = 2.8f;
        int treeFootY = GROUND_Y + 50; // tree feet well into the grass
        int[] treeX = { -20, 130, 310, 480, 650, 820, 1000, 1170 };
        bool flip = false;
        foreach (int tx in treeX)
        {
            var src = flip ? BigTree2Src : BigTreeSrc;
            var anc = flip ? BigTree2Anchor : BigTreeAnchor;
            sb.Draw(_props, new Vector2(tx, treeFootY), src, treeTint,
                    0f, anc, treeScale, SpriteEffects.None, 0f);
            flip = !flip;
        }

        // ---- Foreground bushes: rooted in grass ----
        Color bushTint = Color.White * (0.75f * alpha);
        sb.Draw(_props, new Vector2(50,  GROUND_Y + 35), BushSrc, bushTint, 0f, BushAnchor, 2.5f, SpriteEffects.None, 0f);
        sb.Draw(_props, new Vector2(420, GROUND_Y + 30), BushSrc, bushTint, 0f, BushAnchor, 2.2f, SpriteEffects.None, 0f);
        sb.Draw(_props, new Vector2(700, GROUND_Y + 38), BushSrc, bushTint, 0f, BushAnchor, 2.8f, SpriteEffects.None, 0f);
        sb.Draw(_props, new Vector2(1100,GROUND_Y + 32), BushSrc, bushTint, 0f, BushAnchor, 2.4f, SpriteEffects.None, 0f);
    }

    // ==================== PLAYER ====================

    private void DrawPlayer(SpriteBatch sb, float alpha)
    {
        float shake = _playerFlash > 0 ? MathF.Sin(_totalT * 60f) * 4f * _playerShake : 0f;
        var pos = _playerPos + new Vector2(shake, 0f);
        Color tint = _playerFlash > 0
            ? Color.Lerp(Color.White, Color.Red, _playerFlash * 0.6f) : Color.White;

        // Drop shadow on ground (elliptical, flat)
        sb.Draw(_fireTex, _playerPos + new Vector2(0, 2), null,
            new Color(0, 0, 0) * (0.35f * alpha), 0f,
            new Vector2(24, 24), new Vector2(2.2f, 0.6f), SpriteEffects.None, 0f);

        int gf = IdleFrames[_playerFrame];
        int col = gf % FRAMES_PER_ROW;
        int row = gf / FRAMES_PER_ROW;
        var src = new Rectangle(col * FRAME_SIZE, row * FRAME_SIZE, FRAME_SIZE, FRAME_SIZE);
        var origin = new Vector2(SpriteOrigin.X, IdleOriginY[_playerFrame]);

        sb.Draw(_playerSheet, pos, src, tint * alpha,
                0f, origin, PLAYER_SCALE, SpriteEffects.None, 0f);

        if (_shielded)
        {
            sb.Draw(_fireTex, pos + new Vector2(0, -55), null,
                new Color(255, 210, 70) * (0.25f + 0.15f * MathF.Sin(_totalT * 5f)) * alpha,
                0f, new Vector2(24, 24), 3.5f, SpriteEffects.None, 0f);
        }
    }

    // ==================== ENEMY ====================

    private void DrawEnemy(SpriteBatch sb, float alpha)
    {
        float a = alpha;
        if (_enemyHP <= 0 && _phase == Phase.Victory)
            a *= Math.Max(0f, 1f - _phaseT / 1.5f);

        float shake = _enemyFlash > 0 ? MathF.Sin(_totalT * 60f) * 5f * _enemyShake : 0f;
        var pos = _enemyPos + new Vector2(shake, 0f);
        Color tint = _enemyFlash > 0
            ? Color.Lerp(Color.White, Color.Red, _enemyFlash * 0.8f) : Color.White;
        float bob = 5f * MathF.Sin(_totalT * 2.5f);

        // Drop shadow on ground (elliptical, flat)
        sb.Draw(_fireTex, _enemyPos + new Vector2(0, 2), null,
            new Color(0, 0, 0) * (0.3f * a), 0f,
            new Vector2(24, 24), new Vector2(2.0f, 0.55f), SpriteEffects.None, 0f);

        var origin = new Vector2(_enemyTex.Width / 2f, _enemyTex.Height);
        sb.Draw(_enemyTex, pos + new Vector2(0f, bob - 15f), null, tint * a,
                0f, origin, 1.3f, SpriteEffects.None, 0f);

        // Name + HP bar
        if (_enemyHP > 0 || _phase != Phase.Victory)
        {
            var namePos = pos + new Vector2(0, -115 + bob);
            var nameSize = _font.MeasureString(_enemyName);
            sb.DrawString(_font, _enemyName, namePos + Vector2.One, Color.Black * (0.4f * a),
                0f, new Vector2(nameSize.X / 2f, nameSize.Y), 0.7f, SpriteEffects.None, 0f);
            sb.DrawString(_font, _enemyName, namePos, Color.White * (0.9f * a),
                0f, new Vector2(nameSize.X / 2f, nameSize.Y), 0.7f, SpriteEffects.None, 0f);

            int barW = 100, barH = 10;
            int bx = (int)(namePos.X - barW / 2f);
            int by = (int)(namePos.Y + 6);
            // Bar background
            sb.Draw(Globals.Pixel, new Rectangle(bx - 1, by - 1, barW + 2, barH + 2),
                    new Color(20, 20, 30) * (0.9f * a));
            sb.Draw(Globals.Pixel, new Rectangle(bx, by, barW, barH),
                    new Color(50, 30, 30) * (0.8f * a));
            float hpR = (float)_enemyHP / _enemyMaxHP;
            // Gradient HP bar (green → yellow → red)
            Color hpCol = hpR > 0.5f
                ? Color.Lerp(new Color(255, 200, 50), new Color(80, 200, 80), (hpR - 0.5f) * 2f)
                : Color.Lerp(new Color(200, 50, 50), new Color(255, 200, 50), hpR * 2f);
            sb.Draw(Globals.Pixel, new Rectangle(bx, by, (int)(barW * hpR), barH), hpCol * a);
        }
    }

    // ==================== PROJECTILES ====================

    private void DrawFireProjectile(SpriteBatch sb, Vector2 pos, float alpha)
    {
        var origin = new Vector2(_fireTex.Width / 2f, _fireTex.Height / 2f);
        float pulse = 0.9f + 0.2f * MathF.Sin(_totalT * 20f);
        float t = _attackProgress;

        switch (_currentAttack.Name)
        {
            case "Fireball":
                // Classic fireball — big orange sphere with trailing glow
                float trail = MathF.Max(0f, 1f - t * 1.5f);
                sb.Draw(_fireTex, pos + new Vector2(-15f * (1f - t), 0), null,
                    new Color(255, 80, 20) * (0.5f * alpha * trail),
                    0f, origin, pulse * 2f, SpriteEffects.None, 0f);
                sb.Draw(_fireTex, pos, null,
                    new Color(255, 140, 30) * (0.9f * alpha),
                    0f, origin, pulse * 1.6f, SpriteEffects.None, 0f);
                sb.Draw(_fireTex, pos, null,
                    new Color(255, 240, 160) * (0.95f * alpha),
                    0f, origin, pulse * 0.7f, SpriteEffects.None, 0f);
                break;

            case "Fire Punch":
                // Fast close-range fist — tight red streak
                float stretch = 1.5f + t * 2f;
                sb.Draw(_fireTex, pos, null,
                    new Color(240, 50, 30) * (0.9f * alpha),
                    0.2f, origin, new Vector2(stretch * pulse, pulse * 0.8f),
                    SpriteEffects.None, 0f);
                sb.Draw(_fireTex, pos, null,
                    new Color(255, 200, 120) * (0.8f * alpha),
                    0.2f, origin, new Vector2(stretch * pulse * 0.4f, pulse * 0.4f),
                    SpriteEffects.None, 0f);
                break;

            case "Ember Burst":
                // Scatter of small sparks fanning out
                var rng = new Random(42);
                for (int i = 0; i < 8; i++)
                {
                    float angle = (i / 8f) * MathF.Tau + t * 2f;
                    float spread = 15f + t * 30f;
                    var sparkPos = pos + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * spread;
                    float sparkPulse = 0.7f + 0.3f * MathF.Sin(_totalT * 25f + i);
                    sb.Draw(_fireTex, sparkPos, null,
                        new Color(255, 180, 60) * (0.7f * alpha * sparkPulse),
                        0f, origin, sparkPulse * 0.5f, SpriteEffects.None, 0f);
                }
                // Center glow
                sb.Draw(_fireTex, pos, null,
                    new Color(255, 200, 100) * (0.6f * alpha),
                    0f, origin, pulse * 0.8f, SpriteEffects.None, 0f);
                break;

            default:
                // Fallback
                sb.Draw(_fireTex, pos, null,
                    new Color(255, 120, 30) * (0.8f * alpha),
                    0f, origin, pulse * 1.5f, SpriteEffects.None, 0f);
                sb.Draw(_fireTex, pos, null,
                    new Color(255, 240, 180) * (0.9f * alpha),
                    0f, origin, pulse * 0.7f, SpriteEffects.None, 0f);
                break;
        }
    }

    private void DrawEnemyProjectile(SpriteBatch sb, Vector2 pos, float alpha)
    {
        var origin = new Vector2(_fireTex.Width / 2f, _fireTex.Height / 2f);
        float pulse = 0.9f + 0.2f * MathF.Sin(_totalT * 20f);
        sb.Draw(_fireTex, pos, null, new Color(100, 200, 220) * (0.8f * alpha),
                0f, origin, pulse * 1.2f, SpriteEffects.None, 0f);
        sb.Draw(_fireTex, pos, null, new Color(220, 255, 255) * (0.9f * alpha),
                0f, origin, pulse * 0.5f, SpriteEffects.None, 0f);
    }

    // ==================== HUD (M&L RPG STYLE) ====================

    private void DrawHud(SpriteBatch sb, Rectangle screen, float alpha)
    {
        int panelX = 20, panelY = screen.Height - 115;
        int barW = 200, barH = 14;
        int panelW = barW + 30;
        int panelH = 95;

        // Panel backdrop
        sb.Draw(Globals.Pixel, new Rectangle(panelX, panelY, panelW, panelH),
                new Color(10, 10, 22) * (0.9f * alpha));
        DrawBorder(sb, panelX, panelY, panelW, panelH, new Color(60, 65, 80) * (0.6f * alpha));

        int x = panelX + 15;
        int y = panelY + 10;

        // --- HP label + number on one line, bar on the next ---
        sb.DrawString(_font, "HP", new Vector2(x, y),
            new Color(255, 120, 90) * alpha, 0f, Vector2.Zero, 0.7f, SpriteEffects.None, 0f);
        string hpNum = $"{_playerHP} / {_playerMaxHP}";
        var hpNumSize = _font.MeasureString(hpNum);
        sb.DrawString(_font, hpNum, new Vector2(x + barW - hpNumSize.X * 0.6f, y),
            Color.White * (0.8f * alpha), 0f, Vector2.Zero, 0.6f, SpriteEffects.None, 0f);

        int barY = y + 20;
        sb.Draw(Globals.Pixel, new Rectangle(x, barY, barW, barH),
                new Color(35, 15, 15) * (0.9f * alpha));
        float hpR = (float)_playerHP / _playerMaxHP;
        int hpFill = (int)(barW * hpR);
        for (int bx = 0; bx < hpFill; bx++)
        {
            float bt = (float)bx / barW;
            var c = Color.Lerp(new Color(190, 45, 40), new Color(245, 110, 55), bt);
            sb.Draw(Globals.Pixel, new Rectangle(x + bx, barY, 1, barH), c * alpha);
        }
        if (hpFill > 2)
            sb.Draw(Globals.Pixel, new Rectangle(x, barY, hpFill, 3), Color.White * (0.2f * alpha));
        DrawBorder(sb, x, barY, barW, barH, new Color(100, 70, 70) * (0.4f * alpha));

        // --- MP label + number, then bar ---
        y = barY + barH + 8;
        sb.DrawString(_font, "MP", new Vector2(x, y),
            new Color(80, 150, 255) * alpha, 0f, Vector2.Zero, 0.7f, SpriteEffects.None, 0f);
        string mpNum = $"{_playerMP} / {_playerMaxMP}";
        var mpNumSize = _font.MeasureString(mpNum);
        sb.DrawString(_font, mpNum, new Vector2(x + barW - mpNumSize.X * 0.6f, y),
            Color.White * (0.8f * alpha), 0f, Vector2.Zero, 0.6f, SpriteEffects.None, 0f);

        barY = y + 20;
        sb.Draw(Globals.Pixel, new Rectangle(x, barY, barW, barH),
                new Color(15, 15, 35) * (0.9f * alpha));
        float mpR = (float)_playerMP / _playerMaxMP;
        int mpFill = (int)(barW * mpR);
        for (int bx = 0; bx < mpFill; bx++)
        {
            float bt = (float)bx / barW;
            var c = Color.Lerp(new Color(40, 80, 190), new Color(80, 160, 245), bt);
            sb.Draw(Globals.Pixel, new Rectangle(x + bx, barY, 1, barH), c * alpha);
        }
        if (mpFill > 2)
            sb.Draw(Globals.Pixel, new Rectangle(x, barY, mpFill, 3), Color.White * (0.15f * alpha));
        DrawBorder(sb, x, barY, barW, barH, new Color(70, 70, 100) * (0.4f * alpha));
    }

    private static void DrawBorder(SpriteBatch sb, int x, int y, int w, int h, Color c)
    {
        sb.Draw(Globals.Pixel, new Rectangle(x, y, w, 2), c);
        sb.Draw(Globals.Pixel, new Rectangle(x, y + h - 2, w, 2), c);
        sb.Draw(Globals.Pixel, new Rectangle(x, y, 2, h), c);
        sb.Draw(Globals.Pixel, new Rectangle(x + w - 2, y, 2, h), c);
    }

    // ==================== POPUP / VICTORY ====================

    private void DrawPopup(SpriteBatch sb, float alpha)
    {
        if (_popupText == null) return;
        float popA = Math.Clamp(1f - _popupT / 1.5f, 0f, 1f);
        float rise = _popupT * 40f;
        var pos = _popupPos + new Vector2(0, -rise);
        var sz = _font.MeasureString(_popupText);
        sb.DrawString(_font, _popupText, pos + Vector2.One,
            Color.Black * (popA * 0.6f * alpha), 0f,
            new Vector2(sz.X / 2f, sz.Y / 2f), 1.1f, SpriteEffects.None, 0f);
        sb.DrawString(_font, _popupText, pos,
            Color.White * (popA * alpha), 0f,
            new Vector2(sz.X / 2f, sz.Y / 2f), 1.1f, SpriteEffects.None, 0f);
    }

    private void DrawVictoryDefeat(SpriteBatch sb, Rectangle screen, float alpha)
    {
        if (_phase == Phase.Victory)
        {
            float va = Math.Clamp(_phaseT / 0.5f, 0f, 1f) * alpha;
            string txt = "VICTORY!";
            var sz = _font.MeasureString(txt);
            var pos = new Vector2(screen.Width / 2f, screen.Height / 2f - 60f);
            sb.DrawString(_font, txt, pos + new Vector2(2, 2), Color.Black * (va * 0.5f),
                0f, sz / 2f, 2f, SpriteEffects.None, 0f);
            sb.DrawString(_font, txt, pos, new Color(255, 220, 80) * va,
                0f, sz / 2f, 2f, SpriteEffects.None, 0f);
        }
        if (_phase == Phase.Defeat)
        {
            float va = Math.Clamp(_phaseT / 0.5f, 0f, 1f) * alpha;
            string txt = "DEFEATED...";
            var sz = _font.MeasureString(txt);
            var pos = new Vector2(screen.Width / 2f, screen.Height / 2f - 60f);
            sb.DrawString(_font, txt, pos, new Color(200, 60, 60) * va,
                0f, sz / 2f, 2f, SpriteEffects.None, 0f);
        }
    }

    // ==================== PROCEDURAL TEXTURES ====================

    private static Texture2D BuildFireTexture(int size)
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

    private static Texture2D BuildTrainingSpiritTexture(int size)
    {
        var tex = new Texture2D(Globals.GraphicsDevice, size, size);
        var px = new Color[size * size];
        float cx = (size - 1) / 2f;
        float cy = size * 0.5f;
        float bodyR = size * 0.4f;

        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float dx = x - cx, dy = y - cy;
            float dist = MathF.Sqrt(dx * dx + dy * dy);

            if (dist > bodyR * 1.3f) { px[y * size + x] = Color.Transparent; continue; }
            if (dist > bodyR)
            {
                float glow = 1f - (dist - bodyR) / (bodyR * 0.3f);
                px[y * size + x] = new Color(100, 210, 230, (int)(glow * 120));
                continue;
            }

            float t = dist / bodyR;
            int r = (int)(140 + (1f - t) * 80);
            int g = (int)(220 + (1f - t) * 35);
            int b = (int)(230 + (1f - t) * 25);

            float eyeY = cy - bodyR * 0.15f;
            float eyeSpacing = bodyR * 0.28f;
            float eyeR = bodyR * 0.12f;
            float leDist = MathF.Sqrt((x - (cx - eyeSpacing)) * (x - (cx - eyeSpacing)) + (y - eyeY) * (y - eyeY));
            float reDist = MathF.Sqrt((x - (cx + eyeSpacing)) * (x - (cx + eyeSpacing)) + (y - eyeY) * (y - eyeY));
            if (leDist < eyeR || reDist < eyeR)
            {
                float ed = Math.Min(leDist, reDist);
                float hx = (ed == leDist ? x - (cx - eyeSpacing) : x - (cx + eyeSpacing)) / eyeR + 0.35f;
                float hy = (y - eyeY) / eyeR + 0.45f;
                px[y * size + x] = hx * hx + hy * hy < 0.25f
                    ? new Color(255, 255, 255, 255)
                    : new Color(25, 35, 50, 255);
                continue;
            }

            float mouthY = cy + bodyR * 0.2f;
            float mouthW = bodyR * 0.22f;
            float mxNorm = (x - cx) / mouthW;
            if (Math.Abs(mxNorm) <= 1f)
            {
                float curveY = mouthY - mxNorm * mxNorm * bodyR * 0.08f;
                if (Math.Abs(y - curveY) < 1.2f)
                { px[y * size + x] = new Color(40, 60, 80, 255); continue; }
            }

            px[y * size + x] = new Color(Math.Clamp(r, 0, 255), Math.Clamp(g, 0, 255), Math.Clamp(b, 0, 255), 255);
        }
        tex.SetData(px);
        return tex;
    }
}
