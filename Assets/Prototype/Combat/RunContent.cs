using System;
using System.Collections.Generic;
using System.Linq;

namespace CreatureRun
{
    public enum Upgrade
    {
        WiderStomp, LongerBurn, StompShield, HarderStomp, FasterClaw, BurnHeal, Aftershock, ReinforcedHide, ClawFollowUp,
        SharedLight, SilverReprieve, DoubleHealing, ShieldHeal, DoubleBeamDamage, ShieldPulse, SlowingBeam, Weaken, WiderBeam
    }

    public readonly struct UpgradeCard
    {
        public readonly Upgrade Id;
        public readonly string Title, Category, Mark;
        public readonly Func<BattleBalance, string> Describe;

        public UpgradeCard(Upgrade id, string title, Func<BattleBalance, string> describe, string category, string mark)
        {
            Id = id;
            Title = title;
            Describe = describe;
            Category = category;
            Mark = mark;
        }
    }

    public readonly struct FormationEntry
    {
        public readonly Creature Kind;
        public readonly int Slot;
        public FormationEntry(Creature kind, int slot) { Kind = kind; Slot = slot; }
    }

    public sealed class Encounter
    {
        public readonly string Name, Hint;
        public readonly FormationEntry[] Enemies;

        public Encounter(string name, string hint, params FormationEntry[] enemies)
        {
            Name = name;
            Hint = hint;
            Enemies = enemies;
        }
    }

    public static class RunContent
    {
        public static readonly Encounter[] Encounters =
        {
            new("Slime Crossing", "Two slimes leap in and heal when they envelop their target.", new(Creature.Slime, 0), new(Creature.Slime, 2)),
            new("Goblin Watch", "The guards brace and counter while their healer tends the frontline.",
                new(Creature.Guard, 0), new(Creature.Guard, 2), new(Creature.GoblinHealer, 7)),
            new("Silkwood Hollow", "Spider rolls hit harder below half health. Start farther back to draw the pair together.",
                new(Creature.Spider, 0), new(Creature.Spider, 2)),
            new("The Briar Gate", "Two archers fire in bursts. The right flank gives you a route around the spider.",
                new(Creature.Spider, 0), new(Creature.Archer, 6), new(Creature.GoblinHealer, 7), new(Creature.Archer, 8)),
            new("Hall of Mirrors", "The illusionist copies the ally nearest its target. Try starting on a flank to reach the casters.",
                new(Creature.Armor, 1), new(Creature.Healer, 6), new(Creature.Illusionist, 5)),
            new("The Astral Step", "The mage blinks to your farthest creature. Try placing the kaiju deeper to draw the hit.",
                new(Creature.Armor, 1), new(Creature.Healer, 6), new(Creature.BlinkMage, 8)),
            new("The Hollow Spire", "The right flank starts open. Reach the wizard before his summons build up.",
                new(Creature.Knight, 0), new(Creature.Knight, 1), new(Creature.Wizard, 7))
        };

