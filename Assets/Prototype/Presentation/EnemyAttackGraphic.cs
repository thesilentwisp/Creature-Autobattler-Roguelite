using UnityEngine;
using UnityEngine.UI;

namespace CreatureRun
{
    internal enum EnemyAttackStyle
    {
        None, Arrow, NatureOrb, LightOrb, MirrorOrb, BlinkOrb, WizardOrb, ShadowOrb,
        Slime, Envelop, SlimeHeal, GoblinSlash, GoblinCounter, GoblinBrace, Fangs, SpiderRoll, Volley,
        IronSlam, IronBrace, IronBlock, NatureHeal, LightHeal, MirrorPortal, Teleport, ShadowPortal,
        KnightSlash, ShieldBash, ShadowBurst
    }

    [RequireComponent(typeof(CanvasRenderer))]
    internal sealed class EnemyAttackGraphic : MaskableGraphic
    {
        static readonly Color Green = new Color32(142, 246, 80, 255);
        static readonly Color Amber = new Color32(255, 189, 62, 255);
        static readonly Color Light = new Color32(255, 249, 210, 255);
        static readonly Color Violet = new Color32(199, 128, 255, 255);
        static readonly Color Cyan = new Color32(107, 241, 255, 255);
        static readonly Color Fire = new Color32(255, 86, 44, 255);
        static readonly Color Magenta = new Color32(240, 89, 211, 255);
        static readonly Color Shade = new Color32(71, 32, 106, 255);
        static readonly Color Steel = new Color32(193, 224, 243, 255);
        static readonly Color Silk = new Color32(244, 214, 180, 255);
        static readonly Color Venom = new Color32(255, 88, 116, 255);
        static readonly Color Outline = new Color32(22, 19, 29, 255);
        static readonly Color Dust = new Color32(178, 154, 122, 255);

        public EnemyAttackStyle Style { get; set; }
        Vector2 source, target;
        float progress, radius;
        bool inFlight;

        public void Draw(Vector2 from, Vector2 to, float age, float areaRadius, bool inFlight = false)
        {
            source = new Vector2(from.x, -from.y);
            target = new Vector2(to.x, -to.y);
            progress = age;
            radius = areaRadius;
            this.inFlight = inFlight;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Vector2 direction = (target - source).normalized;
            if (direction == Vector2.zero) direction = Vector2.down;
            Vector2 normal = new(-direction.y, direction.x);
            float fade = 1 - progress;

            switch (Style)
            {
                case EnemyAttackStyle.Arrow:
                    if (inFlight)
                        DrawArrow(mesh, Vector2.Lerp(source, target, progress), direction, normal, 1);
                    else
                    {
                        DrawArrow(mesh, target + direction * 3, direction, normal, fade);
                        Rays(mesh, target, Amber, progress, 5, 19);
                    }
                    break;
                case EnemyAttackStyle.NatureOrb:
                case EnemyAttackStyle.LightOrb:
                case EnemyAttackStyle.MirrorOrb:
                case EnemyAttackStyle.BlinkOrb:
                case EnemyAttackStyle.WizardOrb:
                case EnemyAttackStyle.ShadowOrb:
                    DrawOrb(mesh, direction, normal);
                    break;
                case EnemyAttackStyle.Slime:
                case EnemyAttackStyle.Envelop:
                    DrawSplash(mesh, Style == EnemyAttackStyle.Envelop, direction);
                    break;
                case EnemyAttackStyle.SlimeHeal:
                    RisingMotes(mesh, target, Green, progress, 3);
                    break;
                case EnemyAttackStyle.GoblinSlash:
                    Slash(mesh, target - direction * 8, direction, 22, 5, Amber, progress);
                    break;
                case EnemyAttackStyle.GoblinCounter:
                    Thrust(mesh, target, direction, normal, 32, Amber, fade);
                    Rays(mesh, target, Light, progress, 5, 24);
                    break;
                case EnemyAttackStyle.Fangs:
                    DrawFangs(mesh, direction, normal);
                    break;
                case EnemyAttackStyle.SpiderRoll:
                    DrawSpiderRoll(mesh, direction);
                    break;
                case EnemyAttackStyle.Volley:
                    DrawVolley(mesh);
                    break;
                case EnemyAttackStyle.IronSlam:
                    Thrust(mesh, target, direction, normal, 22, Steel, fade);
                    Band(mesh, target - Vector2.up * 19, 11 + progress * 21, .48f, 2, 0, 360, Steel, fade, 24);
                    Rays(mesh, target, Steel, progress, 5, 24);
                    break;
                case EnemyAttackStyle.GoblinBrace:
                    PanelShield(mesh, target, Vector2.right, Vector2.up, 13 + progress * 4, Amber, fade);
                    break;
                case EnemyAttackStyle.IronBrace:
                    Band(mesh, target, 24 + 4 * progress, 1, 3, 0, 360, Steel, fade, 6);
                    break;
                case EnemyAttackStyle.IronBlock:
                    PanelShield(mesh, target - direction * 14, direction, normal, 17, Steel, fade);
                    Rays(mesh, target - direction * 14, Steel, progress, 6, 25);
                    break;
                case EnemyAttackStyle.NatureHeal:
                case EnemyAttackStyle.LightHeal:
                    DrawHeal(mesh, Style == EnemyAttackStyle.LightHeal);
                    break;
                case EnemyAttackStyle.MirrorPortal:
                case EnemyAttackStyle.ShadowPortal:
                    DrawPortal(mesh, Style == EnemyAttackStyle.MirrorPortal);
                    break;
                case EnemyAttackStyle.Teleport:
                    DrawTeleport(mesh, direction, normal);
                    break;
                case EnemyAttackStyle.KnightSlash:
                    Slash(mesh, target - direction * 8, direction, 24, 8, Violet, progress);
                    Slash(mesh, target - direction * 9, -direction, 21, 3, Shade, progress);
                    break;
                case EnemyAttackStyle.ShieldBash:
                    PanelShield(mesh, target - direction * 12, direction, normal, 16 + progress * 8, Violet, fade);
                    Band(mesh, target - direction * 11, 16 + progress * 20, 1, 4, Heading(direction) - 64, 128, Steel, fade, 9);
                    Rays(mesh, target, Violet, progress, 7, 26);
                    break;
                case EnemyAttackStyle.ShadowBurst:
                    DrawShadowBurst(mesh);
                    break;
            }
        }

