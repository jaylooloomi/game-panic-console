using System;
using System.Collections.Generic;
using PanicConsole.Core;
using PanicConsole.Minigames;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace PanicConsole.App
{
    /// <summary>
    /// Phase 0+ 單人主玩法：每 20 秒(可調)強制切換、同時運行多個小遊戲、共享 HP、卡牌。
    /// 每局從「遊戲池」隨機選 GameCount 個小遊戲，增加變化。渲染為極簡色塊。
    /// </summary>
    public class SliceGame : MonoBehaviour
    {
        static readonly Color DinoColor = new Color(1f, 0.55f, 0.15f);
        static readonly Color SnakeColor = new Color(0.3f, 0.9f, 0.4f);
        static readonly Color PianoColor = new Color(0.3f, 0.8f, 1f);
        static readonly Color TetrisColor = new Color(0.75f, 0.45f, 1f);
        static readonly Color ClickerColor = new Color(1f, 0.85f, 0.3f);

        struct GameDef { public string Id, Name; public Func<MinigameBase> Make; public Color Color; }

        static readonly GameDef[] Pool =
        {
            new GameDef { Id = "dino",    Name = "恐龍 Dino",    Make = () => new DinoSim(),   Color = DinoColor },
            new GameDef { Id = "snake",   Name = "貪食蛇 Snake", Make = () => new SnakeSim(),  Color = SnakeColor },
            new GameDef { Id = "piano",   Name = "鋼琴 Piano",   Make = () => new PianoSim(),  Color = PianoColor },
            new GameDef { Id = "tetris",  Name = "方塊 Tetris",  Make = () => new TetrisSim(), Color = TetrisColor },
            new GameDef { Id = "clicker", Name = "連點 Clicker", Make = () => new ClickerSim(),Color = ClickerColor },
        };

        const int GameCount = 4;

        SwitchEngine _engine;
        IMinigame[] _active = new IMinigame[GameCount];
        GameDef[] _activeDef = new GameDef[GameCount];

        RectTransform[] _panels = new RectTransform[GameCount];
        Outline[] _panelOutlines = new Outline[GameCount];
        Text[] _panelLabels = new Text[GameCount];
        readonly List<Image>[] _pools = new List<Image>[GameCount];

        Text _hud, _banner;
        int _lastHp;

        float _switchScale = 0.5f;
        readonly float[] _panelFlash = new float[GameCount];
        float _shakeTime;
        float _bestSurvival;

        CardEngine _cards;
        int _lastScore;
        float _slowTime;

        readonly System.Random _rng = new System.Random();

        void Awake()
        {
            _bestSurvival = PlayerPrefs.GetFloat("best_survival", 0f);
            BuildUi();
            BuildEngine();
            Debug.Log("[Slice] started (pool of " + Pool.Length + ", pick " + GameCount + ")");
        }

        // ---------- UI ----------
        void BuildUi()
        {
            var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);

            UiFactory.StretchPanel(canvas.transform, "bg", new Color(0.05f, 0.05f, 0.08f), Vector2.zero, Vector2.one);

            for (int i = 0; i < GameCount; i++)
            {
                float x0 = i / (float)GameCount, x1 = (i + 1) / (float)GameCount;
                var panel = UiFactory.StretchPanel(canvas.transform, "panel" + i, new Color(1, 1, 1, 0.08f),
                    new Vector2(x0 + 0.008f, 0.12f), new Vector2(x1 - 0.008f, 0.84f));
                _panels[i] = panel;
                var ol = panel.gameObject.AddComponent<Outline>();
                ol.effectDistance = new Vector2(3f, -3f);
                _panelOutlines[i] = ol;
                _pools[i] = new List<Image>();
                _panelLabels[i] = UiFactory.Label(panel, "label", 18, TextAnchor.UpperCenter,
                    new Vector2(0f, 0.9f), new Vector2(1f, 1f));
            }

            _hud = UiFactory.Label(canvas.transform, "hud", 21, TextAnchor.UpperCenter,
                new Vector2(0f, 0.85f), new Vector2(1f, 1f));
            _banner = UiFactory.Label(canvas.transform, "banner", 40, TextAnchor.MiddleCenter,
                new Vector2(0f, 0.4f), new Vector2(1f, 0.6f));
            _banner.text = "";

            UiFactory.Label(canvas.transform, "controls", 16, TextAnchor.MiddleCenter,
                new Vector2(0f, 0.01f), new Vector2(1f, 0.1f)).text =
                "恐龍/連點:Space  蛇/方塊:方向鍵  鋼琴:A/S/D/F  方塊旋轉:↑ | Z/X/C:出牌 R:重開 [/]:快慢 2:下樓梯對戰 3:爆爆王對戰";
        }

        static Color PanelBg(Color c, bool focused)
        {
            // 深色「螢幕」底：聚焦時帶遊戲色微光、背景時近黑，讓圓角磚塊更跳
            if (focused) return new Color(c.r * 0.20f + 0.05f, c.g * 0.20f + 0.05f, c.b * 0.20f + 0.05f, 0.97f);
            return new Color(0.07f, 0.07f, 0.10f, 0.97f);
        }

        // ---------- 建立 / 重置 ----------
        void BuildEngine()
        {
            // 從池中隨機選 GameCount 個不重複
            var idxs = new List<int>();
            for (int i = 0; i < Pool.Length; i++) idxs.Add(i);
            Shuffle(idxs);
            var games = new List<IMinigame>();
            for (int i = 0; i < GameCount; i++)
            {
                _activeDef[i] = Pool[idxs[i]];
                _active[i] = _activeDef[i].Make();
                games.Add(_active[i]);
            }

            var timer = new SwitchTimer(3f, _switchScale);
            var state = new MatchState();
            _engine = new SwitchEngine(games, timer, state)
            {
                OpeningInvincibility = 2.5f,
                PostFailInvincibility = 1.5f,
                StartFocusIndex = _rng.Next(GameCount),
            };

            timer.OnSwitch += () =>
                Debug.Log($"[Slice] switch -> #{_engine.FocusIndex} {_engine.Focused.GameId} (round {timer.Round})");
            timer.OnWarning += () => _shakeTime = 0.35f;
            for (int i = 0; i < games.Count; i++)
            {
                int idx = i;
                games[i].OnFail += _ => _panelFlash[idx] = 0.45f;
            }
            state.OnGameOver += () =>
            {
                Debug.Log("[Slice] GAME OVER");
                if (state.SurvivalTime > _bestSurvival)
                {
                    _bestSurvival = state.SurvivalTime;
                    PlayerPrefs.SetFloat("best_survival", _bestSurvival);
                    PlayerPrefs.Save();
                }
            };

            _engine.Start();
            _lastHp = state.Hp;
            _banner.text = "";
            for (int i = 0; i < _panelFlash.Length; i++) _panelFlash[i] = 0f;
            _shakeTime = 0f;

            _cards = new CardEngine();
            _cards.Reset();
            _lastScore = 0;
            _slowTime = 0f;
        }

        void Shuffle(List<int> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = _rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        void Restart() { Debug.Log("[Slice] restart"); BuildEngine(); }

        // ---------- 主迴圈 ----------
        void Update()
        {
            if (Input.GetKeyDown(KeyCode.Alpha2)) { SceneManager.LoadScene("Versus"); return; }
            if (Input.GetKeyDown(KeyCode.Alpha3)) { SceneManager.LoadScene("Bomber"); return; }

            float dt = Time.deltaTime;
            for (int i = 0; i < GameCount; i++)
                if (_panelFlash[i] > 0f) _panelFlash[i] = Mathf.Max(0f, _panelFlash[i] - dt);
            if (_shakeTime > 0f) _shakeTime = Mathf.Max(0f, _shakeTime - dt);

            if (Input.GetKeyDown(KeyCode.LeftBracket)) { _switchScale = Mathf.Max(0.2f, _switchScale - 0.1f); Restart(); return; }
            if (Input.GetKeyDown(KeyCode.RightBracket)) { _switchScale = Mathf.Min(1.5f, _switchScale + 0.1f); Restart(); return; }

            if (_engine.State.IsGameOver)
            {
                _banner.color = new Color(1f, 0.3f, 0.4f);
                _banner.text = $"GAME OVER\n你撐了 {_engine.State.SurvivalTime:0.0} 秒　最佳 {_bestSurvival:0.0} 秒\n按 R 再來一局";
                if (Input.GetKeyDown(KeyCode.R)) Restart();
                RenderAll();
                return;
            }

            if (Input.GetKeyDown(KeyCode.R)) { Restart(); return; }

            if (Input.GetKeyDown(KeyCode.Z)) PlayCard(0);
            if (Input.GetKeyDown(KeyCode.X)) PlayCard(1);
            if (Input.GetKeyDown(KeyCode.C)) PlayCard(2);

            float simDt = dt;
            if (_slowTime > 0f) { _slowTime = Mathf.Max(0f, _slowTime - dt); simDt = dt * 0.4f; }

            RouteInput();
            _engine.Tick(simDt);

            int score = _engine.State.Score;
            if (score > _lastScore) { _cards.AddEnergy((score - _lastScore) * 20f); _lastScore = score; }

            if (_engine.State.Hp != _lastHp) { _lastHp = _engine.State.Hp; Debug.Log($"[Slice] HP={_lastHp}"); }

            _banner.text = ""; // 中央大字只留給 Game Over；無敵狀態顯示在 HUD，避免擋住遊戲

            RenderAll();
            UpdateHud();
        }

        void RouteInput()
        {
            switch (_engine.Focused)
            {
                case DinoSim dino:
                    if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.UpArrow)) dino.Jump();
                    break;
                case SnakeSim snake:
                    if (Input.GetKeyDown(KeyCode.UpArrow)) snake.SetDirection(0, 1);
                    else if (Input.GetKeyDown(KeyCode.DownArrow)) snake.SetDirection(0, -1);
                    else if (Input.GetKeyDown(KeyCode.LeftArrow)) snake.SetDirection(-1, 0);
                    else if (Input.GetKeyDown(KeyCode.RightArrow)) snake.SetDirection(1, 0);
                    break;
                case PianoSim piano:
                    if (Input.GetKeyDown(KeyCode.A)) piano.Hit(0);
                    else if (Input.GetKeyDown(KeyCode.S)) piano.Hit(1);
                    else if (Input.GetKeyDown(KeyCode.D)) piano.Hit(2);
                    else if (Input.GetKeyDown(KeyCode.F)) piano.Hit(3);
                    break;
                case TetrisSim tetris:
                    if (Input.GetKeyDown(KeyCode.LeftArrow)) tetris.MoveLeft();
                    else if (Input.GetKeyDown(KeyCode.RightArrow)) tetris.MoveRight();
                    else if (Input.GetKeyDown(KeyCode.UpArrow)) tetris.Rotate();
                    else if (Input.GetKeyDown(KeyCode.DownArrow)) tetris.SoftDrop();
                    break;
                case ClickerSim clicker:
                    if (Input.GetKeyDown(KeyCode.Space)) clicker.Click();
                    break;
            }
        }

        void UpdateHud()
        {
            var s = _engine.State;
            var t = _engine.Timer;
            string warn = t.WarningActive ? "   ⚠ 即將切換！" : "";
            string grace = (!s.IsInvincible && _engine.Focused.InFocusGrace) ? "   準備!" : "";
            string frozen = t.IsFrozen ? "  ❄凍結" : "";
            string inv = s.IsInvincible ? $"  🛡無敵{s.InvincibleRemaining:0.0}s" : "";
            float baseInterval = SwitchTimer.IntervalForRound(t.Round) * t.IntervalScale;
            _hud.text =
                $"HP {s.Hp}/{s.MaxHp}    分數 {s.Score}    生存 {s.SurvivalTime:0.0}s    {t.Remaining:0.0}s 後切換（每輪約 {baseInterval:0.0}s）{warn}{frozen}{inv}\n" +
                $"▶ 現在操作：{ActiveName()}  —  {ActiveHint()}{grace}\n" +
                $"⚡能量 {_cards.Energy:0}%   手牌：{HandText()}   (Z / X / C 出牌)";
            _hud.color = t.WarningActive ? new Color(1f, 0.4f, 0.4f) : Color.white;
        }

        string ActiveName() => _activeDef[_engine.FocusIndex].Name;

        string ActiveHint()
        {
            switch (_engine.Focused.GameId)
            {
                case "dino": return "Space / ↑ 跳";
                case "snake": return "方向鍵 移動";
                case "piano": return "A / S / D / F 點擊";
                case "tetris": return "←→ 移動  ↑ 旋轉  ↓ 下移";
                case "clicker": return "狂點 Space";
                default: return "";
            }
        }

        // ---------- 卡牌 ----------
        void PlayCard(int slot)
        {
            var c = _cards.Play(slot);
            if (!c.HasValue) return;
            switch (c.Value)
            {
                case CardType.Shield: _engine.State.GrantInvincibility(3f); break;
                case CardType.Heal: _engine.State.Heal(1); break;
                case CardType.Freeze: _engine.Timer.Freeze(3f); break;
                case CardType.SlowMo: _slowTime = 3f; break;
            }
            Debug.Log($"[Slice] play card {c.Value}");
        }

        static string CardName(CardType c)
        {
            switch (c)
            {
                case CardType.Shield: return "護盾";
                case CardType.Heal: return "補血";
                case CardType.Freeze: return "凍結";
                case CardType.SlowMo: return "慢動作";
                default: return c.ToString();
            }
        }

        string HandText()
        {
            if (_cards.Hand.Count == 0) return "（無）";
            var parts = new string[_cards.Hand.Count];
            for (int i = 0; i < _cards.Hand.Count; i++) parts[i] = $"[{CardName(_cards.Hand[i])}]";
            return string.Join(" ", parts);
        }

        // ---------- 渲染 ----------
        void RenderAll()
        {
            for (int i = 0; i < GameCount; i++)
            {
                bool focused = _engine.FocusIndex == i;
                Color bg = PanelBg(_activeDef[i].Color, focused);
                if (_panelFlash[i] > 0f) bg = Color.Lerp(bg, new Color(0.9f, 0.1f, 0.1f, 0.65f), _panelFlash[i] / 0.45f);
                _panels[i].GetComponent<Image>().color = bg;

                // 螢幕邊框：聚焦時亮、背景時暗（控制台監視器感）
                var gc = _activeDef[i].Color;
                _panelOutlines[i].effectColor = focused
                    ? new Color(gc.r, gc.g, gc.b, 0.95f)
                    : new Color(gc.r, gc.g, gc.b, 0.3f);

                Vector2 off = Vector2.zero;
                if (focused && _shakeTime > 0f) off = new Vector2(UnityEngine.Random.Range(-7f, 7f), UnityEngine.Random.Range(-7f, 7f));
                _panels[i].anchoredPosition = off;

                bool f = focused;
                switch (_active[i])
                {
                    case DinoSim d: RenderDino(d, i, f); break;
                    case SnakeSim s: RenderSnake(s, i, f); break;
                    case PianoSim p: RenderPiano(p, i, f); break;
                    case TetrisSim t: RenderTetris(t, i, f); break;
                    case ClickerSim c: RenderClicker(c, i, f); break;
                }
                SetPanelLabel(i, _activeDef[i].Name);
            }
        }

        void SetPanelLabel(int i, string name)
        {
            bool focused = _engine.FocusIndex == i;
            string sub = focused ? "← 你正在操作" : "（背景）";
            if (_panelFlash[i] > 0f) sub = "失誤！ -1 HP";
            _panelLabels[i].text = (focused ? "▶ " : "") + name + "\n" + sub;
            _panelLabels[i].color = _panelFlash[i] > 0f
                ? new Color(1f, 0.55f, 0.55f)
                : (focused ? Color.white : new Color(1f, 1f, 1f, 0.4f));
        }

        Image Pooled(int panel, int index, Color color)
        {
            var pool = _pools[panel];
            while (pool.Count <= index) pool.Add(UiFactory.Block(_panels[panel], color));
            var img = pool[index];
            img.color = color; img.enabled = true;
            return img;
        }

        void HideFrom(int panel, int index)
        {
            var pool = _pools[panel];
            for (int i = index; i < pool.Count; i++) pool[i].enabled = false;
        }

        void RenderDino(DinoSim dino, int panel, bool focused)
        {
            var rect = _panels[panel].rect;
            float w = rect.width, h = rect.height;
            float groundY = h * 0.18f;
            Color c = focused ? DinoColor : DinoColor * 0.6f;

            var ground = Pooled(panel, 0, new Color(0.6f, 0.6f, 0.6f));
            ground.rectTransform.sizeDelta = new Vector2(w, 3f);
            ground.rectTransform.anchoredPosition = new Vector2(0, groundY);

            float playerSize = Mathf.Min(w, h) * 0.12f;
            var player = Pooled(panel, 1, c);
            player.rectTransform.sizeDelta = new Vector2(playerSize, playerSize);
            player.rectTransform.anchoredPosition = new Vector2(w * 0.14f, groundY + (dino.IsAirborne ? h * 0.22f : 0f));

            float ox = Mathf.Clamp01(dino.ObstacleX / dino.SpawnDistance) * w;
            var obs = Pooled(panel, 2, new Color(0.9f, 0.2f, 0.3f));
            obs.rectTransform.sizeDelta = new Vector2(playerSize * 0.8f, playerSize * 1.1f);
            obs.rectTransform.anchoredPosition = new Vector2(ox, groundY);

            HideFrom(panel, 3);
        }

        void RenderSnake(SnakeSim snake, int panel, bool focused)
        {
            var rect = _panels[panel].rect;
            float cw = rect.width / snake.Width, ch = rect.height / snake.Height;
            Color c = focused ? SnakeColor : SnakeColor * 0.6f;
            var size = new Vector2(Mathf.Max(1, cw - 1f), Mathf.Max(1, ch - 1f));
            int idx = 0;
            var body = snake.Body;
            for (int i = 0; i < body.Count; i++)
            {
                var img = Pooled(panel, idx++, i == 0 ? Color.white : c);
                img.rectTransform.sizeDelta = size;
                img.rectTransform.anchoredPosition = new Vector2(body[i].X * cw, body[i].Y * ch);
            }
            var food = Pooled(panel, idx++, new Color(1f, 0.85f, 0.2f));
            food.rectTransform.sizeDelta = size;
            food.rectTransform.anchoredPosition = new Vector2(snake.Food.X * cw, snake.Food.Y * ch);
            HideFrom(panel, idx);
        }

        void RenderPiano(PianoSim piano, int panel, bool focused)
        {
            var rect = _panels[panel].rect;
            float w = rect.width, h = rect.height;
            float colW = w / piano.Columns;
            Color c = focused ? PianoColor : PianoColor * 0.6f;
            int idx = 0;
            var line = Pooled(panel, idx++, new Color(1f, 1f, 1f, 0.6f));
            line.rectTransform.sizeDelta = new Vector2(w, 3f);
            line.rectTransform.anchoredPosition = new Vector2(0, h * 0.05f);
            var tiles = piano.Tiles;
            for (int i = 0; i < tiles.Count; i++)
            {
                var img = Pooled(panel, idx++, c);
                img.rectTransform.sizeDelta = new Vector2(Mathf.Max(1, colW - 3f), h * 0.06f);
                img.rectTransform.anchoredPosition = new Vector2(tiles[i].Column * colW, Mathf.Clamp01(tiles[i].Y / piano.Height) * h);
            }
            HideFrom(panel, idx);
        }

        void RenderTetris(TetrisSim tetris, int panel, bool focused)
        {
            var rect = _panels[panel].rect;
            float cw = rect.width / tetris.Width, ch = rect.height / tetris.Height;
            Color c = focused ? TetrisColor : TetrisColor * 0.6f;
            var locked = new Color(c.r * 0.7f, c.g * 0.7f, c.b * 0.7f);
            var size = new Vector2(Mathf.Max(1, cw - 1f), Mathf.Max(1, ch - 1f));
            int idx = 0;
            var grid = tetris.Grid;
            if (grid != null)
                for (int x = 0; x < tetris.Width; x++)
                    for (int y = 0; y < tetris.Height; y++)
                        if (grid[x, y])
                        {
                            var img = Pooled(panel, idx++, locked);
                            img.rectTransform.sizeDelta = size;
                            img.rectTransform.anchoredPosition = new Vector2(x * cw, y * ch);
                        }
            if (tetris.HasPiece)
                foreach (var cell in tetris.Piece)
                {
                    var img = Pooled(panel, idx++, Color.white);
                    img.rectTransform.sizeDelta = size;
                    img.rectTransform.anchoredPosition = new Vector2((tetris.PieceX + cell.X) * cw, (tetris.PieceY + cell.Y) * ch);
                }
            HideFrom(panel, idx);
        }

        void RenderClicker(ClickerSim clicker, int panel, bool focused)
        {
            var rect = _panels[panel].rect;
            float w = rect.width, h = rect.height;
            Color c = focused ? ClickerColor : ClickerColor * 0.6f;
            // 背景條
            var bgBar = Pooled(panel, 0, new Color(1f, 1f, 1f, 0.12f));
            bgBar.rectTransform.sizeDelta = new Vector2(w * 0.4f, h * 0.7f);
            bgBar.rectTransform.anchoredPosition = new Vector2(w * 0.3f, h * 0.15f);
            // 壓力填充
            float fillH = h * 0.7f * Mathf.Clamp01(clicker.Ratio);
            var fill = Pooled(panel, 1, clicker.Ratio < 0.3f ? new Color(0.95f, 0.3f, 0.3f) : c);
            fill.rectTransform.sizeDelta = new Vector2(w * 0.4f, Mathf.Max(1f, fillH));
            fill.rectTransform.anchoredPosition = new Vector2(w * 0.3f, h * 0.15f);
            HideFrom(panel, 2);
        }
    }
}
