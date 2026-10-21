using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
namespace CombatSystem.EditorTools
{
    public static class LocomotionCourseSetup
    {
        static Material stone, accent, metal;
        static Transform root;
        static GameObject Box(string name, Vector3 pos, Vector3 size, Material mat)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name=name; go.transform.SetParent(root,false); go.transform.position=pos; go.transform.localScale=size;
            go.GetComponent<Renderer>().sharedMaterial=mat;
            Undo.RegisterCreatedObjectUndo(go,"Build locomotion course"); return go;
        }
        static void Stairs(string name,float x,float z,float height,float width)
        {
            int count=Mathf.CeilToInt(height/.25f);
            for(int i=0;i<count;i++) { float h=height*(i+1)/count; Box(name+" Step "+(i+1),new Vector3(x,h/2,z+i*.35f),new Vector3(width,h,.36f),accent); }
        }
        static void Ramp(string name,float x,float z,float angle)
        {
            float run=7, rise=Mathf.Tan(angle*Mathf.Deg2Rad)*run;
            var go=Box(name,new Vector3(x,rise/2-.12f,z+run/2),new Vector3(3,.24f,run/Mathf.Cos(angle*Mathf.Deg2Rad)),accent);
            go.transform.rotation=Quaternion.Euler(-angle,0,0);
            Box(name+" Top Platform",new Vector3(x,rise/2,z+run+1.5f),new Vector3(4,rise,3),stone);
        }
        static void Bar(string name,float x,float z,float height)
        {
            Box(name+" Left Support",new Vector3(x-2,height/2,z),new Vector3(.18f,height,.18f),metal);
            Box(name+" Right Support",new Vector3(x+2,height/2,z),new Vector3(.18f,height,.18f),metal);
            var bar=GameObject.CreatePrimitive(PrimitiveType.Cylinder);bar.name=name+" Hang Rail";bar.transform.SetParent(root,false);
            bar.transform.position=new Vector3(x,height,z);bar.transform.rotation=Quaternion.Euler(0,0,90);bar.transform.localScale=new Vector3(.12f,2.1f,.12f);
            bar.GetComponent<Renderer>().sharedMaterial=metal;Undo.RegisterCreatedObjectUndo(bar,"Build hang rail");
            var anchor=new GameObject(name+" Hang Anchor");anchor.transform.SetParent(bar.transform,false);anchor.transform.position=new Vector3(x,height,z);
        }
        static Material MaterialAsset(string name,Color color,Material source)
        {
            string path="Assets/CombatKit/Content/Data/Training/"+name+".mat";
            var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(mat==null){mat=new Material(source);mat.name=name;mat.color=color;AssetDatabase.CreateAsset(mat,path);}return mat;
        }
        [MenuItem("Tools/Combat Kit/Build Locomotion Training Course")]
        public static void Build()
        {
            var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if(EditorApplication.isPlayingOrWillChangePlaymode || scene.path!="Assets/CombatKit/Content/Scenes/ComboPracticeGround.unity") return;
            if(GameObject.Find("Locomotion Training Course")!=null)return;
            var arena=GameObject.Find("Training Arena");
            if(arena==null)throw new System.Exception("Training Arena missing");
            var floor=arena.transform.Find("30m Training Floor");
            if(floor==null)throw new System.Exception("Training floor missing");
            var source=floor.GetComponent<Renderer>().sharedMaterial;
            stone=MaterialAsset("LocomotionStone",new Color(.29f,.35f,.42f),source);
            accent=MaterialAsset("LocomotionRamp",new Color(.22f,.55f,.65f),source);
            metal=MaterialAsset("LocomotionMetal",new Color(.65f,.69f,.74f),source);
            var group=new GameObject("Locomotion Training Course");root=group.transform;Undo.RegisterCreatedObjectUndo(group,"Build locomotion course");
            Box("32m Extension Floor",new Vector3(-31,-.25f,0),new Vector3(32,.5f,30),source);
            foreach(var t in arena.GetComponentsInChildren<Transform>())
                if(t.name=="West Boundary" && Mathf.Abs(t.position.z)<1) {Undo.RecordObject(t,"Open course entrance");t.position=new Vector3(-15,2,-9);t.localScale=new Vector3(.3f,4,12);}
            Box("Entrance North Wall",new Vector3(-15,2,9),new Vector3(.3f,4,12),stone);
            Box("Course North Wall",new Vector3(-31,2,15),new Vector3(32,4,.3f),stone);
            Box("Course South Wall",new Vector3(-31,2,-15),new Vector3(32,4,.3f),stone);
            Box("Course West Wall",new Vector3(-47,2,0),new Vector3(.3f,4,30),stone);
            // Stepped wall tops are wide enough to stand and run on.
            for(int i=0;i<4;i++){float h=.75f*(i+1);float x=-21-i*4;Box("Height Wall "+h+"m",new Vector3(x,h/2,-8),new Vector3(4,h,4),stone);Stairs("Access "+h+"m",x,-10f-Mathf.CeilToInt(h/.25f)*.35f+.175f,h,1.8f);}
            Ramp("20 Degree Ramp",-22,3,20);
            Ramp("30 Degree Ramp",-29,3,30);
            Bar("3m Bar",-39,5,3);
            Bar("4m Bar",-39,10,4);
            Box("Bar Access Platform",new Vector3(-43,1,8),new Vector3(3,2,7),stone);
            Stairs("Bar Access",-43,1.875f,2,2);
            Physics.SyncTransforms();CombatKitNavigationSetup.Bake();
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
            Selection.activeGameObject=group;
            Debug.Log("LOCOMOTION_COURSE_BUILT: 32m extension, stepped platforms, 20/30 degree ramps, 3/4m rails; navigation baked and scene saved.");
        }
    }
}
