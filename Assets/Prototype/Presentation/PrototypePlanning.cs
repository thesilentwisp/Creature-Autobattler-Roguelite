using System.Linq;
using UnityEngine;

namespace CreatureRun
{
    public sealed partial class PrototypeGame
    {
        static readonly Vector2[] Route =
        {
            new(165, 540), new(274, 459), new(150, 385), new(292, 308),
            new(196, 232), new(271, 151), new(196, 78)
        };
        static readonly string[] ShortNames = { "Slime Crossing", "Goblin Watch", "Silkwood Hollow", "The Briar Gate", "Hall of Mirrors", "The Astral Step", "The Hollow Spire" };

        void DrawPlanning()
        {
            Header(PlanningProgress, enemyPreview ? run.Encounter.Name : "Expedition map");
            ButtonLabel(content, new Rect(367, 22, 91, 29), $"Build {run.Chosen.Count}/5", ShowBuild);
            if (enemyPreview) DrawPreview();
            else DrawRoute();
            DrawFormation();
            if (slotMenu >= 0) DrawSlotMenu();
        }

        string PlanningProgress => $"WORLD {run.World}  /  BATTLE {run.EncounterIndex + 1} OF 7";

        void DrawRoute()
        {
            var map = Bordered(content, "Expedition route", new Rect(20, 97, 440, 516), PanelColor, Border);
            MapArt(map, new Rect(0, 0, 438, 514), Color.white);
            Label(map, new Rect(14, 12, 180, 20), "WORLD 2  /  THE HOLLOW", 10, TextColor, bold: true);
            Label(map, new Rect(14, 253, 180, 20), "WORLD 1  /  BRIARWOOD", 10, TextColor, bold: true);
            for (int i = 1; i < Route.Length; i++)
            {
                Vector2 from = RoutePoint(i - 1);
                Vector2 to = RoutePoint(i);
                Vector2 corner = Vector2.Lerp(from, to, .49f) + new Vector2(i % 2 == 0 ? -15 : 14, 1);
                Line(map, from, corner, 4, Alpha(Ink, .8f));
                Line(map, corner, to, 4, Alpha(Ink, .8f));
                Color color = i <= run.EncounterIndex ? Gold : Alpha(TextColor, .58f);
                Line(map, from, corner, 1.5f, color);
                Line(map, corner, to, 1.5f, color);
            }

            for (int i = 0; i < Route.Length; i++)
            {
                Vector2 point = RoutePoint(i);
                bool current = i == run.EncounterIndex;
                bool complete = i < run.EncounterIndex;
                Color accent = current ? Gold : complete ? Green : Muted;
                bool right = i % 2 == 0;
                var hit = new Rect(point.x - 25, point.y - 23, 50, 46);
                if (current)
                {
                    var control = Control(map, hit, $"Preview {run.Encounter.Name}", OpenPreview, Color.clear, Color.clear);
                    // The hit target stays transparent while the visible pin sits above it.
                    control.GetComponent<UnityEngine.UI.Image>().alphaHitTestMinimumThreshold = 0;
                    Texture(map, "Current destination halo", new Rect(point.x - 23, point.y - 23, 46, 46), disc, Alpha(Gold, .28f));
                }
                Texture(map, "Destination", new Rect(point.x - 16, point.y - 16, 32, 32), disc, current ? Gold : Ink);
                Texture(map, "Destination edge", new Rect(point.x - 16, point.y - 16, 32, 32), ring, accent);
                Label(map, new Rect(point.x - 16, point.y - 16, 32, 32), complete ? "✓" : (i + 1).ToString(), 13,
                    current ? Ink : accent, TextAnchor.MiddleCenter, true);
                var nameRect = new Rect(right ? point.x + 23 : point.x - 166, point.y - 12, 143, 24);
                if (current) Control(map, nameRect, run.Encounter.Name, OpenPreview, Color.clear, Color.clear);
                Label(map, nameRect, ShortNames[i], current ? 14 : 12, current ? Gold : TextColor,
                    right ? TextAnchor.MiddleLeft : TextAnchor.MiddleRight, true);
            }
            Label(map, new Rect(14, 479, 406, 22), "Tap the highlighted destination to scout.", 12, TextColor, bold: true);
        }

        static Vector2 RoutePoint(int index) => new(Route[index].x, Route[index].y * .79f + 27);

