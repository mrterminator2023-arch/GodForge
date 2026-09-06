using WBML;

using static NeoModLoader.AndroidCompatibilityModule.IL2CPPHelper;
using NeoModLoader.api;
using NeoModLoader.constants;
using NeoModLoader.General;
using NeoModLoader.services;
using NeoModLoader.utils;
using UnityEngine;
using UnityEngine.UI;

namespace NeoModLoader.ui;

/// <summary>
///     List window of all mods recognized by WBML.
///     Own look: dark rounded cards with a coloured state strip, animated toggle switch, sticky header with counters,
///     friendly empty state and a compact About pill. Everything is drawn with <see cref="UiSkin"/> sprites.
/// </summary>
public class ModListWindow : AbstractListWindow<ModListWindow, IMod>
{
    private readonly Queue<IMod> to_add = new();
    private bool needRefresh;

    private Text _header_counts;
    private GameObject _empty_state;
    private Text _empty_hint;

    private void Update()
    {
        if (!IsOpened) return;
        if (needRefresh)
        {
            if (to_add.Any())
            {
                AddItemToList(to_add.Dequeue());
                return;
            }

            needRefresh = false;
        }
    }

    /// <inheritdoc cref="AbstractListWindow{T,TItem}.Init" />
    protected override void Init()
    {
        // Window background size (fallback to the known "windows/empty" size if layout is not ready yet)
        Rect bg_rect = BackgroundTransform.GetComponent<RectTransform>().rect;
        float bg_w = bg_rect.width > 50 ? bg_rect.width : 250f;
        float bg_h = bg_rect.height > 50 ? bg_rect.height : 330f;

        // Decoration: drifting logos behind the list
        FloatingLogos.Attach(BackgroundTransform, bg_w - 10, bg_h - 10);

        // Leave room under the sticky header
        VerticalLayoutGroup layout = ContentTransform.GetComponent<VerticalLayoutGroup>();
        layout.spacing = 6;
        layout.padding = new RectOffset(30, 30, 28, 14);

        BuildHeader();
        BuildEmptyState();
        BuildAbout(bg_w, bg_h);
    }

    // ---- static chrome -------------------------------------------------------------------------------------

    private void BuildHeader()
    {
        // Scroll View top edge is at y = -6 + 135 = 129 (see AbstractListWindow.CreateAndInit)
        Image bar = UiSkin.Rect("Header", BackgroundTransform, 5, UiSkin.PanelBg, new Vector2(0, 120),
            new Vector2(204, 17));
        bar.transform.SetAsLastSibling();

        UiSkin.Img("Logo", bar.transform, InternalResourcesGetter.GetIcon(), Color.white, new Vector2(-93, 0),
            new Vector2(11, 11));
        UiSkin.Txt("Title", bar.transform, $"<b>{Branding.Name}</b>", 7, UiSkin.TextPrimary, new Vector2(-45, 0),
            new Vector2(80, 14));
        _header_counts = UiSkin.Txt("Counts", bar.transform, "", 6, UiSkin.TextSecondary, new Vector2(45, 0),
            new Vector2(100, 14), TextAnchor.MiddleRight);
    }

    private void BuildEmptyState()
    {
        _empty_state = CreateGameObject("EmptyState", typeof(RectTransform));
        _empty_state.transform.SetParent(BackgroundTransform);
        _empty_state.transform.localPosition = new Vector3(0, 0);
        _empty_state.transform.localScale = Vector3.one;
        _empty_state.GetComponent<RectTransform>().sizeDelta = new Vector2(200, 120);

        UiSkin.Img("BigLogo", _empty_state.transform, InternalResourcesGetter.GetIcon(),
            new Color(1f, 1f, 1f, 0.35f), new Vector2(0, 30), new Vector2(48, 48));
        UiSkin.Txt("Title", _empty_state.transform, LM.Get("wbml_no_mods_title"), 8, UiSkin.TextPrimary,
            new Vector2(0, -6), new Vector2(190, 14), TextAnchor.MiddleCenter);
        _empty_hint = UiSkin.Txt("Hint", _empty_state.transform, "", 5, UiSkin.TextDim, new Vector2(0, -30),
            new Vector2(180, 30), TextAnchor.UpperCenter, true);
        _empty_state.SetActive(false);
    }

