using UnityEngine;
using UnityEngine.UI;

namespace CreatureRun
{
    public sealed partial class PrototypeGame
    {
        Texture2D flameTongue;

        void ShowFlameBreath(Vector2 from, Vector2 target, float coneAngle, UnitView caster)
        {
            const float lifetime = .6f;
            Vector2 direction = (target - from).normalized;
            if (direction == Vector2.zero) direction = Vector2.down;
            bool facesLeft = direction.x < 0;
            if (caster != null)
            {
                caster.BreathTime = lifetime;
                caster.BreathLeft = facesLeft;
            }
            Vector2 origin = from + new Vector2(facesLeft ? -7 : 7, -31);
            float length = Vector2.Distance(from, target) + 12;
            if (flameTongue == null) flameTongue = MakeFlameTongue();
            var root = Box(effectLayer, "Flame Breath", new Rect(origin, Vector2.zero));
            var flame = new FlameBreathEffect(root, flameTongue, disc, direction, length, coneAngle);
            effects.Add(new VisibleEffect { Root = root, Animate = flame.Animate, FinishBeforeBanner = true, Lifetime = lifetime, Remaining = lifetime });
        }

        sealed class FlameBreathEffect
        {
            static readonly Color Red = Hex(0xF63C0E), Orange = Hex(0xFF7A18), Gold = Hex(0xFFC44A), Core = Hex(0xFFF3B0);

            readonly RawImage outer, upper, lower, middle, core;
            readonly RawImage[] embers = new RawImage[10];
            readonly Vector2 direction, cross;
            readonly float heading, length, spread;

            public FlameBreathEffect(RectTransform root, Texture2D tongue, Texture2D disc, Vector2 direction, float length, float coneAngle)
            {
                this.direction = direction;
                cross = new Vector2(-direction.y, direction.x);
                heading = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                this.length = length;
                spread = Mathf.Clamp(coneAngle * .5f, 8, 35);
                outer = MakeTongue(root, tongue, "Red flame", Red);
                upper = MakeTongue(root, tongue, "Upper flame tongue", Orange);
                lower = MakeTongue(root, tongue, "Lower flame tongue", Orange);
                lower.uvRect = new Rect(0, 1, 1, -1);
                middle = MakeTongue(root, tongue, "Gold flame", Gold);
                core = MakeTongue(root, tongue, "Hot core", Core);
                for (int i = 0; i < embers.Length; i++)
                    embers[i] = Texture(root, "Ember", default, disc, i % 3 == 0 ? Core : Gold);
            }

            static RawImage MakeTongue(RectTransform root, Texture2D tongue, string name, Color color)
            {
                RawImage image = Texture(root, name, default, tongue, color);
                image.rectTransform.pivot = new Vector2(0, .5f);
                return image;
            }

            public void Animate(float progress)
            {
                float reach = Mathf.SmoothStep(.42f, 1, Mathf.Clamp01(progress / .24f));
                float heat = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.28f, .88f, progress));
                float width = Mathf.Clamp(length * .34f, 21, 44) * Mathf.Lerp(.76f, 1.18f, progress);
                float flicker = Mathf.Sin(progress * 28);
                float plume = length * reach;
                DrawTongue(outer, 0, heading + 3 * flicker, plume * 1.04f, width * 1.2f, heat * .94f);
                DrawTongue(upper, .1f, heading + spread * .67f + 4 * flicker, plume * .88f, width * .62f, heat);
                DrawTongue(lower, .08f, heading - spread * .72f + 5 * Mathf.Cos(progress * 24), plume * .96f, width * .68f, heat);
                DrawTongue(middle, .02f, heading - 2 * flicker, plume * .92f, width * .78f, heat);
                DrawTongue(core, 0, heading + 2 * flicker, plume * .66f, width * .48f, heat);

                for (int i = 0; i < embers.Length; i++)
                {
                    float age = Mathf.InverseLerp(.025f * i, 1, progress);
                    float drift = (i % 2 == 0 ? -1 : 1) * (.24f + .18f * (i % 5));
                    Vector2 point = direction * (length * Mathf.Lerp(.16f, .8f + .12f * (i % 4), age))
                        + cross * (drift * width * age) - Vector2.up * (16 * age * age);
                    float size = Mathf.Lerp(5 - i % 3, 1.4f, age);
                    RawImage ember = embers[i];
                    ember.color = Alpha(ember.color, Mathf.Clamp01(age * 12) * (1 - age));
                    SetRect(ember.rectTransform, new Rect(point.x - size / 2, point.y - size / 2, size, size));
                }
            }

            void DrawTongue(RawImage image, float offset, float angle, float reach, float width, float opacity)
            {
                Vector2 start = direction * (offset * length);
                SetRect(image.rectTransform, new Rect(start.x, start.y, reach, width));
                image.rectTransform.localRotation = Quaternion.Euler(0, 0, -angle);
                image.color = Alpha(image.color, opacity);
            }
        }

        static Texture2D MakeFlameTongue()
        {
            const int width = 128, height = 64;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = "Flame tongue mask",
                wrapMode = TextureWrapMode.Clamp
            };
            var pixels = new Color32[width * height];
            for (int x = 0; x < width; x++)
            {
                float along = (x + .5f) / width;
                float center = .5f + .08f * Mathf.Sin(along * 9) - .09f * along;
                float halfWidth = .41f * Mathf.Pow(1 - along, .65f) * Mathf.SmoothStep(.42f, 1, along * 7);
                halfWidth *= 1 + .18f * Mathf.Sin(along * 27);
                float startFade = Mathf.Clamp01(along * 30);
                for (int y = 0; y < height; y++)
                {
                    float edge = halfWidth - Mathf.Abs((y + .5f) / height - center);
                    float alpha = Mathf.SmoothStep(0, 1, edge * 22) * startFade;
                    pixels[y * width + x] = new Color32(255, 255, 255, (byte)(255 * alpha));
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }
    }
}