        void OpenPreview()
        {
            enemyPreview = true;
            slotMenu = -1;
            Render();
        }

        void DrawPreview()
        {
            var preview = Bordered(content, "Encounter preview", new Rect(20, 97, 440, 516), PanelColor, Border);
            MapArt(preview, new Rect(0, 0, 438, 514), new Color(.52f, .61f, .52f));
            ButtonLabel(preview, new Rect(13, 13, 91, 31), "‹  Map", () => { enemyPreview = false; Render(); });
            Label(preview, new Rect(210, 16, 209, 24), "ENEMY FORMATION  /  LOCKED", 10, Enemy, TextAnchor.MiddleRight, true);

            for (int row = 2; row >= 0; row--)
            {
                int y = 89 + (2 - row) * 105;
                Label(preview, new Rect(13, y + 34, 45, 19), row == 2 ? "BACK" : row == 0 ? "FRONT" : "MID", 9,
                    row == 0 ? Enemy : Muted, bold: true);
                for (int col = 0; col < 3; col++)
                {
                    int slotIndex = row * 3 + col;
                    var box = new Rect(61 + col * 110, y, 102, 96);
                    bool occupied = run.Encounter.Enemies.Any(entry => entry.Slot == slotIndex);
                    var tile = Bordered(preview, "Enemy slot", box, Alpha(Ink, occupied ? .89f : .48f), occupied ? Alpha(Enemy, .65f) : Alpha(Muted, .3f));
                    if (occupied)
                    {
                        Creature kind = run.Encounter.Enemies.First(entry => entry.Slot == slotIndex).Kind;
                        Texture(tile, "Portrait halo", new Rect(24, 5, 54, 54), disc, Alpha(ColorOf(kind), .13f));
                        Portrait(tile, new Rect(17, 0, 66, 66), kind);
                        Label(tile, new Rect(3, 69, 94, 17), RunContent.Name(kind), 9, TextColor, TextAnchor.MiddleCenter, true);
                    }
                    else Label(tile, new Rect(0, 0, 100, 94), "·", 20, Muted, TextAnchor.MiddleCenter);
                }
            }
            Panel(preview, "Encounter notes", new Rect(14, 417, 410, 78), Alpha(Ink, .9f));
            Label(preview, new Rect(27, 426, 384, 14), run.EncounterIndex == 6 ? "OBJECTIVE  /  DEFEAT THE WIZARD" : "OBJECTIVE  /  DEFEAT ALL ENEMIES", 10, Gold, bold: true);
            Label(preview, new Rect(27, 447, 382, 32), run.Encounter.Hint, 13, TextColor, wrap: true);
        }

        void DrawFormation()
        {
            var formation = Panel(content, "Your formation", new Rect(0, 622, Width, 232), PanelColor);
            Panel(formation, "Formation rule", new Rect(0, 0, Width, 1), Border);
            Label(formation, new Rect(20, 11, 176, 22), "Your formation", 18, TextColor, bold: true);
            Label(formation, new Rect(20, 35, 200, 16), "Tap a slot to place or move.", 11, Muted);

            for (int i = 0; i < 9; i++)
            {
                int slot = i;
                bool placed = run.Slots.Any(entry => entry.Value == slot);
                var tileRect = new Rect(63 + i % 3 * 49, 60 + i / 3 * 49, 44, 44);
                var tile = Control(formation, tileRect, $"Formation slot {i + 1}", () => { slotMenu = slot; Render(); },
                    slotMenu == slot ? Night : placed ? Hex(0x304437) : Tile, slotMenu == slot ? Gold : placed ? Green : Border);
                if (placed)
                {
                    Creature kind = run.Slots.First(entry => entry.Value == slot).Key;
                    Portrait(tile, new Rect(2, 2, 38, 38), kind);
                }
                else Label(tile, new Rect(0, 0, 42, 42), "+", 17, Alpha(Muted, .72f), TextAnchor.MiddleCenter);
            }

            for (int row = 0; row < 3; row++)
                Label(formation, new Rect(19, 70 + row * 49, 42, 23), row == 0 ? "FRONT" : row == 1 ? "MID" : "BACK", 9, Muted, bold: true);

            Label(formation, new Rect(224, 35, 220, 17), $"PARTY  /  {run.Slots.Count} OF {run.Health.Count} PLACED", 10, Gold, bold: true);
            int index = 0;
            foreach (var ally in run.Health)
            {
                float max = balance.Stats(ally.Key).health;
                int y = 61 + index++ * 44;
                Portrait(formation, new Rect(223, y - 2, 33, 33), ally.Key);
                Label(formation, new Rect(264, y, 174, 15), RunContent.Name(ally.Key), 13, TextColor, bold: true);
                Label(formation, new Rect(264, y + 15, 173, 15), $"{Mathf.CeilToInt(ally.Value)} / {max:0} HP", 11, ColorOf(ally.Key));
            }

            if (enemyPreview) ButtonLabel(formation, new Rect(224, 151, 233, 52), "Go  →", run.Ready ? StartCombat : null, true);
            else ButtonLabel(formation, new Rect(224, 151, 233, 52), "Scout encounter  →", OpenPreview, true);
        }

