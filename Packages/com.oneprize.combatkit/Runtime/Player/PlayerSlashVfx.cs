using System.Collections.Generic;
using UnityEngine;
using CombatSystem.Data.Player;
namespace CombatSystem.Player
{
    [DisallowMultipleComponent, DefaultExecutionOrder(100)]
    public sealed class PlayerSlashVfx : MonoBehaviour
    {
        [SerializeField] private PlayerWeaponHitboxConfig hitConfig;
        [SerializeField] private Material trailMaterial;
        [SerializeField, Min(.01f)] private float trailLifetime = .14f;
        [SerializeField, Range(0,1)] private float bladeStart = .22f, bladeEnd = 1f;
        [SerializeField] private Color trailColor = new Color(.75f,.9f,1f,.75f);
        private PlayerCharacter player;
        private Animator animator;
        private BoxCollider sword;
        private Mesh mesh;
        private GameObject trailObject;
        private MeshRenderer renderer;
        private struct Sample { public Vector3 a,b; public float time; public int stroke; }
        private readonly List<Sample> samples = new List<Sample>(96);
        private readonly List<Vector3> vertices = new List<Vector3>(288);
        private readonly List<Color> colors = new List<Color>(288);
        private readonly List<Vector2> uv = new List<Vector2>(288);
        private readonly List<int> triangles = new List<int>(1152);
        private int sequence=-1,stroke;
        private float previousProgress;
        private bool wasActive,previousValid;
        private Vector3 previousStart,previousEnd;
        public int PlayedCount { get; private set; }
        public int TrailSampleCount => samples.Count;
        private void Awake()
        {
            player=GetComponent<PlayerCharacter>(); animator=GetComponent<Animator>();
            var weapon=GetComponentInChildren<PlayerWeaponHitbox>(true);
            if(weapon!=null)sword=weapon.GetComponent<BoxCollider>();
            trailObject=new GameObject("Sword Motion Trail");
            mesh=new Mesh { name="Sword swept blade ribbon" }; mesh.MarkDynamic();
            trailObject.AddComponent<MeshFilter>().sharedMesh=mesh;
            renderer=trailObject.AddComponent<MeshRenderer>(); renderer.sharedMaterial=trailMaterial;
            renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows=false; renderer.enabled=false;
        }
        private bool Window(float t,Vector2 w)=>t>=w.x && previousProgress<=w.y;
        private bool IsSwing()
        {
            var c=player.Combat;
            if(sequence!=c.AttackSequence){sequence=c.AttackSequence;previousProgress=0;wasActive=false;previousValid=false;}
            if(player.IsHitReacting||player.IsGuarding||c.IsChargingPower)return false;
            if(player.AirCombo.IsSlamming){var s=animator.GetCurrentAnimatorStateInfo(0);return s.IsName("AirSlamLoop")||(s.IsName("AirSlamEnd")&&s.normalizedTime<.15f);}
            if(!c.IsAttacking)return false;
            string name="Base Layer."+c.CurrentAttackStateName;
            var state=animator.GetCurrentAnimatorStateInfo(0);
            if(animator.IsInTransition(0)){var next=animator.GetNextAnimatorStateInfo(0);if(next.IsName(name))state=next;}
            if(!state.IsName(name))return false;
            float t=state.normalizedTime;bool active=false;
            if(c.IsUsingSkill)active=Window(t,hitConfig.skillFirstHitWindow)||Window(t,hitConfig.skillHitWindow);
            else if(c.IsReleasingPower)active=Window(t,player.Profile.powerAttack.dashHitWindow)||Window(t,player.Profile.powerAttack.slamHitWindow);
            else if(c.IsUsingPiercingSkill)active=player.PiercingDash.IsDashing;
            else if(c.IsUsingSkillI)active=Window(t,new Vector2(player.Profile.gatherSkill.recallProgress,player.Profile.gatherSkill.recallProgress+.12f));
            else if(c.IsUsingSkillC)active=Window(t,hitConfig.skillCHitWindow);
            else if(c.IsUsingUpperAttack)active=Window(t,hitConfig.upperHitWindow);
            else if(c.IsUsingChaseAttack)active=Window(t,hitConfig.chaseHitWindow);
            else if(c.IsSheathedCombo)active=Window(t,player.Profile.combat.sheathedHitWindow); else if(c.IsUsingEFollowUp)active=Window(t,player.Profile.combat.eFollowUpHitWindow);
            else if(c.IsUsingAirCombo){int i=c.AirComboIndex;active=Window(t,hitConfig.airHitWindows!=null&&i<hitConfig.airHitWindows.Length?hitConfig.airHitWindows[i]:hitConfig.hitWindow);}
            else if(c.IsUsingGroundCombo&&c.AttackComboIndex!=hitConfig.groundKickComboIndex){int i=c.AttackComboIndex;active=Window(t,hitConfig.groundHitWindows!=null&&i<hitConfig.groundHitWindows.Length?hitConfig.groundHitWindows[i]:hitConfig.hitWindow);}
            previousProgress=t;return active;
        }
        private void LateUpdate()
        {
            if(player==null||player.Combat==null||sword==null||hitConfig==null||trailMaterial==null)return;
            var size=sword.size;
            Vector3 axis=size.y>=size.x&&size.y>=size.z?Vector3.up:size.x>=size.z?Vector3.right:Vector3.forward;
            float length=Mathf.Max(size.x,Mathf.Max(size.y,size.z));
            Vector3 start=sword.transform.TransformPoint(sword.center+axis*((bladeStart-.5f)*length));
            Vector3 end=sword.transform.TransformPoint(sword.center+axis*((bladeEnd-.5f)*length));
            bool active=IsSwing();float now=Time.time;
            // Never bridge a teleport, a new attack, or separate hit windows.
            if(previousValid&&Vector3.Distance(end,previousEnd)>4f){wasActive=false;previousValid=false;}
            if(active)
            {
                if(!wasActive){stroke++;PlayedCount++;if(previousValid)samples.Add(new Sample{a=previousStart,b=previousEnd,time=now-Time.deltaTime,stroke=stroke});}
                if(!wasActive||(start-previousStart).sqrMagnitude+(end-previousEnd).sqrMagnitude>.000001f)
                    samples.Add(new Sample{a=start,b=end,time=now,stroke=stroke});
            }
            previousStart=start;previousEnd=end;previousValid=true;wasActive=active;
            while(samples.Count>0&&(now-samples[0].time>trailLifetime||samples.Count>96))samples.RemoveAt(0);
            vertices.Clear();colors.Clear();uv.Clear();triangles.Clear();
            for(int i=0;i<samples.Count;i++)
            {
                var s=samples[i];float fade=Mathf.Clamp01(1-(now-s.time)/Mathf.Max(.01f,trailLifetime));
                for(int j=0;j<3;j++){vertices.Add(Vector3.Lerp(s.a,s.b,j*.5f));var color=trailColor;color.a*=fade*fade*(j==1?1:0);colors.Add(color);uv.Add(new Vector2(fade,j*.5f));}
                if(i==0||s.stroke!=samples[i-1].stroke)continue;
                int a=(i-1)*3,b=i*3;
                for(int j=0;j<2;j++){triangles.Add(a+j);triangles.Add(b+j);triangles.Add(a+j+1);triangles.Add(a+j+1);triangles.Add(b+j);triangles.Add(b+j+1);}
            }
            mesh.Clear();mesh.SetVertices(vertices);mesh.SetColors(colors);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);mesh.RecalculateBounds();renderer.enabled=triangles.Count>0;
        }
        private void OnDisable(){samples.Clear();wasActive=previousValid=false;sequence=-1;if(mesh!=null)mesh.Clear();if(renderer!=null)renderer.enabled=false;}
        private void OnDestroy(){if(trailObject!=null)Destroy(trailObject);if(mesh!=null)Destroy(mesh);}
    }
}
