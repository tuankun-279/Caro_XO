using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.UI;

namespace Caro.EditorTools
{
    /// <summary>
    /// Keyframe helpers shared by the UI and splash builders: every effect of the game is an AnimationClip made
    /// here and played by an Animator, never motion written in a script.
    /// </summary>
    public static class CaroAnimTools
    {
        public const string OutDir = "Assets/_Caro/Art/Animations";

        public static AnimationCurve Curve(params (float t, float v)[] keys)
        {
            var c = new AnimationCurve(keys.Select(k => new Keyframe(k.t, k.v)).ToArray());
            for (int i = 0; i < c.length; i++)
            {
                AnimationUtility.SetKeyLeftTangentMode(c, i, AnimationUtility.TangentMode.ClampedAuto);
                AnimationUtility.SetKeyRightTangentMode(c, i, AnimationUtility.TangentMode.ClampedAuto);
            }
            return c;
        }

        /// <summary>A curve that holds each value until the next key (for on/off properties).</summary>
        public static AnimationCurve Steps(params (float t, float v)[] keys)
        {
            var c = new AnimationCurve(keys.Select(k => new Keyframe(k.t, k.v)).ToArray());
            for (int i = 0; i < c.length; i++)
            {
                AnimationUtility.SetKeyLeftTangentMode(c, i, AnimationUtility.TangentMode.Constant);
                AnimationUtility.SetKeyRightTangentMode(c, i, AnimationUtility.TangentMode.Constant);
            }
            return c;
        }

        public static void Pos(AnimationClip clip, string path, char axis, params (float, float)[] keys) =>
            clip.SetCurve(path, typeof(RectTransform), "m_AnchoredPosition." + axis, Curve(keys));

        public static void Scale(AnimationClip clip, string path, params (float, float)[] keys)
        {
            var c = Curve(keys);
            clip.SetCurve(path, typeof(RectTransform), "m_LocalScale.x", c);
            clip.SetCurve(path, typeof(RectTransform), "m_LocalScale.y", c);
        }

        public static void ScaleY(AnimationClip clip, string path, params (float, float)[] keys) =>
            clip.SetCurve(path, typeof(RectTransform), "m_LocalScale.y", Curve(keys));

        /// <summary>Rotation around one axis ('x', 'y' or 'z'); the other two are held at 0.</summary>
        public static void Rot(AnimationClip clip, string path, char axis, params (float, float)[] keys)
        {
            float end = keys[keys.Length - 1].Item1;
            foreach (var a in "xyz")
                clip.SetCurve(path, typeof(RectTransform), "localEulerAnglesRaw." + a,
                    a == axis ? Curve(keys) : AnimationCurve.Constant(0, end, 0));
        }

        public static void Rot(AnimationClip clip, string path, params (float, float)[] keys) => Rot(clip, path, 'z', keys);

        public static void GroupAlpha(AnimationClip clip, string path, params (float, float)[] keys) =>
            clip.SetCurve(path, typeof(CanvasGroup), "m_Alpha", Curve(keys));

        public static void GroupBlocks(AnimationClip clip, string path, params (float, float)[] keys) =>
            clip.SetCurve(path, typeof(CanvasGroup), "m_BlocksRaycasts", Steps(keys));

        public static void ImageAlpha(AnimationClip clip, string path, params (float, float)[] keys) =>
            clip.SetCurve(path, typeof(Image), "m_Color.a", Curve(keys));

        public static void Active(AnimationClip clip, string path, params (float, float)[] keys) =>
            clip.SetCurve(path, typeof(GameObject), "m_IsActive", Steps(keys));

        public static void Events(AnimationClip clip, params AnimationEvent[] events) =>
            AnimationUtility.SetAnimationEvents(clip, events);

        /// <summary>Saves the clip as an asset (replacing an older one) with looping off.</summary>
        public static AnimationClip Save(AnimationClip clip, string name)
        {
            EnsureFolder();
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = false;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            var path = $"{OutDir}/{name}.anim";
            AssetDatabase.DeleteAsset(path);
            clip.name = name;
            AssetDatabase.CreateAsset(clip, path);
            return clip;
        }

        /// <summary>A controller that plays one clip when the object is enabled.</summary>
        public static AnimatorController SingleClip(string name, AnimationClip clip)
        {
            EnsureFolder();
            var path = $"{OutDir}/{name}.controller";
            AssetDatabase.DeleteAsset(path);
            return AnimatorController.CreateAnimatorControllerAtPathWithClip(path, clip);
        }

        /// <summary>
        /// A controller with an idle state and a one-shot state: the trigger starts the one-shot clip, which
        /// goes back to idle when it ends.
        /// </summary>
        public static AnimatorController IdleAndTrigger(string name, AnimationClip idle, AnimationClip shot, string trigger)
        {
            EnsureFolder();
            var path = $"{OutDir}/{name}.controller";
            AssetDatabase.DeleteAsset(path);
            var c = AnimatorController.CreateAnimatorControllerAtPath(path);
            c.AddParameter(trigger, AnimatorControllerParameterType.Trigger);
            var sm = c.layers[0].stateMachine;
            var idleState = sm.AddState(idle.name);
            idleState.motion = idle;
            var shotState = sm.AddState(shot.name);
            shotState.motion = shot;
            sm.defaultState = idleState;
            var go = idleState.AddTransition(shotState);
            go.hasExitTime = false;
            go.duration = 0;
            go.AddCondition(AnimatorConditionMode.If, 0, trigger);
            var back = shotState.AddTransition(idleState);
            back.hasExitTime = true;
            back.exitTime = 1;
            back.duration = 0;
            return c;
        }

        /// <summary>
        /// A controller with one looping state per clip; an int parameter picks the state (value = index) and
        /// the controller cross-fades to it.
        /// </summary>
        public static AnimatorController IntStates(string name, string param, float fade, params AnimationClip[] clips)
        {
            EnsureFolder();
            var path = $"{OutDir}/{name}.controller";
            AssetDatabase.DeleteAsset(path);
            var c = AnimatorController.CreateAnimatorControllerAtPath(path);
            c.AddParameter(param, AnimatorControllerParameterType.Int);
            var sm = c.layers[0].stateMachine;
            for (int i = 0; i < clips.Length; i++)
            {
                var st = sm.AddState(clips[i].name);
                st.motion = clips[i];
                if (i == 0) sm.defaultState = st;
                var t = sm.AddAnyStateTransition(st);
                t.hasExitTime = false;
                t.duration = fade;
                t.canTransitionToSelf = false;
                t.AddCondition(AnimatorConditionMode.Equals, i, param);
            }
            return c;
        }

        /// <summary>Like <see cref="Save"/> but the clip loops.</summary>
        public static AnimationClip SaveLooping(AnimationClip clip, string name)
        {
            Save(clip, name);
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            EditorUtility.SetDirty(clip);
            return clip;
        }

        private static void EnsureFolder()
        {
            if (!AssetDatabase.IsValidFolder(OutDir)) AssetDatabase.CreateFolder("Assets/_Caro/Art", "Animations");
        }
    }
}