    private void BuildAbout(float bg_w, float bg_h)
    {
        float y = -bg_h * 0.5f + 12;

        // pill button "WBML v0.1.0"
        Image pill = UiSkin.Rect("ModLoaderButton", BackgroundTransform, 6, UiSkin.A(UiSkin.Accent, 0.22f),
            new Vector2(0, y), new Vector2(78, 12), true, typeof(Button), typeof(TipButton));
        pill.transform.SetAsLastSibling();
        UiSkin.Img("Logo", pill.transform, InternalResourcesGetter.GetIcon(), Color.white, new Vector2(-31, 0),
            new Vector2(8, 8));
        UiSkin.Txt("Label", pill.transform, $"{Branding.Name} v{Branding.Version}  <color=#5aa9ff>i</color>", 5,
            UiSkin.TextPrimary, new Vector2(5, 0), new Vector2(64, 12), TextAnchor.MiddleCenter);

        TipButton tip = pill.GetComponent<TipButton>();
        tip.textOnClick = Branding.Name + " v" + Branding.Version;
        foreach (var lang in LocalizedTextManager.getAllLanguages())
            LM.Add(lang, "WBMLCommit", $"commit\n{InternalResourcesGetter.GetCommit()}");
        tip.text_description_2 = "WBMLCommit";
        tip.textOnClickDescription = "WBML About";

        // credits card above the pill
        Image about_panel = UiSkin.Rect("AboutPanel", BackgroundTransform, 7, UiSkin.PanelBg,
            new Vector2(0, y + 6 + 52), new Vector2(bg_w - 30, 98), true);
        about_panel.transform.SetAsLastSibling();
        UiSkin.Rect("Stripe", about_panel.transform, 2, UiSkin.Accent, new Vector2(0, 45), new Vector2(bg_w - 60, 2));
        Text about_text = UiSkin.Txt("Text", about_panel.transform, Branding.AboutText, 6, UiSkin.TextSecondary,
            new Vector2(0, -3), new Vector2(bg_w - 46, 86), TextAnchor.MiddleCenter, true);
        about_text.resizeTextForBestFit = true;
        about_text.resizeTextMinSize = 4;
        about_text.resizeTextMaxSize = 6;
        about_panel.gameObject.SetActive(false);

        pill.GetComponent<Button>().onClick.AddListener(() =>
        {
            about_panel.gameObject.SetActive(!about_panel.gameObject.activeSelf);
        });
    }

    // ---- data ----------------------------------------------------------------------------------------------

    /// <inheritdoc cref="AbstractListWindow{T,TItem}.OnNormalEnable" />
    public override void OnNormalEnable()
    {
        needRefresh = true;
        ClearList();
        foreach (var loaded_mod in WorldBoxMod.LoadedMods)
        {
            to_add.Enqueue(loaded_mod);
        }

        foreach (var mod in WorldBoxMod.AllRecognizedMods.Keys)
        {
            if (WorldBoxMod.AllRecognizedMods[mod] == ModState.LOADED) continue;
            var virtual_mod = new VirtualMod();
            virtual_mod.OnLoad(mod, null);
            to_add.Enqueue(virtual_mod);
        }

        RefreshHeader();
    }

    /// <summary>Recount states for the header and toggle the empty state.</summary>
    internal void RefreshHeader()
    {
        int enabled = 0, disabled = 0, failed = 0;
        foreach (var state in WorldBoxMod.AllRecognizedMods.Values)
        {
            switch (state)
            {
                case ModState.LOADED: enabled++; break;
                case ModState.DISABLED: disabled++; break;
                case ModState.FAILED: failed++; break;
            }
        }

        int total = enabled + disabled + failed;
        if (_header_counts != null)
        {
            string s = UiSkin.Col("●", UiSkin.Green) + enabled + "   " + UiSkin.Col("●", UiSkin.Gray) + disabled;
            if (failed > 0) s += "   " + UiSkin.Col("●", UiSkin.Red) + failed;
            _header_counts.text = s;
        }

        if (_empty_state != null)
        {
            _empty_state.SetActive(total == 0);
            if (total == 0 && _empty_hint != null)
                _empty_hint.text = LM.Get("wbml_no_mods_hint") + "\n" + UiSkin.Col(Paths.ModsPath, UiSkin.Accent);
        }
    }

    // ---- item prefab ---------------------------------------------------------------------------------------

    private const float CardW = 200f;
    private const float CardH = 60f;
    private const float ButtonX = 90f;
    private const float ButtonD = 17f;

