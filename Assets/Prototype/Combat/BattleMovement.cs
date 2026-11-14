using System.Linq;
using UnityEngine;

namespace CreatureRun
{
    public sealed partial class Battle
    {
        static readonly float[] DetourAngles = { 32, 62, 91, 119 };

        void MoveToward(Unit unit, Unit target, float dt, bool slowed)
        {
            Face(unit, target.Position);
            float speed = unit.Stats.speed * (slowed ? balance.beamSlow : 1);
            float distance = Mathf.Min(dt * speed, Vector2.Distance(unit.Position, target.Position) - unit.Stats.range * .97f);
            if (distance <= 0) return;

            Vector2 heading = unit.Facing;
            float lookAhead = Mathf.Max(distance, balance.bodyRadius * 1.55f);
            if (StepIsOpen(unit, heading, lookAhead, distance))
            {
                unit.AvoidSide = 0;
                return;
            }

            int preferred = unit.AvoidSide != 0 ? unit.AvoidSide : unit.Position.x > 0 ? -1 : 1;
            foreach (int side in new[] { preferred, -preferred })
            {
                foreach (float angle in DetourAngles)
                {
                    float radians = side * angle * Mathf.Deg2Rad;
                    var detour = new Vector2(heading.x * Mathf.Cos(radians) - heading.y * Mathf.Sin(radians),
                        heading.x * Mathf.Sin(radians) + heading.y * Mathf.Cos(radians));
                    if (!StepIsOpen(unit, detour, lookAhead, distance)) continue;
                    unit.AvoidSide = side;
                    return;
                }
            }
        }

        bool StepIsOpen(Unit unit, Vector2 heading, float lookAhead, float distance)
        {
            Vector2 ahead = unit.Position + heading * lookAhead;
            Vector2 end = ClampToArena(unit.Position + heading * distance);
            if ((end - unit.Position).sqrMagnitude < .000001f || !PathOpen(unit, ClampToArena(ahead))) return false;
            unit.Position = end;
            return true;
        }

        bool PathOpen(Unit unit, Vector2 end)
        {
            Vector2 travel = end - unit.Position;
            float lengthSquared = travel.sqrMagnitude;
            if (lengthSquared <= .000001f) return false;
            float separationSquared = 4 * balance.bodyRadius * balance.bodyRadius;
            foreach (Unit other in units)
            {
                if (other == unit || !other.Alive) continue;
                Vector2 offset = other.Position - unit.Position;
                float along = Mathf.Clamp01(Vector2.Dot(offset, travel) / lengthSquared);
                float closest = (offset - travel * along).sqrMagnitude;
                if (closest < separationSquared - .0001f && closest < offset.sqrMagnitude - .0001f) return false;
            }
            return true;
        }

        void MoveClear(Unit unit, Vector2 displacement)
        {
            Vector2 end = ClampToArena(unit.Position + displacement);
            if (PathOpen(unit, end)) { unit.Position = end; return; }
            end = ClampToArena(unit.Position + displacement * .5f);
            if (PathOpen(unit, end)) unit.Position = end;
        }

        Vector2 ClampToArena(Vector2 point) => new(Mathf.Clamp(point.x, -balance.arenaHalfWidth, balance.arenaHalfWidth),
            Mathf.Clamp(point.y, -balance.arenaHalfHeight, balance.arenaHalfHeight));

        bool SpotOpen(Vector2 point, Unit ignore = null) =>
            !units.Any(unit => unit != ignore && unit.Alive && InRange(point, unit.Position, balance.bodyRadius * 2 - .005f));

        Vector2 OpenNear(Vector2 center, Vector2 direction, Unit ignore = null)
        {
            float startAngle = Mathf.Atan2(direction.y, direction.x);
            for (int ring = 1; ring <= 12; ring++)
            {
                for (int i = 0; i < 12; i++)
                {
                    float angle = startAngle + i * Mathf.PI / 6;
                    var point = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (balance.bodyRadius * (ring + 1.2f));
                    if (point == ClampToArena(point) && SpotOpen(point, ignore)) return point;
                }
            }
            return Enumerable.Range(0, 9).Select(slot => balance.SlotPosition(false, slot))
                .Where(point => SpotOpen(point, ignore)).OrderBy(point => (point - center).sqrMagnitude)
                .DefaultIfEmpty(center).First();
        }
    }
}
