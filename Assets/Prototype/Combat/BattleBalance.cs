using System;
using UnityEngine;

namespace CreatureRun
{
    public enum Creature { Kaiju, Support, Slime, Guard, GoblinHealer, Spider, Archer, Armor, Healer, Illusionist, BlinkMage, Wizard, Knight, ShadowMage }

    [Serializable]
    public sealed class CreatureStats
    {
        public float health, attack, attackInterval, range, speed, firstCooldown, secondCooldown;

        public CreatureStats(float health, float attack, float attackInterval, float range, float speed, float firstCooldown = 0, float secondCooldown = 0)
        {
            this.health = health;
            this.attack = attack;
            this.attackInterval = attackInterval;
            this.range = range;
            this.speed = speed;
            this.firstCooldown = firstCooldown;
            this.secondCooldown = secondCooldown;
        }
    }

    [CreateAssetMenu(menuName = "Creature Run/Battle Balance")]
    public sealed class BattleBalance : ScriptableObject
    {
        [Header("Run and arena")]
        public float winHeal = 22, knockoutHealthFraction = .5f;
        public float step = 1f / 30, bodyRadius = .34f, attackWindup = .16f, initialCooldownFraction = .45f;
        public float arenaHalfWidth = 3.65f, arenaHalfHeight = 4.75f;
        public float columnSpacing = 2.22f, frontY = 1.36f, rowSpacing = 1.3f;

        [Header("Player stats")]
        public CreatureStats kaiju = new(200, 15, .72f, 1.02f, 2.18f, 4.1f, 6.4f);
        public CreatureStats support = new(96, 4.2f, 1.26f, 3.85f, 1.92f, 6.1f, 7.6f);

        [Header("World 1 enemy stats")]
        public CreatureStats slime = new(51, 4.6f, 1.25f, .95f, 1.55f, 4.6f);
        public CreatureStats guard = new(70, 5.3f, 1.36f, .98f, 1.47f, 4.7f);
        public CreatureStats goblinHealer = new(48, 2.4f, 1.65f, 3.45f, 1.36f, 5.3f);
        public CreatureStats spider = new(103, 7.1f, 1.26f, 1, 1.81f, 4);
        public CreatureStats archer = new(47, 4.3f, 1.4f, 4.2f, 1.31f, 6.4f);

        [Header("World 2 enemy stats")]
        public CreatureStats armor = new(178, 13.1f, 1.3f, 1.04f, 1.36f, 5.5f);
        public CreatureStats healer = new(85, 7, 1.47f, 4.02f, 1.35f, 5.2f);
        public CreatureStats illusionist = new(90, 9, 1.46f, 3.72f, 1.46f, 5.5f);
        public CreatureStats blinkMage = new(158, 11.4f, 1.32f, 3.35f, 1.52f, 3.45f);
        public CreatureStats wizard = new(217, 11.4f, 1.48f, 4.12f, 1.21f, 2.35f);
        public CreatureStats knight = new(108, 4.75f, 1.53f, 1.03f, 1.53f, 5.1f);
        public CreatureStats shadowMage = new(53, 4.6f, 1.72f, 3.75f, 1.27f, 4.4f);

        [Header("Kaiju kit and upgrades")]
        public float stompDamage = 23, stompRadius = 1.53f, breathDamage = 19, breathRange = 2.45f, breathConeAngle = 46;
        public float burnDamage = 4, burnTicks = 3, burnInterval = .6f, missingHealthDamage = .36f;
        public float widerStomp = 1.02f, longerBurnTicks = 5, longerBurnInterval = .4f, stompShield = 12, stompShieldDuration = 3.2f;
        public float harderStomp = 1.6f, fasterClaw = .78f, burnHeal = 4.6f, aftershockDelay = .48f;
        public float aftershockDamage = 15, aftershockRadius = 1.53f, hideThreshold = .5f, hideReduction = .32f, clawFollowUp = 11;

        [Header("Support kit and upgrades")]
        public float beamRadius = 1.38f, beamDuration = 2.8f, beamInterval = .55f, beamHeal = 4.8f, beamDamage = 3.8f;
        public float beamCooldownRate = 1.2f, teamShield = 16, teamShieldDuration = 3.35f;
        public float sharedLightHeal = 8, chainHeal = 18, chainStunRadius = 1.9f, chainStunTime = 1.05f;
        public float doubleHealing = 2, shieldHeal = 13, doubleBeamDamage = 2, shieldPulseRadius = 1.75f, shieldPulseDamage = 17;
        public float beamSlow = .42f, weakenMultiplier = .68f, weakenDuration = 2.8f, widerBeam = .85f;

        [Header("Enemy abilities")]
        public float envelopDamage = 8, envelopHeal = 5, guardDuration = 1.18f, guardReduction = .48f, guardThrust = 8;
        public float goblinHeal = 14, mageHeal = 15, healRange = 6.15f;
        public float spiderRoll = 14, spiderRollRange = 1.32f, enrageThreshold = .5f, enrageMultiplier = 1.6f;
        public int burstShots = 5;
        public float burstSpeed = 2, armorGuardDuration = 1.05f, cloneFraction = .46f, blinkDamage = 39;
        public float wizardMana = 90, summonCost = 30, manaRechargeTime = 10;
        public int knightCap = 3, shadowMageCap = 3;
        public float shieldBash = 7, knockback = .78f, shadowBurst = 21, shadowBurstRange = 3.6f, shadowBurstRadius = 1.45f;

        public CreatureStats Stats(Creature creature) => creature switch
        {
            Creature.Kaiju => kaiju, Creature.Support => support, Creature.Slime => slime, Creature.Guard => guard,
            Creature.GoblinHealer => goblinHealer, Creature.Spider => spider, Creature.Archer => archer, Creature.Armor => armor,
            Creature.Healer => healer, Creature.Illusionist => illusionist, Creature.BlinkMage => blinkMage, Creature.Wizard => wizard,
            Creature.Knight => knight, Creature.ShadowMage => shadowMage, _ => throw new ArgumentOutOfRangeException(nameof(creature))
        };

        public Vector2 SlotPosition(bool player, int slot) => new(
            (slot % 3 - 1) * columnSpacing, (player ? -1 : 1) * (frontY + slot / 3 * rowSpacing));
    }
}
