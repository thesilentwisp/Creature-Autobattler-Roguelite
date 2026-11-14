using System.Linq;
using UnityEngine;

namespace CreatureRun
{
    public sealed partial class Battle
    {
        bool TryAbilities(Unit unit, Unit target)
        {
            if (unit.First <= 0 && CastFirst(unit, target))
            {
                unit.First = unit.Stats.firstCooldown;
                return true;
            }
            if (unit.Second > 0) return false;
            if ((unit.Kind == Creature.Kaiju && CastBreath(unit, target)) || (unit.Kind == Creature.Support && CastTeamShield(unit)))
            {
                unit.Second = unit.Stats.secondCooldown;
                return true;
            }
            return false;
        }

        bool CastFirst(Unit unit, Unit target)
        {
            switch (unit.Kind)
            {
                case Creature.Kaiju: return CastStomp(unit, target);
                case Creature.Support: return CastBeam(unit);
                case Creature.Slime:
                    if (!HitAbility(unit, target, unit.Stats.range, balance.envelopDamage, CueKind.Roll)) return false;
                    Heal(unit, balance.envelopHeal);
                    return true;
                case Creature.Guard:
                    if (!InRange(unit, target, unit.Stats.range)) return false;
                    unit.GuardTime = balance.guardDuration;
                    Emit(CueKind.Shield, unit, unit.Position);
                    return true;
                case Creature.GoblinHealer: return CastEnemyHeal(unit, balance.goblinHeal);
                case Creature.Healer: return CastEnemyHeal(unit, balance.mageHeal);
                case Creature.Spider:
                    return HitAbility(unit, target, balance.spiderRollRange, balance.spiderRoll * (unit.Enraged ? balance.enrageMultiplier : 1), CueKind.Roll);
                case Creature.Archer:
                    if (unit.BurstShots > 0 || !InRange(unit, target, unit.Stats.range)) return false;
                    unit.BurstShots = balance.burstShots;
                    unit.Basic = Mathf.Min(unit.Basic, unit.Stats.attackInterval / balance.burstSpeed);
                    Emit(CueKind.Shock, unit, unit.Position, .55f);
                    return true;
                case Creature.Armor:
                    if (!InRange(unit, target, unit.Stats.range)) return false;
                    unit.GuardTime = balance.armorGuardDuration;
                    Emit(CueKind.Shield, unit, unit.Position);
                    return true;
                case Creature.Illusionist: return Multiply(unit, target);
                case Creature.BlinkMage: return Blink(unit);
                case Creature.Wizard: return Summon(unit);
                case Creature.Knight: return Bash(unit, target);
                case Creature.ShadowMage: return CastShadowBurst(unit, target);
                default: return false;
            }
        }

        bool HitAbility(Unit source, Unit target, float range, float damage, CueKind cue)
        {
            if (!InRange(source, target, range)) return false;
            Face(source, target.Position);
            Emit(cue, source, target.Position);
            Damage(source, target, damage);
            return true;
        }

        bool CastStomp(Unit unit, Unit target)
        {
            float widening = Has(Upgrade.WiderStomp) ? balance.widerStomp : 0;
            float radius = balance.stompRadius + widening;
            if (!InRange(unit, target, radius)) return false;
            if (Has(Upgrade.StompShield)) Shield(unit, balance.stompShield, balance.stompShieldDuration);
            float damage = balance.stompDamage * (Has(Upgrade.HarderStomp) ? balance.harderStomp : 1);
            AreaHit(unit, unit.Position, radius, damage, CueKind.Stomp);
            if (Has(Upgrade.Aftershock)) aftershocks.Add((unit, unit.Position, Elapsed + balance.aftershockDelay));
            return true;
        }

        bool CastBreath(Unit unit, Unit target)
        {
            if (!InRange(unit, target, balance.breathRange)) return false;
            Face(unit, target.Position);
            float coneDot = Mathf.Cos(balance.breathConeAngle * .5f * Mathf.Deg2Rad);
            Unit firstHit = RivalsWithin(unit, unit.Position, balance.breathRange)
                .Where(foe => Vector2.Dot(unit.Facing, (foe.Position - unit.Position).normalized) >= coneDot)
                .OrderBy(foe => (foe.Position - unit.Position).sqrMagnitude).FirstOrDefault();
            if (firstHit == null) return false;
            Emit(CueKind.Breath, unit, firstHit.Position, balance.breathConeAngle);
            Damage(unit, firstHit, balance.breathDamage);
            if (!firstHit.Alive) return true;
            firstHit.BurnSource = unit;
            firstHit.BurnTicks = Mathf.RoundToInt(balance.burnTicks + (Has(Upgrade.LongerBurn) ? balance.longerBurnTicks : 0));
            firstHit.BurnTimer = BurnInterval;
            return true;
        }