    /// <inheritdoc cref="AbstractListWindow{T,TItem}.CreateItemPrefab" />
    protected override AbstractListWindowItem<IMod> CreateItemPrefab()
    {
        GameObject obj = CreateGameObject("ModListItemPrefab", typeof(Image), typeof(CanvasGroup), typeof(ModListItem));
        obj.SetActive(false);
        obj.transform.SetParent(WorldBoxMod.Transform);
        obj.GetComponent<RectTransform>().sizeDelta = new Vector2(0, CardH);
        Image bg = obj.GetComponent<Image>();
        bg.sprite = UiSkin.Rounded(7);
        bg.type = Image.Type.Sliced;
        bg.color = UiSkin.CardBg;
        bg.raycastTarget = false;

        // coloured state strip on the left
        UiSkin.Rect("Accent", obj.transform, 2, UiSkin.Green, new Vector2(-96, 0), new Vector2(3, CardH - 16));

        // icon in a tinted rounded frame
        Image frame = UiSkin.Rect("IconFrame", obj.transform, 6, UiSkin.A(UiSkin.Green, 0.30f), new Vector2(-76, 0),
            new Vector2(40, 40));
        UiSkin.Img("Icon", frame.transform, InternalResourcesGetter.GetIcon(), Color.white, Vector2.zero,
            new Vector2(32, 32));

        // texts
        UiSkin.Txt("Name", obj.transform, "", 7, UiSkin.TextPrimary, new Vector2(-8, 18), new Vector2(92, 12));
        UiSkin.Txt("Author", obj.transform, "", 5, UiSkin.TextSecondary, new Vector2(-8, 8), new Vector2(92, 9));
        UiSkin.Txt("Desc", obj.transform, "", 5, UiSkin.TextDim, new Vector2(-8, -4), new Vector2(92, 14),
            TextAnchor.UpperLeft, true);

        // status pill + type badge
        Image status = UiSkin.Rect("Status", obj.transform, 4, UiSkin.A(UiSkin.Green, 0.22f), new Vector2(-18, -21),
            new Vector2(72, 9));
        UiSkin.Txt("Text", status.transform, "", 5, UiSkin.Green, Vector2.zero, new Vector2(72, 9),
            TextAnchor.MiddleCenter);
        Image badge = UiSkin.Rect("Badge", obj.transform, 4, UiSkin.A(UiSkin.Accent, 0.18f), new Vector2(30, -21),
            new Vector2(22, 9));
        UiSkin.Txt("Text", badge.transform, "DLL", 5, UiSkin.Accent, Vector2.zero, new Vector2(22, 9),
            TextAnchor.MiddleCenter);

        // toggle switch (track + knob), the whole track is the button
        Image track = UiSkin.Rect("Toggle", obj.transform, 6, UiSkin.Green, new Vector2(64, 0), new Vector2(28, 13),
            true, typeof(Button), typeof(TipButton));
        track.GetComponent<TipButton>().type = "normal";
        UiSkin.Img("Knob", track.transform, UiSkin.Circle(9), Color.white, new Vector2(7.5f, 0), new Vector2(9, 9));

        // small round action buttons
        UiSkin.IconButton("Configure", obj.transform, Resources.Load<Sprite>("ui/icons/iconoptions"),
            UiSkin.ButtonBg, UiSkin.TextPrimary, new Vector2(ButtonX, 20), ButtonD, "ModConfigure Title");
        UiSkin.IconButton("OpenFolder", obj.transform, SpriteTextureLoader.getSprite("ui/icons/iconCustomWorld"),
            UiSkin.ButtonBg, UiSkin.TextPrimary, new Vector2(ButtonX, 0), ButtonD, "OpenFolder Title");
        UiSkin.IconButton("Website", obj.transform, Resources.Load<Sprite>("ui/icons/actor_traits/iconcommunity"),
            UiSkin.ButtonBg, UiSkin.TextPrimary, new Vector2(ButtonX, -20), ButtonD, "ModCommunity Title");

        return obj.GetWrappedComponent<ModListItem>();
    }

    /// <summary>
    ///     A single card for <see cref="ModListWindow" />. Owns its little animations (appear, toggle knob, pulse).
    /// </summary>
    public class ModListItem : AbstractListWindowItem<IMod>
    {
        private IMod _mod;
        private ModDeclare _declare;

        private CanvasGroup _group;
        private Image _bg, _accent, _frame, _icon, _track, _knob, _status_bg;
        private Text _name, _author, _desc, _status, _badge;
        private TipButton _toggle_tip;
        private Button _configure, _folder, _website;
        private IConfigurable _configurable;

        private bool _wired;
        private float _appear;          // 0..1 slide/fade in
        private float _knob_x;          // current knob position
        private float _knob_target;
        private Color _track_color, _track_target;
        private float _pop;             // icon pop timer
        private bool _pulse;            // failed mods pulse their strip
        private float _time;

