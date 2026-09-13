// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Idel Nigmatullin and GodForge contributors
// This file is part of GodForge (GFML). See LICENSE for details.

using static NeoModLoader.AndroidCompatibilityModule.IL2CPPHelper;
using UnityEngine;
using UnityEngine.UI;

namespace NeoModLoader.ui;

/// <summary>
///     Tiny runtime-generated UI skin: rounded rectangles / circles drawn into textures once and cached,
///     plus a colour palette and a few builders that cut down the GameObject boilerplate.
///     Nothing here depends on game sprites, so the look is fully ours.
/// </summary>
public static class UiSkin
{
    // ---- palette -------------------------------------------------------------------------------------------
    public static readonly Color CardBg        = Hex("#171a22", 0.90f);
    public static readonly Color CardBgOff     = Hex("#12141a", 0.90f);
    public static readonly Color PanelBg       = Hex("#10131a", 0.96f);
    public static readonly Color Accent        = Hex("#5aa9ff");
    public static readonly Color Green         = Hex("#4cd97b");
    public static readonly Color Gray          = Hex("#7c8598");
    public static readonly Color Red           = Hex("#ff5a5f");
    public static readonly Color Amber         = Hex("#ffb347");
    public static readonly Color TextPrimary   = Hex("#f2f4f8");
    public static readonly Color TextSecondary = Hex("#aab2c3");
    public static readonly Color TextDim       = Hex("#7a8294");
    public static readonly Color ButtonBg      = Hex("#262b36");

    /// <summary>Texture pixels per UI unit. UI units are scaled up a lot on phones, so draw with headroom.</summary>
    private const int K = 8;

    private static readonly Dictionary<int, Sprite> _rounded = new();
    private static readonly Dictionary<int, Sprite> _circles = new();

    /// <summary>Parse "#rrggbb" into a Color with the given alpha.</summary>
    public static Color Hex(string hex, float alpha = 1f)
    {
        int r = Convert.ToInt32(hex.Substring(1, 2), 16);
        int g = Convert.ToInt32(hex.Substring(3, 2), 16);
        int b = Convert.ToInt32(hex.Substring(5, 2), 16);
        return new Color(r / 255f, g / 255f, b / 255f, alpha);
    }

    /// <summary>Same colour with a different alpha.</summary>
    public static Color A(Color c, float alpha) => new(c.r, c.g, c.b, alpha);

    /// <summary>
    ///     White 9-sliced rounded rectangle with anti-aliased corners of <paramref name="radius"/> px.
    ///     Tint via <see cref="Image.color"/>. Use with <see cref="Image.Type.Sliced"/>.
    /// </summary>
    public static Sprite Rounded(int radius)
    {
        if (_rounded.TryGetValue(radius, out var cached) && cached != null) return cached;
        int r = radius * K;
        int size = r * 2 + 2 * K; // straight run in the middle so slicing has something to stretch
        Texture2D tex = new(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float alpha = 1f;
            if ((x < r || x >= size - r) && (y < r || y >= size - r))
            {
                float cx = x < r ? r : size - r;
                float cy = y < r ? r : size - r;
                float dx = x + 0.5f - cx, dy = y + 0.5f - cy;
                alpha = Mathf.Clamp01(r - Mathf.Sqrt(dx * dx + dy * dy) + 0.5f);
            }

            tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
        }

        tex.Apply();
        float b = r + 1;
        Sprite sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), K, 0,
            SpriteMeshType.FullRect, new Vector4(b, b, b, b));
        sprite.name = $"gfml_rounded_{radius}";
        _rounded[radius] = sprite;
        return sprite;
    }

    /// <summary>White anti-aliased disc of <paramref name="diameter"/> px. Tint via <see cref="Image.color"/>.</summary>
    public static Sprite Circle(int diameter)
    {
        if (_circles.TryGetValue(diameter, out var cached) && cached != null) return cached;
        int px = diameter * K;
        Texture2D tex = new(px, px, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        float r = px * 0.5f;
        for (int y = 0; y < px; y++)
        for (int x = 0; x < px; x++)
        {
            float dx = x + 0.5f - r, dy = y + 0.5f - r;
            float alpha = Mathf.Clamp01(r - Mathf.Sqrt(dx * dx + dy * dy) + 0.5f);
            tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
        }

        tex.Apply();
        Sprite sprite = Sprite.Create(tex, new Rect(0, 0, px, px), new Vector2(0.5f, 0.5f), K, 0,
            SpriteMeshType.FullRect);
        sprite.name = $"gfml_circle_{diameter}";
        _circles[diameter] = sprite;
        return sprite;
    }

    // ---- builders ------------------------------------------------------------------------------------------

    /// <summary>Plain image child (optionally with extra components, e.g. Button / TipButton).</summary>
    public static Image Img(string name, Transform parent, Sprite sprite, Color color, Vector2 pos, Vector2 size,
        bool sliced = false, bool raycast = false, params Type[] extra)
    {
        Type[] types = new Type[extra.Length + 1];
        types[0] = typeof(Image);
        Array.Copy(extra, 0, types, 1, extra.Length);
        GameObject obj = CreateGameObject(name, types);
        obj.transform.SetParent(parent);
        obj.transform.localPosition = pos;
        obj.transform.localScale = Vector3.one;
        obj.GetComponent<RectTransform>().sizeDelta = size;
        Image img = obj.GetComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.type = sliced ? Image.Type.Sliced : Image.Type.Simple;
        img.raycastTarget = raycast;
        return img;
    }

    /// <summary>Rounded rect image child.</summary>
    public static Image Rect(string name, Transform parent, int radius, Color color, Vector2 pos, Vector2 size,
        bool raycast = false, params Type[] extra)
    {
        return Img(name, parent, Rounded(radius), color, pos, size, true, raycast, extra);
    }

    /// <summary>Text child with our defaults (game font, rich text, no raycast).</summary>
    public static Text Txt(string name, Transform parent, string text, int size, Color color, Vector2 pos,
        Vector2 rect, TextAnchor anchor = TextAnchor.MiddleLeft, bool wrap = false)
    {
        GameObject obj = CreateGameObject(name, typeof(Text));
        obj.transform.SetParent(parent);
        obj.transform.localPosition = pos;
        obj.transform.localScale = Vector3.one;
        obj.GetComponent<RectTransform>().sizeDelta = rect;
        Text t = obj.GetComponent<Text>();
        t.font = LocalizedTextManager.current_font;
        t.fontSize = size;
        t.color = color;
        t.text = text;
        t.alignment = anchor;
        t.supportRichText = true;
        t.raycastTarget = false;
        t.horizontalOverflow = wrap ? HorizontalWrapMode.Wrap : HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Truncate;
        return t;
    }

    /// <summary>
    ///     Small round icon button: disc background + icon sprite. No hover tooltip: this is a touch UI.
    /// </summary>
    public static Button IconButton(string name, Transform parent, Sprite icon, Color bg, Color iconTint,
        Vector2 pos, float diameter)
    {
        Image disc = Img(name, parent, Circle(Mathf.RoundToInt(diameter)), bg, pos, new Vector2(diameter, diameter),
            false, true, typeof(Button));
        Img("Icon", disc.transform, icon, iconTint, Vector2.zero, new Vector2(diameter * 0.66f, diameter * 0.66f));
        return disc.GetComponent<Button>();
    }

    /// <summary>Rich-text helper: colour a fragment.</summary>
    public static string Col(string s, Color c)
    {
        return $"<color=#{ColorUtility.ToHtmlStringRGB(c)}>{s}</color>";
    }
}
