using UnityEngine;

namespace CreatureRun
{
    public sealed partial class PrototypeGame
    {
        void DrawDraft()
        {
            Header($"BATTLE {run.EncounterIndex + 1} WON  /  {run.Encounter.Name.ToUpperInvariant()}", "Choose an upgrade");
            Creature kind = run.World == 1 ? Creature.Kaiju : Creature.Support;
            Color accent = ColorOf(kind);
            var hero = Bordered(content, "Upgrade recipient", new Rect(20, 103, 440, 188), PanelColor, Border);
            MapArt(hero, new Rect(0, 0, 438, 186), new Color(.6f, .7f, .65f));
            Texture(hero, "Creature halo", new Rect(271, 16, 145, 145), disc, Alpha(accent, .13f));
            Portrait(hero, new Rect(239, 3, 197, 174), kind);
            Label(hero, new Rect(18, 45, 230, 34), RunContent.Name(kind), 25, TextColor, bold: true);
            Label(hero, new Rect(18, 87, 200, 51), "Choose one to keep for the rest of this run.", 14, Muted, wrap: true);

            for (int i = 0; i < run.Offers.Count; i++)
            {
                UpgradeCard card = RunContent.Cards[(int)run.Offers[i]];
                var choice = Control(content, new Rect(20, 313 + i * 139, 440, 124), card.Title, () =>
                {
                    if (run.Pick(card.Id)) PreparePlanning();
                }, PanelColor, Border);
                Panel(choice, "Upgrade accent", new Rect(0, 0, 3, 122), accent);
                Texture(choice, "Upgrade symbol background", new Rect(17, 32, 60, 60), disc, Alpha(accent, .13f));
                Label(choice, new Rect(17, 32, 60, 60), card.Mark, 25, accent, TextAnchor.MiddleCenter, true);
                Label(choice, new Rect(92, 13, 285, 16), card.Category, 10, accent, bold: true);
                Label(choice, new Rect(92, 34, 305, 26), card.Title, 20, TextColor, bold: true);
                Label(choice, new Rect(92, 65, 302, 50), card.Describe(balance), 13, Muted, wrap: true);
                Label(choice, new Rect(400, 36, 24, 28), "›", 22, accent, TextAnchor.MiddleCenter);
            }

            Label(content, new Rect(22, 769, 426, 22), $"{run.Chosen.Count} OF 5 UPGRADES CHOSEN", 10, Gold, bold: true);
            ButtonLabel(content, new Rect(20, 800, 440, 38), "View current build", ShowBuild);
        }

        void DrawRecruit()
        {
            Header("BATTLE 4 WON  /  THE BRIAR GATE", "Recruit a companion");
            var region = Bordered(content, "Recruitment", new Rect(20, 102, 440, 532), PanelColor, Border);
            MapArt(region, new Rect(0, 0, 438, 530), new Color(.53f, .6f, .7f));
            int count = run.RecruitOffers.Count;
            for (int i = 0; i < count; i++)
            {
                Creature recruit = run.RecruitOffers[i];
                int artWidth = count == 1 ? 294 : 118;
                int center = (i * 2 + 1) * 438 / (2 * count);
                Texture(region, "Recruit halo", new Rect(center - artWidth / 2f, 51, artWidth, artWidth), disc, Alpha(ColorOf(recruit), .17f));
                Portrait(region, new Rect(center - artWidth / 2f, 54, artWidth, artWidth), recruit);
                Label(region, new Rect(center - 130, 362, 260, 39), RunContent.Name(recruit), count == 1 ? 31 : 13,
                    TextColor, TextAnchor.MiddleCenter, true);
                if (count == 1)
                {
                    Label(region, new Rect(30, 415, 378, 52), "A cloaked healer who channels a stationary beam and shields the whole party.",
                        15, Muted, TextAnchor.MiddleCenter, wrap: true);
                }
                float buttonWidth = 436f / count - 8;
                ButtonLabel(content, new Rect(20 + i * (buttonWidth + 8), 763, buttonWidth, 64),
                    count == 1 ? $"Recruit {RunContent.Name(recruit)}  →" : $"Recruit {RunContent.Name(recruit)}", () =>
                    {
                        if (run.Recruit(recruit)) PreparePlanning();
                    }, true);
            }

            Label(content, new Rect(22, 664, 420, 22), "WORLD 2  /  THE HOLLOW", 11, Gold, bold: true);
            Label(content, new Rect(22, 695, 420, 40), "The kaiju returns to full health when you recruit. Both creatures fight in the final three battles.",
                14, Muted, wrap: true);
        }

        void DrawEnding()
        {
            bool won = run.Phase == RunPhase.Victory;
            Color accent = won ? Gold : Enemy;
            Header(won ? "WORLD 2  /  THE HOLLOW SPIRE" : $"BATTLE {run.EncounterIndex + 1}  /  {run.Encounter.Name.ToUpperInvariant()}",
                won ? "Expedition complete" : "Expedition lost");
            var scene = Bordered(content, "Expedition result", new Rect(20, 100, 440, 475), PanelColor, Border);
            MapArt(scene, new Rect(0, 0, 438, 473), won ? Color.white : new Color(.68f, .51f, .63f));
            Texture(scene, "Result halo", new Rect(96, 53, 245, 245), disc, Alpha(accent, .21f));
            Texture(scene, "Result halo edge", new Rect(96, 53, 245, 245), ring, Alpha(accent, .4f));
            Portrait(scene, new Rect(47, 88, 226, 198), Creature.Kaiju);
            if (run.Health.ContainsKey(Creature.Support)) Portrait(scene, new Rect(213, 69, 174, 177), Creature.Support);
            Label(scene, new Rect(20, 319, 398, 47), won ? "Victory" : "Defeat", 39, accent, TextAnchor.MiddleCenter, true);
            Label(scene, new Rect(23, 378, 392, 51), won ? "The Hollow Wizard has fallen." : "Your party was knocked out.", 17,
                TextColor, TextAnchor.MiddleCenter, wrap: true);
            Label(content, new Rect(24, 602, 420, 24), $"{(won ? 7 : run.EncounterIndex)} OF 7 BATTLES WON", 12, accent, bold: true);
            Label(content, new Rect(24, 638, 420, 23), $"{run.Chosen.Count} upgrades drafted", 18, TextColor, bold: true);
            ButtonLabel(content, new Rect(20, 705, 440, 47), "View final build", ShowBuild);
            ButtonLabel(content, new Rect(20, 770, 440, 64), "New expedition  →", NewRun, true);
        }
    }
}
