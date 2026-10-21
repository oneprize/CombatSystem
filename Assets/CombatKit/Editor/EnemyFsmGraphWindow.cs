using UnityEngine;
using UnityEditor;
using CombatSystem.Combat;
using System.Collections.Generic;

/// <summary>
/// 적 상태 머신(EnemyStateMachine)의 상태와 전이 조건을 비헤이비어 트리 형태의 노드 그래프로 보여 주는 관찰 전용 에디터 창.
/// 플레이 중에는 현재 상태와 최근 진입 상태를 강조하고, 좌측 Blackboard와 우측 Inspector에 판단 값과 상태 로그를 표시한다.
/// 노드 배치는 실행 로직에 영향을 주지 않는다.
/// </summary>
public class EnemyFsmGraphWindow : EditorWindow
{
    #region 그래프 데이터 타입

    /// <summary>그래프의 상태 노드. 상태 ID, 요약 설명, 더블클릭 시 열 상태 스크립트 경로를 가진다.</summary>
    private class Node
    {
        public string id, summary, path;
        public Rect rect;
        /// <summary>노드를 만들고 상태 ID 규칙(Enemy{Id}State.cs, HIT는 EnemyDamageReaction.cs)으로 소스 경로를 정한다.</summary>
        public Node(string id, string summary, float x, float y)
        {
            this.id = id; this.summary = summary; rect = new Rect(x, y, 190, 80);
            path = id == "HIT" ? "Assets/Enemy/EnemyDamageReaction.cs" :
                "Assets/Enemy/States/Enemy" + char.ToUpper(id[0]) + id.Substring(1).ToLower() + "State.cs";
        }
    }
    /// <summary>상태 간 전이 하나와 그 조건 설명. Inspector 패널에서 선택 상태의 나가는 전이를 보여 줄 때 쓴다.</summary>
    private class Edge
    {
        public string from, to, condition;
        /// <summary>출발/도착 상태 ID와 전이 조건 설명을 저장한다.</summary>
        public Edge(string from, string to, string condition)
        { this.from = from; this.to = to; this.condition = condition; }
    }

    #endregion

    #region 필드

    [SerializeField] private EnemyStateMachine target;
    [SerializeField] private string selected = "OBSERVE";
    private Vector2 detailScroll;
    private List<Node> nodes;
    private List<Edge> edges;
    private double nextRepaint;

    #endregion

    #region 창 열기와 갱신

    /// <summary>FSM 그래프 창을 연다.</summary>
    [MenuItem("Tools/Combat System/FSM Graph")]
    public static void Open()
    {
        var window = GetWindow<EnemyFsmGraphWindow>("Enemy FSM Graph");
        window.minSize = new Vector2(800, 500);
        window.Show();
    }
    /// <summary>그래프를 구성하고, 주기적 다시 그리기와 선택 변경 이벤트를 구독한다.</summary>
    private void OnEnable()
    {
        Build();
        EditorApplication.update += Refresh;
        Selection.selectionChanged += SelectionChanged;
        SelectionChanged();
    }
    /// <summary>OnEnable에서 구독한 에디터 이벤트를 해제한다.</summary>
    private void OnDisable()
    {
        EditorApplication.update -= Refresh;
        Selection.selectionChanged -= SelectionChanged;
    }
    /// <summary>선택한 오브젝트(또는 그 부모)에 적 상태 머신이 있으면 관찰 대상으로 바꾼다. 대상이 없으면 씬의 첫 적을 쓴다.</summary>
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
    /// <summary>실시간 상태 표시를 위해 0.1초 간격으로 창을 다시 그린다.</summary>
    private void Refresh()
    {
        if (EditorApplication.timeSinceStartup < nextRepaint) return;
        nextRepaint = EditorApplication.timeSinceStartup + .1;
        Repaint();
    }

    #endregion

    #region 그래프 정의