        static void DrawArrow(VertexHelper mesh, Vector2 tip, Vector2 direction, Vector2 normal, float opacity)
        {
            Vector2 tail = tip - direction * 30;
            Segment(mesh, tail - direction * 16, tail, 2, Tint(Light, opacity * .5f));
            Segment(mesh, tail, tip - direction * 5, 5, Tint(Outline, opacity));
            Segment(mesh, tail, tip - direction * 5, 2.5f, Tint(Amber, opacity));
            Triangle(mesh, tip, tip - direction * 11 + normal * 5, tip - direction * 11 - normal * 5, Tint(Steel, opacity));
            Triangle(mesh, tail, tail + direction * 8, tail - direction * 5 + normal * 5, Tint(Light, opacity));
            Triangle(mesh, tail, tail + direction * 8, tail - direction * 5 - normal * 5, Tint(Light, opacity));
        }

        void DrawOrb(VertexHelper mesh, Vector2 direction, Vector2 normal)
        {
            var (color, core, size, sides) = Style switch
            {
                EnemyAttackStyle.NatureOrb => (Green, Light, 7f, 10),
                EnemyAttackStyle.LightOrb => (Amber, Light, 8f, 8),
                EnemyAttackStyle.MirrorOrb => (Violet, Light, 9f, 4),
                EnemyAttackStyle.BlinkOrb => (Cyan, Light, 7f, 6),
                EnemyAttackStyle.WizardOrb => (Fire, Amber, 12f, 12),
                _ => (Magenta, Shade, 9f, 10)
            };
            if (!inFlight)
            {
                Band(mesh, target, size + progress * size * 2, 1, 2.5f, progress * 55, 360, color, 1 - progress, sides);
                Rays(mesh, target, color, progress, sides == 12 ? 9 : 5, size * 3);
                return;
            }

            size *= 1.24f;
            Vector2 point = Vector2.Lerp(source, target, progress);
            float twist = progress * 160;
            for (int i = 3; i >= 1; i--)
            {
                float drift = Style == EnemyAttackStyle.BlinkOrb ? (i % 2 == 0 ? 4 : -4) : Mathf.Sin(twist * Mathf.Deg2Rad + i) * 3;
                Vector2 mote = point - direction * (i * size * .84f) + normal * drift;
                Polygon(mesh, mote, size * (.55f - i * .1f), 1, twist, sides, Tint(color, .55f - i * .1f));
            }
            Polygon(mesh, point, size * 1.45f, 1, twist, sides, Tint(color, .24f));
            Polygon(mesh, point, size * 1.16f, 1, twist, sides, Tint(Outline, .9f));
            Polygon(mesh, point, size, 1, twist, sides, color);
            Polygon(mesh, point + direction * 1.5f, size * .52f, 1, twist, sides, core);

            switch (Style)
            {
                case EnemyAttackStyle.NatureOrb:
                    Polygon(mesh, point + normal * 8, 5, .45f, Heading(direction) + 32, 4, Green);
                    Polygon(mesh, point - normal * 8, 5, .45f, Heading(direction) - 32, 4, Green);
                    break;
                case EnemyAttackStyle.LightOrb:
                    Glint(mesh, point, size * 1.6f, Light, 1);
                    break;
                case EnemyAttackStyle.MirrorOrb:
                    Polygon(mesh, point + normal * 12 - direction * 4, 4, 1, twist, 4, Violet);
                    Polygon(mesh, point - normal * 12 - direction * 4, 4, 1, twist, 4, Violet);
                    break;
                case EnemyAttackStyle.BlinkOrb:
                    Vector2 bend = point - direction * 10 + normal * 6;
                    Segment(mesh, point + direction * 5, bend, 2, Light);
                    Segment(mesh, bend, point - direction * 20 - normal * 4, 2, Cyan);
                    break;
                case EnemyAttackStyle.WizardOrb:
                    Triangle(mesh, point - direction * 27, point + normal * 8, point - normal * 7, Tint(Fire, .85f));
                    Polygon(mesh, point, 6, 1, twist, 10, Amber);
                    break;
                case EnemyAttackStyle.ShadowOrb:
                    Band(mesh, point, 12, 1, 2.5f, twist, 95, Light, .9f, 5);
                    break;
            }
        }

