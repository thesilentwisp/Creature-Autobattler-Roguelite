using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CreatureRun
{
    public enum BattleResult { Fighting, Victory, Defeat }
    public enum CueKind { Strike, Stomp, Breath, Heal, Shield, Beam, Roll, Blink, Summon, Shock, Block, Counter, Windup }

    public readonly struct BattleCue
    {
        public readonly CueKind Kind;
        public readonly Vector2 From, To;
        public readonly float Radius;
        public readonly Unit Actor, Target;

        public BattleCue(CueKind kind, Vector2 from, Vector2 to, float radius, Unit actor, Unit target = null)
        {
            Kind = kind; From = from; To = to; Radius = radius; Actor = actor; Target = target;
        }
    }

    public sealed class Unit
    {
        public Creature Kind { get; }
        public bool Player { get; }
        public bool Clone { get; }
        public int StartingSlot { get; }
        public Vector2 Position { get; internal set; }
        public Vector2 Facing { get; internal set; }
        public float MaxHealth { get; }
        public float AttackDamage { get; }
        public float Health { get; internal set; }
        public float Shield { get; internal set; }
        public float Mana { get; internal set; }
        public bool Recharging { get; internal set; }
        public bool Enraged { get; internal set; }
        public bool Alive => Health > 0;
        public float HealthFraction => Health / MaxHealth;
        public bool Burning => BurnTicks > 0;

        internal readonly CreatureStats Stats;
        internal float Basic, First, Second, Windup, GuardTime, ShieldTime, StunTime, WeakenTime, BurnTimer;
        internal int BurnTicks, BurstShots, AvoidSide;
        internal Unit AttackTarget, BurnSource, SlimeTarget;
        internal bool Guarded => GuardTime > 0;

        internal Unit(Creature kind, bool player, int slot, Vector2 position, CreatureStats stats, BattleBalance balance, float? health, bool clone)
        {
            Kind = kind; Player = player; StartingSlot = slot; Position = position; Stats = stats; Clone = clone;
            MaxHealth = stats.health * (clone ? balance.cloneFraction : 1);
            AttackDamage = stats.attack * (clone ? balance.cloneFraction : 1);
            Health = Mathf.Clamp(health ?? MaxHealth, 0, MaxHealth);
            Facing = player ? Vector2.up : Vector2.down;
            First = stats.firstCooldown * balance.initialCooldownFraction;
            Second = stats.secondCooldown * balance.initialCooldownFraction;
            if (kind == Creature.Wizard && !clone) Mana = balance.wizardMana;
        }
    }

    public sealed class BeamField
    {
        public Vector2 Center { get; }
        public float Radius { get; }
        public float Remaining { get; internal set; }
        internal float TickTimer;
        internal readonly Unit Source;

        internal BeamField(Unit source, Vector2 center, float radius, float duration)
        {
            Source = source; Center = center; Radius = radius; Remaining = duration;
        }

        public bool Contains(Vector2 point) => (point - Center).sqrMagnitude <= Radius * Radius;
    }

    public sealed partial class Battle
    {
        readonly BattleBalance balance;
        readonly bool wizardFinale;
        readonly HashSet<Upgrade> upgrades;
        readonly List<Unit> units = new();
        readonly List<BeamField> beams = new();
        readonly List<BattleCue> cues = new();
        readonly List<(Unit Source, Vector2 Center, float At)> aftershocks = new();
        Unit wizard;

        public IReadOnlyList<Unit> Units => units;
        public IReadOnlyList<BeamField> Beams => beams;
        public IReadOnlyList<BattleCue> Cues => cues;
        public BattleResult Result { get; private set; }
        public float Elapsed { get; private set; }

        public Battle(BattleBalance balance, bool wizardFinale, IEnumerable<Upgrade> upgrades)
        {
            this.balance = balance;
            this.wizardFinale = wizardFinale;
            this.upgrades = new HashSet<Upgrade>(upgrades);
        }

        public Unit Add(Creature kind, bool player, int slot, float? health = null) =>
            AddAt(kind, player, balance.SlotPosition(player, slot), slot, health);

        Unit AddAt(Creature kind, bool player, Vector2 position, int slot = -1, float? health = null, bool clone = false)
        {
            var unit = new Unit(kind, player, slot, position, balance.Stats(kind), balance, health, clone);
            units.Add(unit);
            if (kind == Creature.Wizard && !clone) wizard = unit;
            return unit;
        }

        public void Step()
        {
            cues.Clear();
            if (Result != BattleResult.Fighting) return;
            float dt = balance.step;
            Elapsed += dt;
            CheckResult();
            if (Result != BattleResult.Fighting) return;
            UpdateFields(dt);

            int count = units.Count;
            for (int i = 0; i < count && Result == BattleResult.Fighting; i++)
            {
                Unit unit = units[i];
                if (!unit.Alive) continue;
                UpdateStatus(unit, dt);
                if (unit.Alive && Result == BattleResult.Fighting) TakeTurn(unit, dt);
            }
        }

        void CheckResult()
        {
            if (Result != BattleResult.Fighting) return;
            if (wizardFinale && wizard != null && !wizard.Alive) Result = BattleResult.Victory;
            else if (!units.Any(unit => unit.Player && unit.Alive)) Result = BattleResult.Defeat;
            else if (!wizardFinale && !units.Any(unit => !unit.Player && unit.Alive)) Result = BattleResult.Victory;
        }

        bool Has(Upgrade upgrade) => upgrades.Contains(upgrade);
        static bool InRange(Unit a, Unit b, float range) => InRange(a.Position, b.Position, range);
        static bool InRange(Vector2 a, Vector2 b, float range) => (a - b).sqrMagnitude <= range * range;

        Unit Closest(Unit unit, bool ally = false, bool farthest = false) => Closest(unit.Position, unit.Player == ally, farthest);

        Unit Closest(Vector2 origin, bool player, bool farthest = false)
        {
            Unit best = null;
            float bestDistance = farthest ? -1 : float.MaxValue;
            foreach (Unit unit in units)
            {
                if (!unit.Alive || unit.Player != player) continue;
                float distance = (unit.Position - origin).sqrMagnitude;
                if (farthest ? distance <= bestDistance : distance >= bestDistance) continue;
                bestDistance = distance;
                best = unit;
            }
            return best;
        }

        void TakeTurn(Unit unit, float dt)
        {
            bool insideBeam = beams.Any(beam => beam.Contains(unit.Position));
            float cooldownStep = dt * (unit.Player && insideBeam ? balance.beamCooldownRate : 1);
            bool slowed = !unit.Player && insideBeam && Has(Upgrade.SlowingBeam);
            unit.Basic -= slowed ? dt * balance.beamSlow : cooldownStep;
            unit.First -= cooldownStep;
            unit.Second -= cooldownStep;
            if (unit.StunTime > 0 || (unit.Kind == Creature.Armor && unit.Guarded)) return;

            if (unit.AttackTarget != null)
            {
                unit.Windup -= dt;
                if (unit.Windup <= 0)
                {
                    Unit locked = unit.AttackTarget;
                    unit.AttackTarget = null;
                    ResolveBasic(unit, locked);
                }
                return;
            }

            Unit target = Closest(unit);
            if (target == null) return;
            if (unit.Kind == Creature.Slime)
            {
                if (unit.SlimeTarget == null || !unit.SlimeTarget.Alive) unit.SlimeTarget = target;
                target = unit.SlimeTarget;
            }

            if (!unit.Clone && TryAbilities(unit, target)) return;
            if (!InRange(unit, target, unit.Stats.range)) { MoveToward(unit, target, dt, slowed); return; }
            if (unit.Basic > 0) return;

            Face(unit, target.Position);
            unit.AttackTarget = target;
            unit.Windup = balance.attackWindup;
            if (!unit.Player) Emit(CueKind.Windup, unit, target.Position, target: target);
            float interval = unit.Stats.attackInterval;
            if (unit.Kind == Creature.Kaiju && Has(Upgrade.FasterClaw)) interval *= balance.fasterClaw;
            if (unit.BurstShots > 0)
            {
                unit.BurstShots--;
                if (unit.BurstShots > 0) interval /= balance.burstSpeed;
            }
            unit.Basic = interval;
        }

        void ResolveBasic(Unit unit, Unit target)
        {
            if (!target.Alive) return;
            float damage = unit.AttackDamage;
            if (unit.Kind == Creature.Kaiju && target.Burning && Has(Upgrade.ClawFollowUp)) damage += balance.clawFollowUp;
            Emit(CueKind.Strike, unit, target.Position, target: target);
            Damage(unit, target, damage);
        }

        void UpdateStatus(Unit unit, float dt)
        {
            unit.StunTime -= dt;
            unit.WeakenTime -= dt;
            if (unit.GuardTime > 0)
            {
                unit.GuardTime -= dt;
                if (!unit.Guarded && unit.Kind == Creature.Guard)
                {
                    Unit target = Closest(unit);
                    if (target != null && InRange(unit, target, unit.Stats.range))
                    {
                        Emit(CueKind.Counter, unit, target.Position);
                        Damage(unit, target, balance.guardThrust);
                    }
                }
            }
            if (unit.Shield > 0)
            {
                unit.ShieldTime -= dt;
                if (unit.ShieldTime <= 0) EndShield(unit);
            }
            if (unit.Burning)
            {
                unit.BurnTimer -= dt;
                if (unit.BurnTimer <= .0001f)
                {
                    unit.BurnTicks--;
                    unit.BurnTimer += BurnInterval;
                    if (Damage(unit.BurnSource, unit, balance.burnDamage) > 0 && Has(Upgrade.BurnHeal))
                        Heal(unit.BurnSource, balance.burnHeal);
                }
            }

            if (!unit.Recharging) return;
            unit.Mana = Mathf.Min(balance.wizardMana, unit.Mana + dt * balance.wizardMana / Mathf.Max(dt, balance.manaRechargeTime));
            if (unit.Mana >= balance.wizardMana - .001f) { unit.Recharging = false; unit.Mana = balance.wizardMana; }
        }

        float Damage(Unit source, Unit target, float amount)
        {
            if (!target.Alive || Result != BattleResult.Fighting) return 0;
            if (target.Kind == Creature.Armor && target.Guarded)
            {
                Emit(CueKind.Block, target, target.Position, from: source.Position);
                return 0;
            }
            if (source.Kind == Creature.Kaiju) amount *= 1 + (1 - source.HealthFraction) * balance.missingHealthDamage;
            if (source.WeakenTime > 0) amount *= balance.weakenMultiplier;
            if (target.Kind == Creature.Guard && target.Guarded) amount *= 1 - balance.guardReduction;
            if (target.Kind == Creature.Kaiju && Has(Upgrade.ReinforcedHide) && target.HealthFraction <= balance.hideThreshold)
                amount *= 1 - balance.hideReduction;

            bool shieldBroke = false;
            if (target.Shield > 0)
            {
                float absorbed = Mathf.Min(target.Shield, amount);
                target.Shield -= absorbed;
                amount -= absorbed;
                shieldBroke = target.Shield <= 0;
            }

            float lost = Mathf.Min(target.Health, amount);
            target.Health -= lost;
            if (target.Kind == Creature.Spider && !target.Clone && target.HealthFraction <= balance.enrageThreshold) target.Enraged = true;
            if (shieldBroke && target.Alive) EndShield(target);
            if (!target.Alive) CheckResult();
            return lost;
        }

        float Heal(Unit unit, float amount, Unit source = null)
        {
            if (!unit.Alive) return 0;
            float restored = Mathf.Min(unit.MaxHealth - unit.Health, amount);
            unit.Health += restored;
            if (restored > 0) Emit(CueKind.Heal, source ?? unit, unit.Position);
            return restored;
        }

        void Shield(Unit unit, float amount, float duration)
        {
            unit.Shield = Mathf.Max(unit.Shield, amount);
            unit.ShieldTime = duration;
            Emit(CueKind.Shield, unit, unit.Position);
        }

        void EndShield(Unit unit)
        {
            unit.Shield = 0;
            if (unit.Kind != Creature.Support || !Has(Upgrade.SilverReprieve)) return;
            Heal(unit, balance.chainHeal);
            Emit(CueKind.Shock, unit, unit.Position, balance.chainStunRadius);
            foreach (Unit foe in RivalsWithin(unit, unit.Position, balance.chainStunRadius))
            {
                foe.StunTime = Mathf.Max(foe.StunTime, balance.chainStunTime);
                Weaken(foe);
            }
        }

        IEnumerable<Unit> RivalsWithin(Unit source, Vector2 center, float radius) =>
            units.Where(unit => unit.Alive && unit.Player != source.Player && InRange(center, unit.Position, radius));

        void Emit(CueKind kind, Unit actor, Vector2 to, float radius = 0, Vector2? from = null, Unit target = null) =>
            cues.Add(new BattleCue(kind, from ?? actor.Position, to, radius, actor, target));

        static void Face(Unit unit, Vector2 point)
        {
            Vector2 direction = point - unit.Position;
            if (direction.sqrMagnitude > .0001f) unit.Facing = direction.normalized;
        }
    }
}