    /// <summary>
    /// 표시할 상태 노드와 전이 조건 목록을 정의한다. 실제 상태 머신 코드에서 읽어 오는 것이 아니라 수동으로 적은 설명이므로,
    /// 전이 로직이 바뀌면 여기도 함께 갱신해야 한다.
    /// </summary>
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
            new Edge("HIT","OBSERVE","피격 완료 또는 넉다운 → 기상 완료")
        };
    }
    /// <summary>상태 ID로 노드를 찾는다.</summary>
    private Node Find(string id) { return nodes.Find(n => n.id == id); }
    /// <summary>플레이 중 해당 상태에 마지막으로 진입한 지 1초가 안 됐는지 판정한다. 너무 짧게 지나가는 상태를 눈에 띄게 하려는 용도.</summary>
    private bool Recent(string id)
    {
        if (!Application.isPlaying || target == null) return false;
        var log = target.MonitorLog;
        for (int i = log.Count - 1; i >= 0; i--)
            if (log[i].Phase == "ENTER" && log[i].State == id)
                return Time.time - log[i].Time < 1f;
        return false;
    }

    #endregion

    #region 그래프 그리기

    private float zoom = .7f;
    private Vector2 pan = new Vector2(30, 45);
    private Vector2 blackboardScroll;
    /// <summary>그래프 좌표의 사각형을 현재 확대/이동을 적용한 화면 좌표로 변환한다.</summary>
    private Rect ScreenRect(Rect r) { return new Rect(pan + r.position * zoom, r.size * zoom); }
    /// <summary>부모 카드 아래쪽에서 자식 카드 위쪽으로 꺾인 연결선과 화살촉을 그린다. 활성 경로는 굵은 초록색으로 표시한다.</summary>
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
    /// <summary>
    /// 헤더 색과 제목/본문이 있는 카드를 그린다. 테두리 색으로 현재 상태(초록), 최근 진입(주황), 선택(파랑)을 구분하고,
    /// 상태 카드를 클릭하면 선택하며 더블클릭하면 해당 상태 스크립트를 연다.
    /// </summary>
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
    /// <summary>
    /// 툴바(대상 선택, 전체 보기/현재 상태로 이동, 확대 슬라이더), 가운데 그래프 영역(휠 확대·드래그 이동, 격자, 루트→분기→조건→상태 카드),
    /// 좌측 Blackboard와 우측 Inspector 패널을 그린다.
    /// </summary>
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

    #endregion

    #region 사이드 패널

    /// <summary>Blackboard 항목 하나를 라벨과 복사 가능한 값으로 그린다.</summary>
    private void Value(string label,string value)
    {
        EditorGUILayout.LabelField(label,EditorStyles.miniBoldLabel);
        EditorGUILayout.SelectableLabel(value,EditorStyles.wordWrappedMiniLabel,GUILayout.Height(30));
    }
    /// <summary>대상 적의 판단 입력값(플레이어 거리, 탐지/공격 범위 여부, 관찰 거리, 체력 비율, 플레이어 가드·공격 여부)을 표시한다. 실시간 값은 플레이 중에만 나온다.</summary>
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
        Value("PlayerGuarding",live?c.IsPlayerGuarding.ToString():"—");
        Value("PlayerAttacking",live?c.IsPlayerAttacking.ToString():"—");
    }
    /// <summary>대상 적의 현재/이전 상태와 유지 시간, 선택한 상태의 나가는 전이와 보충 설명, 최근 상태 로그 18줄을 Inspector 패널에 표시한다.</summary>
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
                EditorGUILayout.HelpBox("외부 피격 진입: 일반 공격은 Attack/넉다운 중 모션 유지. Skill_A는 넉다운 중이면 피해만 적용. Skill_G는 피격 상태로 진입/재시작. 넉다운과 기상은 이 상태 내부에서 처리.",MessageType.None);
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

    #endregion

    #region 소스 열기

    /// <summary>노드에 연결된 상태 스크립트를 외부 편집기로 연다. 경로에 스크립트가 없으면 아무 것도 하지 않는다.</summary>
    private void OpenSource(Node node)
    {
        var script=AssetDatabase.LoadAssetAtPath<MonoScript>(node.path);
        if(script!=null) AssetDatabase.OpenAsset(script);
    }

    #endregion
}