        float BurnInterval => Has(Upgrade.LongerBurn) ? balance.longerBurnInterval : balance.burnInterval;

        bool CastBeam(Unit unit)
        {
            Unit target = units.Where(ally => ally.Player == unit.Player && ally.Alive)
                .OrderBy(ally => ally.HealthFraction).First();
            float radius = balance.beamRadius + (Has(Upgrade.WiderBeam) ? balance.widerBeam : 0);
            beams.Add(new BeamField(unit, target.Position, radius, balance.beamDuration));
            Emit(CueKind.Beam, unit, target.Position, radius);
            return true;
        }

        void UpdateFields(float dt)
        {
            for (int i = 0; i < beams.Count && Result == BattleResult.Fighting;)
            {
                BeamField beam = beams[i];
                beam.TickTimer -= dt;
                if (beam.TickTimer <= .0001f)
                {
                    TickBeam(beam);
                    beam.TickTimer += Mathf.Max(dt, balance.beamInterval);
                }
                beam.Remaining -= dt;
                if (beam.Remaining <= .0001f) beams.RemoveAt(i);
                else i++;
            }

            for (int i = 0; i < aftershocks.Count && Result == BattleResult.Fighting;)
            {
                var shock = aftershocks[i];
                if (Elapsed < shock.At) { i++; continue; }
                aftershocks.RemoveAt(i);
                float radius = balance.aftershockRadius + (Has(Upgrade.WiderStomp) ? balance.widerStomp : 0);
                AreaHit(shock.Source, shock.Center, radius, balance.aftershockDamage, CueKind.Stomp);
            }
        }

        void TickBeam(BeamField beam)
        {
            foreach (Unit unit in units)
            {
                if (Result != BattleResult.Fighting) break;
                if (!unit.Alive || !beam.Contains(unit.Position)) continue;
                if (unit.Player == beam.Source.Player)
                {
                    if (Heal(unit, balance.beamHeal * HealingMultiplier) > 0 && unit.Kind == Creature.Kaiju && Has(Upgrade.SharedLight))
                        Heal(beam.Source, balance.sharedLightHeal);
                }
                else
                {
                    Damage(beam.Source, unit, balance.beamDamage * (Has(Upgrade.DoubleBeamDamage) ? balance.doubleBeamDamage : 1));
                    Weaken(unit);
                }
            }
        }

        float HealingMultiplier => Has(Upgrade.DoubleHealing) ? balance.doubleHealing : 1;

        bool CastTeamShield(Unit source)
        {
            Unit[] allies = units.Where(unit => unit.Player && unit.Alive).ToArray();
            foreach (Unit ally in allies)
            {
                Shield(ally, balance.teamShield, balance.teamShieldDuration);
                if (Has(Upgrade.ShieldHeal)) Heal(ally, balance.shieldHeal * HealingMultiplier);
            }
            if (Has(Upgrade.ShieldPulse))
            {
                foreach (Unit ally in allies)
                {
                    if (Result != BattleResult.Fighting) break;
                    AreaHit(source, ally.Position, balance.shieldPulseRadius, balance.shieldPulseDamage, CueKind.Shock, true);
                }
            }
            return true;
        }

        void AreaHit(Unit source, Vector2 center, float radius, float damage, CueKind cue, bool weaken = false)
        {
            Emit(cue, source, center, radius);
            foreach (Unit foe in RivalsWithin(source, center, radius))
            {
                if (Result != BattleResult.Fighting) break;
                Damage(source, foe, damage);
                if (weaken) Weaken(foe);
            }
        }

        void Weaken(Unit unit)
        {
            if (Has(Upgrade.Weaken)) unit.WeakenTime = balance.weakenDuration;
        }

