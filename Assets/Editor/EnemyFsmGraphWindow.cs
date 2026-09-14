using UnityEngine;
using UnityEditor;
using ParrySystem.Combat;
using System.Collections.Generic;

public class EnemyFsmGraphWindow : EditorWindow
{
    private class Node
    {
        public string id, summary, path;
        public Rect rect;
        public Node(string id, string summary, float x, float y)
        {
            this.id = id; this.summary = summary; rect = new Rect(x, y, 190, 80);
            path = id == "HIT" ? "Assets/Enemy/EnemyDamageReaction.cs" :
                "Assets/Enemy/States/Enemy" + char.ToUpper(id[0]) + id.Substring(1).ToLower() + "State.cs";
        }
    }
    private class Edge
    {
        public string from, to, condition;
        public Edge(string from, string to, string condition)
        { this.from = from; this.to = to; this.condition = condition; }
    }
    [SerializeField] private EnemyStateMachine target;
    [SerializeField] private string selected = "OBSERVE";
    private Vector2 detailScroll;
    private List<Node> nodes;
    private List<Edge> edges;
    private double nextRepaint;

    [MenuItem("Tools/Parry System/FSM Graph")]
    public static void Open()
    {
        var window = GetWindow<EnemyFsmGraphWindow>("Enemy FSM Graph");
        window.minSize = new Vector2(800, 500);
        window.Show();
    }
    private void OnEnable()
    {
        Build();
        EditorApplication.update += Refresh;
        Selection.selectionChanged += SelectionChanged;
        SelectionChanged();
    }
    private void OnDisable()
    {
        EditorApplication.update -= Refresh;
        Selection.selectionChanged -= SelectionChanged;
    }
    private void SelectionChanged()
    {
        if (Selection.activeGameObject != null)
        {
            var machine = Selection.activeGameObject.GetComponentInParent<EnemyStateMachine>();
            if (machine != null) target = machine;
        }
        if (target == null) target = Object.FindFirstObjectByType<EnemyStateMachine>();
        Repaint();
    }
    private void Refresh()
    {
        if (EditorApplication.timeSinceStartup < nextRepaint) return;
        nextRepaint = EditorApplication.timeSinceStartup + .1;
        Repaint();
    }
    private void Build()
    {
        nodes = new List<Node>
        {
            new Node("IDLE", "대기 / 플레이어 감지", 40, 60),
            new Node("PATROL", "주변 순찰 / 플레이어 감지", 40, 255),
            new Node("CHASE", "플레이어에게 접근", 340, 60),
            new Node("ATTACK", "Combo 1 → 2 → 3 → 4", 640, 60),
            new Node("OBSERVE", "거리 유지 / 전투 위치 재선택", 940, 255),
            new Node("DECISION", "가중치에 따른 다음 행동 선택", 640, 450),
            new Node("RETREAT", "잠시 후퇴", 940, 645),
            new Node("GROGGY", "패링 게이지 충족 / 경직", 340, 645),
            new Node("HIT", "일반 피격 / 스킬 넉다운 / 기상", 40, 450)
        };
        edges = new List<Edge>
        {
            new Edge("IDLE","PATROL","IdleDuration 경과"),
            new Edge("IDLE","CHASE","수평 거리 ≤ DetectionRange"),
            new Edge("PATROL","CHASE","수평 거리 ≤ DetectionRange"),
            new Edge("CHASE","ATTACK","수평 거리 ≤ AttackRange"),
            new Edge("ATTACK","OBSERVE","Combo4 애니메이션 완료"),
            new Edge("OBSERVE","CHASE","거리 > DetectionRange"),
            new Edge("OBSERVE","DECISION","ObserveDuration 경과"),
            new Edge("DECISION","CHASE","공격 또는 재접근 선택"),
            new Edge("DECISION","OBSERVE","계속 관찰 또는 위치 변경 선택"),
            new Edge("DECISION","RETREAT","후퇴 선택"),
            new Edge("RETREAT","OBSERVE","RetreatDuration 경과 / 목표 재선택"),
            new Edge("ATTACK","GROGGY","패링 누적 게이지 ≥ 최대치"),
            new Edge("GROGGY","IDLE","StaggerDuration 경과 / 게이지 초기화"),
            new Edge("HIT","OBSERVE","피격 완료 또는 넉다운 → 기상 완료")
        };
    }
    private Node Find(string id) { return nodes.Find(n => n.id == id); }
    private bool Recent(string id)
    {
        if (!Application.isPlaying || target == null) return false;
        var log = target.MonitorLog;
        for (int i = log.Count - 1; i >= 0; i--)
            if (log[i].Phase == "ENTER" && log[i].State == id)
                return Time.time - log[i].Time < 1f;
        return false;
    }
    private float zoom = .7f;
    private Vector2 pan = new Vector2(30, 45);
    private Vector2 blackboardScroll;
    private Rect ScreenRect(Rect r) { return new Rect(pan + r.position * zoom, r.size * zoom); }
    private void Connect(Rect parent, Rect child, bool active)
    {
        Vector2 start = ScreenRect(parent).center;
        start.y = ScreenRect(parent).yMax;
        Vector2 end = ScreenRect(child).center;
        end.y = ScreenRect(child).yMin;
        float middle = (start.y + end.y) * .5f;
        Handles.color = active ? new Color(.3f, 1f, .6f) : new Color(.7f,.7f,.7f);
        Handles.DrawAAPolyLine(active ? 3f : 1.5f, start,
            new Vector2(start.x,middle),new Vector2(end.x,middle),end);
        Handles.DrawAAPolyLine(2f,end+new Vector2(-4,-6),end,end+new Vector2(4,-6));
    }
    private void Card(Rect rect, string title, string body, Color header, string state, string current)
    {
        Rect r = ScreenRect(rect);
        bool active = state != null && state == current;
        Color border = active ? new Color(.3f,1f,.6f) :
            state != null && Recent(state) ? new Color(1f,.7f,.2f) :
            state != null && selected == state ? new Color(.3f,.75f,1f) : new Color(.25f,.25f,.25f);
        EditorGUI.DrawRect(r,border);
        EditorGUI.DrawRect(new Rect(r.x+1,r.y+1,r.width-2,r.height-2),new Color(.21f,.21f,.21f));
        EditorGUI.DrawRect(new Rect(r.x+1,r.y+1,r.width-2,24*zoom),header);
        var style = new GUIStyle(EditorStyles.whiteMiniLabel) {
            alignment=TextAnchor.MiddleCenter, wordWrap=true, fontSize=Mathf.Max(9,Mathf.RoundToInt(12*zoom)) };
        GUI.Label(new Rect(r.x,r.y,r.width,24*zoom),title+(active?" ●":""),style);
        GUI.Label(new Rect(r.x+4,r.y+26*zoom,r.width-8,r.height-28*zoom),body,style);
        if(state!=null && Event.current.type==EventType.MouseDown && Event.current.button==0 && r.Contains(Event.current.mousePosition))
        {
            selected=state;
            if(Event.current.clickCount==2) OpenSource(Find(state));
            Event.current.Use();
        }
    }
    private void OnGUI()
    {
        if(nodes==null) Build();
        using(new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            target=(EnemyStateMachine)EditorGUILayout.ObjectField(target,typeof(EnemyStateMachine),true,GUILayout.Width(200));
            GUILayout.Label(Application.isPlaying?"● LIVE":"FSM · 구조 보기",EditorStyles.miniBoldLabel);
            GUILayout.FlexibleSpace();
            if(GUILayout.Button("전체 보기",EditorStyles.toolbarButton))
            { zoom=Mathf.Clamp((position.width-530)/2520f,.25f,1.2f); pan=new Vector2(15,55); }
            if(GUILayout.Button("현재 상태",EditorStyles.toolbarButton) && target!=null)
            {
                selected=EnemyStateMachine.StateName(target.CurrentState);
                int index=nodes.FindIndex(n=>n.id==selected);
                if(index>=0) pan=new Vector2((position.width-520)*.5f-(index*280+130)*zoom,40);
            }
            zoom=GUILayout.HorizontalSlider(zoom,.25f,1.2f,GUILayout.Width(100));
            GUILayout.Label(Mathf.RoundToInt(zoom*100)+"%",GUILayout.Width(38));
        }
        Rect view=new Rect(220,24,Mathf.Max(100,position.width-520),position.height-24);
        EditorGUI.DrawRect(view,new Color(.105f,.105f,.105f));
        GUI.BeginGroup(view);
        var evt=Event.current;
        if(evt.type==EventType.ScrollWheel)
        {
            float before=zoom;
            zoom=Mathf.Clamp(zoom*(1-evt.delta.y*.05f),.25f,1.2f);
            pan=evt.mousePosition-(evt.mousePosition-pan)*(zoom/before);
            evt.Use();
        }
        if(evt.type==EventType.MouseDrag && (evt.button==2 || evt.button==1))
        { pan+=evt.delta; evt.Use(); Repaint(); }
        float grid=25*zoom;
        for(float x=pan.x%grid;x<view.width;x+=grid)
            EditorGUI.DrawRect(new Rect(x,0,1,view.height),new Color(1,1,1,.045f));
        for(float y=pan.y%grid;y<view.height;y+=grid)
            EditorGUI.DrawRect(new Rect(0,y,view.width,1),new Color(1,1,1,.045f));
        string current=Application.isPlaying && target!=null?EnemyStateMachine.StateName(target.CurrentState):"";
        Rect root=new Rect(1130,0,220,64), router=new Rect(1130,140,220,64);
        Color purple=new Color(.55f,.28f,.9f), orange=new Color(.8f,.43f,0);
        Handles.BeginGUI();
        Connect(root,router,current!="");
        for(int i=0;i<nodes.Count;i++)
        {
            Rect branch=new Rect(i*280+30,290,200,64);
            Rect condition=new Rect(i*280+30,430,200,68);
            Rect action=new Rect(i*280+30,575,200,80);
            Connect(router,branch,nodes[i].id==current);
            Connect(branch,condition,nodes[i].id==current);
            Connect(condition,action,nodes[i].id==current);
        }
        Handles.EndGUI();
        Card(root,"FSM Monitor","기존 StateMachine 관찰",new Color(.05f,.6f,.35f),null,current);
        Card(router,"Current State","현재 실행 상태별 분기",purple,null,current);
        for(int i=0;i<nodes.Count;i++)
        {
            var n=nodes[i];
            Card(new Rect(i*280+30,290,200,64),"State Branch",n.id,purple,n.id,current);
            Card(new Rect(i*280+30,430,200,68),"Pass If","CurrentState == "+n.id,orange,n.id,current);
            Card(new Rect(i*280+30,575,200,80),n.id,n.summary,new Color(.3f,.3f,.3f),n.id,current);
        }
        GUI.Label(new Rect(12,view.height-25,view.width-20,20),"휠: 확대/축소 · 우클릭/휠 드래그: 이동 · 노드 클릭: Inspector",EditorStyles.whiteMiniLabel);
        GUI.EndGroup();
        GUILayout.BeginArea(new Rect(8,32,204,position.height-40),EditorStyles.helpBox);
        blackboardScroll=EditorGUILayout.BeginScrollView(blackboardScroll);
        DrawBlackboard();
        EditorGUILayout.EndScrollView();
        GUILayout.EndArea();
        GUILayout.BeginArea(new Rect(position.width-292,32,284,position.height-40),EditorStyles.helpBox);
        detailScroll=EditorGUILayout.BeginScrollView(detailScroll);
        EditorGUILayout.LabelField("Inspector",EditorStyles.boldLabel);
        DrawDetails();
        EditorGUILayout.EndScrollView();
        GUILayout.EndArea();
    }
    private void Value(string label,string value)
    {
        EditorGUILayout.LabelField(label,EditorStyles.miniBoldLabel);
        EditorGUILayout.SelectableLabel(value,EditorStyles.wordWrappedMiniLabel,GUILayout.Height(30));
    }
    private void DrawBlackboard()
    {
        EditorGUILayout.LabelField("Blackboard",EditorStyles.boldLabel);
        EditorGUILayout.Space();
        Value("Self",target!=null?target.name:"—");
        if(target==null) return;
        var c=target.GetComponent<EnemyCombat>();
        var health=target.GetComponent<EnemyDamageReaction>();
        if(c==null) return;
        Value("Target",c.PlayerTransform!=null?c.PlayerTransform.name:"—");
        bool live=Application.isPlaying;
        bool valid=live && c.PlayerTransform!=null;
        Vector3 delta=valid?c.PlayerTransform.position-target.transform.position:Vector3.zero;
        delta.y=0;
        Value("PlayerDistance",valid?delta.magnitude.ToString("0.00")+" m":"—");
        Value("PlayerDetected",valid?(delta.magnitude<=c.DetectionRange).ToString():"—");
        Value("InAttackRange",valid?(delta.magnitude<=c.AttackRange).ToString():"—");
        Value("Detection / Attack Range",c.DetectionRange+" m / "+c.AttackRange+" m");
        Value("Observe Distance",c.CombatMinDistance+" – "+c.CombatMaxDistance+" m");
        Value("HealthPercent",live && health!=null?(health.CurrentHealth/health.MaxHealth*100).ToString("0")+"%":"—");
        Value("Groggy",live?c.GroggyGauge+" / "+c.MaxGroggyGauge:"—");
        Value("PlayerGuarding",live?c.IsPlayerGuarding.ToString():"—");
        Value("PlayerAttacking",live?c.IsPlayerAttacking.ToString():"—");
    }
    private void DrawDetails()
    {
        EditorGUILayout.LabelField("FSM MONITOR",EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("기존 FSM을 관찰하는 전용 창입니다. 노드 배치는 실행에 영향을 주지 않습니다. 전환 설명은 현재 코드 기준이며 로직 변경 시 갱신해야 합니다.",MessageType.Info);
        if(target!=null)
        {
            EditorGUILayout.LabelField("Enemy",target.name);
            if(Application.isPlaying)
            {
                EditorGUILayout.LabelField("현재",EnemyStateMachine.StateName(target.CurrentState));
                EditorGUILayout.LabelField("이전",target.PreviousStateName);
                EditorGUILayout.LabelField("유지 시간",(Time.time-target.StateEnteredAt).ToString("0.0")+" s");
            }
            var c=target.GetComponent<EnemyCombat>();
            if(c!=null)
            {
                EditorGUILayout.LabelField("감지 / 공격",c.DetectionRange+" m / "+c.AttackRange+" m");
                EditorGUILayout.LabelField("Observe 거리",c.CombatMinDistance+" – "+c.CombatMaxDistance+" m");
                if(Application.isPlaying) EditorGUILayout.LabelField("그로기",c.GroggyGauge+" / "+c.MaxGroggyGauge);
                if(GUILayout.Button("Enemy Inspector 선택")) Selection.activeGameObject=target.gameObject;
            }
        }
        EditorGUILayout.Space();
        var node=Find(selected);
        if(node!=null)
        {
            EditorGUILayout.LabelField(selected,EditorStyles.boldLabel);
            if(GUILayout.Button("상태 스크립트 열기")) OpenSource(node);
            foreach(var edge in edges)
                if(edge.from==selected) EditorGUILayout.HelpBox("→ "+edge.to+"\n"+edge.condition,MessageType.None);
            if(selected=="HIT")
                EditorGUILayout.HelpBox("외부 피격 진입: 일반 공격은 Attack/Groggy/넉다운 중 모션 유지. Skill_A는 넉다운 중이면 피해만 적용. Skill_G는 피격 상태로 진입/재시작. 넉다운과 기상은 이 상태 내부에서 처리.",MessageType.None);
            if(selected=="DECISION")
                EditorGUILayout.HelpBox("Enter 안에서 즉시 분기합니다. 공격 선택도 먼저 Chase로 이동합니다. 짧은 상태는 최근 진입 표시와 로그에서 확인하세요.",MessageType.None);
        }
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("ENTER / EXECUTE / EXIT",EditorStyles.boldLabel);
        if(target!=null && Application.isPlaying)
        {
            var log=target.MonitorLog;
            for(int i=log.Count-1;i>=Mathf.Max(0,log.Count-18);i--)
                EditorGUILayout.LabelField(log[i].Time.ToString("0.00")+"  "+log[i].State+" "+log[i].Phase+
                    (log[i].Phase=="EXECUTE"?" ×"+log[i].Count:""),EditorStyles.miniLabel);
        }
    }
    private void OpenSource(Node node)
    {
        var script=AssetDatabase.LoadAssetAtPath<MonoScript>(node.path);
        if(script!=null) AssetDatabase.OpenAsset(script);
    }
}
