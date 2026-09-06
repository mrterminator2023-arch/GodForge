using NeoModLoader.AndroidCompatibilityModule;
using NeoModLoader.constants;
using NeoModLoader.utils;
using static NeoModLoader.AndroidCompatibilityModule.IL2CPPHelper;
using UnityEngine;
using UnityEngine.UI;

namespace NeoModLoader.ui;

/// <summary>
///     Decoration: a handful of small translucent WBML logos slowly drifting and bouncing inside a rect.
///     Allocation-free Update (all state lives in preallocated arrays). Disabled by <see cref="Branding.FloatingLogos"/>.
/// </summary>
public class FloatingLogos : WrappedBehaviour
{
    private RectTransform[] _icons;
    private Vector2[] _pos;
    private Vector2[] _vel;
    private float _half_w, _half_h;

    /// <summary>
    ///     Create the decoration as the first child of <paramref name="pParent"/> (drawn behind its other children).
    /// </summary>
    /// <param name="pParent">Parent rect (e.g. a window background)</param>
    /// <param name="pWidth">Area width in UI units</param>
    /// <param name="pHeight">Area height in UI units</param>
    public static FloatingLogos Attach(Transform pParent, float pWidth, float pHeight)
    {
        if (!Branding.FloatingLogos) return null;
        GameObject root = CreateGameObject("FloatingLogos", typeof(RectTransform), typeof(FloatingLogos));
        root.transform.SetParent(pParent);
        root.transform.SetAsFirstSibling();
        root.transform.localPosition = Vector3.zero;
        root.transform.localScale = Vector3.one;
        RectTransform root_rect = root.GetComponent<RectTransform>();
        root_rect.sizeDelta = new Vector2(pWidth, pHeight);

        FloatingLogos logos = root.GetComponent<Il2CPPBehaviour>().WrappedBehaviour as FloatingLogos;
        if (logos == null) return null;
        logos.Build(root.transform, pWidth, pHeight);
        return logos;
    }

    private void Build(Transform pRoot, float pWidth, float pHeight)
    {
        int count = Branding.FloatingLogoCount;
        float size = Branding.FloatingLogoSize;
        _half_w = pWidth * 0.5f - size * 0.5f;
        _half_h = pHeight * 0.5f - size * 0.5f;
        _icons = new RectTransform[count];
        _pos = new Vector2[count];
        _vel = new Vector2[count];
        Sprite sprite = InternalResourcesGetter.GetIcon();
        for (int i = 0; i < count; i++)
        {
            GameObject icon = CreateGameObject("Logo" + i, typeof(Image));
            icon.transform.SetParent(pRoot);
            icon.transform.localScale = Vector3.one;
            RectTransform rect = icon.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(size, size);
            Image image = icon.GetComponent<Image>();
            image.sprite = sprite;
            image.raycastTarget = false;
            image.color = new Color(1f, 1f, 1f, Branding.FloatingLogoAlpha);

            _pos[i] = new Vector2(UnityEngine.Random.Range(-_half_w, _half_w), UnityEngine.Random.Range(-_half_h, _half_h));
            float angle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
            float speed = UnityEngine.Random.Range(Branding.FloatingLogoSpeedMin, Branding.FloatingLogoSpeedMax);
            _vel[i] = new Vector2(Mathf.Cos(angle) * speed, Mathf.Sin(angle) * speed);
            rect.anchoredPosition = _pos[i];
            _icons[i] = rect;
        }
    }

    private void Update()
    {
        if (_icons == null) return;
        float dt = Time.unscaledDeltaTime;
        if (dt > 0.1f) dt = 0.1f;
        for (int i = 0; i < _icons.Length; i++)
        {
            float x = _pos[i].x + _vel[i].x * dt;
            float y = _pos[i].y + _vel[i].y * dt;
            if (x > _half_w) { x = _half_w; _vel[i].x = -_vel[i].x; }
            else if (x < -_half_w) { x = -_half_w; _vel[i].x = -_vel[i].x; }
            if (y > _half_h) { y = _half_h; _vel[i].y = -_vel[i].y; }
            else if (y < -_half_h) { y = -_half_h; _vel[i].y = -_vel[i].y; }
            _pos[i].x = x;
            _pos[i].y = y;
            _icons[i].anchoredPosition = _pos[i];
        }
    }
}
