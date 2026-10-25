// Ride from anywhere on a saddled tame (Valheim, PotatoRider).
//
// Mounting normally means looking at the saddle itself and pressing Use. On a big animal whose body wraps around the
// saddle (Mount Up's bear: its hit box covers the whole back), the look-at ray hits the body first, so the saddle's
// prompt never shows and there's no way on. Zephyrfit Taming worked around this with its own pick box round the
// seat; Mount Up only had a force-mount key inside its adjust mode.
//
// Here, holding the ride key (Left Alt by default) while pressing Use on any part of a tamed, saddled animal mounts
// it. It sends the saddle's own mount request (Sadle.Interact), so the game, Mount Up and Odin Horse handle it exactly
// as if you'd clicked the saddle: tamed check, distance, someone already riding, Mount Up setting the seat. Plain Use
// still pets, Shift + Use still renames, Odin Horse's B + Use still opens saddlebags, and Shift + Use on the saddle
// itself still takes it off. The animal's hover text shows the key while it can be ridden.
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace PotatoRider
{
    internal static class RideAnywhere
    {
        internal static ConfigEntry<bool> Enabled;
        internal static ConfigEntry<KeyCode> Key;

        private static readonly System.Func<Tameable, bool> HaveSaddle =
            AccessTools.MethodDelegate<System.Func<Tameable, bool>>(AccessTools.Method(typeof(Tameable), "HaveSaddle"));

        internal static void Bind(ConfigFile config)
        {
            Enabled = config.Bind("4 - Mounting", "Ride from anywhere on the animal", true, "Hold the ride key and press Use anywhere on a tamed, saddled animal to get on, not just on the saddle. Helps with big animals whose body hides the saddle.");
            Key = config.Bind("4 - Mounting", "Ride key", KeyCode.LeftAlt, "The key held with Use to get on from anywhere on the animal.");
        }

        // The saddle of a tamed, saddled animal that nobody is riding, or null.
        private static Sadle Rideable(GameObject go)
        {
            var animal = go ? go.GetComponentInParent<Character>() : null;
            if (!animal || animal.IsPlayer() || !animal.IsTamed() || animal.IsDead()) return null;
            var tame = animal.GetComponent<Tameable>();
            if (!tame || !tame.m_saddle || !HaveSaddle(tame)) return null;
            return tame.m_saddle.HaveValidUser() ? null : tame.m_saddle;
        }

        // Mount Up's adjust mode has its own Alt + E (force mount). While it's on, ours steps aside: one prompt, one mount.
        private static ConfigEntry<bool> mountUpAdjust;
        private static bool mountUpLooked;
        private static bool MountUpAdjusting()
        {
            if (!mountUpLooked)
            {
                mountUpLooked = true;
                if (BepInEx.Bootstrap.Chainloader.PluginInfos.TryGetValue("meldurson.MountUpRestored", out var info) && info.Instance)
                    info.Instance.Config.TryGetEntry("Custom Mounts", "Enable Adjust Custom Mounts", out mountUpAdjust);
            }
            return mountUpAdjust != null && mountUpAdjust.Value;
        }

        private static string KeyName(KeyCode k)
        {
            switch (k)
            {
                case KeyCode.LeftAlt: case KeyCode.RightAlt: return "Alt";
                case KeyCode.LeftControl: case KeyCode.RightControl: return "Ctrl";
                case KeyCode.LeftShift: case KeyCode.RightShift: return "Shift";
                default: return k.ToString();
            }
        }

        [HarmonyPatch(typeof(Player), "Interact")]
        internal static class Mount
        {
            private static bool Prefix(Player __instance, GameObject go, bool hold)
            {
                if (!Enabled.Value || MountUpAdjusting() || hold || __instance != Player.m_localPlayer || __instance.IsAttached() || !Input.GetKey(Key.Value)) return true;
                var saddle = Rideable(go);
                if (!saddle) return true;
                saddle.Interact(__instance, false, false);     // the saddle's own mount request
                return false;                                   // and not a pet as well
            }
        }

        [HarmonyPatch(typeof(Tameable), nameof(Tameable.GetHoverText))]
        internal static class Prompt
        {
            private static void Postfix(Tameable __instance, ref string __result)
            {
                if (!Enabled.Value || MountUpAdjusting() || string.IsNullOrEmpty(__result) || !Rideable(__instance.gameObject)) return;
                __result += Localization.instance.Localize($"\n[<color=yellow><b>{KeyName(Key.Value)} + $KEY_Use</b></color>] Ride");
            }
        }
    }
}
