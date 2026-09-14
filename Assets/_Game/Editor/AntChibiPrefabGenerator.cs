#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace ColonyFlow.Editor
{
    [InitializeOnLoad]
    public static class AntChibiPrefabGenerator
    {
        private const int Version = 7;
        private const string PrefabPath = "Assets/_Game/Resources/Prefabs/AntChibi.prefab";
        private const string AnimFolder = "Assets/_Game/Art/Ant/Animations";
        private const string ControllerPath = AnimFolder + "/AntChibi.controller";

        static AntChibiPrefabGenerator() => EditorApplication.delayCall += GenerateIfOutdated;

        [MenuItem("Colony Flow/Ant/Regenerate Chibi Ant Prefab")]
        public static void Generate()
        {
            EnsureFolder(AnimFolder);
            var root = new GameObject("AntChibi");
            Transform body = new GameObject("Body").transform;
            body.SetParent(root.transform, false);
            body.localRotation = Quaternion.Euler(7f, 0f, 0f);
            var colored = new List<Renderer>();
            var legs = new List<Transform>(6);
            var antennae = new List<Transform>(2);

            // Nearly round depth keeps a full 3D silhouette while turning sideways.
            AddSphere(body, "Abdomen", new Vector3(0f, -.47f, .03f), new Vector3(.88f, 1.02f, .86f), colored);
            AddSphere(body, "Thorax", new Vector3(0f, .12f, 0f), new Vector3(.53f, .56f, .58f), colored);
            AddSphere(body, "Head", new Vector3(0f, .64f, -.03f), new Vector3(.82f, .78f, .76f), colored);
            for (int side = -1; side <= 1; side += 2)
            {
                // Both rows remain visible; the tiny depth difference is enough for
                // natural body occlusion without making the far row disappear.
                float depth = side < 0 ? -.05f : -.2f;
                float reach = side < 0 ? .48f : .54f;
                legs.Add(AddCapsule(body, $"LegFront_{side}", new Vector3(side*.14f,.29f,depth), new Vector3(side*reach,.4f,depth), colored, .1f));
                legs.Add(AddCapsule(body, $"LegMiddle_{side}", new Vector3(side*.15f,.06f,depth), new Vector3(side*(reach+.02f),-.01f,depth), colored, .1f));
                legs.Add(AddCapsule(body, $"LegBack_{side}", new Vector3(side*.14f,-.17f,depth), new Vector3(side*(reach-.02f),-.42f,depth), colored, .1f));
                antennae.Add(AddCapsule(body, $"Antenna_{side}", new Vector3(side*.16f,.91f,-.08f), new Vector3(side*.34f,1.16f,-.12f), colored, .055f));
                AddSphere(body, $"AntennaTip_{side}", new Vector3(side*.35f,1.17f,-.12f), Vector3.one*.12f, colored);
            }

            Material white = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Art/Ant/Materials/Ant_06_White.mat");
            Material black = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Art/Ant/Materials/Ant_08_Black__211C2B.mat");
            // Eyes sit toward the travel direction, not in the middle of the face.
            AddSphere(body,"EyeLeft",new Vector3(-.2f,.83f,-.35f),new Vector3(.27f,.31f,.22f),null).sharedMaterial=white;
            AddSphere(body,"EyeRight",new Vector3(.2f,.83f,-.35f),new Vector3(.27f,.31f,.22f),null).sharedMaterial=white;
            AddSphere(body,"PupilLeft",new Vector3(-.2f,.88f,-.505f),new Vector3(.105f,.14f,.075f),null).sharedMaterial=black;
            AddSphere(body,"PupilRight",new Vector3(.2f,.88f,-.505f),new Vector3(.105f,.14f,.075f),null).sharedMaterial=black;

            Animator animator = root.AddComponent<Animator>();
            animator.runtimeAnimatorController = BuildController(root.transform, legs, antennae);
            AntVisual visual = root.AddComponent<AntVisual>();
            SerializedObject data = new(visual);
            data.FindProperty("animator").objectReferenceValue = animator;
            data.FindProperty("visualVersion").intValue = Version;
            SerializedProperty renderers = data.FindProperty("coloredRenderers");
            renderers.arraySize = colored.Count;
            for (int i=0;i<colored.Count;i++) renderers.GetArrayElementAtIndex(i).objectReferenceValue=colored[i];
            data.ApplyModifiedPropertiesWithoutUndo();
            AntAgent agent=root.AddComponent<AntAgent>(); agent.Configure(visual); agent.poolType=PoolType.Ant;
            PrefabUtility.SaveAsPrefabAsset(root,PrefabPath);
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            Debug.Log("Generated Animator-driven chibi ant prefab: "+PrefabPath);
        }

        private static RuntimeAnimatorController BuildController(Transform root,List<Transform> legs,List<Transform> antennae)
        {
            AnimationClip idle=CreateClip(AnimFolder+"/Ant_Idle.anim",true);
            Loop(idle,"Body","m_LocalPosition.y",0f,.018f,0f,.8f);
            Loop(idle,"Body","localEulerAnglesRaw.z",-1.5f,1.5f,-1.5f,.8f);
            AnimationClip walk=CreateClip(AnimFolder+"/Ant_Walk.anim",true);
            Loop(walk,"Body","m_LocalPosition.y",0f,.075f,0f,.32f);
            Loop(walk,"Body","m_LocalPosition.z",0f,-.045f,0f,.32f);
            Loop(walk,"Body","localEulerAnglesRaw.z",-4f,4f,-4f,.32f);
            for(int i=0;i<legs.Count;i++)
            {
                float baseAngle=Angle(legs[i].localEulerAngles.z);
                float direction=((i&1)==0?1f:-1f)*(((i/2)&1)==0?1f:-1f);
                string legPath=Path(root,legs[i]);
                Loop(walk,legPath,"localEulerAnglesRaw.z",baseAngle-20f*direction,baseAngle+20f*direction,baseAngle-20f*direction,.32f);
            }
            for(int i=0;i<antennae.Count;i++)
            {
                float baseAngle=Angle(antennae[i].localEulerAngles.z);
                Loop(walk,Path(root,antennae[i]),"localEulerAnglesRaw.z",baseAngle-6f,baseAngle+6f,baseAngle-6f,.36f);
            }
            AnimationClip attack=CreateClip(AnimFolder+"/Ant_Attack.anim",false);
            Curve(attack,"Body","m_LocalScale.x",1f,1.06f,1f,.32f);
            Curve(attack,"Body","m_LocalScale.y",1f,.94f,1f,.32f);
            Curve(attack,"Body/Head","m_LocalPosition.y",.64f,.82f,.64f,.32f);
            Curve(attack,"Body/Head","m_LocalPosition.z",-.03f,-.22f,-.03f,.32f);
            Curve(attack,"Body/Head","localEulerAnglesRaw.x",0f,-24f,0f,.32f);
            AssetDatabase.SaveAssets();

            AssetDatabase.DeleteAsset(ControllerPath);
            AnimatorController controller=AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            foreach(string parameter in new[]{"Idle","Walk","Attack"}) controller.AddParameter(parameter,AnimatorControllerParameterType.Trigger);
            AnimatorStateMachine machine=controller.layers[0].stateMachine;
            AnimatorState idleState=machine.AddState("Idle"); idleState.motion=idle; machine.defaultState=idleState;
            AnimatorState walkState=machine.AddState("Walk"); walkState.motion=walk; walkState.speed=1.25f;
            AnimatorState attackState=machine.AddState("Attack"); attackState.motion=attack; attackState.speed=1f;
            Trigger(machine,idleState,"Idle",.08f); Trigger(machine,walkState,"Walk",.08f); Trigger(machine,attackState,"Attack",.04f);
            AnimatorStateTransition done=attackState.AddTransition(idleState); done.hasExitTime=true; done.exitTime=1f; done.duration=.06f;
            return controller;
        }

        private static AnimationClip CreateClip(string path,bool loop)
        {
            AssetDatabase.DeleteAsset(path);
            var clip=new AnimationClip{name=System.IO.Path.GetFileNameWithoutExtension(path),frameRate=30f};
            SerializedObject data=new(clip); SerializedProperty settings=data.FindProperty("m_AnimationClipSettings");
            if(settings!=null) settings.FindPropertyRelative("m_LoopTime").boolValue=loop;
            data.ApplyModifiedPropertiesWithoutUndo(); AssetDatabase.CreateAsset(clip,path); return clip;
        }
        private static void Loop(AnimationClip clip,string path,string property,float a,float b,float c,float duration)=>
            AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve(path,typeof(Transform),property),new AnimationCurve(new Keyframe(0,a),new Keyframe(duration*.5f,b),new Keyframe(duration,c)));
        private static void Curve(AnimationClip clip,string path,string property,float a,float b,float c,float duration)=>Loop(clip,path,property,a,b,c,duration);
        private static void Trigger(AnimatorStateMachine machine,AnimatorState state,string name,float duration)
        {
            AnimatorStateTransition transition=machine.AddAnyStateTransition(state); transition.hasExitTime=false; transition.duration=duration; transition.canTransitionToSelf=false;
            transition.AddCondition(AnimatorConditionMode.If,0f,name);
        }
        private static string Path(Transform root,Transform target)=>AnimationUtility.CalculateTransformPath(target,root);
        private static float Angle(float value)=>value>180f?value-360f:value;
        private static void GenerateIfOutdated()
        {
            GameObject prefab=AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath); AntVisual visual=prefab!=null?prefab.GetComponent<AntVisual>():null;
            if(visual==null||prefab.GetComponent<Animator>()==null||new SerializedObject(visual).FindProperty("visualVersion").intValue<Version) Generate();
        }
        private static Renderer AddSphere(Transform parent,string name,Vector3 position,Vector3 scale,List<Renderer> colored)
        {
            GameObject part=GameObject.CreatePrimitive(PrimitiveType.Sphere); part.name=name; part.transform.SetParent(parent,false); part.transform.localPosition=position; part.transform.localScale=scale;
            Object.DestroyImmediate(part.GetComponent<Collider>()); Renderer renderer=part.GetComponent<Renderer>(); colored?.Add(renderer); return renderer;
        }
        private static Transform AddCapsule(Transform parent,string name,Vector3 from,Vector3 to,List<Renderer> colored,float width)
        {
            GameObject part=GameObject.CreatePrimitive(PrimitiveType.Capsule); part.name=name; part.transform.SetParent(parent,false); Vector3 delta=to-from;
            part.transform.localPosition=(from+to)*.5f; part.transform.localScale=new Vector3(width,delta.magnitude*.5f,width); part.transform.localRotation=Quaternion.Euler(0,0,-Mathf.Atan2(delta.x,delta.y)*Mathf.Rad2Deg);
            Object.DestroyImmediate(part.GetComponent<Collider>()); colored.Add(part.GetComponent<Renderer>()); return part.transform;
        }
        private static void EnsureFolder(string path)
        {
            string current="Assets"; string[] parts=path.Split('/'); for(int i=1;i<parts.Length;i++){string next=current+"/"+parts[i]; if(!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current,parts[i]); current=next;}
        }
    }
}
#endif
