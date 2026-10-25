// Smooth movement for every ridden mount (Valheim, PotatoRider).
//
// The game moves animals in physics steps (50 a second) and draws more frames than that. A body with Unity's
// interpolation on is drawn smoothly between steps; one without it jumps from step to step. The Odin Horse, the Lox
// and Mount Up's bear have it on, Mount Up's wolf doesn't, so a ridden wolf moved and turned in visible jerks under a
// rider (and camera) that are drawn smoothly. While you ride, the mount's body gets interpolation; getting off puts
// back what it had.
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace PotatoRider
{
    internal static class Smoothing
    {
        private static readonly Dictionary<Rigidbody, RigidbodyInterpolation> before = new Dictionary<Rigidbody, RigidbodyInterpolation>();

        [HarmonyPatch(typeof(Player), nameof(Player.StartDoodadControl))]
        internal static class On
        {
            private static void Postfix(Player __instance, IDoodadController shipControl)
            {
                if (__instance != Player.m_localPlayer || !(shipControl is Sadle saddle) || !saddle || !saddle.GetCharacter()) return;
                var body = saddle.GetCharacter().GetComponent<Rigidbody>();
                if (!body || body.interpolation != RigidbodyInterpolation.None) return;
                before[body] = body.interpolation;
                body.interpolation = RigidbodyInterpolation.Interpolate;
            }
        }

        [HarmonyPatch(typeof(Player), nameof(Player.StopDoodadControl))]
        internal static class Off
        {
            private static void Prefix(Player __instance)
            {
                if (__instance != Player.m_localPlayer || before.Count == 0) return;
                foreach (var pair in before) if (pair.Key) pair.Key.interpolation = pair.Value;
                before.Clear();
            }
        }
    }
}
