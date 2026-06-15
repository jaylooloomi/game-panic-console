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
    /// 新主玩法：1v1 對戰。左＝你、右＝AI 對手，各自全螢幕玩「同一種」小遊戲；
    /// 固定秒數同步切換，每輪兩邊一起換到新遊戲；任一邊先掉光 HP 就輸。含干擾卡（女鬼/反轉/噴墨）。
    /// </summary>
    public class DuelGame : MonoBehaviour
    {
        static readonly Color DinoColor = new Color(1f, 0.55f, 0.15f);
        static readonly Color SnakeColor = new Color(0.3f, 0.9f, 0.4f);
        static readonly Color PianoColor = new Color(0.3f, 0.8f, 1f);
        static readonly Color TetrisColor = new Color(0.75f, 0.45f, 1f);
        static readonly Color ClickerColor = new Color(1f, 0.85f, 0.3f);

        struct GameDef { public string Id, Name, Hint; public Func<IMinigame> Make; public Color Color; }

        static readonly GameDef[] Pool =
        {
            new GameDef { Id="dino",    Name="恐龍 Dino",    Hint="Space/↑ 跳",        Make=() => new DinoSim(),    Color=DinoColor },
            new GameDef { Id="snake",   Name="貪食蛇 Snake", Hint="方向鍵 移動",        Make=() => new SnakeSim(),   Color=SnakeColor },
            new GameDef { Id="piano",   Name="鋼琴 Piano",   Hint="A/S/D/F 點擊",       Make=() => new PianoSim(),   Color=PianoColor },
            new GameDef { Id="tetris",  Name="方塊 Tetris",  Hint="←→移動 ↑旋轉 ↓下",  Make=() => new TetrisSim(),  Color=TetrisColor },
            new GameDef { Id="clicker", Name="連點 Clicker", Hint="狂點 Space",         Make=() => new ClickerSim(), Color=ClickerColor },
        };

        static Dictionary<string, GameDef> _byId;
        static GameDef Def(string id) => _byId[id];

        class SideView
        {
            public RectTransform Monitor;
            public Image MonitorImg;
            public Outline MonitorOutline;
            public RectTransform Area;
            public Text NameLabel;
            public Text StatLabel;
            public Image Flash;
            public Image Ink;
            public Text Ghost;
            public readonly List<Image> Pool = new List<Image>();
            public float FlashTime;
            public int LastHp;
        }

        DuelEngine _engine;
        OpponentAi _ai;
        readonly SideView[] _views = new SideView[2];

        Text _topHud, _banner, _controls, _vs;
        Image _warnTint;

        float _switchScale = 0.6f;     // ~12s 首輪
        float _aiDifficulty = 0.65f;
        float _shakeTime;
        readonly System.Random _rng = new System.Random();

        void Awake()
        {
            if (_byId == null)
            {
                _byId = new Dictionary<string, GameDef>();
                foreach (var d in Pool) _byId[d.Id] = d;
            }
            BuildUi();
            BuildEngine();
            Debug.Log("[Duel] started");
        }

        // ---------------- UI ----------------
        void BuildUi()
        {
            var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);

            UiFactory.StretchPanel(canvas.transform, "bg", new Color(0.04f, 0.04f, 0.07f), Vector2.zero, Vector2.one);

            _warnTint = UiFactory.StretchPanel(canvas.transform, "warnTint", new Color(1f, 0.2f, 0.2f, 0f),
                Vector2.zero, Vector2.one).GetComponent<Image>();
            _warnTint.raycastTarget = false;

            _views[0] = BuildSide(canvas.transform, 0, new Vector2(0.015f, 0.10f), new Vector2(0.492f, 0.83f), "你 (Player)");
            _views[1] = BuildSide(canvas.transform, 1, new Vector2(0.508f, 0.10f), new Vector2(0.985f, 0.83f), "對手 (AI)");

            // 中央 VS
            _vs = UiFactory.Label(canvas.transform, "vs", 44, TextAnchor.MiddleCenter,
                new Vector2(0.45f, 0.46f), new Vector2(0.55f, 0.56f));
            _vs.text = "VS";

            _topHud = UiFactory.Label(canvas.transform, "topHud", 24, TextAnchor.UpperCenter,
                new Vector2(0f, 0.85f), new Vector2(1f, 1f));
            _banner = UiFactory.Label(canvas.transform, "banner", 46, TextAnchor.MiddleCenter,
                new Vector2(0f, 0.40f), new Vector2(1f, 0.62f));
            _banner.text = "";
            _controls = UiFactory.Label(canvas.transform, "controls", 15, TextAnchor.MiddleCenter,
                new Vector2(0f, 0.005f), new Vector2(1f, 0.085f));
            _controls.text = "你的操作見左側提示  |  Z/X/C 出牌（干擾右側對手）  R 重開  [ ] 調節奏  1/2/3 難度  8 下樓梯  9 爆爆王";
        }

        SideView BuildSide(Transform parent, int idx, Vector2 min, Vector2 max, string title)
        {
            var v = new SideView();
            var mon = UiFactory.StretchPanel(parent, "monitor" + idx, new Color(0.07f, 0.07f, 0.10f, 0.98f), min, max);
            v.Monitor = mon;
            v.MonitorImg = mon.GetComponent<Image>();
            v.MonitorOutline = mon.gameObject.AddComponent<Outline>();
            v.MonitorOutline.effectDistance = new Vector2(4f, -4f);

            // 內部留邊的遊戲區（讓圓角磚塊不貼邊）
            v.Area = UiFactory.StretchPanel(mon, "area", new Color(0, 0, 0, 0), new Vector2(0.04f, 0.10f), new Vector2(0.96f, 0.88f));
            v.Area.GetComponent<Image>().raycastTarget = false;

            v.NameLabel = UiFactory.Label(mon, "name", 20, TextAnchor.UpperCenter, new Vector2(0f, 0.90f), new Vector2(1f, 1f));
            v.StatLabel = UiFactory.Label(mon, "stat", 17, TextAnchor.LowerCenter, new Vector2(0f, 0f), new Vector2(1f, 0.10f));

            // 失誤紅閃
            v.Flash = UiFactory.StretchPanel(mon, "flash", new Color(0.9f, 0.1f, 0.1f, 0f), Vector2.zero, Vector2.one).GetComponent<Image>();
            v.Flash.raycastTarget = false;
            // 噴墨遮蔽
            v.Ink = UiFactory.StretchPanel(v.Area, "ink", new Color(0.05f, 0.02f, 0.08f, 0f), Vector2.zero, Vector2.one).GetComponent<Image>();
            v.Ink.raycastTarget = false;
            // 女鬼閃現
            v.Ghost = UiFactory.Label(v.Area, "ghost", 90, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one);
            v.Ghost.text = "";

            v.NameLabel.text = title;
            return v;
        }

        // ---------------- Engine ----------------
        void BuildEngine()
        {
            var factories = new List<Func<IMinigame>>();
            foreach (var d in Pool) factories.Add(d.Make);

            var timer = new SwitchTimer(3f, _switchScale);
            _engine = new DuelEngine(factories, timer, seed: _rng.Next(1, 99999), maxHp: 5)
            {
                OpeningInvincibility = 2.5f,
                PostFailInvincibility = 1.2f,
            };

            timer.OnWarning += () => { _shakeTime = 0.4f; };
            timer.OnSwitch += () => Debug.Log($"[Duel] switch -> {_engine.CurrentGameId} (round {timer.Round})");
            for (int s = 0; s < 2; s++)
            {
                int si = s;
                _engine.Sides[si].State.OnGameOver += () => Debug.Log($"[Duel] side {si} ({_engine.Sides[si].Name}) OUT");
            }

            _engine.Start();
            _ai = new OpponentAi(_engine, 1, seed: _rng.Next(1, 99999), difficulty: _aiDifficulty);

            for (int s = 0; s < 2; s++) { _views[s].LastHp = _engine.Sides[s].State.Hp; _views[s].FlashTime = 0f; }
            _banner.text = "";
            if (_vs != null) _vs.enabled = true;
            _shakeTime = 0f;
        }

        void Restart() { Debug.Log("[Duel] restart"); BuildEngine(); }

        // ---------------- Loop ----------------
        void Update()
        {
            if (Input.GetKeyDown(KeyCode.Alpha8)) { SceneManager.LoadScene("Versus"); return; }
            if (Input.GetKeyDown(KeyCode.Alpha9)) { SceneManager.LoadScene("Bomber"); return; }
            if (Input.GetKeyDown(KeyCode.R)) { Restart(); return; }
            if (Input.GetKeyDown(KeyCode.LeftBracket)) { _switchScale = Mathf.Max(0.3f, _switchScale - 0.1f); Restart(); return; }
            if (Input.GetKeyDown(KeyCode.RightBracket)) { _switchScale = Mathf.Min(1.5f, _switchScale + 0.1f); Restart(); return; }
            if (Input.GetKeyDown(KeyCode.Alpha1)) { _aiDifficulty = 0.4f; Restart(); return; }
            if (Input.GetKeyDown(KeyCode.Alpha2)) { _aiDifficulty = 0.65f; Restart(); return; }
            if (Input.GetKeyDown(KeyCode.Alpha3)) { _aiDifficulty = 0.9f; Restart(); return; }

            float dt = Time.deltaTime;
            for (int s = 0; s < 2; s++)
                if (_views[s].FlashTime > 0f) _views[s].FlashTime = Mathf.Max(0f, _views[s].FlashTime - dt);
            if (_shakeTime > 0f) _shakeTime = Mathf.Max(0f, _shakeTime - dt);

            if (_engine.IsOver)
            {
                ShowResult();
                RenderAll();
                return;
            }

            // 出牌（干擾右側對手 / 自助）
            if (Input.GetKeyDown(KeyCode.Z)) PlayCard(0);
            if (Input.GetKeyDown(KeyCode.X)) PlayCard(1);
            if (Input.GetKeyDown(KeyCode.C)) PlayCard(2);

            RouteHumanInput();
            _ai.Tick(dt);
            _engine.Tick(dt);

            // 失誤紅閃偵測
            for (int s = 0; s < 2; s++)
            {
                int hp = _engine.Sides[s].State.Hp;
                if (hp < _views[s].LastHp) _views[s].FlashTime = 0.45f;
                _views[s].LastHp = hp;
            }

            _banner.text = "";
            RenderAll();
            UpdateTopHud();
        }

        void PlayCard(int slot)
        {
            var played = _engine.PlayCard(0, slot);
            if (played.HasValue) Debug.Log($"[Duel] player card {played.Value}");
        }

        void ShowResult()
        {
            switch (_engine.Result)
            {
                case DuelResult.PlayerWins: _banner.color = new Color(0.4f, 1f, 0.5f); _banner.text = "🏆 你贏了！\n按 R 再戰"; break;
                case DuelResult.OpponentWins: _banner.color = new Color(1f, 0.4f, 0.45f); _banner.text = "對手獲勝…\n按 R 再戰"; break;
                default: _banner.color = Color.white; _banner.text = "平手！\n按 R 再戰"; break;
            }
            if (_vs != null) _vs.enabled = false; // 讓出中央給結果橫幅
            _topHud.color = Color.white;
            _topHud.text = $"對局結束　你 撐了 {_engine.Player.State.SurvivalTime:0.0}s　|　對手 {_engine.Opponent.State.SurvivalTime:0.0}s\n按 R 再戰一場　[ ] 調節奏　1/2/3 改難度";
            _warnTint.color = new Color(1f, 0.2f, 0.2f, 0f);
        }

        void RouteHumanInput()
        {
            bool inv = _engine.Player.IsInverted;
            switch (_engine.Player.Current)
            {
                case DinoSim dino:
                    if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.UpArrow)) dino.Jump();
                    break;
                case SnakeSim snake:
                    int sx = 0, sy = 0;
                    if (Input.GetKeyDown(KeyCode.UpArrow)) sy = 1;
                    else if (Input.GetKeyDown(KeyCode.DownArrow)) sy = -1;
                    else if (Input.GetKeyDown(KeyCode.LeftArrow)) sx = -1;
                    else if (Input.GetKeyDown(KeyCode.RightArrow)) sx = 1;
                    if (sx != 0 || sy != 0) { if (inv) { sx = -sx; sy = -sy; } snake.SetDirection(sx, sy); }
                    break;
                case PianoSim piano:
                    int col = -1;
                    if (Input.GetKeyDown(KeyCode.A)) col = 0;
                    else if (Input.GetKeyDown(KeyCode.S)) col = 1;
                    else if (Input.GetKeyDown(KeyCode.D)) col = 2;
                    else if (Input.GetKeyDown(KeyCode.F)) col = 3;
                    if (col >= 0) { if (inv) col = piano.Columns - 1 - col; piano.Hit(col); }
                    break;
                case TetrisSim tetris:
                    if (Input.GetKeyDown(KeyCode.LeftArrow)) { if (inv) tetris.MoveRight(); else tetris.MoveLeft(); }
                    else if (Input.GetKeyDown(KeyCode.RightArrow)) { if (inv) tetris.MoveLeft(); else tetris.MoveRight(); }
                    else if (Input.GetKeyDown(KeyCode.UpArrow)) tetris.Rotate();
                    else if (Input.GetKeyDown(KeyCode.DownArrow)) tetris.SoftDrop();
                    break;
                case ClickerSim clicker:
                    if (Input.GetKeyDown(KeyCode.Space)) clicker.Click();
                    break;
            }
        }

        void UpdateTopHud()
        {
            var t = _engine.Timer;
            float baseInterval = SwitchTimer.IntervalForRound(t.Round) * t.IntervalScale;
            string warn = t.WarningActive ? "  ⚠ 即將一起切換！" : "";
            string name = Def(_engine.CurrentGameId).Name;
            _topHud.text =
                $"第 {t.Round} 關 ▶ {name}　　{t.Remaining:0.0}s 後同步切換（每輪約 {baseInterval:0.0}s）{warn}\n" +
                $"先讓對手 HP 歸零者獲勝　|　AI 難度 {DifficultyName()}";
            _topHud.color = t.WarningActive ? new Color(1f, 0.5f, 0.5f) : Color.white;
            _warnTint.color = new Color(1f, 0.2f, 0.2f, t.WarningActive ? 0.06f + 0.04f * Mathf.PingPong(Time.time * 4f, 1f) : 0f);
        }

        string DifficultyName() => _aiDifficulty <= 0.45f ? "簡單" : _aiDifficulty >= 0.85f ? "困難" : "普通";

        // ---------------- Render ----------------
        void RenderAll()
        {
            for (int s = 0; s < 2; s++) RenderSide(s);
        }

        void RenderSide(int s)
        {
            var v = _views[s];
            var side = _engine.Sides[s];
            var def = Def(side.Current.GameId);

            // 螢幕底 + 邊框（玩家側用較亮邊框做區分）
            Color bg = new Color(def.Color.r * 0.16f + 0.05f, def.Color.g * 0.16f + 0.05f, def.Color.b * 0.16f + 0.05f, 0.98f);
            if (v.FlashTime > 0f) bg = Color.Lerp(bg, new Color(0.9f, 0.1f, 0.1f, 0.7f), v.FlashTime / 0.45f);
            v.MonitorImg.color = bg;
            v.MonitorOutline.effectColor = new Color(def.Color.r, def.Color.g, def.Color.b, s == 0 ? 0.95f : 0.7f);

            // 警報抖動
            Vector2 off = Vector2.zero;
            if (_shakeTime > 0f) off = new Vector2(UnityEngine.Random.Range(-6f, 6f), UnityEngine.Random.Range(-6f, 6f));
            v.Monitor.anchoredPosition = off;

            switch (side.Current)
            {
                case DinoSim d: RenderDino(v, d, def.Color); break;
                case SnakeSim sn: RenderSnake(v, sn, def.Color); break;
                case PianoSim p: RenderPiano(v, p, def.Color); break;
                case TetrisSim te: RenderTetris(v, te, def.Color); break;
                case ClickerSim cl: RenderClicker(v, cl, def.Color); break;
            }

            // 干擾/狀態 overlay（磚塊是後加的子物件，需把遮罩拉到最前才會蓋住遊戲畫面）
            v.Ink.transform.SetAsLastSibling();
            v.Ghost.transform.SetAsLastSibling();
            v.Ink.color = new Color(0.05f, 0.02f, 0.08f, side.IsInked ? 0.82f : 0f);
            if (side.IsGhosted) { v.Ghost.text = "👻"; v.Ghost.color = new Color(1f, 1f, 1f, 0.5f + 0.5f * Mathf.PingPong(Time.time * 8f, 1f)); }
            else v.Ghost.text = "";
            v.Flash.color = new Color(0.9f, 0.1f, 0.1f, v.FlashTime > 0f ? v.FlashTime * 0.6f : 0f);

            // 名稱 + 狀態列
            string who = s == 0 ? "你" : "對手(AI)";
            v.NameLabel.text = $"{who} ▶ {def.Name}";
            v.NameLabel.color = Color.white;
            v.StatLabel.text = StatText(side);
            v.StatLabel.color = side.State.Hp <= 1 ? new Color(1f, 0.5f, 0.5f) : Color.white;
        }

        string StatText(DuelSide side)
        {
            string hearts = new string('♥', Mathf.Max(0, side.State.Hp)) + new string('·', Mathf.Max(0, side.State.MaxHp - side.State.Hp));
            string status = "";
            if (side.State.IsInvincible) status += " 🛡";
            if (side.IsSlowed) status += " 🐌";
            if (side.IsInverted) status += " 🔄反轉";
            if (side.IsInked) status += " 🖤墨";
            if (side.IsGhosted) status += " 👻";
            string hand = HandText(side.Cards);
            return $"HP {hearts}   ⚡{side.Cards.Energy:0}%{status}\n手牌 {hand}";
        }

        static string HandText(CardEngine cards)
        {
            if (cards.Hand.Count == 0) return "（無）";
            var parts = new string[cards.Hand.Count];
            for (int i = 0; i < cards.Hand.Count; i++) parts[i] = "[" + CardName(cards.Hand[i]) + "]";
            return string.Join(" ", parts);
        }

        static string CardName(CardType c)
        {
            switch (c)
            {
                case CardType.Shield: return "護盾";
                case CardType.Heal: return "補血";
                case CardType.SlowMo: return "慢動作";
                case CardType.Ghost: return "女鬼";
                case CardType.Invert: return "反轉";
                case CardType.Ink: return "噴墨";
                case CardType.Freeze: return "凍結";
                default: return c.ToString();
            }
        }

        // ---- pooled blocks ----
        Image Pooled(SideView v, int index, Color color)
        {
            while (v.Pool.Count <= index) v.Pool.Add(UiFactory.Block(v.Area, color));
            var img = v.Pool[index];
            img.color = color; img.enabled = true;
            // ink/ghost overlay 要蓋在磚塊上 → 把它們移到最後
            return img;
        }

        void HideFrom(SideView v, int index)
        {
            for (int i = index; i < v.Pool.Count; i++) v.Pool[i].enabled = false;
        }

        void RenderDino(SideView v, DinoSim dino, Color c)
        {
            var rect = v.Area.rect;
            float w = rect.width, h = rect.height;
            float groundY = h * 0.18f;

            var ground = Pooled(v, 0, new Color(0.6f, 0.6f, 0.6f));
            ground.rectTransform.sizeDelta = new Vector2(w, 3f);
            ground.rectTransform.anchoredPosition = new Vector2(0, groundY);

            float playerSize = Mathf.Min(w, h) * 0.12f;
            float px = w * 0.14f;
            float py = groundY + (dino.IsAirborne ? h * 0.22f : 0f);
            var body = Pooled(v, 1, c);
            body.rectTransform.sizeDelta = new Vector2(playerSize, playerSize);
            body.rectTransform.anchoredPosition = new Vector2(px, py);
            var head = Pooled(v, 2, new Color(Mathf.Min(1, c.r * 1.3f), Mathf.Min(1, c.g * 1.3f), Mathf.Min(1, c.b * 1.3f)));
            head.rectTransform.sizeDelta = new Vector2(playerSize * 0.6f, playerSize * 0.55f);
            head.rectTransform.anchoredPosition = new Vector2(px + playerSize * 0.45f, py + playerSize * 0.6f);

            float ox = Mathf.Clamp01(dino.ObstacleX / dino.SpawnDistance) * w;
            var cactus = Pooled(v, 3, new Color(0.35f, 0.75f, 0.35f));
            cactus.rectTransform.sizeDelta = new Vector2(playerSize * 0.5f, playerSize * 1.3f);
            cactus.rectTransform.anchoredPosition = new Vector2(ox, groundY);
            var arm = Pooled(v, 4, new Color(0.35f, 0.75f, 0.35f));
            arm.rectTransform.sizeDelta = new Vector2(playerSize * 0.3f, playerSize * 0.3f);
            arm.rectTransform.anchoredPosition = new Vector2(ox + playerSize * 0.45f, groundY + playerSize * 0.6f);
            HideFrom(v, 5);
        }

        void RenderSnake(SideView v, SnakeSim snake, Color c)
        {
            var rect = v.Area.rect;
            float cw = rect.width / snake.Width, ch = rect.height / snake.Height;
            var size = new Vector2(Mathf.Max(1, cw - 1f), Mathf.Max(1, ch - 1f));
            int idx = 0;
            var body = snake.Body;
            for (int i = 0; i < body.Count; i++)
            {
                var img = Pooled(v, idx++, i == 0 ? Color.white : c);
                img.rectTransform.sizeDelta = size;
                img.rectTransform.anchoredPosition = new Vector2(body[i].X * cw, body[i].Y * ch);
            }
            var food = Pooled(v, idx++, new Color(1f, 0.9f, 0.3f));
            food.rectTransform.sizeDelta = size * 1.1f;
            food.rectTransform.anchoredPosition = new Vector2(snake.Food.X * cw - cw * 0.05f, snake.Food.Y * ch - ch * 0.05f);
            if (body.Count > 0)
            {
                float hx = body[0].X * cw, hy = body[0].Y * ch;
                var eyeSize = new Vector2(Mathf.Max(2f, cw * 0.2f), Mathf.Max(2f, ch * 0.2f));
                var eyeColor = new Color(0.1f, 0.1f, 0.13f);
                var e1 = Pooled(v, idx++, eyeColor); e1.rectTransform.sizeDelta = eyeSize;
                e1.rectTransform.anchoredPosition = new Vector2(hx + cw * 0.2f, hy + ch * 0.5f);
                var e2 = Pooled(v, idx++, eyeColor); e2.rectTransform.sizeDelta = eyeSize;
                e2.rectTransform.anchoredPosition = new Vector2(hx + cw * 0.55f, hy + ch * 0.5f);
            }
            HideFrom(v, idx);
        }

        void RenderPiano(SideView v, PianoSim piano, Color c)
        {
            var rect = v.Area.rect;
            float w = rect.width, h = rect.height;
            float colW = w / piano.Columns;
            int idx = 0;
            for (int col = 1; col < piano.Columns; col++)
            {
                var div = Pooled(v, idx++, new Color(1f, 1f, 1f, 0.08f));
                div.rectTransform.sizeDelta = new Vector2(2f, h);
                div.rectTransform.anchoredPosition = new Vector2(col * colW, 0);
            }
            var line = Pooled(v, idx++, new Color(c.r, c.g, c.b, 0.9f));
            line.rectTransform.sizeDelta = new Vector2(w, 6f);
            line.rectTransform.anchoredPosition = new Vector2(0, h * 0.06f);
            var tiles = piano.Tiles;
            for (int i = 0; i < tiles.Count; i++)
            {
                var img = Pooled(v, idx++, c);
                img.rectTransform.sizeDelta = new Vector2(Mathf.Max(1, colW - 3f), h * 0.06f);
                img.rectTransform.anchoredPosition = new Vector2(tiles[i].Column * colW, Mathf.Clamp01(tiles[i].Y / piano.Height) * h);
            }
            HideFrom(v, idx);
        }

        void RenderTetris(SideView v, TetrisSim tetris, Color c)
        {
            var rect = v.Area.rect;
            float cw = rect.width / tetris.Width, ch = rect.height / tetris.Height;
            var locked = new Color(c.r * 0.7f, c.g * 0.7f, c.b * 0.7f);
            var size = new Vector2(Mathf.Max(1, cw - 1f), Mathf.Max(1, ch - 1f));
            int idx = 0;
            var gcol = new Color(1f, 1f, 1f, 0.06f);
            for (int x = 1; x < tetris.Width; x++)
            {
                var vline = Pooled(v, idx++, gcol);
                vline.rectTransform.sizeDelta = new Vector2(1.5f, rect.height);
                vline.rectTransform.anchoredPosition = new Vector2(x * cw, 0);
            }
            for (int y = 1; y < tetris.Height; y++)
            {
                var hl = Pooled(v, idx++, gcol);
                hl.rectTransform.sizeDelta = new Vector2(rect.width, 1.5f);
                hl.rectTransform.anchoredPosition = new Vector2(0, y * ch);
            }
            var grid = tetris.Grid;
            if (grid != null)
                for (int x = 0; x < tetris.Width; x++)
                    for (int y = 0; y < tetris.Height; y++)
                        if (grid[x, y])
                        {
                            var img = Pooled(v, idx++, locked);
                            img.rectTransform.sizeDelta = size;
                            img.rectTransform.anchoredPosition = new Vector2(x * cw, y * ch);
                        }
            if (tetris.HasPiece)
                foreach (var cell in tetris.Piece)
                {
                    var img = Pooled(v, idx++, Color.white);
                    img.rectTransform.sizeDelta = size;
                    img.rectTransform.anchoredPosition = new Vector2((tetris.PieceX + cell.X) * cw, (tetris.PieceY + cell.Y) * ch);
                }
            HideFrom(v, idx);
        }

        void RenderClicker(SideView v, ClickerSim clicker, Color c)
        {
            var rect = v.Area.rect;
            float w = rect.width, h = rect.height;
            var bgBar = Pooled(v, 0, new Color(1f, 1f, 1f, 0.12f));
            bgBar.rectTransform.sizeDelta = new Vector2(w * 0.4f, h * 0.7f);
            bgBar.rectTransform.anchoredPosition = new Vector2(w * 0.3f, h * 0.15f);
            float fillH = h * 0.7f * Mathf.Clamp01(clicker.Ratio);
            var fill = Pooled(v, 1, clicker.Ratio < 0.3f ? new Color(0.95f, 0.3f, 0.3f) : c);
            fill.rectTransform.sizeDelta = new Vector2(w * 0.4f, Mathf.Max(1f, fillH));
            fill.rectTransform.anchoredPosition = new Vector2(w * 0.3f, h * 0.15f);
            HideFrom(v, 2);
        }
    }
}