        private void Wire()
        {
            if (_wired) return;
            _wired = true;
            _group = gameObject.GetComponent<CanvasGroup>();
            _bg = gameObject.GetComponent<Image>();
            _accent = transform.Find("Accent").GetComponent<Image>();
            _frame = transform.Find("IconFrame").GetComponent<Image>();
            _icon = transform.Find("IconFrame/Icon").GetComponent<Image>();
            _name = transform.Find("Name").GetComponent<Text>();
            _author = transform.Find("Author").GetComponent<Text>();
            _desc = transform.Find("Desc").GetComponent<Text>();
            _status_bg = transform.Find("Status").GetComponent<Image>();
            _status = transform.Find("Status/Text").GetComponent<Text>();
            _badge = transform.Find("Badge/Text").GetComponent<Text>();
            _track = transform.Find("Toggle").GetComponent<Image>();
            _toggle_tip = _track.GetComponent<TipButton>();
            _knob = transform.Find("Toggle/Knob").GetComponent<Image>();
            _configure = transform.Find("Configure").GetComponent<Button>();
            _folder = transform.Find("OpenFolder").GetComponent<Button>();
            _website = transform.Find("Website").GetComponent<Button>();

            _track.GetComponent<Button>().onClick.AddListener(OnToggle);
            _configure.onClick.AddListener(() => ModConfigureWindow.ShowWindow(_configurable?.GetConfig()));
            _folder.onClick.AddListener(() => { if (_declare != null) Application.OpenURL(_declare.FolderPath); });
            _website.onClick.AddListener(() =>
            {
                string url = _mod?.GetUrl();
                if (!string.IsNullOrEmpty(url)) Application.OpenURL(url);
            });
        }

        /// <inheritdoc cref="AbstractListWindowItem{TItem}.Setup" />
        /// <param name="mod">The mod to display</param>
        public override void Setup(IMod mod)
        {
            Wire();
            _mod = mod;
            _declare = mod.GetDeclaration();

            string mod_name = _declare.GetDisplayName();
            string mod_author = _declare.GetDisplayAuthor();
            string mod_desc = _declare.GetDisplayDesc();

            string prefix = _declare.ModType == ModTypeEnum.BEPINEX ? "[BepInEx] " : "";
            _name.text = $"<b>{prefix}{mod_name}</b>  <size=5>{UiSkin.Col("v" + _declare.Version, UiSkin.TextDim)}</size>";
            _author.text = string.IsNullOrEmpty(mod_author) ? "" : "by " + mod_author;
            _desc.text = mod_desc ?? "";

            _badge.text = _declare.ModType switch
            {
                ModTypeEnum.COMPILED_NEOMOD => "DLL",
                ModTypeEnum.NEOMOD => "SRC",
                ModTypeEnum.RESOURCE_PACK => "PACK",
                ModTypeEnum.BEPINEX => "BEPX",
                _ => "MOD"
            };

            Sprite sprite = null;
            if (!string.IsNullOrEmpty(_declare.IconPath) &&
                File.Exists(Path.Combine(_declare.FolderPath, _declare.IconPath)))
            {
                sprite = SpriteLoadUtils.LoadSingleSprite(Path.Combine(_declare.FolderPath, _declare.IconPath));
            }

            _icon.sprite = sprite != null ? sprite : InternalResourcesGetter.GetIcon();

            _configurable = mod.GetGameObject()?.GetWrappedComponent<IConfigurable>();
            _configure.gameObject.SetActive(_configurable != null);
            _website.gameObject.SetActive(!string.IsNullOrEmpty(mod.GetUrl()));
            // Opening a folder makes no sense on a phone
            _folder.gameObject.SetActive(!Config.isAndroid);
            StackButtons();

            // appear animation: start hidden, scaled down
            _appear = 0f;
            _time = 0f;
            _pop = 0f;
            if (_group != null) _group.alpha = 0f;
            transform.localScale = new Vector3(0.94f, 0.94f, 1f);

            RefreshState(true);
        }

        /// <summary>Lay the visible action buttons out in a column at the right edge, centred vertically.</summary>
        private void StackButtons()
        {
            var visible = new List<Button>();
            foreach (var b in new[] { _configure, _folder, _website })
                if (b.gameObject.activeSelf) visible.Add(b);
            float step = ButtonD + 3f;
            float top = (visible.Count - 1) * step * 0.5f;
            for (int i = 0; i < visible.Count; i++)
                visible[i].transform.localPosition = new Vector3(ButtonX, top - i * step);
        }

