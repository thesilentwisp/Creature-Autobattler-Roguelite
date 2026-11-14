using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace CreatureRun
{
    public sealed partial class PrototypeGame : MonoBehaviour
    {
        const float Width = 480, Height = 854;
        [SerializeField] BattleBalance balance;
        [SerializeField] Texture2D expeditionMap = null;
        [SerializeField] Texture2D creaturePortraits = null;
        [SerializeField] Font interfaceFont = null;

        static readonly Color Ink = Hex(0x101719), PanelColor = Hex(0x192627), Tile = Hex(0x223132);
        static readonly Color Border = Hex(0x40544E), TextColor = Hex(0xF5EDDD), Muted = Hex(0xABBDB4);
        static readonly Color Gold = Hex(0xF0BD72), Green = Hex(0xA1D2A1), Violet = Hex(0xBFA8EA), Enemy = Hex(0xF19180);
        static readonly Color Night = Hex(0x222333);

        RunState run;
        RectTransform screen, content;
        RectTransform overlay;
        Font font;
        Texture2D disc, ring, shade;
        int slotMenu = -1;
        bool enemyPreview;
        bool inspectingBuild;
        int speed = 1;
        float accumulator, resultHold;

        void Awake()
        {
            if (balance == null) balance = ScriptableObject.CreateInstance<BattleBalance>();
            font = interfaceFont != null ? interfaceFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            disc = RadialTexture(false);
            ring = RadialTexture(true);
            shade = ShadeTexture();
            var canvasObject = new GameObject("Portrait interface", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var backdrop = Panel(canvasObject.GetComponent<RectTransform>(), "Letterbox", default, Ink);
            backdrop.anchorMin = Vector2.zero;
            backdrop.anchorMax = Vector2.one;
            backdrop.offsetMin = backdrop.offsetMax = Vector2.zero;
            screen = Box(backdrop, "Portrait viewport", new Rect(0, 0, Width, Height));
            screen.anchorMin = screen.anchorMax = new Vector2(.5f, .5f);
            screen.pivot = new Vector2(.5f, .5f);
            screen.anchoredPosition = Vector2.zero;
            if (EventSystem.current == null)
            {
                var eventSystem = new GameObject("Touch and mouse", typeof(EventSystem), typeof(InputSystemUIInputModule));
                eventSystem.transform.SetParent(transform, false);
            }
            NewRun();
        }

        void OnDestroy()
        {
            if (disc != null) Destroy(disc);
            if (ring != null) Destroy(ring);
            if (shade != null) Destroy(shade);
            if (flameTongue != null) Destroy(flameTongue);
        }

        void NewRun()
        {
            run = new RunState(balance, Guid.NewGuid().GetHashCode());
            speed = 1;
            PreparePlanning();
        }

        void PreparePlanning()
        {
            enemyPreview = false;
            slotMenu = -1;
            inspectingBuild = false;
            Render();
        }

        void Update()
        {
            Rect safe = Screen.safeArea;
            float scale = Mathf.Min(safe.width / Width, safe.height / Height);
            screen.localScale = new Vector3(scale, scale, 1);
            screen.anchoredPosition = safe.center - new Vector2(Screen.width / 2f, Screen.height / 2f);

            if (run.Phase != RunPhase.Combat) return;
            if (run.Battle.Result == BattleResult.Fighting)
            {
                accumulator += Mathf.Min(Time.unscaledDeltaTime, .15f) * speed;
                while (accumulator >= balance.step && run.Battle.Result == BattleResult.Fighting)
                {
                    accumulator -= balance.step;
                    run.Battle.Step();
                    RecordCues();
                }
            }
            else
            {
                resultHold -= Time.unscaledDeltaTime;
                if (resultHold <= 0)
                {
                    run.ResolveBattle();
                    Render();
                    return;
                }
            }
            UpdateBattleView();
        }

        void Clear(ref RectTransform item)
        {
            if (item == null) return;
            item.gameObject.SetActive(false);
            Destroy(item.gameObject);
            item = null;
        }

        void Render()
        {
            Clear(ref overlay);
            Clear(ref content);
            content = Panel(screen, "Screen", new Rect(0, 0, Width, Height), Ink);
            switch (run.Phase)
            {
                case RunPhase.Planning: DrawPlanning(); break;
                case RunPhase.Combat: DrawBattle(); break;
                case RunPhase.Draft: DrawDraft(); break;
                case RunPhase.Recruit: DrawRecruit(); break;
                default: DrawEnding(); break;
            }
        }

        void Header(string subtitle, string title)
        {
            Label(content, new Rect(22, 17, 320, 18), subtitle, 11, Gold, bold: true);
            Label(content, new Rect(22, 37, 428, 37), title, 31, TextColor, bold: true);
            Panel(content, "Header rule", new Rect(22, 83, 436, 1), Border);
        }

        static Color Hex(int color) => new Color32((byte)(color >> 16), (byte)(color >> 8), (byte)color, 255);
        static Color Alpha(Color color, float a) { color.a = a; return color; }

        static RectTransform Box(RectTransform parent, string name, Rect rect)
        {
            var box = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            box.SetParent(parent, false);
            box.anchorMin = box.anchorMax = new Vector2(0, 1);
            box.pivot = new Vector2(0, 1);
            SetRect(box, rect);
            return box;
        }

        static void SetRect(RectTransform box, Rect rect)
        {
            box.anchoredPosition = new Vector2(rect.x, -rect.y);
            box.sizeDelta = rect.size;
        }

        static RectTransform Panel(RectTransform parent, string name, Rect rect, Color color)
        {
            var box = Box(parent, name, rect);
            var image = box.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return box;
        }

        static RectTransform Bordered(RectTransform parent, string name, Rect rect, Color fill, Color edge)
        {
            var box = Panel(parent, name, rect, edge);
            return Panel(box, "Fill", new Rect(1, 1, rect.width - 2, rect.height - 2), fill);
        }

        Text Label(RectTransform parent, Rect rect, string value, int size, Color color,
            TextAnchor anchor = TextAnchor.MiddleLeft, bool bold = false, bool wrap = false)
        {
            var label = Box(parent, value.Length == 0 ? "Label" : value, rect).gameObject.AddComponent<Text>();
            label.font = font;
            label.fontSize = size;
            label.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            label.color = color;
            label.alignment = anchor;
            label.text = value;
            label.horizontalOverflow = wrap ? HorizontalWrapMode.Wrap : HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.raycastTarget = false;
            return label;
        }

        RectTransform Control(RectTransform parent, Rect rect, string name, Action press, Color fill, Color edge)
        {
            var face = Bordered(parent, name, rect, fill, edge);
            var image = face.GetComponent<Image>();
            image.raycastTarget = press != null;
            if (press == null) return face;
            var button = face.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.highlightedColor = new Color(.84f, .92f, .88f);
            colors.pressedColor = new Color(.68f, .78f, .71f);
            colors.selectedColor = Color.white;
            button.colors = colors;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.onClick.AddListener(() => press());
            return face;
        }

        void ButtonLabel(RectTransform parent, Rect rect, string title, Action press, bool primary = false)
        {
            var button = Control(parent, rect, title, press, press == null ? Tile : primary ? Gold : PanelColor, primary && press != null ? Gold : Border);
            Label(button, new Rect(0, 0, rect.width - 2, rect.height - 2), title, 14,
                press == null ? Muted : primary ? Ink : TextColor, TextAnchor.MiddleCenter, true);
        }

        static RawImage Texture(RectTransform parent, string name, Rect rect, Texture texture, Color color)
        {
            var image = Box(parent, name, rect).gameObject.AddComponent<RawImage>();
            image.texture = texture;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        RawImage Portrait(RectTransform parent, Rect rect, Creature creature, Color? tint = null)
        {
            if (creaturePortraits == null)
            {
                Texture(parent, "Creature token", rect, disc, ColorOf(creature));
                Label(parent, rect, RunContent.Name(creature).Substring(0, 1), 16, Ink, TextAnchor.MiddleCenter, true);
                return Texture(parent, RunContent.Name(creature), rect, null, Color.clear);
            }
            const float cellWidth = .25f, cellHeight = .25f;
            float imageAspect = creaturePortraits.width / (float)creaturePortraits.height;
            float width = Mathf.Min(rect.width, rect.height * imageAspect);
            float height = width / imageAspect;
            var image = Texture(parent, RunContent.Name(creature), new Rect(rect.center.x - width / 2, rect.center.y - height / 2, width, height),
                creaturePortraits, tint ?? Color.white);
            int cell = (int)creature;
            image.uvRect = new Rect(cell % 4 * cellWidth, (3 - cell / 4) * cellHeight, cellWidth, cellHeight);
            return image;
        }

        static Color ColorOf(Creature creature) => creature switch
        {
            Creature.Kaiju => Green, Creature.Support => Violet, Creature.Slime => Hex(0x86D893),
            Creature.Guard or Creature.GoblinHealer or Creature.Archer => Gold,
            Creature.BlinkMage => Hex(0x82CFDA), Creature.Spider or Creature.Wizard => Enemy, _ => Violet
        };

        void MapArt(RectTransform parent, Rect rect, Color tint)
        {
            Panel(parent, "Land", rect, Hex(0x233E37));
            if (expeditionMap == null) return;
            RawImage art = Texture(parent, "Painted terrain", rect, expeditionMap, tint);
            float crop = (float)expeditionMap.width / expeditionMap.height / (rect.width / rect.height);
            art.uvRect = crop < 1 ? new Rect(0, (1 - crop) / 2, 1, crop) : new Rect((1 - 1 / crop) / 2, 0, 1 / crop, 1);
            Texture(parent, "Map shading", rect, shade, Ink);
        }

        static void Line(RectTransform parent, Vector2 from, Vector2 to, float thickness, Color color)
        {
            Vector2 delta = to - from;
            var line = Panel(parent, "Line", new Rect(from.x, from.y, delta.magnitude, thickness), color);
            line.pivot = new Vector2(0, .5f);
            line.localRotation = Quaternion.Euler(0, 0, -Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
        }

        static Texture2D RadialTexture(bool outline)
        {
            const int size = 96;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var colors = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float r = Vector2.Distance(new Vector2(x + .5f, y + .5f), new Vector2(size / 2, size / 2));
                    float coverage = Mathf.Clamp01(size / 2f - r - 1);
                    if (outline) coverage *= Mathf.Clamp01(r - size / 2f + 4);
                    colors[y * size + x] = new Color32(255, 255, 255, (byte)(255 * coverage));
                }
            texture.SetPixels32(colors);
            texture.Apply();
            return texture;
        }

        static Texture2D ShadeTexture()
        {
            var texture = new Texture2D(1, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int i = 0; i < 64; i++)
            {
                float y = i / 63f;
                texture.SetPixel(0, i, new Color(1, 1, 1, Mathf.Pow(y, 5) * .45f + Mathf.Pow(1 - y, 6) * .9f));
            }
            texture.Apply();
            return texture;
        }
    }
}