        public static readonly UpgradeCard[] Cards =
        {
            new(Upgrade.WiderStomp, "Wider Stomp",
                b => $"Stomp and Aftershock each gain {b.widerStomp:0.##} units of radius.", "AREA", "◎"),
            new(Upgrade.LongerBurn, "Longer Burn",
                b => $"Flame Breath burns for {b.burnTicks + b.longerBurnTicks:0} ticks, one every {b.longerBurnInterval:0.##} s.", "BURN", "✦"),
            new(Upgrade.StompShield, "Stomp Shield",
                b => $"Stomp grants a {b.stompShield:0.#} HP shield for {b.stompShieldDuration:0.##} s.", "GUARD", "◇"),
            new(Upgrade.HarderStomp, "Harder Stomp",
                b => $"Stomp deals {(b.harderStomp - 1) * 100:0}% more damage.", "DAMAGE", "◆"),
            new(Upgrade.FasterClaw, "Faster Claw Swipe",
                b => $"Claw Swipe recovers in {b.kaiju.attackInterval * b.fasterClaw:0.##} s, down from {b.kaiju.attackInterval:0.##} s.", "SPEED", "↗"),
            new(Upgrade.BurnHeal, "Burn Heal",
                b => $"Each burn tick heals the kaiju for {b.burnHeal:0.#} HP.", "HEALING", "+"),
            new(Upgrade.Aftershock, "Aftershock",
                b => $"Stomp sends out a second shockwave for {b.aftershockDamage:0.#} damage.", "AREA", "◎"),
            new(Upgrade.ReinforcedHide, "Reinforced Hide",
                b => $"Take {b.hideReduction * 100:0}% less damage at {b.hideThreshold * 100:0}% HP or lower.", "GUARD", "◇"),
            new(Upgrade.ClawFollowUp, "Claw Follow-Up",
                b => $"Claw Swipe deals +{b.clawFollowUp:0.#} damage to burning enemies.", "DAMAGE", "◆"),
            new(Upgrade.SharedLight, "Shared Light",
                b => $"Each tick that heals the kaiju also heals Chainkeeper for {b.sharedLightHeal:0.#} HP.", "SUSTAIN", "+"),
            new(Upgrade.SilverReprieve, "Silver Reprieve",
                b => $"When Chainkeeper’s shield ends, heal {b.chainHeal:0.#} HP and stun nearby foes.", "SUSTAIN", "◇"),
            new(Upgrade.DoubleHealing, "Double Healing",
                b => $"Multiplies beam healing and Shield Heal by {b.doubleHealing:0.##}.", "HEALING", "+"),
            new(Upgrade.ShieldHeal, "Shield Heal",
                b => $"Casting the team shield heals each ally for {b.shieldHeal:0.#} HP.", "HEALING", "+"),
            new(Upgrade.DoubleBeamDamage, "Double Beam Damage",
                b => $"Beam damage per tick rises to {b.beamDamage * b.doubleBeamDamage:0.#}.", "DAMAGE", "◆"),
            new(Upgrade.ShieldPulse, "Shield Pulse",
                b => $"Casting the team shield deals {b.shieldPulseDamage:0.#} damage around each ally.", "DAMAGE", "◎"),
            new(Upgrade.SlowingBeam, "Slowing Beam",
                b => $"In the beam, enemies move and recover basic attacks {(1 - b.beamSlow) * 100:0}% slower.", "DEBUFF", "↓"),
            new(Upgrade.Weaken, "Withering Light",
                b => $"Beam, Shield Pulse and Silver Reprieve weaken foes by {(1 - b.weakenMultiplier) * 100:0}% for {b.weakenDuration:0.##} s.", "DEBUFF", "↓"),
            new(Upgrade.WiderBeam, "Wider Beam",
                b => $"The beam radius grows from {b.beamRadius:0.##} to {b.beamRadius + b.widerBeam:0.##} units.", "AREA", "◎")
        };

        public static string Name(Creature kind) => kind switch
        {
            Creature.Kaiju => "Baby Kaiju", Creature.Support => "Chainkeeper", Creature.Slime => "Slime",
            Creature.Guard => "Goblin Guard", Creature.GoblinHealer => "Goblin Healer", Creature.Spider => "Silkwood Spider",
            Creature.Archer => "Goblin Archer", Creature.Armor => "Animated Armor", Creature.Healer => "Healer Mage",
            Creature.Illusionist => "Illusionist", Creature.BlinkMage => "Blink Mage", Creature.Wizard => "Hollow Wizard",
            Creature.Knight => "Shadow Knight", Creature.ShadowMage => "Shadow Mage", _ => kind.ToString()
        };
    }

    public enum RunPhase { Planning, Combat, Draft, Recruit, Victory, Defeat }

    public sealed class RunState
    {
        readonly BattleBalance balance;
        readonly Random random;
        readonly List<Upgrade> chosen = new();
        readonly Dictionary<Creature, float> health = new();
        readonly Dictionary<Creature, int> slots = new();
        readonly List<Creature> recruitOffers = new();

