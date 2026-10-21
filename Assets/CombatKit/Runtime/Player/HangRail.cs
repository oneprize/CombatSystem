using UnityEngine;
namespace CombatSystem.Player
{
    public sealed class HangRail : MonoBehaviour
    {
        public Vector3 localStart = new Vector3(0,-.75f,0);
        public Vector3 localEnd = new Vector3(0,.75f,0);
        public Vector3 ClosestPoint(Vector3 p)
        {
            Vector3 a=transform.TransformPoint(localStart),b=transform.TransformPoint(localEnd);
            Vector3 d=b-a;
            return a+d*Mathf.Clamp01(Vector3.Dot(p-a,d)/Mathf.Max(.001f,d.sqrMagnitude));
        }
        public Vector3 Axis => (transform.TransformPoint(localEnd)-transform.TransformPoint(localStart)).normalized;
    }
}