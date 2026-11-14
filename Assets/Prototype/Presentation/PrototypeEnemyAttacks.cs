using System.Collections.Generic;
using UnityEngine;

namespace CreatureRun
{
    public sealed partial class PrototypeGame
    {
        const float EnemyLungeDuration = .24f;
        readonly Dictionary<Unit, EnemyShot> pendingEnemyShots = new();

        sealed class EnemyShot
        {
            public Unit Target;
            public Vector2 Aim;
            public BattleCue? Hit;
            public bool Arrived;
        }

        static EnemyAttackStyle BasicEnemyAttack(Creature kind) => kind switch
        {
            Creature.Slime => EnemyAttackStyle.Slime,
            Creature.Guard => EnemyAttackStyle.GoblinSlash,
            Creature.GoblinHealer => EnemyAttackStyle.NatureOrb,
            Creature.Spider => EnemyAttackStyle.Fangs,
            Creature.Archer => EnemyAttackStyle.Arrow,
            Creature.Armor => EnemyAttackStyle.IronSlam,
            Creature.Healer => EnemyAttackStyle.LightOrb,
            Creature.Illusionist => EnemyAttackStyle.MirrorOrb,
            Creature.BlinkMage => EnemyAttackStyle.BlinkOrb,
            Creature.Wizard => EnemyAttackStyle.WizardOrb,
            Creature.Knight => EnemyAttackStyle.KnightSlash,
            Creature.ShadowMage => EnemyAttackStyle.ShadowOrb,
            _ => EnemyAttackStyle.None
        };

        static bool IsProjectile(EnemyAttackStyle style) => style is EnemyAttackStyle.Arrow or EnemyAttackStyle.NatureOrb
            or EnemyAttackStyle.LightOrb or EnemyAttackStyle.MirrorOrb or EnemyAttackStyle.BlinkOrb or EnemyAttackStyle.WizardOrb
            or EnemyAttackStyle.ShadowOrb;

        EnemyAttackGraphic MakeEnemyGraphic(EnemyAttackStyle style)
        {
            var graphic = Box(effectLayer, style.ToString(), new Rect(0, 0, 438, 501)).gameObject.AddComponent<EnemyAttackGraphic>();
            graphic.raycastTarget = false;
            graphic.Style = style;
            return graphic;
        }

        void LaunchEnemyShot(BattleCue cue, EnemyAttackStyle style, Vector2 from, Vector2 target, bool resolved)
        {
            Vector2 aim = (target - from).normalized;
            Vector2 origin = from - Vector2.up * 32 + aim * 14;
            var shot = new EnemyShot { Target = cue.Target, Aim = target - Vector2.up * 30, Hit = resolved ? cue : null };
            if (!resolved) pendingEnemyShots[cue.Actor] = shot;
            EnemyAttackGraphic graphic = MakeEnemyGraphic(style);
            float flightTime = style == EnemyAttackStyle.Arrow ? .29f : .38f;
            const float impactTime = .16f;
            float lifetime = flightTime + impactTime;
            effects.Add(new VisibleEffect
            {
                Root = graphic.rectTransform, Lifetime = lifetime, Remaining = lifetime, FinishBeforeBanner = true,
                Animate = progress =>
                {
                    float elapsed = progress * lifetime;
                    if (elapsed < flightTime)
                    {
                        if (shot.Target != null && shot.Target.Alive)
                            shot.Aim = ArenaPoint(shot.Target.Position) - Vector2.up * 30;
                        graphic.Draw(origin, shot.Aim, elapsed / flightTime, 0, inFlight: true);
                        return;
                    }
                    shot.Arrived = true;
                    if (shot.Hit is BattleCue hit)
                    {
                        FlashHit(hit);
                        shot.Hit = null;
                    }
                    graphic.Draw(origin, shot.Aim, (elapsed - flightTime) / impactTime, 0);
                }
            });
        }

