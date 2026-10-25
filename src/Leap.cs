// A wolf that leaps like a running wolf (Valheim, PotatoRider).
//
// Mount Up lets its mounts jump, and the wolf plays its own "Jump" clip, which on a ridden wolf looks like it springs
// up on straight legs. While a ridden wolf is in that clip, its pose is blended, after the animator has run, toward one
// frame of its run (C_Run): the frame where the legs reach furthest fore and aft. That frame is found once, by
// sampling the run at 24 points and measuring how far the lower half of the body's bones spread along the body, and
// kept as each bone's local pose. The blend eases in over BlendSeconds as the jump starts and out as it ends, so the
// legs reach out and fold back instead of popping into place.
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace PotatoRider
{
    internal static class Leap
    {
        private static readonly Dictionary<string, string> RunClipOf = new Dictionary<string, string> { { "Wolf", "C_Run" } };
        private static readonly AccessTools.FieldRef<Character, Animator> AnimatorOf = AccessTools.FieldRefAccess<Character, Animator>("m_animator");
        private static bool broken;

        private const float BlendSeconds = 0.25f;
        private sealed class Pose { public Quaternion[] Rotation; public Vector3[] Position; }
        private static readonly Dictionary<string, Pose> poseOf = new Dictionary<string, Pose>();
        private static Animator bonesFor;
        private static Transform[] bones;
        private static float blend;

        internal static void Frame()
        {
            if (broken) return;
            try
            {
                var player = Player.m_localPlayer;
                var mount = player && player.GetDoodadController() is Sadle saddle && saddle ? saddle.GetCharacter() : null;
                string name = mount ? Utils.GetPrefabName(mount.gameObject) : null;
                if (!mount || !RunClipOf.TryGetValue(name, out var runName)) { blend = 0f; return; }
                var animator = AnimatorOf(mount);
                if (!animator || !animator.runtimeAnimatorController) return;
                if (bonesFor != animator) { bonesFor = animator; bones = animator.GetComponentsInChildren<Transform>(); blend = 0f; }
                blend = Mathf.MoveTowards(blend, InJump(animator) ? 1f : 0f, Time.deltaTime / BlendSeconds);
                if (blend <= 0f) return;
                if (!poseOf.TryGetValue(name, out var pose))
                {
                    AnimationClip run = null;
                    foreach (var clip in animator.runtimeAnimatorController.animationClips) if (clip && clip.name == runName) { run = clip; break; }
                    if (!run) { broken = true; return; }
                    float at = Widest(animator.gameObject, run, mount.transform);
                    run.SampleAnimation(animator.gameObject, at * run.length);
                    pose = new Pose { Rotation = new Quaternion[bones.Length], Position = new Vector3[bones.Length] };
                    for (int i = 0; i < bones.Length; i++) { pose.Rotation[i] = bones[i].localRotation; pose.Position[i] = bones[i].localPosition; }
                    poseOf[name] = pose;
                    Plugin.Log.LogInfo($"Leap: {name} leaps with {runName} at {at:0.00} of the clip");
                    blend = 0f;   // this frame's pose was used for measuring; the animator rewrites it next frame
                    return;
                }
                if (pose.Rotation.Length != bones.Length) return;
                float weight = Mathf.SmoothStep(0f, 1f, blend);
                for (int i = 1; i < bones.Length; i++)   // 0 is the model itself, which stays where the game puts it
                {
                    if (!bones[i]) continue;
                    bones[i].localRotation = Quaternion.Slerp(bones[i].localRotation, pose.Rotation[i], weight);
                    bones[i].localPosition = Vector3.Lerp(bones[i].localPosition, pose.Position[i], weight);
                }
            }
            catch (System.Exception e) { broken = true; Plugin.Log.LogError("Leap is disabled: " + e.Message); }
        }

        private static bool InJump(Animator animator)
        {
            foreach (var info in animator.GetCurrentAnimatorClipInfo(0))
                if (info.clip && info.clip.name == "Jump" && info.weight > 0.5f) return true;
            return false;
        }

        // The point in the clip (0-1) where the bones in the lower part of the body spread furthest along it.
        private static float Widest(GameObject model, AnimationClip clip, Transform root)
        {
            var bones = model.GetComponentsInChildren<Transform>();
            float best = 0f, widest = -1f;
            for (int i = 0; i < 24; i++)
            {
                float t = i / 24f;
                clip.SampleAnimation(model, t * clip.length);
                float low = float.MaxValue, high = float.MinValue;
                foreach (var bone in bones) { float y = root.InverseTransformPoint(bone.position).y; low = Mathf.Min(low, y); high = Mathf.Max(high, y); }
                float cut = low + (high - low) * 0.4f, back = float.MaxValue, front = float.MinValue;
                foreach (var bone in bones)
                {
                    var p = root.InverseTransformPoint(bone.position);
                    if (p.y > cut) continue;
                    back = Mathf.Min(back, p.z); front = Mathf.Max(front, p.z);
                }
                if (front - back > widest) { widest = front - back; best = t; }
            }
            return best;
        }
    }
}
