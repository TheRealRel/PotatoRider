// A wolf that leaps like a running wolf (Valheim, PotatoRider).
//
// Mount Up lets its mounts jump, and the wolf plays its own "Jump" clip, which on a ridden wolf looks like it springs
// up on straight legs. While a ridden wolf is in that clip, its pose is replaced, after the animator has run, with one
// frame of its run (C_Run): the frame where the legs reach furthest fore and aft. That frame is found once, by
// sampling the run at 24 points and measuring how far the lower half of the body's bones spread along the body.
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace PotatoRider
{
    internal static class Leap
    {
        private static readonly Dictionary<string, string> RunClipOf = new Dictionary<string, string> { { "Wolf", "C_Run" } };
        private static readonly Dictionary<string, float> frameOf = new Dictionary<string, float>();
        private static readonly AccessTools.FieldRef<Character, Animator> AnimatorOf = AccessTools.FieldRefAccess<Character, Animator>("m_animator");
        private static bool broken;

        internal static void Frame()
        {
            if (broken) return;
            try
            {
                var player = Player.m_localPlayer;
                var mount = player && player.GetDoodadController() is Sadle saddle && saddle ? saddle.GetCharacter() : null;
                if (!mount) return;
                string name = Utils.GetPrefabName(mount.gameObject);
                if (!RunClipOf.TryGetValue(name, out var runName)) return;
                var animator = AnimatorOf(mount);
                if (!animator || !animator.runtimeAnimatorController || !InJump(animator)) return;
                AnimationClip run = null;
                foreach (var clip in animator.runtimeAnimatorController.animationClips) if (clip && clip.name == runName) { run = clip; break; }
                if (!run) return;
                if (!frameOf.TryGetValue(name, out float at))
                {
                    at = Widest(animator.gameObject, run, mount.transform);
                    frameOf[name] = at;
                    Plugin.Log.LogInfo($"Leap: {name} leaps with {runName} at {at:0.00} of the clip");
                }
                run.SampleAnimation(animator.gameObject, at * run.length);
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