        void DrawSlotMenu()
        {
            var sheet = Control(content, new Rect(0, 0, Width, Height), "Dismiss placement", () => { slotMenu = -1; Render(); }, Alpha(Ink, .74f), Color.clear);
            int count = run.Health.Count;
            var menu = Bordered(sheet, "Creature placement", new Rect(28, 357 - (count - 1) * 30, 420, 160 + count * 53), PanelColor, Gold);
            Label(menu, new Rect(17, 16, 310, 28), "Place a creature", 22, TextColor, bold: true);
            string row = slotMenu < 3 ? "Front" : slotMenu < 6 ? "Middle" : "Back";
            string col = slotMenu % 3 == 0 ? "left" : slotMenu % 3 == 1 ? "center" : "right";
            Label(menu, new Rect(18, 50, 300, 19), $"{row} {col} slot", 13, Muted);
            ButtonLabel(menu, new Rect(344, 15, 53, 34), "×", () => { slotMenu = -1; Render(); });
            int index = 0;
            foreach (Creature kind in run.Health.Keys)
            {
                int y = 81 + index++ * 53;
                var choice = Control(menu, new Rect(16, y, 384, 46), RunContent.Name(kind), () =>
                {
                    run.Place(kind, slotMenu);
                    slotMenu = -1;
                    Render();
                }, Tile, ColorOf(kind));
                Portrait(choice, new Rect(8, 2, 40, 40), kind);
                Label(choice, new Rect(57, 3, 282, 40), RunContent.Name(kind), 16, TextColor, bold: true);
                Label(choice, new Rect(337, 3, 25, 40), "→", 16, ColorOf(kind), bold: true);
            }

            if (run.Slots.Values.Contains(slotMenu))
                ButtonLabel(menu, new Rect(16, 90 + count * 53, 384, 38), "Clear slot", () => { run.Remove(slotMenu); slotMenu = -1; Render(); });
        }

        void ShowBuild()
        {
            if (inspectingBuild) return;
            inspectingBuild = true;
            overlay = Control(content, new Rect(0, 0, Width, Height), "Close build", CloseBuild, Alpha(Ink, .8f), Color.clear);
            var sheet = Bordered(overlay, "Run upgrades", new Rect(26, 148, 424, 128 + Mathf.Max(1, run.Chosen.Count) * 73), PanelColor, Gold);
            Label(sheet, new Rect(18, 21, 320, 33), "Run upgrades", 26, TextColor, bold: true);
            ButtonLabel(sheet, new Rect(350, 19, 50, 36), "×", CloseBuild);
            if (run.Chosen.Count == 0)
                Label(sheet, new Rect(18, 72, 375, 44), "Win the first battle to draft your first upgrade.", 15, Muted, wrap: true);
            for (int i = 0; i < run.Chosen.Count; i++)
            {
                UpgradeCard card = RunContent.Cards[(int)run.Chosen[i]];
                int y = 69 + i * 73;
                Panel(sheet, "Upgrade rule", new Rect(19, y, 380, 1), Border);
                Label(sheet, new Rect(19, y + 9, 379, 20), card.Title, 16, ColorForUpgrade(card.Id), bold: true);
                Label(sheet, new Rect(19, y + 32, 379, 29), card.Describe(balance), 12, Muted, wrap: true);
            }
        }

        void CloseBuild() { Clear(ref overlay); inspectingBuild = false; }
        static Color ColorForUpgrade(Upgrade id) => id >= Upgrade.SharedLight ? Violet : Green;
    }
}
