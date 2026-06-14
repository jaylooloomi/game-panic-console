using UnityEngine;
using UnityEngine.UI;

namespace PanicConsole.App
{
    /// <summary>程式化建立 uGUI 元件的小工具（切片用，不依賴 prefab / 手動拉場景）。</summary>
    public static class UiFactory
    {
        static Sprite _rounded;
        /// <summary>Unity 內建圓角 9-slice sprite，讓方塊/面板呈圓角而非硬方塊。</summary>
        public static Sprite Rounded()
        {
            if (_rounded == null) _rounded = Resources.GetBuiltinResource<Sprite>("UI/Skin/UISprite.psd");
            return _rounded;
        }

        public static Font BuiltinFont()
        {
            // Unity 2022+/6 內建字型（Arial.ttf 已移除）
            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        public static RectTransform StretchPanel(Transform parent, string name, Color color,
            Vector2 anchorMin, Vector2 anchorMax)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            var img = go.GetComponent<Image>();
            img.color = color;
            img.sprite = Rounded();
            img.type = Image.Type.Sliced;
            return rt;
        }

        /// <summary>建立一個以左下角為基準點的圓角方塊 Image（給遊戲格子用）。</summary>
        public static Image Block(Transform parent, Color color)
        {
            var go = new GameObject("block", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.zero;
            rt.pivot = Vector2.zero;
            var img = go.GetComponent<Image>();
            img.color = color;
            img.sprite = Rounded();
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = 2.5f; // 圓角比例（數值大→角較小，適合小格子）
            return img;
        }

        /// <summary>加上描邊（控制台螢幕邊框感）。</summary>
        public static void AddOutline(GameObject go, Color color, float dist = 2f)
        {
            var o = go.AddComponent<Outline>();
            o.effectColor = color;
            o.effectDistance = new Vector2(dist, -dist);
        }

        public static Text Label(Transform parent, string name, int fontSize, TextAnchor anchor,
            Vector2 anchorMin, Vector2 anchorMax)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            var t = go.GetComponent<Text>();
            var font = BuiltinFont();
            if (font == null) Debug.LogError("[UiFactory] 內建字型 LegacyRuntime.ttf 找不到，文字將不顯示");
            t.font = font;
            t.fontSize = fontSize;
            t.alignment = anchor;
            t.color = Color.white;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }
    }
}