        bool CastEnemyHeal(Unit source, float heal)
        {
            Unit[] targets = units.Where(unit => unit.Player == source.Player && unit != source && unit.Alive && InRange(unit, source, balance.healRange))
                .OrderBy(unit => unit.HealthFraction).Take(2).ToArray();
            if (!targets.Any(unit => unit.Health < unit.MaxHealth)) return false;
            foreach (Unit target in targets) Heal(target, heal, source);
            return true;
        }

        bool Multiply(Unit source, Unit closestPlayer)
        {
            Unit original = units.Where(unit => !unit.Player && unit.Alive && !unit.Clone && unit != source)
                .OrderBy(unit => (unit.Position - closestPlayer.Position).sqrMagnitude).FirstOrDefault();
            if (original == null) return false;
            for (int i = 0; i < 2; i++)
            {
                Vector2 direction = i == 0 ? Vector2.left : Vector2.right;
                Vector2 spawn = OpenNear(original.Position, direction);
                Unit clone = AddAt(original.Kind, false, spawn, clone: true);
                Emit(CueKind.Summon, clone, spawn, .6f);
            }
            return true;
        }

        bool Blink(Unit source)
        {
            Unit target = Closest(source, farthest: true);
            if (target == null) return false;
            Vector2 from = source.Position;
            source.Position = OpenNear(target.Position, (from - target.Position).normalized, source);
            Face(source, target.Position);
            Emit(CueKind.Blink, source, target.Position, from: from);
            Damage(source, target, balance.blinkDamage);
            return true;
        }

        bool Summon(Unit source)
        {
            if (source.Recharging) return false;
            float cost = Mathf.Clamp(balance.summonCost, 0, balance.wizardMana);
            int knights = units.Count(unit => unit.Alive && unit.Kind == Creature.Knight && !unit.Clone);
            int mages = units.Count(unit => unit.Alive && unit.Kind == Creature.ShadowMage && !unit.Clone);
            bool summonKnight = (knights == 0 || mages >= balance.shadowMageCap) && knights < balance.knightCap;
            if (!summonKnight && mages >= balance.shadowMageCap) return false;
            int slot = SummonSlot(source, summonKnight);
            if (slot < 0) return false;

            Unit summon = Add(summonKnight ? Creature.Knight : Creature.ShadowMage, false, slot);
            Emit(CueKind.Summon, summon, summon.Position, .72f);
            source.Mana = Mathf.Max(0, source.Mana - cost);
            // A remainder smaller than one summon is spent with the last affordable cast.
            if (source.Mana <= .001f || source.Mana + .001f < cost)
            {
                source.Mana = 0;
                source.Recharging = true;
            }
            return true;
        }

        int SummonSlot(Unit source, bool knight)
        {
            // Enemy front row faces the player. The wizard's current column determines the knight's first choice.
            int preferred = Mathf.Clamp(Mathf.RoundToInt(source.Position.x / balance.columnSpacing) + 1, 0, 2);
            if (!knight)
            {
                Unit[] otherMages = units.Where(unit => unit.Alive && !unit.Player && (unit.Kind == Creature.ShadowMage || unit == source)).ToArray();
                preferred = Enumerable.Range(6, 3).OrderByDescending(slot => otherMages.Min(mage =>
                    (mage.Position - balance.SlotPosition(false, slot)).sqrMagnitude)).First();
            }

            Vector2 origin = balance.SlotPosition(false, preferred);
            return Enumerable.Range(0, 9).OrderBy(slot => (balance.SlotPosition(false, slot) - origin).sqrMagnitude)
                .ThenBy(slot => Mathf.Abs(slot / 3 - preferred / 3))
                .Where(slot => SpotOpen(balance.SlotPosition(false, slot))).DefaultIfEmpty(-1).First();
        }

        bool Bash(Unit source, Unit closest)
        {
            Unit target = units.FirstOrDefault(unit => unit.Alive && unit.Player && unit.Kind == Creature.Kaiju) ?? closest;
            if (!HitAbility(source, target, source.Stats.range, balance.shieldBash, CueKind.Shock)) return false;
            if (target.Alive) MoveClear(target, (target.Position - source.Position).normalized * balance.knockback);
            return true;
        }

        bool CastShadowBurst(Unit source, Unit target)
        {
            if (!InRange(source, target, balance.shadowBurstRange)) return false;
            AreaHit(source, target.Position, balance.shadowBurstRadius, balance.shadowBurst, CueKind.Shock);
            return true;
        }
    }
}