        void DrawSplash(VertexHelper mesh, bool envelop, Vector2 direction)
        {
            Vector2 center = target - direction * (envelop ? 6 : 3);
            float fade = 1 - progress;
            Polygon(mesh, center, 10 + progress * (envelop ? 26 : 12), .55f, 0, 12, Tint(Green, fade * .35f));
            if (envelop)
                Band(mesh, center, 15 + progress * 19, .81f, 4.5f, 15 + progress * 30, 295, Green, fade, 14);
            int drops = envelop ? 9 : 5;
            for (int i = 0; i < drops; i++)
            {
                Vector2 offset = Polar(i * 360f / drops + 12, (envelop ? 33 : 19) * progress, .65f);
                Vector2 droplet = center + offset + Vector2.up * (Mathf.Sin(progress * Mathf.PI) * (5 + i % 3 * 4));
                Polygon(mesh, droplet, (i % 2 == 0 ? 5 : 3.5f) * fade, .78f, i * 15, 8, Tint(Green, fade));
            }
        }

        void DrawFangs(VertexHelper mesh, Vector2 direction, Vector2 normal)
        {
            for (int sign = -1; sign <= 1; sign += 2)
            {
                Vector2 edge = target + normal * (sign * Mathf.Lerp(14, 5, progress));
                Triangle(mesh, edge + direction * 12 - normal * sign * 5, edge - direction * 4, edge + normal * sign * 4,
                    Tint(Silk, 1 - progress));
            }
            Rays(mesh, target, Venom, progress, 4, 17);
        }

        void DrawSpiderRoll(VertexHelper mesh, Vector2 direction)
        {
            float fade = 1 - progress;
            Vector2 normal = new(-direction.y, direction.x);
            for (int i = 0; i < 5; i++)
            {
                Vector2 point = Vector2.Lerp(source, target, .12f + i * .12f)
                    - direction * (progress * (7 + i)) + normal * ((i % 2 == 0 ? -1 : 1) * (4 + 8 * progress));
                Polygon(mesh, point, (3 + i % 3) * fade, .55f, 0, 7, Tint(Dust, fade * .68f));
            }
            Vector2 impact = target - direction * 8;
            for (int sign = -1; sign <= 1; sign += 2)
                Segment(mesh, impact + normal * (sign * (6 + 14 * progress)),
                    impact - direction * 10 + normal * (sign * (10 + 18 * progress)), 2, Tint(Silk, fade * .78f));
        }

