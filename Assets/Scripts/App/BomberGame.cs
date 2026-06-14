using System.Collections.Generic;
using PanicConsole.Minigames;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace PanicConsole.App
{
    /// <summary>爆爆王（炸彈超人）本機雙人對戰：共用棋盤，P1=WASD+空白鍵放彈、P2=方向鍵+Enter 放彈，
    /// 炸死對手者勝。架構預留 Phase 3：把 P2 輸入換成連線對手。</summary>
    public class BomberGame : MonoBehaviour
    {
        static readonly Color P1Color = new Color(0.3f, 0.8f, 1f);
        static readonly Color P2Color = new Color(1f, 0.55f, 0.15f);
        static readonly Color SolidColor = new Color(0.35f, 0.35f, 0.42f);
        static readonly Color BrickColor = new Color(0.6f, 0.4f, 0.25f);
        static readonly Color BombColor = new Color(0.12f, 0.12f, 0.12f);
        static readonly Color BlastColor = new Color(1f, 0.6f, 0.1f, 0.95f);

        BomberSim _sim;
        RectTransform _board;
        readonly List<Image> _pool = new List<Image>();
        Text _hud, _banner;

        void Awake()
        {
            BuildUi();
            _sim = new BomberSim();
            _sim.Reset();
            Debug.Log("[Bomber] started (P1 vs P2)");
        }

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
            _board = UiFactory.StretchPanel(canvas.transform, "board", new Color(0.12f, 0.12f, 0.16f, 0.5f),
                new Vector2(0.32f, 0.10f), new Vector2(0.68f, 0.84f));

            _hud = UiFactory.Label(canvas.transform, "hud", 24, TextAnchor.MiddleCenter,
                new Vector2(0, 0.86f), new Vector2(1, 0.99f));
            _hud.text = "爆爆王・對戰 — 炸死對手者勝！　P1(藍) vs P2(橘)";

            _banner = UiFactory.Label(canvas.transform, "banner", 44, TextAnchor.MiddleCenter,
                new Vector2(0, 0.4f), new Vector2(1, 0.6f));
            _banner.color = new Color(1f, 0.85f, 0.2f);
            _banner.text = "";

            UiFactory.Label(canvas.transform, "ctrl", 16, TextAnchor.MiddleCenter,
                new Vector2(0, 0.01f), new Vector2(1, 0.09f)).text =
                "P1: WASD 移動 / 空白鍵 放炸彈     P2: ←↑↓→ / Enter 放炸彈     |     R: 重來   1: 回單人";
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.Alpha1)) { SceneManager.LoadScene("Slice"); return; }
            float dt = Time.deltaTime;

            if (!_sim.IsOver)
            {
                // P1：WASD + 空白鍵
                if (Input.GetKeyDown(KeyCode.W)) _sim.Move(0, 0, 1);
                else if (Input.GetKeyDown(KeyCode.S)) _sim.Move(0, 0, -1);
                else if (Input.GetKeyDown(KeyCode.A)) _sim.Move(0, -1, 0);
                else if (Input.GetKeyDown(KeyCode.D)) _sim.Move(0, 1, 0);
                if (Input.GetKeyDown(KeyCode.Space)) _sim.PlaceBomb(0);

                // P2：方向鍵 + Enter
                if (Input.GetKeyDown(KeyCode.UpArrow)) _sim.Move(1, 0, 1);
                else if (Input.GetKeyDown(KeyCode.DownArrow)) _sim.Move(1, 0, -1);
                else if (Input.GetKeyDown(KeyCode.LeftArrow)) _sim.Move(1, -1, 0);
                else if (Input.GetKeyDown(KeyCode.RightArrow)) _sim.Move(1, 1, 0);
                if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) _sim.PlaceBomb(1);

                _sim.Tick(dt);
                if (_sim.IsOver)
                {
                    _banner.text = _sim.Winner == -1 ? "平手！\n按 R 再戰　按 1 回單人"
                                                      : $"P{_sim.Winner} WIN!\n按 R 再戰　按 1 回單人";
                }
            }
            else if (Input.GetKeyDown(KeyCode.R)) { _sim.Reset(); _banner.text = ""; }

            Render();
        }

        void Render()
        {
            var rect = _board.rect;
            float cell = Mathf.Min(rect.width / _sim.Width, rect.height / _sim.Height);
            float ox = (rect.width - cell * _sim.Width) * 0.5f;
            float oy = (rect.height - cell * _sim.Height) * 0.5f;
            var size = new Vector2(Mathf.Max(1, cell - 1f), Mathf.Max(1, cell - 1f));
            int idx = 0;

            // 棋盤格
            for (int x = 0; x < _sim.Width; x++)
                for (int y = 0; y < _sim.Height; y++)
                {
                    Color c;
                    if (_sim.IsBlast(x, y)) c = BlastColor;
                    else if (_sim.Grid[x, y] == BomberSim.Tile.Solid) c = SolidColor;
                    else if (_sim.Grid[x, y] == BomberSim.Tile.Brick) c = BrickColor;
                    else continue; // 空格不畫
                    var b = Get(idx++, c);
                    b.rectTransform.sizeDelta = size;
                    b.rectTransform.anchoredPosition = new Vector2(ox + x * cell, oy + y * cell);
                }

            // 炸彈
            foreach (var bomb in _sim.Bombs)
            {
                var b = Get(idx++, BombColor);
                b.rectTransform.sizeDelta = size * 0.8f;
                b.rectTransform.anchoredPosition = new Vector2(ox + bomb.X * cell + cell * 0.1f, oy + bomb.Y * cell + cell * 0.1f);
            }

            // 玩家
            for (int i = 0; i < 2; i++)
            {
                if (!_sim.Players[i].Alive) continue;
                var b = Get(idx++, i == 0 ? P1Color : P2Color);
                b.rectTransform.sizeDelta = size * 0.85f;
                b.rectTransform.anchoredPosition = new Vector2(
                    ox + _sim.Players[i].X * cell + cell * 0.075f,
                    oy + _sim.Players[i].Y * cell + cell * 0.075f);
            }

            for (int i = idx; i < _pool.Count; i++) _pool[i].enabled = false;
        }

        Image Get(int index, Color color)
        {
            while (_pool.Count <= index) _pool.Add(UiFactory.Block(_board, color));
            var img = _pool[index];
            img.color = color;
            img.enabled = true;
            return img;
        }
    }
}
