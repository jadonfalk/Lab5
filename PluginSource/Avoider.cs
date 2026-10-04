using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace AvoiderPlugin
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Avoider/Avoider")]
    public sealed class Avoider : MonoBehaviour
    {
        [Header("Required setup")]
        [SerializeField] private Transform avoidee;
        [Tooltip("Only solid cover layers. Exclude player and avoider layers.")]
        [SerializeField] private LayerMask coverMask;
        [Header("Movement")]
        [SerializeField, Min(0.1f)] private float range = 10f;
        [SerializeField, Min(0.1f)] private float speed = 4f;
        [Header("Sampling")]
        [SerializeField, Range(2f, 25f)] private float searchRadius = 12f;
        [SerializeField, Min(0.5f)] private float pointSpacing = 2f;
        [SerializeField, Min(0.1f)] private float checkInterval = 0.5f;
        [SerializeField, Min(0.05f)] private float navMeshSnapDistance = 0.75f;
        [Tooltip("Avoidee eye height above its transform pivot.")]
        [SerializeField] private float avoideeEyeOffset = 0.7f;
        [Tooltip("Height above the NavMesh used for hiding visibility tests.")]
        [SerializeField, Min(0.1f)] private float hidingHeight = 1.5f;
        [Header("Debug")]
        [SerializeField] private bool showGizmos = true;

        public Transform Avoidee { get => avoidee; set => avoidee = value; }
        public LayerMask CoverMask { get => coverMask; set => coverMask = value; }
        public string Status { get; private set; } = "Waiting";
        private NavMeshAgent agent;
        private float nextCheck;
        private bool ownsPath, rotationSaved, oldUpdateRotation;
        private Vector3 destination;
        private string lastWarning;
        private readonly List<Vector3> points = new List<Vector3>();
        private readonly List<bool> hiddenPoints = new List<bool>();

        private void OnValidate()
        {
            range = Mathf.Max(0.1f, range); speed = Mathf.Max(0.1f, speed);
            searchRadius = Mathf.Clamp(searchRadius, 2f, 25f);
            pointSpacing = Mathf.Max(0.5f, pointSpacing);
            checkInterval = Mathf.Max(0.1f, checkInterval);
            navMeshSnapDistance = Mathf.Max(0.05f, navMeshSnapDistance);
            if (!GetComponent<NavMeshAgent>())
                Debug.LogWarning("Avoider: Add a NavMeshAgent and bake a NavMesh.", this);
        }

        private void OnEnable() { nextCheck = 0f; }
        private void Update()
        {
            // Face the target independently from travel direction (including when hidden).
            if (avoidee)
            {
                Vector3 direction = avoidee.position - transform.position;
                direction.y = 0;
                if (direction.sqrMagnitude > 0.0001f)
                    transform.rotation = Quaternion.Euler(0, Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg, 0);
            }
            if (Time.time < nextCheck) return;
            nextCheck = Time.time + checkInterval;
            if (!ValidateSetup()) { StopOwnedPath(); return; }
            agent.speed = speed;
            if (!rotationSaved)
            {
                oldUpdateRotation = agent.updateRotation;
                rotationSaved = true;
            }
            agent.updateRotation = false;

            Vector3 delta = avoidee.position - transform.position;
            delta.y = 0;
            if (delta.sqrMagnitude > range * range)
            {
                StopOwnedPath(); Status = "Target outside range"; points.Clear(); hiddenPoints.Clear(); return;
            }
            // Finish the chosen route even after first passing behind cover.
            if (ownsPath && IsHidden(destination) && agent.hasPath && !agent.isPathStale &&
                agent.pathStatus == NavMeshPathStatus.PathComplete &&
                agent.remainingDistance > agent.stoppingDistance + 0.1f)
            { Status = "Escaping"; return; }
            StopOwnedPath();
            if (IsHidden(agent.nextPosition)) { Status = "Hidden"; return; }
            FindHidingSpot();
        }

        private bool ValidateSetup()
        {
            if (!agent) agent = GetComponent<NavMeshAgent>();
            string warning = !agent ? "Add a NavMeshAgent and bake a NavMesh." :
                !agent.enabled || !agent.isOnNavMesh ? "Enable the NavMeshAgent, bake a NavMesh, and place the agent on it." :
                !avoidee ? "Assign the object to avoid (Avoidee)." :
                avoidee == transform ? "Avoidee must be a different object." :
                coverMask.value == 0 ? "Select your cover layer in Cover Mask." : null;
            if (warning != null)
            {
                Status = warning;
                if (lastWarning != warning) Debug.LogWarning("Avoider: " + warning, this);
                lastWarning = warning;
                return false;
            }
            lastWarning = null;
            return true;
        }

        private bool IsHidden(Vector3 groundPoint)
        {
            Vector3 eye = avoidee.position + Vector3.up * avoideeEyeOffset;
            return Physics.Linecast(eye, groundPoint + Vector3.up * hidingHeight,
                coverMask, QueryTriggerInteraction.Ignore);
        }

        private void FindHidingSpot()
        {
            points.Clear(); hiddenPoints.Clear();
            Vector3 origin = agent.nextPosition;
            float bestDistance = float.PositiveInfinity;
            NavMeshPath bestPath = null;
            var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
            var sampler = new PoissonDiscSampler(searchRadius * 2, searchRadius * 2, pointSpacing);
            foreach (Vector2 sample in sampler.Samples())
            {
                Vector3 candidate = origin + new Vector3(sample.x - searchRadius, 0, sample.y - searchRadius);
                if ((candidate - origin).sqrMagnitude > searchRadius * searchRadius) continue;
                NavMeshHit hit;
                if (!NavMesh.SamplePosition(candidate, out hit, navMeshSnapDistance, filter)) continue;
                bool hidden = IsHidden(hit.position);
                points.Add(hit.position); hiddenPoints.Add(hidden);
                float distance = (hit.position - origin).sqrMagnitude;
                if (!hidden || distance >= bestDistance) continue;
                var path = new NavMeshPath();
                // A sampled NavMesh point may be on a disconnected island.
                if (!agent.CalculatePath(hit.position, path) || path.status != NavMeshPathStatus.PathComplete) continue;
                bestDistance = distance; destination = hit.position; bestPath = path;
            }
            if (bestPath != null)
            {
                agent.isStopped = false;
                ownsPath = agent.SetPath(bestPath);
                Status = ownsPath ? "Escaping" : "Path assignment failed";
            }
            else Status = "No reachable hiding spot";
        }

        private void StopOwnedPath()
        {
            if (ownsPath && agent && agent.enabled && agent.isOnNavMesh) agent.ResetPath();
            ownsPath = false;
        }
        private void OnDisable()
        {
            StopOwnedPath();
            if (rotationSaved && agent) agent.updateRotation = oldUpdateRotation;
            rotationSaved = false;
        }
        private void OnDrawGizmos()
        {
            if (!showGizmos) return;
            Gizmos.color = Color.yellow; Gizmos.DrawWireSphere(transform.position, range);
            if (avoidee) { Gizmos.color = Color.white; Gizmos.DrawLine(transform.position, avoidee.position); }
            for (int i = 0; i < points.Count; i++)
            {
                Gizmos.color = hiddenPoints[i] ? Color.green : Color.red;
                Vector3 p = points[i] + Vector3.up * 0.1f;
                Gizmos.DrawLine(transform.position, p); Gizmos.DrawSphere(p, 0.08f);
            }
            if (ownsPath) { Gizmos.color = Color.cyan; Gizmos.DrawWireSphere(destination, 0.3f); }
        }
    }
}
