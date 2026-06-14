using System.Collections.Generic;
using PanicConsole.Core;
using PanicConsole.Minigames;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace PanicConsole.App
{
    /// <summary>
    /// Phase 0 垂直切片的可玩入口：程式化建立 uGUI 畫面、持有 SwitchEngine、
    /// 每幀 Tick、把輸入送給前台、渲染三個小遊戲與 HUD。
    /// 渲染刻意極簡（色塊），目的只為驗證「20 秒切換多工」是否好玩。
    /// </summary>
    public class SliceGame : MonoBehaviour
    {
        // 焦點面板的顏色（飽和）；背景面板降低 alpha
        static readonly Color DinoColor = new Color(1f, 0.55f, 0.15f);
        static readonly Color SnakeColor = new Color(0.3f, 0.9f, 0.4f);
        static readonly Color PianoColor = new Color(0.3f, 0.8f, 1f);
        static readonly Color TetrisColor = new Color(0.75f, 0.45f, 1f);

        SwitchEngine _engine;
        DinoSim _dino;
        SnakeSim _snake;
        PianoSim _piano;
        TetrisSim _tetris;

        const int GameCount = 4;
        RectTransform[] _panels;
        Color[] _baseColors;
        Text[] _panelLabels;
        readonly List<Image>[] _pools = new List<Image>[GameCount];

        Text _hud;
        Text _banner;        // 開場熱身 / Game Over
        int _lastHp;

        // 切換節奏縮放（1 = 規格 20/15/10）。切片預設較快讓玩家快速體驗輪替，可用 [ ] 即時調。
        float _switchScale = 0.5f;

        // 視覺回饋
        readonly float[] _panelFlash = new float[GameCount]; // 失誤紅閃計時（秒）
        float _shakeTime;                            // 切換警報抖動計時（秒）
        float _bestSurvival;                         // 最佳生存秒數（PlayerPrefs 持久化）

        // 卡牌系統
        CardEngine _cards;
        int _lastScore;
        float _slowTime; // 慢動作卡剩餘秒數

        void Awake()
        {
            _bestSurvival = PlayerPrefs.GetFloat("best_survival", 0f);
            BuildUi();
            BuildEngine();
            Debug.Log("[Slice] started with 4 games (dino/snake/piano/tetris)");
        }

        // ---------- 建立 UI ----------
        void BuildUi()
        {
            // 純鍵盤輸入，不需 GraphicRaycaster/EventSystem
            var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);

            // 背景
            UiFactory.StretchPanel(canvas.transform, "bg", new Color(0.05f, 0.05f, 0.08f),
                Vector2.zero, Vector2.one);

            // 四欄面板（上方留 HUD，下方留控制提示）
            _panels = new RectTransform[GameCount];
            _baseColors = new[] { DinoColor, SnakeColor, PianoColor, TetrisColor };
            _panelLabels = new Text[GameCount];
            string[] names = { "dino", "snake", "piano", "tetris" };
            for (int i = 0; i < GameCount; i++)
            {
                float x0 = i / (float)GameCount, x1 = (i + 1) / (float)GameCount;
                var panel = UiFactory.StretchPanel(canvas.transform, "panel_" + names[i],
                    PanelBg(_baseColors[i], false),
                    new Vector2(x0 + 0.008f, 0.12f), new Vector2(x1 - 0.008f, 0.84f));
                _panels[i] = panel;
                _pools[i] = new List<Image>();
                _panelLabels[i] = UiFactory.Label(panel, "label", 18, TextAnchor.UpperCenter,
                    new Vector2(0f, 0.9f), new Vector2(1f, 1f));
            }

            _hud = UiFactory.Label(canvas.transform, "hud", 21, TextAnchor.UpperCenter,
                new Vector2(0f, 0.85f), new Vector2(1f, 1f));
            _banner = UiFactory.Label(canvas.transform, "banner", 40, TextAnchor.MiddleCenter,
                new Vector2(0f, 0.4f), new Vector2(1f, 0.6f));
            _banner.color = new Color(1f, 0.3f, 0.4f);
            _banner.text = "";

            // 底部控制提示
            UiFactory.Label(canvas.transform, "controls", 16, TextAnchor.MiddleCenter,
                new Vector2(0f, 0.01f), new Vector2(1f, 0.1f)).text =
                "恐龍:Space/↑ 蛇:方向鍵 鋼琴:A/S/D/F 方塊:←→↑↓ | Z/X/C:出牌 R:重開 [/]:快慢 2:下樓梯對戰 3:爆爆王對戰";
        }

        static Color PanelBg(Color c, bool focused)
        {
            return new Color(c.r, c.g, c.b, focused ? 0.28f : 0.08f);
        }

        // ---------- 建立 / 重置遊戲核心 ----------
        void BuildEngine()
        {
            _dino = new DinoSim();
            _snake = new SnakeSim();
            _piano = new PianoSim();
            _tetris = new TetrisSim();
            var games = new List<IMinigame> { _dino, _snake, _piano, _tetris };
            var timer = new SwitchTimer(3f, _switchScale);
            var state = new MatchState();
            _engine = new SwitchEngine(games, timer, state)
            {
                OpeningInvincibility = 2.5f,  // 開場熱身：先熟悉操作不扣血
                PostFailInvincibility = 1.5f, // 失誤後冷卻，避免連環爆扣（拉長助玩家撐到後棒）
                StartFocusIndex = Random.Range(0, GameCount), // 隨機起始棒，讓每個遊戲都可能先玩到
            };

            timer.OnSwitch += () =>
                Debug.Log($"[Slice] switch -> #{_engine.FocusIndex} {_engine.Focused.GameId} (round {timer.Round})");
            timer.OnWarning += () => _shakeTime = 0.35f; // 切換警報：前台面板抖動
            for (int i = 0; i < games.Count; i++)
            {
                int idx = i;
                games[i].OnFail += _ => _panelFlash[idx] = 0.45f; // 失誤：該面板紅閃
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

        void Restart()
        {
            Debug.Log("[Slice] restart");
            BuildEngine();
        }

        // ---------- 主迴圈 ----------
        void Update()
        {
            float dt = Time.deltaTime;

            if (Input.GetKeyDown(KeyCode.Alpha2)) { SceneManager.LoadScene("Versus"); return; } // 雙人對戰：下樓梯
            if (Input.GetKeyDown(KeyCode.Alpha3)) { SceneManager.LoadScene("Bomber"); return; } // 雙人對戰：爆爆王

            for (int i = 0; i < GameCount; i++)
                if (_panelFlash[i] > 0f) _panelFlash[i] = Mathf.Max(0f, _panelFlash[i] - dt);
            if (_shakeTime > 0f) _shakeTime = Mathf.Max(0f, _shakeTime - dt);

            // 即時調校切換節奏（會重開一局套用）
            if (Input.GetKeyDown(KeyCode.LeftBracket))
            {
                _switchScale = Mathf.Max(0.2f, _switchScale - 0.1f); Restart(); return;
            }
            if (Input.GetKeyDown(KeyCode.RightBracket))
            {
                _switchScale = Mathf.Min(1.5f, _switchScale + 0.1f); Restart(); return;
            }

            if (_engine.State.IsGameOver)
            {
                _banner.color = new Color(1f, 0.3f, 0.4f);
                _banner.text = $"GAME OVER\n你撐了 {_engine.State.SurvivalTime:0.0} 秒　最佳 {_bestSurvival:0.0} 秒\n按 R 再來一局";
                if (Input.GetKeyDown(KeyCode.R)) Restart();
                RenderAll();
                return;
            }

            if (Input.GetKeyDown(KeyCode.R)) { Restart(); return; }

            // 出牌（Z/X/C 對應手牌 1/2/3）
            if (Input.GetKeyDown(KeyCode.Z)) PlayCard(0);
            if (Input.GetKeyDown(KeyCode.X)) PlayCard(1);
            if (Input.GetKeyDown(KeyCode.C)) PlayCard(2);

            // 慢動作卡：放慢小遊戲模擬（切換倒數不受影響）
            float simDt = dt;
            if (_slowTime > 0f) { _slowTime = Mathf.Max(0f, _slowTime - dt); simDt = dt * 0.4f; }

            RouteInput();
            _engine.Tick(simDt);

            // 得分累積能量
            int score = _engine.State.Score;
            if (score > _lastScore) { _cards.AddEnergy((score - _lastScore) * 20f); _lastScore = score; }

            if (_engine.State.Hp != _lastHp)
            {
                _lastHp = _engine.State.Hp;
                Debug.Log($"[Slice] HP={_lastHp}");
            }

            // 開場/失誤熱身提示（無敵期間）
            if (_engine.State.IsInvincible)
            {
                _banner.color = new Color(0.4f, 0.9f, 1f);
                _banner.text = $"熱身中（暫不扣血）{_engine.State.InvincibleRemaining:0.0}s\n顧好「亮起來」的那個遊戲！";
            }
            else _banner.text = "";

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
            }
        }

        void UpdateHud()
        {
            var s = _engine.State;
            var t = _engine.Timer;
            string warn = t.WarningActive ? "   ⚠ 即將切換！" : "";
            string grace = (!s.IsInvincible && _engine.Focused.InFocusGrace) ? "   準備!" : "";
            float baseInterval = SwitchTimer.IntervalForRound(t.Round) * t.IntervalScale;
            string frozen = t.IsFrozen ? "  ❄凍結" : "";
            _hud.text =
                $"HP {s.Hp}/{s.MaxHp}    分數 {s.Score}    生存 {s.SurvivalTime:0.0}s    {t.Remaining:0.0}s 後切換（每輪約 {baseInterval:0.0}s）{warn}{frozen}\n" +
                $"▶ 現在操作：{ActiveName()}  —  {ActiveHint()}{grace}\n" +
                $"⚡能量 {_cards.Energy:0}%   手牌：{HandText()}   (Z / X / C 出牌)";
            _hud.color = t.WarningActive ? new Color(1f, 0.4f, 0.4f) : Color.white;
        }

        // ---------- 渲染 ----------
        void RenderAll()
        {
            for (int i = 0; i < GameCount; i++)
            {
                bool focused = _engine.FocusIndex == i;
                Color bg = PanelBg(_baseColors[i], focused);
                if (_panelFlash[i] > 0f)
                    bg = Color.Lerp(bg, new Color(0.9f, 0.1f, 0.1f, 0.65f), _panelFlash[i] / 0.45f);
                _panels[i].GetComponent<Image>().color = bg;

                Vector2 off = Vector2.zero;
                if (focused && _shakeTime > 0f)
                    off = new Vector2(Random.Range(-7f, 7f), Random.Range(-7f, 7f));
                _panels[i].anchoredPosition = off;
            }

            RenderDino(0, _engine.FocusIndex == 0);
            RenderSnake(1, _engine.FocusIndex == 1);
            RenderPiano(2, _engine.FocusIndex == 2);
            RenderTetris(3, _engine.FocusIndex == 3);

            SetPanelLabel(0, "恐龍 Dino");
            SetPanelLabel(1, "貪食蛇 Snake");
            SetPanelLabel(2, "鋼琴 Piano");
            SetPanelLabel(3, "方塊 Tetris");
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

        string ActiveName()
        {
            switch (_engine.Focused.GameId)
            {
                case "dino": return "恐龍";
                case "snake": return "貪食蛇";
                case "piano": return "鋼琴";
                case "tetris": return "方塊";
                default: return "?";
            }
        }

        string ActiveHint()
        {
            switch (_engine.Focused.GameId)
            {
                case "dino": return "Space / ↑ 跳";
                case "snake": return "方向鍵 移動";
                case "piano": return "A / S / D / F 點擊";
                case "tetris": return "←→ 移動  ↑ 旋轉  ↓ 下移";
                default: return "";
            }
        }

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

        Image Pooled(int panel, int index, Color color)
        {
            var pool = _pools[panel];
            while (pool.Count <= index)
                pool.Add(UiFactory.Block(_panels[panel], color));
            var img = pool[index];
            img.color = color;
            img.enabled = true;
            return img;
        }

        void HideFrom(int panel, int index)
        {
            var pool = _pools[panel];
            for (int i = index; i < pool.Count; i++) pool[i].enabled = false;
        }

        void RenderDino(int panel, bool focused)
        {
            var rect = _panels[panel].rect;
            float w = rect.width, h = rect.height;
            float groundY = h * 0.18f;
            Color c = focused ? DinoColor : DinoColor * 0.6f;

            // 0: 地面
            var ground = Pooled(panel, 0, new Color(0.6f, 0.6f, 0.6f));
            var grt = ground.rectTransform;
            grt.sizeDelta = new Vector2(w, 3f);
            grt.anchoredPosition = new Vector2(0, groundY);

            // 1: 玩家
            float playerSize = Mathf.Min(w, h) * 0.12f;
            var player = Pooled(panel, 1, c);
            player.rectTransform.sizeDelta = new Vector2(playerSize, playerSize);
            float py = groundY + (_dino.IsAirborne ? h * 0.22f : 0f);
            player.rectTransform.anchoredPosition = new Vector2(w * 0.14f, py);

            // 2: 障礙
            float ox = Mathf.Clamp01(_dino.ObstacleX / _dino.SpawnDistance) * w;
            var obs = Pooled(panel, 2, new Color(0.9f, 0.2f, 0.3f));
            obs.rectTransform.sizeDelta = new Vector2(playerSize * 0.8f, playerSize * 1.1f);
            obs.rectTransform.anchoredPosition = new Vector2(ox, groundY);

            HideFrom(panel, 3);
        }

        void RenderSnake(int panel, bool focused)
        {
            var rect = _panels[panel].rect;
            float cw = rect.width / _snake.Width;
            float ch = rect.height / _snake.Height;
            Color c = focused ? SnakeColor : SnakeColor * 0.6f;

            int idx = 0;
            var body = _snake.Body;
            for (int i = 0; i < body.Count; i++)
            {
                var img = Pooled(panel, idx++, i == 0 ? Color.white : c);
                img.rectTransform.sizeDelta = new Vector2(Mathf.Max(1, cw - 1f), Mathf.Max(1, ch - 1f));
                img.rectTransform.anchoredPosition = new Vector2(body[i].X * cw, body[i].Y * ch);
            }
            // 食物
            var food = Pooled(panel, idx++, new Color(1f, 0.85f, 0.2f));
            food.rectTransform.sizeDelta = new Vector2(Mathf.Max(1, cw - 1f), Mathf.Max(1, ch - 1f));
            food.rectTransform.anchoredPosition = new Vector2(_snake.Food.X * cw, _snake.Food.Y * ch);

            HideFrom(panel, idx);
        }

        void RenderPiano(int panel, bool focused)
        {
            var rect = _panels[panel].rect;
            float w = rect.width, h = rect.height;
            float colW = w / _piano.Columns;
            Color c = focused ? PianoColor : PianoColor * 0.6f;

            int idx = 0;
            // 命中線
            var line = Pooled(panel, idx++, new Color(1f, 1f, 1f, 0.6f));
            line.rectTransform.sizeDelta = new Vector2(w, 3f);
            line.rectTransform.anchoredPosition = new Vector2(0, h * 0.05f);

            var tiles = _piano.Tiles;
            for (int i = 0; i < tiles.Count; i++)
            {
                var img = Pooled(panel, idx++, c);
                float tx = tiles[i].Column * colW;
                float ty = Mathf.Clamp01(tiles[i].Y / _piano.Height) * h;
                img.rectTransform.sizeDelta = new Vector2(Mathf.Max(1, colW - 3f), h * 0.06f);
                img.rectTransform.anchoredPosition = new Vector2(tx, ty);
            }
            HideFrom(panel, idx);
        }

        void RenderTetris(int panel, bool focused)
        {
            var rect = _panels[panel].rect;
            float cw = rect.width / _tetris.Width;
            float ch = rect.height / _tetris.Height;
            Color c = focused ? TetrisColor : TetrisColor * 0.6f;
            var locked = new Color(c.r * 0.7f, c.g * 0.7f, c.b * 0.7f);
            var cellSize = new Vector2(Mathf.Max(1, cw - 1f), Mathf.Max(1, ch - 1f));

            int idx = 0;
            var grid = _tetris.Grid;
            if (grid != null)
            {
                for (int x = 0; x < _tetris.Width; x++)
                    for (int y = 0; y < _tetris.Height; y++)
                        if (grid[x, y])
                        {
                            var img = Pooled(panel, idx++, locked);
                            img.rectTransform.sizeDelta = cellSize;
                            img.rectTransform.anchoredPosition = new Vector2(x * cw, y * ch);
                        }
            }
            if (_tetris.HasPiece)
            {
                foreach (var cell in _tetris.Piece)
                {
                    int wx = _tetris.PieceX + cell.X, wy = _tetris.PieceY + cell.Y;
                    var img = Pooled(panel, idx++, Color.white);
                    img.rectTransform.sizeDelta = cellSize;
                    img.rectTransform.anchoredPosition = new Vector2(wx * cw, wy * ch);
                }
            }
            HideFrom(panel, idx);
        }
    }
}
