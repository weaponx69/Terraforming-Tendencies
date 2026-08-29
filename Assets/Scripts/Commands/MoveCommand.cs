using GameDevTV.RTS.Units;
using GameDevTV.RTS.Utilities;
using UnityEngine;
using GameDevTV.RTS.VisualScriptingStubs;

namespace GameDevTV.RTS.Commands
{
    /// <summary>
    /// Move command with formation spreading (Mathf.Cos/Sin).
    /// Formation math stays in C#. VS reads formation parameters.
    /// </summary>
    [IncludeInSettings(true)]
    [CreateAssetMenu(fileName = "Move Action", menuName = "Units/Commands/Move", order = 100)]
    public class MoveCommand : BaseCommand
    {
        /// <summary>Multiplier controlling unit spread radius in formation.</summary>
        [Inspectable]
        [SerializeField] private float radiusMultiplier = 3.5f;

        private int unitsOnLayer = 0;
        private int maxUnitsOnLayer = 1;
        private float circleRadius = 0;
        private float radialOffset = 0;
        
        public override bool CanHandle(CommandContext context)
        {
            return context.Commandable is AbstractUnit;
        }

        public override void Handle(CommandContext context)
        {
            AbstractUnit unit = (AbstractUnit)context.Commandable;

            if (context.Hit.collider != null && context.Hit.collider.TryGetComponent(out AbstractCommandable commandable)
                && commandable.IsVisible)
            {
                Vector3 followDest = SampleMoveDestination(unit, commandable.transform.position);
                Debug.Log($"[MoveCommand] {unit.name} follow={commandable.name} dest={followDest}");
                unit.MoveTo(followDest);
                return;
            }

            if (context.UnitIndex == 0)
            {
                unitsOnLayer = 0;
                maxUnitsOnLayer = 1;
                circleRadius = 0;
                radialOffset = 0;
            }
            
            Vector3 targetPosition = new(
                context.Hit.point.x + circleRadius * Mathf.Cos(radialOffset * unitsOnLayer),
                context.Hit.point.y,
                context.Hit.point.z + circleRadius * Mathf.Sin(radialOffset * unitsOnLayer)
            );

            Vector3 destination = SampleMoveDestination(unit, targetPosition);
            Debug.Log($"[MoveCommand] {unit.name} hit={context.Hit.collider?.name} point={context.Hit.point} dest={destination}");
            unit.MoveTo(destination);
            unitsOnLayer++;

            if (unitsOnLayer >= maxUnitsOnLayer)
            {
                unitsOnLayer = 0;
                circleRadius += unit.AgentRadius * radiusMultiplier;
                maxUnitsOnLayer = Mathf.FloorToInt(2 * Mathf.PI * circleRadius / (unit.AgentRadius * 2));
                radialOffset = 2 * Mathf.PI / maxUnitsOnLayer;
            }
        }

        private static Vector3 SampleMoveDestination(AbstractUnit unit, Vector3 approximatePosition)
        {
            if (unit.Agent != null &&
                NavMeshSpawnUtility.TrySamplePosition(
                    approximatePosition,
                    unit.Agent.agentTypeID,
                    NavMeshSpawnUtility.DefaultSampleRadius,
                    out UnityEngine.AI.NavMeshHit navHit))
            {
                return navHit.position;
            }

            return approximatePosition;
        }

        public override bool IsLocked(CommandContext context) => false;
    }
}