        private void OnToggle()
        {
            if (_declare == null) return;
            if (ModInfoUtils.isModDisabled(_declare.UID))
                ModCompileLoadService.TryEnableMod(_declare);
            else
                ModCompileLoadService.DisableMod(_declare);

            _pop = 1f;
            RefreshState(false);
            ModListWindow.Instance?.RefreshHeader();
        }

        private void RefreshState(bool instant)
        {
            ModState state = WorldBoxMod.AllRecognizedMods[_declare];
            bool disabled_next = ModInfoUtils.isModDisabled(_declare.UID);
            bool on = !disabled_next;

            Color state_color = state switch
            {
                ModState.LOADED => UiSkin.Green,
                ModState.FAILED => UiSkin.Red,
                _ => UiSkin.Gray
            };
            string state_text = state switch
            {
                ModState.LOADED => LM.Get("wbml_state_enabled"),
                ModState.FAILED => LM.Get("wbml_state_failed"),
                _ => LM.Get("wbml_state_disabled")
            };

            // pending change (takes effect after restart) is shown in amber
            bool pending = state == ModState.LOADED && disabled_next || state == ModState.DISABLED && !disabled_next;
            if (pending)
            {
                state_color = UiSkin.Amber;
                state_text = LM.Get(disabled_next ? "wbml_pending_off" : "wbml_pending_on");
            }

            _pulse = state == ModState.FAILED;
            _accent.color = state_color;
            _frame.color = UiSkin.A(state_color, 0.30f);
            _status_bg.color = UiSkin.A(state_color, 0.22f);
            _status.color = state_color;
            _status.text = state_text;
            _bg.color = on ? UiSkin.CardBg : UiSkin.CardBgOff;
            _icon.color = state == ModState.FAILED ? UiSkin.A(UiSkin.Red, 0.9f) : on ? Color.white : UiSkin.A(Color.white, 0.55f);
            _name.color = on ? UiSkin.TextPrimary : UiSkin.TextSecondary;

            _knob_target = on ? 7.5f : -7.5f;
            _track_target = on ? (pending ? UiSkin.Amber : UiSkin.Green) : UiSkin.A(UiSkin.Gray, 0.55f);
            if (instant)
            {
                _knob_x = _knob_target;
                _track_color = _track_target;
                _knob.transform.localPosition = new Vector3(_knob_x, 0);
                _track.color = _track_color;
            }

            if (state == ModState.FAILED)
            {
                _toggle_tip.textOnClick = "ModLoadFailed Title";
                _toggle_tip.textOnClickDescription = "ModLoadFailed Description";
                _toggle_tip.text_description_2 = _declare.FailReason.ToString();
            }
            else
            {
                _toggle_tip.textOnClick = "ToggleMod Title";
                _toggle_tip.textOnClickDescription = disabled_next ? "ModDisabled Description" : "ModEnabled Description";
                _toggle_tip.text_description_2 = "";
            }
        }

        private void Update()
        {
            if (_declare == null) return;
            float dt = Time.unscaledDeltaTime;
            if (dt > 0.1f) dt = 0.1f;
            _time += dt;

            // appear
            if (_appear < 1f)
            {
                _appear = Mathf.Min(1f, _appear + dt * 5f);
                float e = 1f - (1f - _appear) * (1f - _appear); // ease-out
                if (_group != null) _group.alpha = e;
                float s = 0.94f + 0.06f * e;
                transform.localScale = new Vector3(s, s, 1f);
            }

            // toggle knob + track colour glide
            float k = 1f - Mathf.Exp(-dt * 18f);
            if (Mathf.Abs(_knob_x - _knob_target) > 0.01f)
            {
                _knob_x = Mathf.Lerp(_knob_x, _knob_target, k);
                _knob.transform.localPosition = new Vector3(_knob_x, 0);
            }

            if (_track_color != _track_target)
            {
                _track_color = Color.Lerp(_track_color, _track_target, k);
                _track.color = _track_color;
            }

            // icon pop
            if (_pop > 0f)
            {
                _pop = Mathf.Max(0f, _pop - dt * 4f);
                float s = 1f + 0.18f * Mathf.Sin(_pop * Mathf.PI);
                _frame.transform.localScale = new Vector3(s, s, 1f);
            }

            // failed: breathe the strip
            if (_pulse)
            {
                float a = 0.55f + 0.45f * (0.5f + 0.5f * Mathf.Sin(_time * 4f));
                _accent.color = UiSkin.A(UiSkin.Red, a);
            }
        }
    }
}
