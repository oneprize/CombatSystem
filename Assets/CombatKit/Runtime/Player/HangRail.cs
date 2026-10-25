using System.Collections.Generic;
using UnityEngine;
namespace CombatSystem.Player
{
    /// <summary>매달릴 수 있는 철봉. localStart~localEnd 선분이 손으로 잡는 구간이다.</summary>
    public sealed class HangRail : MonoBehaviour
    {
        public Vector3 localStart = new Vector3(0,-.75f,0);
        public Vector3 localEnd = new Vector3(0,.75f,0);

        /// <summary>활성화된 철봉 목록. 매 프레임 FindObjectsByType으로 찾지 않기 위해 사용한다.</summary>
        public static readonly List<HangRail> Active = new List<HangRail>();
        void OnEnable() { if (!Active.Contains(this)) Active.Add(this); }
        void OnDisable() { Active.Remove(this); }

        public Vector3 StartPoint => transform.TransformPoint(localStart);
        public Vector3 EndPoint => transform.TransformPoint(localEnd);
        public float Length => Vector3.Distance(StartPoint, EndPoint);

        public Vector3 ClosestPoint(Vector3 p)
        {
            Vector3 a=StartPoint,b=EndPoint;
            Vector3 d=b-a;
            return a+d*Mathf.Clamp01(Vector3.Dot(p-a,d)/Mathf.Max(.001f,d.sqrMagnitude));
        }

        /// <summary>
        /// p에 가장 가까운 잡기 중심점. 양손 간격(handSpacing)이 철봉 구간 안에 모두 들어오도록 끝점에서 안쪽으로 당긴다.
        /// 철봉이 양손 간격보다 짧으면 false.
        /// </summary>
        public bool TwoHandGrip(Vector3 p, float handSpacing, out Vector3 grip)
        {
            Vector3 a=StartPoint,b=EndPoint; float length=Vector3.Distance(a,b); grip=a;
            float half=handSpacing*.5f;
            if(length<handSpacing)return false;
            Vector3 axis=(b-a)/length;
            float along=Mathf.Clamp(Vector3.Dot(p-a,axis),half,length-half);
            grip=a+axis*along;
            return true;
        }

        public Vector3 Axis => (EndPoint-StartPoint).normalized;
    }
}