        void DrawVolley(VertexHelper mesh)
        {
            for (int i = -1; i <= 1; i++)
            {
                Vector2 top = target + new Vector2(i * 10, 12 + 14 * progress);
                Vector2 heel = top - Vector2.up * 7;
                Segment(mesh, heel + new Vector2(-4, 0), top, 2, Tint(Amber, 1 - progress));
                Segment(mesh, heel + new Vector2(4, 0), top, 2, Tint(Amber, 1 - progress));
            }
        }

        void DrawHeal(VertexHelper mesh, bool light)
        {
            Color color = light ? Amber : Green;
            float fade = 1 - progress;
            if (progress < .63f)
            {
                Vector2 mote = Vector2.Lerp(source, target, progress / .63f) + Vector2.up * (11 * Mathf.Sin(progress / .63f * Mathf.PI));
                if (light)
                    Glint(mesh, mote, 9, Light, 1);
                else
                    Polygon(mesh, mote, 8, .51f, 32 + progress * 100, 4, Green);
            }
            Band(mesh, target - Vector2.up * 12, 12 + progress * 15, light ? .7f : .45f, 2, 0, 360, color, fade, light ? 12 : 6);
            RisingMotes(mesh, target, color, progress, light ? 5 : 4);
        }

        void DrawPortal(VertexHelper mesh, bool mirror)
        {
            float fade = Mathf.Clamp01((1 - progress) * 2.5f);
            float size = Mathf.SmoothStep(11, 33, progress * 2);
            Color color = mirror ? Violet : Magenta;
            int sides = mirror ? 4 : 20;
            Polygon(mesh, target, size, .48f, 0, sides, Tint(Shade, fade * .7f));
            Band(mesh, target, size, .48f, 2.5f, 0, 360, color, fade, sides);
            for (int i = 0; i < 5; i++)
            {
                Vector2 point = target + Polar(i * 72 + progress * 112, size, .48f) + Vector2.up * (progress * (22 + i % 2 * 10));
                Polygon(mesh, point, 5 * fade, mirror ? .64f : 1, progress * 150, mirror ? 4 : 7, Tint(color, fade));
            }
        }

        void DrawTeleport(VertexHelper mesh, Vector2 direction, Vector2 normal)
        {
            float fade = 1 - progress;
            Band(mesh, source, 9 + progress * 21, 1, 2, 0, 360, Cyan, fade, 6);
            Vector2 previous = source;
            for (int i = 1; i <= 6; i++)
            {
                Vector2 next = Vector2.Lerp(source, target, i / 6f) + normal * (i == 6 ? 0 : i % 2 == 0 ? -7 : 7);
                Segment(mesh, previous, next, 2, Tint(Cyan, fade * fade));
                previous = next;
            }
            Glint(mesh, target, 15 + progress * 10, Light, fade);
            Rays(mesh, target + direction * 5, Cyan, progress, 6, 28);
        }

        void DrawShadowBurst(VertexHelper mesh)
        {
            float fade = 1 - progress;
            float size = Mathf.Lerp(radius * .2f, radius, Mathf.Sqrt(progress));
            Polygon(mesh, target, size, 1, 0, 24, Tint(Shade, fade * .62f));
            Band(mesh, target, size, 1, 4, progress * 70, 360, Magenta, fade, 24);
            Band(mesh, target, size * .78f, 1, 2, -progress * 160, 360, Violet, fade * .76f, 8);
            for (int i = 0; i < 8; i++)
            {
                float angle = i * 45 + progress * 70;
                Vector2 point = target + Polar(angle, size * .92f);
                Polygon(mesh, point, 8 * fade, .62f, angle, 4, Tint(Magenta, fade));
            }
        }

        static void Slash(VertexHelper mesh, Vector2 center, Vector2 direction, float size, float width, Color color, float age)
        {
            float angle = Heading(direction) - 70 + age * 100;
            Band(mesh, center, size, 1, width * (1 - age * .65f), angle, 127, color, 1 - age, 12);
            Band(mesh, center, size + 2, 1, 1.5f, angle + 9, 96, Light, 1 - age, 10);
        }

        static void Thrust(VertexHelper mesh, Vector2 point, Vector2 direction, Vector2 normal, float length, Color color, float opacity)
        {
            Triangle(mesh, point + direction * 9, point - direction * length + normal * 5, point - direction * length - normal * 5,
                Tint(color, opacity));
            Segment(mesh, point - direction * (length * .66f), point + direction * 5, 2, Tint(Light, opacity));
        }

