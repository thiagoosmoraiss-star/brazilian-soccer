using System;
using Game.UI.Presenters;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game.UI
{
    /// <summary>
    /// Basic visual identity of the menus (GAME_DESIGN §2 proposed palette, dark by default; X-95) and the building
    /// blocks of the Shell's screens. Sizes are in the panel's reference pixels (1080×1920 portrait): a touch target of
    /// <see cref="MinTouch"/> is ≥ 44 pt on a phone (ROADMAP C3).
    /// </summary>
    public static class UiKit
    {
        public const float RefWidth = 1080f, RefHeight = 1920f;
        /// <summary>44 pt ≈ 115 reference px on a ~410 dp-wide phone; rounded up.</summary>
        public const float MinTouch = 120f;

        public static readonly Color Base = Hex("#0F2A1D");
        public static readonly Color CardColor = Hex("#173B2A");
        public static readonly Color CardStrong = Hex("#1F4D37");
        public static readonly Color Surface = Hex("#F3EFE4");
        public static readonly Color Accent = Hex("#F5C518");
        public static readonly Color Positive = Hex("#2FA84F");
        public static readonly Color Negative = Hex("#D7263D");
        public static readonly Color Neutral = Hex("#8A8F87");
        public static readonly Color Ink = Hex("#0F2A1D");

        public const int TitleSize = 56, BigSize = 46, TextSize = 34, SmallSize = 28;

        public static Color Hex(string hex) => ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.magenta;

        public static Color Sign(int sign) => sign > 0 ? Positive : sign < 0 ? Negative : Neutral;

        public static Label Text(VisualElement parent, string text, int size = TextSize, Color? color = null, bool bold = false)
        {
            var l = new Label(text ?? "");
            l.style.fontSize = size;
            l.style.color = color ?? Surface;
            l.style.whiteSpace = WhiteSpace.Normal;
            l.style.flexShrink = 0;
            l.style.marginTop = l.style.marginBottom = 4;
            if (bold) l.style.unityFontStyleAndWeight = FontStyle.Bold;
            parent?.Add(l);
            return l;
        }

        /// <summary>A button of at least <see cref="MinTouch"/> height; primary = accent fill.</summary>
        public static Button Button(VisualElement parent, ButtonModel model, Action onClick, bool primary = false, int size = TextSize)
        {
            var b = new Button(onClick) { text = model.Label };
            b.SetEnabled(model.Enabled);
            b.style.minHeight = MinTouch;
            b.style.fontSize = size;
            b.style.flexShrink = 0;
            b.style.flexGrow = 1;
            b.style.marginTop = b.style.marginBottom = 8;
            b.style.marginLeft = b.style.marginRight = 6;
            b.style.paddingLeft = b.style.paddingRight = 20;
            Round(b, 14);
            b.style.borderTopWidth = b.style.borderBottomWidth = b.style.borderLeftWidth = b.style.borderRightWidth = 0;
            b.style.backgroundColor = primary ? Accent : CardStrong;
            b.style.color = primary ? Ink : Surface;
            b.style.unityFontStyleAndWeight = primary ? FontStyle.Bold : FontStyle.Normal;
            if (!model.Enabled) b.style.opacity = 0.45f;
            parent?.Add(b);
            return b;
        }

        public static VisualElement Card(VisualElement parent, Color? background = null)
        {
            var c = new VisualElement();
            c.style.backgroundColor = background ?? CardColor;
            c.style.flexShrink = 0;
            c.style.paddingLeft = c.style.paddingRight = 28;
            c.style.paddingTop = c.style.paddingBottom = 20;
            c.style.marginBottom = 20;
            Round(c, 18);
            parent?.Add(c);
            return c;
        }

        public static VisualElement Row(VisualElement parent)
        {
            var r = new VisualElement();
            r.style.flexDirection = FlexDirection.Row;
            r.style.alignItems = Align.Center;
            r.style.flexShrink = 0;
            parent?.Add(r);
            return r;
        }

        /// <summary>A fixed-width (or flexible when width ≤ 0) single-line cell of a table row.</summary>
        public static Label Cell(VisualElement row, string text, float width, int size = SmallSize, Color? color = null, bool bold = false,
            TextAnchor align = TextAnchor.MiddleCenter)
        {
            var l = new Label(text ?? "");
            l.style.fontSize = size;
            l.style.color = color ?? Surface;
            l.style.unityTextAlign = align;
            l.style.overflow = Overflow.Hidden;
            l.style.whiteSpace = WhiteSpace.NoWrap;
            if (width > 0) { l.style.width = width; l.style.flexShrink = 0; }
            else { l.style.flexGrow = 1; l.style.flexShrink = 1; l.style.flexBasis = 0; }
            if (bold) l.style.unityFontStyleAndWeight = FontStyle.Bold;
            row.Add(l);
            return l;
        }

        public static VisualElement Stripe(VisualElement parent, Color color, float width, float height)
        {
            var s = new VisualElement();
            s.style.width = width;
            s.style.height = height;
            s.style.backgroundColor = color;
            s.style.flexShrink = 0;
            Round(s, 4);
            parent?.Add(s);
            return s;
        }

        public static void Round(VisualElement e, float radius)
        {
            e.style.borderTopLeftRadius = e.style.borderTopRightRadius = e.style.borderBottomLeftRadius = e.style.borderBottomRightRadius = radius;
        }
    }
}
