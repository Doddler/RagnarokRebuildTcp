using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Utility.Editor
{
    [CustomEditor(typeof(VideoRecorder))]
    public class VideoRecorderEditor : UnityEditor.Editor
    {
        private SerializedProperty stateName;
        private string lastState;
        private List<string> stateNames;
        private List<string> fakeClipNames;
        private int selected = 0;

        private bool hasCreatedOnChangeEvent;
        private GameObject targetObject;
        private AnimationClip targetClip;

        private Animator GetAnimatorFromProperty()
        {
            var animProp = serializedObject.FindProperty("VirtualCamera");
            var animator = animProp.objectReferenceValue as Animator;
            return animator;
        }
        
        private AnimatorController GetAnimatorControllerFromProperty()
        {
            var animProp = serializedObject.FindProperty("VirtualCamera");
            var animator = animProp.objectReferenceValue as Animator;
            if (animator == null)
                return null;
            
            //fuck unity, fuck the editor
            string assetPath = AssetDatabase.GetAssetPath(animator.runtimeAnimatorController);
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(assetPath);

            return controller;
        }
        
        public void OnEnable()
        {
            stateName = serializedObject.FindProperty("StateName");
            lastState = stateName.stringValue;
            
            var controller = GetAnimatorControllerFromProperty();
            if (controller == null)
                return;
            var dict = new Dictionary<string, string>();
            

            var layer = controller.layers[0];

            for (var i = 0; i < layer.stateMachine.states.Length; i++)
            {
                var state = layer.stateMachine.states[i];
                if (state.state.motion == null)
                    continue;
                
                var path = AssetDatabase.GetAssetPath(state.state.motion);
                path = path.Replace("Assets/", "");
                path = path.Replace("Cinemachine/", "");
                dict.Add(path, state.state.name);
            }

            dict = dict.OrderBy(d => d.Key).ToDictionary(d => d.Key, d => d.Value);
            fakeClipNames = dict.Keys.ToList();
            stateNames = dict.Values.ToList();

            selected = stateNames.IndexOf(lastState);

            if(!hasCreatedOnChangeEvent)
                Selection.selectionChanged += OnSelectionChange;
            
            hasCreatedOnChangeEvent = true;
        }

        public EditorWindow GetEditorWindow(string name) {
            EditorWindow[] windows = Resources.FindObjectsOfTypeAll<EditorWindow>();
            if (windows != null && windows.Length > 0)
            {
                foreach (var window in windows)
                    if (window.GetType().Name.Contains(name))
                        return window;
            }
            return null;
        }
        
        
        [ContextMenu("Open Clip in animation window")]
        public void OpenClip(string stateName)
        {
            var animProp = serializedObject.FindProperty("VirtualCamera");
            var animator = animProp.objectReferenceValue as Animator;
            if (animator == null)
                return;
            
            //fuck unity, fuck the editor
            string assetPath = AssetDatabase.GetAssetPath(animator.runtimeAnimatorController);
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(assetPath);

            var state = controller.layers[0].stateMachine.states.First(s => s.state.name == stateName);
            
            var clipPath = AssetDatabase.GetAssetPath(state.state.motion);
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);

            Selection.activeGameObject.SetActive(true); //this should always be the video recorder object with our camera, we want to turn it on
            SwitchToAndRememberAnimationClip(animator.gameObject, clip);
        }

        private void SwitchToAndRememberAnimationClip(GameObject target, AnimationClip clip)
        {
            var animationWindow = EditorWindow.GetWindow<AnimationWindow>();
            targetObject = target;
            targetClip = clip;
            
            Selection.activeGameObject = target;
            animationWindow.animationClip = clip;

            if(!hasCreatedOnChangeEvent)
                Selection.selectionChanged += OnSelectionChange;
            hasCreatedOnChangeEvent = true;

        }

        private void OnSelectionChange()
        {
            // Debug.Log($"{targetClip} {targetObject} {Selection.activeGameObject} {EditorWindow.HasOpenInstances<AnimationWindow>()}");
            
            if (targetClip == null || !EditorWindow.HasOpenInstances<AnimationWindow>())
                return;

            if (Selection.activeGameObject == targetObject)
            {
                var animationWindow = EditorWindow.GetWindow<AnimationWindow>();
                if(animationWindow.animationClip != targetClip)
                 animationWindow.animationClip = targetClip;
            }
        }


        public void SetDefaultState(string stateName)
        {
            var controller = GetAnimatorControllerFromProperty();
            
            
            var layer = controller.layers[0];

            var newStateIndex = -1;

            for (var i = 0; i < layer.stateMachine.states.Length; i++)
            {
                if (layer.stateMachine.states[i].state.name == stateName)
                    newStateIndex = i;
            }

            if (newStateIndex >= 0)
            {
                var newState = layer.stateMachine.states[newStateIndex];
                // if (newStateIndex > 0)
                // {
                //     var oldState = layer.stateMachine.states[0];
                //     layer.stateMachine.states[0] = newState;
                //     layer.stateMachine.states[newStateIndex] = oldState;
                // }
                //
                layer.stateMachine.defaultState = newState.state;
                
                EditorUtility.SetDirty(controller);
                return;
            }
            
            Debug.LogWarning($"Could not change default state to {stateName}!");
        }

        public void MakeNewClip()
        {
            var newClipPath = EditorUtility.SaveFilePanel("Animation clip", "Assets/Cinemachine/", "New Animation Clip", "anim");

            if (string.IsNullOrWhiteSpace(newClipPath))
                return;

            newClipPath = Path.GetRelativePath(Path.Combine(Application.dataPath, "../"), newClipPath);
            
            var baseName = Path.GetFileNameWithoutExtension(newClipPath);

            var controller = GetAnimatorControllerFromProperty();
            if (controller == null)
                return;

            var clip = new AnimationClip();
            clip.name = baseName;
            clip.wrapMode = WrapMode.ClampForever;
            AssetDatabase.CreateAsset(clip, newClipPath);

            if (controller.layers[0].stateMachine.states.Any(s => s.state.name == baseName))
                baseName = $"{baseName}_{Random.Range(0, 999)}";

            var newState = new AnimatorState();
            newState.name = baseName;
            newState.motion = clip;
            
            var layer = controller.layers[0];
            layer.stateMachine.AddState(newState, Vector3.zero);
            
            Selection.activeGameObject.SetActive(true); //this should always be the video recorder object with our camera, we want to turn it on
            
            stateName.stringValue = baseName;
            SetDefaultState(baseName);
            serializedObject.ApplyModifiedProperties();

            var animator = GetAnimatorFromProperty();
            SwitchToAndRememberAnimationClip(animator.gameObject, clip);

            //controller.AddMotion()
        }

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            
            serializedObject.Update();
            EditorGUILayout.PrefixLabel("AnimationClip");
            
            if(fakeClipNames == null)
                OnEnable();
            if (fakeClipNames == null)
            {
                
                return;
            }

            var sel = EditorGUILayout.Popup(selected, fakeClipNames.ToArray());
            if (selected != sel)
            {
                var oldState = "";
                if(selected >= 0 && selected < stateNames.Count)
                    oldState = stateNames[selected];
                selected = sel;
                stateName.stringValue = stateNames[sel];
                SetDefaultState(stateNames[sel]);
                serializedObject.ApplyModifiedProperties();
            }

            if (EditorGUILayout.LinkButton("Open in animation window"))
                OpenClip(stateNames[sel]);
            
            
            if (EditorGUILayout.LinkButton("Create new clip"))
                MakeNewClip();
            
        }
    }
}