        static void PanelShield(VertexHelper mesh, Vector2 point, Vector2 direction, Vector2 normal, float size, Color color, float opacity)
        {
            Vector2 top = point + normal * size;
            Vector2 bottom = point - normal * size;
            Vector2 front = point + direction * 9;
            Triangle(mesh, top, front + normal * (size * .56f), front - normal * (size * .56f), Tint(color, opacity * .63f));
            Triangle(mesh, top, front - normal * (size * .56f), bottom, Tint(color, opacity * .63f));
            Segment(mesh, top, front + normal * (size * .56f), 2.5f, Tint(color, opacity));
            Segment(mesh, front + normal * (size * .56f), front - normal * (size * .56f), 2.5f, Tint(Light, opacity));
            Segment(mesh, front - normal * (size * .56f), bottom, 2.5f, Tint(color, opacity));
        }

        static void Rays(VertexHelper mesh, Vector2 center, Color color, float age, int count, float reach)
        {
            for (int i = 0; i < count; i++)
            {
                Vector2 direction = Polar(i * 360f / count + 21, 1);
                float distance = 4 + age * reach;
                Segment(mesh, center + direction * distance, center + direction * (distance + 7 * (1 - age)), 2,
                    Tint(color, 1 - age));
            }
        }

        static void RisingMotes(VertexHelper mesh, Vector2 center, Color color, float age, int count)
        {
            for (int i = 0; i < count; i++)
            {
                float t = Mathf.InverseLerp(i * .075f, 1, age);
                Vector2 point = center + new Vector2((i - (count - 1) / 2f) * 9, 28 * t - 10 + i % 2 * 8);
                Glint(mesh, point, 5, color, Mathf.Sin(t * Mathf.PI));
            }
        }

        static void Glint(VertexHelper mesh, Vector2 center, float size, Color color, float opacity)
        {
            color = Tint(color, opacity);
            Polygon(mesh, center, size, .23f, 0, 4, color);
            Polygon(mesh, center, size, .23f, 90, 4, color);
        }

        static float Heading(Vector2 direction) => Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

        static Vector2 Polar(float angle, float radius, float verticalScale = 1)
        {
            float radians = angle * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians) * verticalScale) * radius;
        }

        static Color Tint(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }

        static void Band(VertexHelper mesh, Vector2 center, float radius, float verticalScale, float width,
            float start, float sweep, Color color, float opacity, int segments)
        {
            Color tint = Tint(color, opacity);
            for (int i = 0; i < segments; i++)
            {
                float a = start + sweep * i / segments;
                float b = start + sweep * (i + 1) / segments;
                Quad(mesh, center + Polar(a, radius, verticalScale), center + Polar(b, radius, verticalScale),
                    center + Polar(b, radius - width, verticalScale), center + Polar(a, radius - width, verticalScale), tint);
            }
        }

        static void Polygon(VertexHelper mesh, Vector2 center, float radius, float verticalScale, float angle, int sides, Color color)
        {
            int first = mesh.currentVertCount;
            mesh.AddVert(center, color, Vector2.zero);
            for (int i = 0; i < sides; i++)
                mesh.AddVert(center + Polar(angle + i * 360f / sides, radius, verticalScale), color, Vector2.zero);
            for (int i = 0; i < sides; i++)
                mesh.AddTriangle(first, first + 1 + i, first + 1 + (i + 1) % sides);
        }

        static void Segment(VertexHelper mesh, Vector2 a, Vector2 b, float width, Color color)
        {
            Vector2 direction = (b - a).normalized;
            Vector2 normal = new Vector2(-direction.y, direction.x) * (width * .5f);
            Quad(mesh, a + normal, b + normal, b - normal, a - normal, color);
        }

        static void Triangle(VertexHelper mesh, Vector2 a, Vector2 b, Vector2 c, Color color)
        {
            int first = mesh.currentVertCount;
            mesh.AddVert(a, color, Vector2.zero);
            mesh.AddVert(b, color, Vector2.zero);
            mesh.AddVert(c, color, Vector2.zero);
            mesh.AddTriangle(first, first + 1, first + 2);
        }

        static void Quad(VertexHelper mesh, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color color)
        {
            int first = mesh.currentVertCount;
            mesh.AddVert(a, color, Vector2.zero);
            mesh.AddVert(b, color, Vector2.zero);
            mesh.AddVert(c, color, Vector2.zero);
            mesh.AddVert(d, color, Vector2.zero);
            mesh.AddTriangle(first, first + 1, first + 2);
            mesh.AddTriangle(first, first + 2, first + 3);
        }
    }
}