        public int EncounterIndex { get; private set; }
        public Encounter Encounter => RunContent.Encounters[EncounterIndex];
        public RunPhase Phase { get; private set; }
        public IReadOnlyList<Upgrade> Chosen => chosen;
        public IReadOnlyList<Upgrade> Offers { get; private set; } = Array.Empty<Upgrade>();
        public IReadOnlyList<Creature> RecruitOffers => recruitOffers;
        public IReadOnlyDictionary<Creature, int> Slots => slots;
        public IReadOnlyDictionary<Creature, float> Health => health;
        public Battle Battle { get; private set; }
        public bool Ready => Phase == RunPhase.Planning && slots.Count == health.Count;
        public int World => EncounterIndex < 4 ? 1 : 2;

        public RunState(BattleBalance balance, int seed)
        {
            this.balance = balance;
            random = new Random(seed);
            health.Add(Creature.Kaiju, balance.kaiju.health);
        }

        public bool Place(Creature creature, int slot)
        {
            if (Phase != RunPhase.Planning || !health.ContainsKey(creature) || slot < 0 || slot >= 9) return false;
            foreach (var entry in slots.ToArray())
            {
                if (entry.Key == creature || entry.Value != slot) continue;
                if (slots.TryGetValue(creature, out int previous)) slots[entry.Key] = previous;
                else slots.Remove(entry.Key);
            }
            slots[creature] = slot;
            return true;
        }

        public void Remove(int slot)
        {
            if (Phase != RunPhase.Planning) return;
            foreach (var entry in slots.ToArray())
                if (entry.Value == slot) slots.Remove(entry.Key);
        }

        public bool StartBattle()
        {
            if (!Ready) return false;
            Battle = new Battle(balance, EncounterIndex == RunContent.Encounters.Length - 1, chosen);
            foreach (var kind in health.Keys)
                Battle.Add(kind, true, slots[kind], health[kind]);
            foreach (var enemy in Encounter.Enemies)
                Battle.Add(enemy.Kind, false, enemy.Slot);
            Phase = RunPhase.Combat;
            return true;
        }

        public List<Upgrade> Eligible(bool support) => RunContent.Cards
            .Where(card => ((int)card.Id >= (int)Upgrade.SharedLight) == support && !chosen.Contains(card.Id))
            .Select(card => card.Id).ToList();

        public void ResolveBattle()
        {
            if (Phase != RunPhase.Combat || Battle.Result == BattleResult.Fighting) return;
            if (Battle.Result == BattleResult.Defeat) { Phase = RunPhase.Defeat; return; }

            foreach (Unit ally in Battle.Units.Where(unit => unit.Player))
                health[ally.Kind] = ally.Alive ? Math.Min(ally.MaxHealth, ally.Health + balance.winHeal) : ally.MaxHealth * balance.knockoutHealthFraction;

            if (EncounterIndex == RunContent.Encounters.Length - 1) { Phase = RunPhase.Victory; return; }
            if (EncounterIndex == 3)
            {
                recruitOffers.Clear();
                recruitOffers.AddRange(AvailableRecruits().Take(3));
                Phase = RunPhase.Recruit;
                return;
            }

            List<Upgrade> pool = Eligible(World == 2);
            var offers = new Upgrade[3];
            for (int i = 0; i < offers.Length; i++)
            {
                int pick = random.Next(pool.Count);
                offers[i] = pool[pick];
                pool.RemoveAt(pick);
            }
            Offers = offers;
            Phase = RunPhase.Draft;
        }

        static IEnumerable<Creature> AvailableRecruits() { yield return Creature.Support; }

        public bool Pick(Upgrade upgrade)
        {
            if (Phase != RunPhase.Draft || !Offers.Contains(upgrade)) return false;
            chosen.Add(upgrade);
            Offers = Array.Empty<Upgrade>();
            Advance();
            return true;
        }

        public bool Recruit(Creature creature)
        {
            if (Phase != RunPhase.Recruit || !recruitOffers.Contains(creature)) return false;
            health.Add(creature, balance.Stats(creature).health);
            health[Creature.Kaiju] = balance.kaiju.health;
            recruitOffers.Clear();
            Advance();
            return true;
        }

        void Advance()
        {
            EncounterIndex++;
            Battle = null;
            Phase = RunPhase.Planning;
        }
    }
}
