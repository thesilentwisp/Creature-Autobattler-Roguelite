using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace CreatureRun
{
    public sealed partial class PrototypeGame
    {
        const float ArenaScale = 49;
        RectTransform arena, fieldLayer, unitLayer, effectLayer;
        Text clock, latestCue, speedLabel;
        readonly Dictionary<Unit, UnitView> unitViews = new();
        readonly Dictionary<BeamField, RectTransform> fieldViews = new();
        readonly List<VisibleEffect> effects = new();
        readonly Dictionary<Unit, Text> rosterBars = new();
        bool bannerShown;

        sealed class UnitView
        {
            public RectTransform Root, Health, Shield, Mana;
            public RawImage Art, Footing, Guard;
            public EnemyAttackStyle Melee;
            public Vector2 Last, Current, AttackDirection;
            public float Motion, Flash, BreathTime, AttackTime;
            public bool BreathLeft;
        }

        sealed class VisibleEffect
        {
            public RectTransform Root;
            public RawImage Graphic;
            public Action<float> Animate;
            public bool FinishBeforeBanner;
            public Vector2 Center;
            public float Lifetime, Remaining, StartSize, EndSize, Elongation;
        }

        void StartCombat()
        {
            if (!run.StartBattle()) return;
            accumulator = -.45f;
            resultHold = 1.2f;
            Render();
        }

        void DrawBattle()
        {
            Header(PlanningProgress, run.Encounter.Name);
            clock = Label(content, new Rect(340, 52, 118, 18), "00:00", 12, Muted, TextAnchor.MiddleRight, true);

            var frame = Bordered(content, "Battlefield", new Rect(20, 98, 440, 579), PanelColor, Border);
            MapArt(frame, new Rect(0, 0, 438, 577), run.World == 1 ? new Color(.46f, .59f, .47f) : new Color(.38f, .42f, .62f));
            Panel(frame, "Arena tint", new Rect(0, 0, 438, 577), Alpha(Ink, .21f));
            arena = Box(frame, "Arena", new Rect(0, 37, 438, 501));
            fieldLayer = Box(arena, "Ground effects", new Rect(0, 0, 438, 501));
            unitLayer = Box(arena, "Combatants", new Rect(0, 0, 438, 501));
            effectLayer = Box(arena, "Combat effects", new Rect(0, 0, 438, 501));
            Line(fieldLayer, new Vector2(26, 250), new Vector2(412, 250), 1, Alpha(Muted, .27f));
            for (int i = 0; i < 9; i++)
            {
                FormationMark(balance.SlotPosition(true, i), Green);
                FormationMark(balance.SlotPosition(false, i), Enemy);
            }
            Label(frame, new Rect(12, 10, 410, 20), "ENEMY SIDE", 10, Enemy, bold: true);
            Label(frame, new Rect(12, 542, 410, 20), "YOUR SIDE", 10, Green, bold: true);
            string objective = run.EncounterIndex == 6 ? "Defeat the wizard" : "Defeat all enemies";
            Label(frame, new Rect(192, 10, 227, 20), objective, 11, TextColor, TextAnchor.MiddleRight, true);

            unitViews.Clear(); fieldViews.Clear(); effects.Clear(); rosterBars.Clear(); pendingEnemyShots.Clear(); bannerShown = false;
            latestCue = Label(content, new Rect(22, 690, 436, 21), "", 13, Gold, bold: true);
            var partyPanel = Bordered(content, "Party in combat", new Rect(20, 723, 440, 67), PanelColor, Border);
            Unit[] party = run.Battle.Units.Where(unit => unit.Player).ToArray();
            for (int i = 0; i < party.Length; i++)
            {
                Unit ally = party[i];
                int left = 9 + i * 216;
                Portrait(partyPanel, new Rect(left, 7, 48, 48), ally.Kind);
                Label(partyPanel, new Rect(left + 54, 9, 149, 22), RunContent.Name(ally.Kind), 13, TextColor, bold: true);
                rosterBars[ally] = Label(partyPanel, new Rect(left + 54, 33, 149, 20), "", 11, ColorOf(ally.Kind));
            }
            Label(content, new Rect(22, 810, 226, 24), "AUTO-COMBAT", 10, Muted, bold: true);
            var control = Control(content, new Rect(327, 802, 133, 39), "Battle speed", () => speed = speed == 1 ? 2 : 1, PanelColor, Border);
            speedLabel = Label(control, new Rect(0, 0, 131, 37), "", 13, TextColor, TextAnchor.MiddleCenter, true);
            UpdateBattleView();
        }

        void FormationMark(Vector2 position, Color tint)
        {
            Vector2 point = ArenaPoint(position);
            Texture(fieldLayer, "Starting tile", new Rect(point.x - 20, point.y - 10, 40, 20), ring, Alpha(tint, .28f));
        }

        static Vector2 ArenaPoint(Vector2 p) => new(219 + p.x * ArenaScale, 250 - p.y * ArenaScale);

        UnitView MakeUnitView(Unit unit)
        {
            RectTransform root = Box(unitLayer, RunContent.Name(unit.Kind), new Rect(0, 0, 88, 85));
            var view = new UnitView { Root = root, Last = unit.Position, Current = unit.Position };
            view.Footing = Texture(root, "Team marker", new Rect(20, 63, 48, 20), disc,
                Alpha(unit.Player ? Green : unit.Clone ? Violet : Enemy, .62f));
            view.Guard = Texture(root, "Guard ring", new Rect(7, 10, 74, 74), ring, Color.clear);
            view.Art = Portrait(root, new Rect(8, 7, 72, 68), unit.Kind);
            Panel(root, "Health track", new Rect(16, 2, 56, 7), Ink);
            view.Health = Panel(root, "Health", new Rect(17, 3, 54, 5), unit.Player ? Green : Enemy);
            view.Shield = Panel(root, "Shield", new Rect(17, -2, 54, 3), Hex(0x8ED8ED));
            if (unit.Kind == Creature.Wizard && !unit.Clone)
            {
                Panel(root, "Mana track", new Rect(16, 11, 56, 5), Ink);
                view.Mana = Panel(root, "Mana", new Rect(17, 12, 54, 3), Violet);
            }
            return view;
        }

        void UpdateBattleView()
        {
            Battle battle = run.Battle;
            int elapsed = Mathf.FloorToInt(battle.Elapsed);
            clock.text = $"{elapsed / 60:00}:{elapsed % 60:00}";
            speedLabel.text = $"Speed  {speed}×";
            foreach (Unit unit in battle.Units.OrderByDescending(unit => unit.Position.y))
            {
                if (!unit.Alive)
                {
                    if (unitViews.TryGetValue(unit, out var fallen)) fallen.Root.gameObject.SetActive(false);
                    if (!unit.Player) pendingEnemyShots.Remove(unit);
                    if (rosterBars.TryGetValue(unit, out var knockedOut)) knockedOut.text = "Knocked out";
                    continue;
                }

                if (!unitViews.TryGetValue(unit, out UnitView view)) unitViews[unit] = view = MakeUnitView(unit);
                view.Root.SetAsLastSibling();
                view.Motion = Mathf.MoveTowards(view.Motion, 0, Time.unscaledDeltaTime);
                view.Flash = Mathf.MoveTowards(view.Flash, 0, Time.unscaledDeltaTime);
                view.BreathTime = Mathf.MoveTowards(view.BreathTime, 0, Time.unscaledDeltaTime);
                view.AttackTime = Mathf.MoveTowards(view.AttackTime, 0, Time.unscaledDeltaTime);
                bool teleported = Vector2.Distance(view.Last, unit.Position) > .9f;
                view.Current = teleported ? unit.Position : Vector2.Lerp(view.Current, unit.Position, 1 - Mathf.Exp(-24 * Time.unscaledDeltaTime));
                view.Last = unit.Position;
                Vector2 at = ArenaPoint(view.Current);
                float bounce = Mathf.Sin(view.Motion / .28f * Mathf.PI) * (unit.Kind == Creature.Slime ? 19 : 8);
                float idle = Mathf.Sin(Time.unscaledTime * 3.4f + unit.GetHashCode() % 7) * 1.25f;
                float attackMotion = view.AttackTime / EnemyLungeDuration;
                bool spiderRoll = view.Melee == EnemyAttackStyle.SpiderRoll && attackMotion > 0;
                if (spiderRoll)
                {
                    float age = 1 - attackMotion;
                    attackMotion = Mathf.SmoothStep(0, 1, age / .3f) * (1 - Mathf.SmoothStep(0, 1, (age - .3f) / .7f));
                    bounce = -3 * attackMotion;
                }
                Vector2 lunge = view.AttackDirection * attackMotion;
                SetRect(view.Root, new Rect(at.x - 44 + lunge.x, at.y - 69 - bounce + idle + lunge.y, 88, 85));
                float artScale = unit.Enraged ? 1.14f : 1;
                float facing = view.BreathTime > 0 && view.BreathLeft && view.Art.texture != null ? -1 : 1;
                float squash = spiderRoll ? attackMotion * .13f : unit.Kind == Creature.Slime ? view.AttackTime * 1.1f : 0;
                view.Art.rectTransform.localScale = new Vector3(artScale * facing * (1 + squash), artScale * (1 - squash * .7f), 1);
                view.Art.color = view.Art.texture == null ? Color.clear : unit.Clone ? new Color(.73f, .65f, 1, .69f) :
                    Color.Lerp(Color.white, Hex(0xFA916C), view.Flash * 4);
                view.Guard.color = unit.Guarded ? Alpha(Gold, .84f) : unit.Shield > 0 ? Alpha(Hex(0x8ED8ED), .8f) : Color.clear;
                view.Health.sizeDelta = new Vector2(54 * unit.HealthFraction, 5);
                view.Shield.sizeDelta = new Vector2(Mathf.Min(54, 54 * unit.Shield / Mathf.Max(1, balance.teamShield)), 3);
                if (view.Mana != null)
                {
                    view.Mana.sizeDelta = new Vector2(54 * unit.Mana / Mathf.Max(1, balance.wizardMana), 3);
                    view.Mana.GetComponent<Image>().color = unit.Recharging ? Hex(0x82CFDA) : Violet;
                }
                if (rosterBars.TryGetValue(unit, out Text bar))
                    bar.text = $"{Mathf.CeilToInt(unit.Health)}/{unit.MaxHealth:0} HP" + (unit.Shield > 0 ? $"  +{Mathf.CeilToInt(unit.Shield)} shield" : "");
            }

            foreach (BeamField beam in battle.Beams)
            {
                if (fieldViews.ContainsKey(beam)) continue;
                float size = beam.Radius * 2 * ArenaScale;
                Vector2 center = ArenaPoint(beam.Center);
                RectTransform node = Box(fieldLayer, "Fixed light field", new Rect(center.x - size / 2, center.y - size / 2, size, size));
                Texture(node, "Beam light", new Rect(0, 0, size, size), disc, Alpha(Gold, .23f));
                Texture(node, "Beam edge", new Rect(0, 0, size, size), ring, Alpha(Gold, .75f));
                Texture(node, "Beam center", new Rect(size / 2 - 19, size / 2 - 19, 38, 38), ring, Alpha(TextColor, .52f));
                fieldViews.Add(beam, node);
            }
            foreach (BeamField expired in fieldViews.Keys.Where(field => !battle.Beams.Contains(field)).ToArray())
            {
                Destroy(fieldViews[expired].gameObject);
                fieldViews.Remove(expired);
            }
            AnimateEffects();
            if (battle.Result == BattleResult.Fighting || bannerShown || effects.Any(effect => effect.FinishBeforeBanner)) return;
            bannerShown = true;
            string result = battle.Result == BattleResult.Victory ? "Battle won" : "Party knocked out";
            Color accent = battle.Result == BattleResult.Victory ? Gold : Enemy;
            var banner = Bordered(content, "Battle result", new Rect(63, 341, 354, 72), Ink, accent);
            Label(banner, new Rect(0, 0, 352, 70), result, 25, accent, TextAnchor.MiddleCenter, true);
        }

        void RecordCues()
        {
            foreach (BattleCue cue in run.Battle.Cues)
            {
                unitViews.TryGetValue(cue.Actor, out UnitView actor);
                if (actor != null && cue.Actor.Player && cue.Kind == CueKind.Strike) actor.Motion = .28f;
                Vector2 from = ArenaPoint(cue.From);
                Vector2 center = ArenaPoint(cue.To);
                if (!cue.Actor.Player && ShowEnemyCue(cue, from, center, actor)) continue;
                if (cue.Kind is CueKind.Strike or CueKind.Breath) FlashHit(cue);
                if (cue.Kind == CueKind.Breath)
                {
                    ShowFlameBreath(from, center, cue.Radius, actor);
                    latestCue.text = "Baby Kaiju · Flame Breath";
                    continue;
                }
                Color color = cue.Kind switch
                {
                    CueKind.Heal => Green, CueKind.Shield or CueKind.Block => Hex(0x8ED8ED),
                    CueKind.Summon or CueKind.Blink => Violet, CueKind.Strike => cue.Actor.Player ? TextColor : Enemy,
                    _ => Gold
                };
                if (cue.Kind is CueKind.Strike or CueKind.Roll or CueKind.Blink)
                {
                    RectTransform trace = Box(effectLayer, "Attack trace", new Rect(0, 0, 438, 501));
                    Line(trace, from + Vector2.up * -20, center + Vector2.up * -20, 3, Alpha(color, .77f));
                    effects.Add(new VisibleEffect { Root = trace, Lifetime = .25f, Remaining = .25f });
                }
                if (cue.Kind == CueKind.Beam) { latestCue.text = "Chainkeeper · Light Beam"; continue; }
                float endSize = cue.Radius > 0 ? cue.Radius * 2 * ArenaScale : 43;
                var effect = Texture(effectLayer, cue.Kind.ToString(), default, ring, color);
                effects.Add(new VisibleEffect
                {
                    Root = effect.rectTransform, Graphic = effect, Center = center,
                    Lifetime = .46f, Remaining = .46f, StartSize = endSize * .4f, EndSize = endSize,
                    Elongation = cue.Kind is CueKind.Stomp or CueKind.Summon ? .73f : 1
                });
                string caption = cue.Kind switch
                {
                    CueKind.Stomp => "Baby Kaiju · Earthquake Stomp",
                    CueKind.Shield when cue.Actor.Kind == Creature.Support => "Chainkeeper · Team Shield",
                    _ => null
                };
                if (caption != null) latestCue.text = caption;
            }
        }

        void FlashHit(BattleCue cue)
        {
            if (cue.Target != null)
            {
                if (unitViews.TryGetValue(cue.Target, out UnitView target)) target.Flash = .21f;
                return;
            }
            foreach (var entry in unitViews)
                if (entry.Key.Player != cue.Actor.Player && Vector2.Distance(entry.Key.Position, cue.To) < .12f) entry.Value.Flash = .21f;
        }

        void AnimateEffects()
        {
            for (int i = effects.Count - 1; i >= 0; i--)
            {
                VisibleEffect effect = effects[i];
                effect.Remaining -= Time.unscaledDeltaTime;
                if (effect.Remaining <= 0)
                {
                    Destroy(effect.Root.gameObject);
                    effects.RemoveAt(i);
                    continue;
                }
                float progress = 1 - effect.Remaining / effect.Lifetime;
                if (effect.Animate != null) { effect.Animate(progress); continue; }
                if (effect.Graphic == null) continue;
                float size = Mathf.Lerp(effect.StartSize, effect.EndSize, progress);
                float height = size * effect.Elongation;
                effect.Graphic.color = Alpha(effect.Graphic.color, (1 - progress) * .9f);
                SetRect(effect.Root, new Rect(effect.Center.x - size / 2, effect.Center.y - height / 2, size, height));
            }
        }
    }
}
