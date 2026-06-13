using System.Collections.Generic;
using PanicConsole.Core;
using PanicConsole.Minigames;
using UnityEngine;
using UnityEngine.UI;

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

        SwitchEngine _engine;
        DinoSim _dino;
        SnakeSim _snake;
        PianoSim _piano;

        RectTransform[] _panels;
        Color[] _baseColors;
        Text[] _panelLabels;
        readonly List<Image>[] _pools = new List<Image>[3];

        Text _hud;
        Text _banner;        // 切換警報 / Game Over
        int _lastHp;

        // 切換節奏縮放（1 = 規格 20/15/10）。切片預設較快讓玩家快速體驗輪替，可用 [ ] 即時調。
        float _switchScale = 0.5f;

        void Awake()
        {
            BuildUi();
            BuildEngine();
            Debug.Log("[Slice] started with 3 games (dino/snake/piano)");
        }

        // ---------- 建立 UI ----------
        void BuildUi()
        {
            var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);

            // 背景
            UiFactory.StretchPanel(canvas.transform, "bg", new Color(0.05f, 0.05f, 0.08f),
                Vector2.zero, Vector2.one);

            // 三欄面板（上方留 HUD，下方留控制提示）
            _panels = new RectTransform[3];
            _baseColors = new[] { DinoColor, SnakeColor, PianoColor };
            _panelLabels = new Text[3];
            string[] names = { "dino", "snake", "piano" };
            for (int i = 0; i < 3; i++)
            {
                float x0 = i / 3f, x1 = (i + 1) / 3f;
                var panel = UiFactory.StretchPanel(canvas.transform, "panel_" + names[i],
                    PanelBg(_baseColors[i], false),
                    new Vector2(x0 + 0.01f, 0.12f), new Vector2(x1 - 0.01f, 0.84f));
                _panels[i] = panel;
                _pools[i] = new List<Image>();
                _panelLabels[i] = UiFactory.Label(panel, "label", 18, TextAnchor.UpperCenter,
                    new Vector2(0f, 0.9f), new Vector2(1f, 1f));
            }

            _hud = UiFactory.Label(canvas.transform, "hud", 26, TextAnchor.MiddleCenter,
                new Vector2(0f, 0.86f), new Vector2(1f, 0.99f));
            _banner = UiFactory.Label(canvas.transform, "banner", 40, TextAnchor.MiddleCenter,
                new Vector2(0f, 0.4f), new Vector2(1f, 0.6f));
            _banner.color = new Color(1f, 0.3f, 0.4f);
            _banner.text = "";

            // 底部控制提示
            UiFactory.Label(canvas.transform, "controls", 16, TextAnchor.MiddleCenter,
                new Vector2(0f, 0.01f), new Vector2(1f, 0.1f)).text =
                "前台操作 → 恐龍: Space/↑ 跳   蛇: 方向鍵   鋼琴: A/S/D/F     |     R: 重新開始     |     [ / ] : 切換變快/變慢";
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
            var games = new List<IMinigame> { _dino, _snake, _piano };
            var timer = new SwitchTimer(3f, _switchScale);
            var state = new MatchState();
            _engine = new SwitchEngine(games, timer, state);

            timer.OnSwitch += () =>
                Debug.Log($"[Slice] switch -> #{_engine.FocusIndex} {_engine.Focused.GameId} (round {timer.Round})");
            state.OnGameOver += () => Debug.Log("[Slice] GAME OVER");

            _engine.Start();
            _lastHp = state.Hp;
            _banner.text = "";
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
                _banner.text = "GAME OVER\n按 R 重新開始";
                if (Input.GetKeyDown(KeyCode.R)) Restart();
                RenderAll();
                return;
            }

            if (Input.GetKeyDown(KeyCode.R)) { Restart(); return; }

            RouteInput();
            _engine.Tick(dt);

            if (_engine.State.Hp != _lastHp)
            {
                _lastHp = _engine.State.Hp;
                Debug.Log($"[Slice] HP={_lastHp}");
            }

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
            }
        }

        void UpdateHud()
        {
            var s = _engine.State;
            var t = _engine.Timer;
            string warn = t.WarningActive ? "   ⚠ 即將切換！" : "";
            float baseInterval = SwitchTimer.IntervalForRound(t.Round) * t.IntervalScale;
            _hud.text =
                $"HP {s.Hp}/{s.MaxHp}    生存 {s.SurvivalTime:0.0}s    {t.Remaining:0.0}s 後切換（每輪約 {baseInterval:0.0}s）{warn}\n" +
                $"▶ 現在操作：{ActiveName()}  —  {ActiveHint()}";
            _hud.color = t.WarningActive ? new Color(1f, 0.4f, 0.4f) : Color.white;
        }

        // ---------- 渲染 ----------
        void RenderAll()
        {
            for (int i = 0; i < 3; i++)
            {
                bool focused = _engine.FocusIndex == i;
                _panels[i].GetComponent<Image>().color = PanelBg(_baseColors[i], focused);
            }

            RenderDino(0, _engine.FocusIndex == 0);
            RenderSnake(1, _engine.FocusIndex == 1);
            RenderPiano(2, _engine.FocusIndex == 2);

            SetPanelLabel(0, "恐龍 Dino");
            SetPanelLabel(1, "貪食蛇 Snake");
            SetPanelLabel(2, "鋼琴 Piano");
        }

        void SetPanelLabel(int i, string name)
        {
            bool focused = _engine.FocusIndex == i;
            _panelLabels[i].text = focused ? ("▶ " + name + "\n← 你正在操作") : (name + "\n（背景）");
            _panelLabels[i].color = focused ? Color.white : new Color(1f, 1f, 1f, 0.4f);
        }

        string ActiveName()
        {
            switch (_engine.Focused.GameId)
            {
                case "dino": return "恐龍";
                case "snake": return "貪食蛇";
                case "piano": return "鋼琴";
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
                default: return "";
            }
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
    }
}