        bool ShowEnemyCue(BattleCue cue, Vector2 from, Vector2 target, UnitView actor)
        {
            EnemyAttackStyle style = cue.Kind switch
            {
                CueKind.Strike or CueKind.Windup => BasicEnemyAttack(cue.Actor.Kind),
                CueKind.Counter => EnemyAttackStyle.GoblinCounter,
                CueKind.Roll => cue.Actor.Kind == Creature.Slime ? EnemyAttackStyle.Envelop : EnemyAttackStyle.SpiderRoll,
                CueKind.Shock when cue.Actor.Kind == Creature.Archer => EnemyAttackStyle.Volley,
                CueKind.Shock when cue.Actor.Kind == Creature.Knight => EnemyAttackStyle.ShieldBash,
                CueKind.Shock when cue.Actor.Kind == Creature.ShadowMage => EnemyAttackStyle.ShadowBurst,
                CueKind.Heal when cue.Actor.Kind == Creature.GoblinHealer => EnemyAttackStyle.NatureHeal,
                CueKind.Heal when cue.Actor.Kind == Creature.Healer => EnemyAttackStyle.LightHeal,
                CueKind.Heal when cue.Actor.Kind == Creature.Slime => EnemyAttackStyle.SlimeHeal,
                CueKind.Shield when cue.Actor.Kind == Creature.Guard => EnemyAttackStyle.GoblinBrace,
                CueKind.Shield when cue.Actor.Kind == Creature.Armor => EnemyAttackStyle.IronBrace,
                CueKind.Block => EnemyAttackStyle.IronBlock,
                CueKind.Blink => EnemyAttackStyle.Teleport,
                CueKind.Summon => cue.Actor.Clone ? EnemyAttackStyle.MirrorPortal : EnemyAttackStyle.ShadowPortal,
                _ => EnemyAttackStyle.None
            };
            bool ranged = IsProjectile(style);
            if (cue.Kind == CueKind.Windup)
            {
                if (ranged) LaunchEnemyShot(cue, style, from, target, resolved: false);
                return true;
            }
            if (style == EnemyAttackStyle.None) return false;

            if (ranged)
            {
                if (pendingEnemyShots.TryGetValue(cue.Actor, out EnemyShot shot) && shot.Target == cue.Target)
                {
                    pendingEnemyShots.Remove(cue.Actor);
                    if (shot.Arrived) FlashHit(cue);
                    else
                    {
                        shot.Aim = target - Vector2.up * 30;
                        shot.Hit = cue;
                    }
                }
                else
                    LaunchEnemyShot(cue, style, from, target, resolved: true);
                return true;
            }

            if (cue.Kind is CueKind.Strike or CueKind.Counter or CueKind.Roll or CueKind.Blink)
                FlashHit(cue);
            if (actor != null)
            {
                float lunge = style switch
                {
                    EnemyAttackStyle.Envelop => 16,
                    EnemyAttackStyle.SpiderRoll => 13,
                    EnemyAttackStyle.GoblinCounter or EnemyAttackStyle.ShieldBash => 11,
                    EnemyAttackStyle.Fangs or EnemyAttackStyle.GoblinSlash or EnemyAttackStyle.KnightSlash => 6,
                    _ => 0
                };
                if (lunge > 0 || style is EnemyAttackStyle.Slime or EnemyAttackStyle.IronSlam)
                {
                    actor.Melee = style;
                    actor.AttackTime = EnemyLungeDuration;
                    actor.AttackDirection = (target - from).normalized * lunge;
                    actor.Motion = style == EnemyAttackStyle.SpiderRoll ? 0 : .28f;
                }
            }

            string caption = style switch
            {
                EnemyAttackStyle.Envelop => "Slime · Envelop",
                EnemyAttackStyle.GoblinCounter => "Goblin Guard · Counter",
                EnemyAttackStyle.SpiderRoll => "Silkwood Spider · Roll",
                EnemyAttackStyle.Volley => "Goblin Archer · Burst Fire",
                EnemyAttackStyle.NatureHeal or EnemyAttackStyle.LightHeal => $"{RunContent.Name(cue.Actor.Kind)} · Heal",
                EnemyAttackStyle.Teleport => "Blink Mage · Teleport Strike",
                EnemyAttackStyle.ShieldBash => "Shadow Knight · Shield Bash",
                EnemyAttackStyle.ShadowBurst => "Shadow Mage · Shadow Burst",
                EnemyAttackStyle.MirrorPortal => "Illusionist · Multiply",
                EnemyAttackStyle.ShadowPortal => $"Hollow Wizard · Summon {RunContent.Name(cue.Actor.Kind)}",
                _ => null
            };
            if (caption != null) latestCue.text = caption;

            float lifetime = style switch
            {
                EnemyAttackStyle.MirrorPortal or EnemyAttackStyle.ShadowPortal => .64f,
                EnemyAttackStyle.Teleport or EnemyAttackStyle.NatureHeal or EnemyAttackStyle.LightHeal or EnemyAttackStyle.ShadowBurst => .52f,
                _ => .32f
            };
            bool ground = style is EnemyAttackStyle.ShadowBurst or EnemyAttackStyle.MirrorPortal or EnemyAttackStyle.ShadowPortal
                or EnemyAttackStyle.SpiderRoll;
            Vector2 start = from - Vector2.up * (ground ? 3 : 32);
            Vector2 end = target - Vector2.up * (ground ? 3 : 30);
            if (style == EnemyAttackStyle.Teleport)
                end = ArenaPoint(cue.Actor.Position) - Vector2.up * 30;

            var graphic = MakeEnemyGraphic(style);
            if (ground) graphic.transform.SetParent(fieldLayer, false);
            float radius = cue.Radius * ArenaScale;
            effects.Add(new VisibleEffect
            {
                Root = graphic.rectTransform, Lifetime = lifetime, Remaining = lifetime, FinishBeforeBanner = true,
                Animate = progress => graphic.Draw(start, end, progress, radius)
            });
            return true;
        }
    }
}
