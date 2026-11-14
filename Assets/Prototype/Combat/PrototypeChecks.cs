#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace CreatureRun
{
    public static class PrototypeChecks
    {
        static readonly int[] KaijuFormation = { 1, 0, 6, 2, 7, 6, 2 };
        static readonly int[] SupportFormation = { 4, 4, 4, 4, 1, 1, 4 };
        static readonly Upgrade[] DraftPriority =
        {
            Upgrade.StompShield, Upgrade.FasterClaw, Upgrade.HarderStomp, Upgrade.Aftershock, Upgrade.ReinforcedHide,
            Upgrade.ClawFollowUp, Upgrade.BurnHeal, Upgrade.WiderStomp, Upgrade.LongerBurn,
            Upgrade.DoubleHealing, Upgrade.WiderBeam, Upgrade.ShieldHeal, Upgrade.Weaken, Upgrade.DoubleBeamDamage,
            Upgrade.ShieldPulse, Upgrade.SharedLight, Upgrade.SilverReprieve, Upgrade.SlowingBeam
        };

        static BattleBalance balance;

        [MenuItem("Creature Run/Verify prototype")]
        public static void Run()
        {
            balance = AssetDatabase.LoadAssetAtPath<BattleBalance>("Assets/Prototype/Battle Balance.asset");
            if (balance == null) balance = ScriptableObject.CreateInstance<BattleBalance>();
            VerifyDifficulty();
            int victories = 0;
            var defeats = new int[RunContent.Encounters.Length];
            string firstDefeat = null;
            for (int kaijuSlot = 0; kaijuSlot < 9; kaijuSlot++)
            {
                for (int seed = 0; seed < 27; seed++)
                {
                    int supportSlot = (seed / 3 + (seed / 3 >= kaijuSlot ? 1 : 0)) % 9;
                    if (supportSlot == kaijuSlot) supportSlot = (kaijuSlot + 1) % 9;
                    RunState run = FullRun(seed, _ => (kaijuSlot, supportSlot), offers => offers[seed % 3], out string report);
                    if (run.Phase == RunPhase.Victory) victories++;
                    else { defeats[run.EncounterIndex]++; firstDefeat ??= report; }
                }
            }
            Debug.Log($"Placement sweep: {victories} victories, defeats by battle: {string.Join("/", defeats)}.\nFirst defeat: {firstDefeat ?? "none"}");
            VerifyRecoveryAndDefeat();
            VerifyMovementAndPriority();
            VerifyKaijuUpgrades();
            VerifySupport();
            VerifyEnemyKits();
            VerifyRangedAttackCues();
            VerifyWizard();
            Debug.Log("CREATURE RUN PASS: full runs, drafts, placement, pathing and targeting, recovery and defeat, kaiju upgrades, support fields and shields, clone and blink kits, wizard priority / fallback / mana / victory.");
        }

        static void VerifyDifficulty()
        {
            for (int slot = 0; slot < 9; slot++)
            {
                var run = new RunState(balance, 0);
                run.Place(Creature.Kaiju, slot);
                Check(run.StartBattle(), "Slime Crossing starts with one placed creature");
                Simulate(run.Battle);
                Unit kaiju = run.Battle.Units[0];
                float slimeLoss = kaiju.MaxHealth - kaiju.Health;
                Check(run.Battle.Result == BattleResult.Victory && kaiju.HealthFraction >= .75f,
                    $"Slime Crossing leaves a comfortable margin from slot {slot + 1}");
                run.ResolveBattle();
                Check(run.Pick(FirstPick(run.Offers)) && run.StartBattle(), "Goblin Watch starts after drafting");
                float startingHealth = run.Battle.Units[0].Health;
                Simulate(run.Battle);
                Check(run.Battle.Result == BattleResult.Victory && startingHealth - run.Battle.Units[0].Health > slimeLoss,
                    $"Goblin Watch costs more health than Slime Crossing from slot {slot + 1}");
            }

            const int samples = 128;
            int firstPickWins = 0, draftWins = 0, formationWins = 0, plannedWins = 0;
            string winningRun = null;
            for (int seed = 0; seed < samples; seed++)
            {
                if (FullRun(seed, FixedFormation, FirstPick, out _).Phase == RunPhase.Victory) firstPickWins++;
                if (FullRun(seed, FixedFormation, PreferredPick, out _).Phase == RunPhase.Victory) draftWins++;
                if (FullRun(seed, PlannedFormation, FirstPick, out _).Phase == RunPhase.Victory) formationWins++;
                if (FullRun(seed, PlannedFormation, PreferredPick, out string summary).Phase != RunPhase.Victory) continue;
                plannedWins++;
                winningRun ??= $"Seed {seed}: {summary}";
            }

            Check(firstPickWins < samples / 2, "Fixed formation and first picks seldom clear the expedition");
            Check(draftWins > firstPickWins && formationWins > firstPickWins, "Draft and formation choices each improve results");
            Check(plannedWins > Math.Max(draftWins, formationWins), "Draft and formation together improve results");
            Debug.Log($"Balance sweep ({samples} shared seeds per strategy): fixed formation and first picks {firstPickWins}; preferred drafts {draftWins}; " +
                $"encounter-specific formations {formationWins}; both {plannedWins}.\nReference victory: {winningRun}");
        }

        static (int Kaiju, int Support) FixedFormation(int _) => (1, 4);
        static (int Kaiju, int Support) PlannedFormation(int index) => (KaijuFormation[index], SupportFormation[index]);
        static Upgrade FirstPick(IReadOnlyList<Upgrade> offers) => offers[0];
        static Upgrade PreferredPick(IReadOnlyList<Upgrade> offers) => offers.OrderBy(upgrade => Array.IndexOf(DraftPriority, upgrade)).First();

        static RunState FullRun(int seed, Func<int, (int Kaiju, int Support)> formation, Func<IReadOnlyList<Upgrade>, Upgrade> draft, out string summary)
        {
            var run = new RunState(balance, seed);
            var report = new List<string>();
            Check(!run.StartBattle(), "Go requires a placed creature");
            for (int index = 0; index < RunContent.Encounters.Length; index++)
            {
                var (kaijuSlot, supportSlot) = formation(index);
                if (index == 4) Check(!run.StartBattle(), "Go requires both creatures after recruitment");
                run.Place(Creature.Kaiju, kaijuSlot);
                if (index >= 4) run.Place(Creature.Support, supportSlot);
                bool support = index >= 4;
                int expectedPool = index < 4 ? 9 - index : 9 - (index - 4);
                Check(run.EncounterIndex == index && run.StartBattle(), $"Start battle {index + 1}");
                Battle battle = run.Battle;
                Check(battle.Units.Count(unit => unit.Player) == (support ? 2 : 1), "Correct party size");
                Check(battle.Units.First().Position == balance.SlotPosition(true, kaijuSlot), "Player start uses placement");
                foreach (var entry in run.Encounter.Enemies)
                    Check(battle.Units.Any(unit => !unit.Player && unit.Kind == entry.Kind && unit.StartingSlot == entry.Slot &&
                        unit.Position == balance.SlotPosition(false, entry.Slot)), "Preview and spawn share formation");
                Simulate(battle);
                string party = string.Join("/", battle.Units.Where(unit => unit.Player).Select(unit => Mathf.RoundToInt(unit.Health)));
                string foes = string.Join(", ", battle.Units.Where(unit => !unit.Player && unit.Alive).Select(unit => $"{unit.Kind}:{unit.Health:F0}"));
                string result = $"Seed {seed}, slots {kaijuSlot + 1}/{supportSlot + 1}, battle {index + 1}, {battle.Result}, {battle.Elapsed:F1}s, party {party}, remaining {foes}";
                Check(battle.Result != BattleResult.Fighting, result);
                report.Add($"{index + 1}: {battle.Elapsed:F1}s [{party} HP]");
                run.ResolveBattle();
                if (battle.Result == BattleResult.Defeat)
                {
                    Check(run.Phase == RunPhase.Defeat, "A simulated wipe ends the run");
                    summary = result;
                    return run;
                }
                if (index == 3)
                {
                    Check(run.Phase == RunPhase.Recruit && run.RecruitOffers.SequenceEqual(new[] { Creature.Support }), "Single recruit after battle four");
                    Check(run.Recruit(Creature.Support) && run.Health[Creature.Kaiju] == balance.kaiju.health, "Recruitment fully heals kaiju");
                }
                else if (index != 6)
                {
                    Check(run.Phase == RunPhase.Draft && run.Eligible(support).Count == expectedPool, "Correct draft pool size");
                    Check(run.Offers.Count == 3 && run.Offers.Distinct().Count() == 3, "Three distinct offers");
                    Upgrade[] offers = run.Offers.ToArray();
                    Upgrade pick = draft(offers);
                    Check(run.Pick(pick) && run.Chosen.Contains(pick), "Chosen upgrade persists");
                    Check(run.Eligible(support).Count == expectedPool - 1 && !run.Eligible(support).Contains(pick) &&
                        offers.Where(offer => offer != pick).All(run.Eligible(support).Contains), "Only choice leaves pool");
                    report.Add(pick.ToString());
                }
            }
            Check(run.Phase == RunPhase.Victory && run.Chosen.Count == 5, "Run victory with five upgrades");
            summary = string.Join(" | ", report);
            return run;
        }

        static void Simulate(Battle battle)
        {
            for (int i = 0; i < 6300 && battle.Result == BattleResult.Fighting; i++) battle.Step();
        }

        static void VerifyRecoveryAndDefeat()
        {
            var run = new RunState(balance, 0);
            run.Place(Creature.Kaiju, 1);
            for (int i = 0; i <= 4; i++)
            {
                if (i == 4) run.Place(Creature.Support, 4);
                Check(run.StartBattle(), "Recovery fixture starts");
                Unit kaiju = run.Battle.Units.First();
                kaiju.Health = 50;
                foreach (Unit unit in run.Battle.Units.Where(unit => !unit.Player || unit.Kind == Creature.Support)) unit.Health = 0;
                run.Battle.Step();
                run.ResolveBattle();
                Near(run.Health[Creature.Kaiju], 50 + balance.winHeal, "Survivor gets win heal");
                if (i == 3) run.Recruit(Creature.Support);
                else run.Pick(run.Offers[0]);
            }
            Check(run.StartBattle(), "Duo recovery enters next fight");
            Near(run.Battle.Units.Single(unit => unit.Kind == Creature.Support).Health, .5f * balance.support.health, "KO ally starts at half health");
            Near(run.Battle.Units.Single(unit => unit.Kind == Creature.Kaiju).Health, 50 + balance.winHeal, "Surviving ally carries HP into combat");
            foreach (Unit ally in run.Battle.Units.Where(unit => unit.Player)) ally.Health = 0;
            run.Battle.Step();
            run.ResolveBattle();
            Check(run.Phase == RunPhase.Defeat, "Team wipe ends run");
        }

        static void VerifyMovementAndPriority()
        {
            Battle battle = Fixture();
            Unit kaiju = Add(battle, Creature.Kaiju, true, 0, -3);
            Unit blocker = Add(battle, Creature.Support, true, 0, -2);
            blocker.StunTime = 100;
            Unit enemy = Add(battle, Creature.Armor, false, 0, 0);
            enemy.StunTime = 100;
            float side = 0;
            for (int i = 0; i < 105; i++) { battle.Step(); side = Mathf.Max(side, Mathf.Abs(kaiju.Position.x)); }
            Check(side > balance.bodyRadius && enemy.Health < enemy.MaxHealth, "Kaiju can detour around a teammate and attack");

            battle = Fixture();
            kaiju = Add(battle, Creature.Kaiju, true, 0, 0);
            enemy = Add(battle, Creature.Armor, false, 0, .9f);
            enemy.StunTime = 100;
            kaiju.First = kaiju.Second = 0;
            battle.Step();
            Check(battle.Cues.Any(cue => cue.Kind == CueKind.Stomp), "Stomp casts on one enemy and takes priority");
            battle.Step();
            Check(battle.Cues.Any(cue => cue.Kind == CueKind.Breath) && enemy.Burning, "Breath follows stomp and applies burn");

            battle = Fixture();
            kaiju = Add(battle, Creature.Kaiju, true, 0, 0);
            enemy = Add(battle, Creature.Armor, false, 0, .84f);
            Unit other = Add(battle, Creature.Armor, false, .9f, 0);
            enemy.StunTime = other.StunTime = 100;
            kaiju.First = kaiju.Second = 100;
            battle.Step();
            other.Position = new Vector2(.7f, 0);
            for (int i = 0; i < 6; i++) battle.Step();
            Check(enemy.Health < enemy.MaxHealth && other.Health == other.MaxHealth, "Target stays locked during swing");
            for (int i = 0; i < 28; i++) battle.Step();
            Check(other.Health < other.MaxHealth, "Next swing retargets nearest enemy");
        }

        static void VerifyKaijuUpgrades()
        {
            Battle battle = Fixture(Upgrade.WiderStomp, Upgrade.HarderStomp, Upgrade.Aftershock, Upgrade.StompShield);
            Unit kaiju = Add(battle, Creature.Kaiju, true, 0, 0);
            Unit close = Add(battle, Creature.Armor, false, 0, 1.45f);
            Unit wide = Add(battle, Creature.Armor, false, 0, 1.85f);
            close.StunTime = wide.StunTime = 100;
            kaiju.First = 0;
            kaiju.Second = 100;
            battle.Step();
            Near(wide.MaxHealth - wide.Health, balance.stompDamage * balance.harderStomp, "Harder Wider Stomp hits outside base radius");
            Near(kaiju.Shield, balance.stompShield, "Stomp Shield grants absorption");
            kaiju.StunTime = 100;
            for (int i = 0; i < Mathf.CeilToInt(balance.aftershockDelay / balance.step) + 1; i++) battle.Step();
            Near(close.MaxHealth - close.Health, balance.stompDamage * balance.harderStomp + balance.aftershockDamage,
                "Delayed aftershock reaches enemies beyond its base radius with Wider Stomp");

            battle = Fixture(Upgrade.LongerBurn, Upgrade.BurnHeal, Upgrade.ClawFollowUp, Upgrade.FasterClaw);
            kaiju = Add(battle, Creature.Kaiju, true, 0, 0);
            close = Add(battle, Creature.Armor, false, 0, .84f);
            wide = Add(battle, Creature.Armor, false, 0, 1.74f);
            close.StunTime = wide.StunTime = 100;
            kaiju.First = 100;
            kaiju.Second = 0;
            kaiju.Health = kaiju.MaxHealth * .5f;
            float damageMultiplier = 1 + .5f * balance.missingHealthDamage;
            battle.Step();
            Near(close.MaxHealth - close.Health, balance.breathDamage * damageMultiplier, "Low health scales breath damage");
            Check(close.BurnTicks == balance.burnTicks + balance.longerBurnTicks && !wide.Burning && wide.Health == wide.MaxHealth,
                "Breath stops at first enemy and Longer Burn adds ticks");
            battle.Step();
            Near(kaiju.Basic, balance.kaiju.attackInterval * balance.fasterClaw, "Faster Claw reduces attack interval");
            for (int i = 0; i < Mathf.CeilToInt(balance.attackWindup / balance.step) + 1; i++) battle.Step();
            Near(close.MaxHealth - close.Health, (balance.breathDamage + balance.kaiju.attack + balance.clawFollowUp) * damageMultiplier,
                "Claw Follow-Up adds damage while target is burning");
            kaiju.StunTime = 100;
            for (int i = 0; i < Mathf.CeilToInt(balance.longerBurnInterval / balance.step) - 1; i++) battle.Step();
            Near(kaiju.Health, kaiju.MaxHealth * .5f + balance.burnHeal, "Burn Heal restores health on a burn tick");
        }

        static void VerifySupport()
        {
            Battle battle = Fixture(Upgrade.DoubleHealing, Upgrade.ShieldHeal, Upgrade.SlowingBeam, Upgrade.Weaken);
            Unit kaiju = Add(battle, Creature.Kaiju, true, 0, 0);
            Unit support = Add(battle, Creature.Support, true, 3, -4);
            Unit enemy = Add(battle, Creature.Slime, false, 3, 3);
            enemy.StunTime = 100;
            kaiju.Health = 80;
            support.Health = 64;
            kaiju.StunTime = 100;
            support.First = 0;
            battle.Step();
            BeamField beam = battle.Beams.Single();
            Check(beam.Center == Vector2.zero, "Beam targets lowest percentage health ally");
            float first = kaiju.First;
            battle.Step();
            Near(kaiju.Health, 80 + balance.beamHeal * balance.doubleHealing, "Beam heals allies inside");
            Near(first - kaiju.First, balance.step * balance.beamCooldownRate, "Inside cooldown acceleration");
            kaiju.Position = new Vector2(-3, -3);
            enemy.Position = new Vector2(.5f, .3f);
            float basic = enemy.Basic;
            float hp = kaiju.Health;
            first = kaiju.First;
            battle.Step();
            Near(first - kaiju.First, balance.step, "Leaving returns cooldown rate to normal");
            Near(basic - enemy.Basic, balance.step * balance.beamSlow, "Slowing Beam delays enemy basic attack recovery");
            for (int i = 0; i < 18; i++) battle.Step();
            Check(enemy.Health < enemy.MaxHealth && enemy.WeakenTime > 0 && beam.Center == Vector2.zero, "Fixed beam damages and debuffs entering enemies");
            Near(kaiju.Health, hp, "Departing ally no longer healed");
            support.Second = 0;
            support.AttackTarget = null;
            battle.Step();
            Near(kaiju.Shield, balance.teamShield, "Shield reaches distant ally");
            Near(support.Shield, balance.teamShield, "Shield includes support");
            Near(kaiju.Health, hp + balance.shieldHeal * balance.doubleHealing, "Double Healing also doubles Shield Heal");
        }

        static void VerifyEnemyKits()
        {
            Battle battle = Fixture();
            Unit kaiju = Add(battle, Creature.Kaiju, true, 0, -2);
            Unit armor = Add(battle, Creature.Armor, false, 0, 0);
            Unit healer = Add(battle, Creature.Healer, false, 2, 2);
            Unit illusionist = Add(battle, Creature.Illusionist, false, 0, 3);
            kaiju.StunTime = armor.StunTime = healer.StunTime = 100;
            illusionist.First = 0;
            battle.Step();
            Unit[] clones = battle.Units.Where(unit => unit.Clone).ToArray();
            Check(clones.Length == 2 && clones.All(unit => unit.Kind == Creature.Armor), "Multiply copies closest original to illusionist's closest player");
            foreach (Unit clone in clones)
            {
                Near(clone.MaxHealth, armor.MaxHealth * balance.cloneFraction, "Clone has a fraction of the original HP");
                Near(clone.AttackDamage, armor.AttackDamage * balance.cloneFraction, "Clone has a fraction of the original damage");
                clone.First = 0;
                clone.Position = new Vector2(clone == clones[0] ? -.5f : .5f, -1.25f);
            }
            armor.Position = new Vector2(2, 2.8f);
            illusionist.First = 0;
            battle.Step();
            Check(battle.Units.Count(unit => unit.Clone && unit.Alive) == 4 && battle.Units.Count(unit => unit.Clone && unit.Kind == Creature.Healer) == 2,
                "Repeat Multiply ignores living clones in target selection");
            Check(clones.All(unit => !unit.Guarded && unit.AttackTarget == kaiju), "Clones use only basic attacks");

            battle = Fixture();
            kaiju = Add(battle, Creature.Kaiju, true, 0, -1);
            Unit support = Add(battle, Creature.Support, true, 2, -4);
            Unit blink = Add(battle, Creature.BlinkMage, false, 0, 1);
            kaiju.StunTime = support.StunTime = 100;
            blink.First = 0;
            battle.Step();
            Near(kaiju.Health, kaiju.MaxHealth, "Blink skips closer ally");
            Near(support.Health, support.MaxHealth - balance.blinkDamage, "Blink hits farthest ally");
            Check(Vector2.Distance(blink.Position, support.Position) < 1, "Blink lands next to its target");
        }

        static void VerifyRangedAttackCues()
        {
            Battle battle = Fixture();
            Unit kaiju = Add(battle, Creature.Kaiju, true, 0, -1.5f);
            Unit archer = Add(battle, Creature.Archer, false, 0, 2);
            kaiju.StunTime = 100;
            archer.First = 100;

            battle.Step();
            BattleCue launch = battle.Cues.Single(cue => cue.Kind == CueKind.Windup);
            Check(launch.Actor == archer && launch.Target == kaiju && launch.From == archer.Position,
                "Backline launches identify the shooter, origin and locked target");
            Near(kaiju.Health, kaiju.MaxHealth, "Launch cue fires before damage");

            for (int i = 0; i < Mathf.CeilToInt(balance.attackWindup / balance.step) + 1 && kaiju.Health == kaiju.MaxHealth; i++)
                battle.Step();
            BattleCue hit = battle.Cues.Single(cue => cue.Kind == CueKind.Strike);
            Check(hit.Actor == launch.Actor && hit.Target == launch.Target, "Backline hit matches its launch");
            Near(kaiju.Health, kaiju.MaxHealth - archer.AttackDamage, "Launch cue preserves basic attack damage");
        }

        static void VerifyWizard()
        {
            var battle = new Battle(balance, true, Array.Empty<Upgrade>());
            Unit kaiju = Add(battle, Creature.Kaiju, true, 0, -4);
            kaiju.StunTime = 100;
            Unit wizard = battle.Add(Creature.Wizard, false, 7);
            Unit blocker = battle.Add(Creature.Armor, false, 1);
            blocker.StunTime = 100;
            wizard.First = 0;
            battle.Step();
            Unit knight = battle.Units.Single(unit => unit.Kind == Creature.Knight);
            Check(knight.StartingSlot == 4, "First knight falls back to nearest open slot when front center is occupied");
            for (int i = 0; i < 2; i++) { wizard.First = 0; battle.Step(); }
            Check(battle.Units.Count(unit => unit.Kind == Creature.ShadowMage) == 2 && wizard.Recharging && wizard.Mana == 0,
                "Mages follow initial knight and three summons deplete mana");
            foreach (Unit unit in battle.Units.Where(unit => unit != wizard)) unit.StunTime = 100;
            wizard.Basic = 100;
            wizard.First = 0;
            for (int i = 0; i < Mathf.FloorToInt(balance.manaRechargeTime / balance.step) - 2; i++) battle.Step();
            Check(wizard.Recharging && battle.Units.Count == 6, "Wizard cannot summon during partial recharge");
            for (int i = 0; i < 6; i++) battle.Step();
            Check(!wizard.Recharging && battle.Units.Count(unit => unit.Kind == Creature.ShadowMage) == 3, "Ready summon fires after full recharge");
            for (int i = 0; i < 2; i++) { wizard.First = 0; battle.Step(); }
            Check(battle.Units.Count(unit => unit.Kind == Creature.Knight) == 3, "Wizard fills knights after hitting mage cap");
            wizard.Recharging = false;
            wizard.Mana = balance.wizardMana;
            int total = battle.Units.Count;
            wizard.First = 0;
            battle.Step();
            Check(battle.Units.Count == total && wizard.Mana == balance.wizardMana, "At both caps summoning spends no mana");
            wizard.Health = 1;
            kaiju.Position = wizard.Position + Vector2.down * .8f;
            kaiju.StunTime = 0;
            kaiju.First = 0;
            battle.Step();
            Check(battle.Result == BattleResult.Victory && battle.Units.Any(unit => unit.Kind == Creature.Knight && unit.Alive), "Wizard death wins immediately with summons alive");
        }

        static Battle Fixture(params Upgrade[] upgrades) => new(balance, false, upgrades);

        static Unit Add(Battle battle, Creature kind, bool player, float x, float y)
        {
            Unit unit = battle.Add(kind, player, 1);
            unit.Position = new Vector2(x, y);
            return unit;
        }

        static void Near(float value, float expected, string label) => Check(Mathf.Abs(value - expected) < .015f, $"{label}, expected {expected}, got {value}");
        static void Check(bool result, string label) { if (!result) throw new InvalidOperationException(label); }
    }
}
#endif
