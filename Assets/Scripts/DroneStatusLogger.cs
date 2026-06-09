using UnityEngine;
using UnityEngine.AI;
using Unity.Behavior;
using GameDevTV.RTS.Units;
using GameDevTV.RTS.Utilities;

public class DroneStatusLogger : MonoBehaviour
{
    private float lastLogTime = 0f;
    private const float LOG_INTERVAL = 2.0f;

    private void Update()
    {
        if (Time.time - lastLogTime < LOG_INTERVAL) return;
        lastLogTime = Time.time;

        var units = Object.FindObjectsByType<AbstractUnit>(FindObjectsInactive.Exclude);
        bool found = false;
        foreach (var unit in units)
        {
            if (!unit.name.Contains("Drone")) continue;
            found = true;

            var agent = unit.GetComponent<NavMeshAgent>();
            var graph = unit.GetComponent<BehaviorGraphAgent>();
            
            string cmd = "Unknown";
            if (graph != null && graph.GetVariable(BlackboardConstants.COMMAND, out BlackboardVariable<UnitCommands> cmdVar))
                cmd = cmdVar.Value.ToString();

            Vector3 target = Vector3.zero;
            if (graph != null && graph.GetVariable(BlackboardConstants.TARGET_LOCATION, out BlackboardVariable<Vector3> locVar))
                target = locVar.Value;

            GameObject targetObj = null;
            if (graph != null && graph.GetVariable(BlackboardConstants.TARGET_GAME_OBJECT, out BlackboardVariable<GameObject> objVar))
                targetObj = objVar.Value;

            Debug.Log($"[DroneDiag] {unit.name} | cmd={cmd} | onMesh={agent.isOnNavMesh} | hasPath={agent.hasPath} | isStopped={agent.isStopped} | targetLoc={target} | vel={agent.velocity.magnitude:F2}");
        }
        if (!found) Debug.Log("[DroneDiag] No drones found in scene.");
    }
}
