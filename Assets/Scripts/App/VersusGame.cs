using System.Collections.Generic;
using PanicConsole.Minigames;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace PanicConsole.App
{
    /// <summary>本機雙人對戰（先行版）：小朋友下樓梯，左右分屏 P1 vs P2，先被頂到天花板尖刺者輸。
    /// 架構預留 Phase 3：把 P2 換成 Steam 連線對手即為正式 1v1。</summary>
    public class VersusGame : MonoBehaviour
    {
        static readonly Color P1Color = new Color(0.3f, 0.8f, 1f);
        static readonly Color P2Color = new Color(1f, 0.55f, 0.15f);
        static readonly Color PlatColor = new Color(0.55f, 0.55f, 0.6f);
        static readonly Color SpikeColor = new Color(0.9f, 0.2f, 0.25f, 0.5f);

        DownstairsSim _p1, _p2;
        RectTransform _left, _right;
        readonly List<Image> _poolL = new List<Image>();
        readonly List<Image> _poolR = new List<Image>();
        Text _hud, _banner;
        bool _over;

        void Awake()
        {
            BuildUi();
            BuildMatch();
            Debug.Log("[Versus] started (downstairs P1 vs P2)");
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

            _left = UiFactory.StretchPanel(canvas.transform, "p1",
                new Color(P1Color.r, P1Color.g, P1Color.b, 0.10f),
                new Vector2(0.02f, 0.10f), new Vector2(0.49f, 0.86f));
            _right = UiFactory.StretchPanel(canvas.transform, "p2",
                new Color(P2Color.r, P2Color.g, P2Color.b, 0.10f),
                new Vector2(0.51f, 0.10f), new Vector2(0.98f, 0.86f));

            UiFactory.Label(_left, "l", 20, TextAnchor.UpperCenter,
                new Vector2(0, 0.92f), new Vector2(1, 1f)).text = "P1　（A / D 移動）";
            UiFactory.Label(_right, "r", 20, TextAnchor.UpperCenter,
                new Vector2(0, 0.92f), new Vector2(1, 1f)).text = "P2　（← / → 移動）";

            _hud = UiFactory.Label(canvas.transform, "hud", 24, TextAnchor.MiddleCenter,
                new Vector2(0, 0.88f), new Vector2(1, 0.99f));
            _hud.text = "小朋友下樓梯・對戰 — 先被頂到天花板尖刺者輸！";

            _banner = UiFactory.Label(canvas.transform, "banner", 44, TextAnchor.MiddleCenter,
                new Vector2(0, 0.4f), new Vector2(1, 0.6f));
            _banner.color = new Color(1f, 0.85f, 0.2f);
            _banner.text = "";

            UiFactory.Label(canvas.transform, "ctrl", 16, TextAnchor.MiddleCenter,
                new Vector2(0, 0.01f), new Vector2(1, 0.09f)).text =
                "P1: A / D     P2: ← / →     |     R: 重新對戰     1: 回主畫面 (1v1)";
        }

        void BuildMatch()
        {
            _p1 = new DownstairsSim();
            _p2 = new DownstairsSim();
            _p1.OnFail += _ => EndMatch(2); // P1 掛 → P2 贏
            _p2.OnFail += _ => EndMatch(1);
            _p1.Reset();
            _p2.Reset();
            _over = false;
            _banner.text = "";
        }

        void EndMatch(int winner)
        {
            if (_over) return;
            _over = true;
            _banner.text = $"P{winner} WIN!\n按 R 再戰　按 1 回主畫面";
            Debug.Log($"[Versus] P{winner} wins");
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.Alpha1)) { SceneManager.LoadScene("Duel"); return; }

            float dt = Time.deltaTime;

            if (!_over)
            {
                if (Input.GetKeyDown(KeyCode.A)) _p1.MoveLeft();
                if (Input.GetKeyDown(KeyCode.D)) _p1.MoveRight();
                if (Input.GetKeyDown(KeyCode.LeftArrow)) _p2.MoveLeft();
                if (Input.GetKeyDown(KeyCode.RightArrow)) _p2.MoveRight();

                _p1.Tick(dt, true);
                _p2.Tick(dt, true);
            }
            else if (Input.GetKeyDown(KeyCode.R)) { BuildMatch(); }

            RenderShaft(_p1, _left, _poolL, P1Color);
            RenderShaft(_p2, _right, _poolR, P2Color);
        }

        void RenderShaft(DownstairsSim sim, RectTransform panel, List<Image> pool, Color playerColor)
        {
            var rect = panel.rect;
            float cw = rect.width / sim.Width;
            float ch = rect.height / sim.Height;
            var size = new Vector2(Mathf.Max(1, cw - 1f), Mathf.Max(1, ch - 1f));
            int idx = 0;

            // 天花板尖刺列
            for (int x = 0; x < sim.Width; x++)
            {
                var spike = GetBlock(pool, panel, idx++, SpikeColor);
                spike.rectTransform.sizeDelta = size;
                spike.rectTransform.anchoredPosition = new Vector2(x * cw, (sim.Height - 1) * ch);
            }
            // 平台（缺口不畫）
            foreach (var p in sim.Platforms)
            {
                for (int x = 0; x < sim.Width; x++)
                {
                    bool inGap = x >= p.GapCol && x < p.GapCol + sim.GapWidth;
                    if (inGap) continue;
                    var b = GetBlock(pool, panel, idx++, PlatColor);
                    b.rectTransform.sizeDelta = size;
                    b.rectTransform.anchoredPosition = new Vector2(x * cw, p.Row * ch);
                }
            }
            // 玩家
            var pl = GetBlock(pool, panel, idx++, playerColor);
            pl.rectTransform.sizeDelta = size;
            pl.rectTransform.anchoredPosition = new Vector2(sim.PlayerCol * cw, sim.PlayerRow * ch);

            for (int i = idx; i < pool.Count; i++) pool[i].enabled = false;
        }

        Image GetBlock(List<Image> pool, RectTransform panel, int index, Color color)
        {
            while (pool.Count <= index) pool.Add(UiFactory.Block(panel, color));
            var img = pool[index];
            img.color = color;
            img.enabled = true;
            return img;
        }
    }
}
