// Riding the Odin Horse with the keys instead of the mouse (Valheim, PotatoRider).
//
// The base game's saddle sends the horse wherever the camera looks while W or Shift is held; A and D do nothing.
// Here the rider keeps a heading of their own: A and D turn it at the horse's own turn speed (Odin Horse's "Turn
// Speed" and "Running Turn Speed"), and the saddle is told to go that way instead of where the camera looks.
// Hold W to walk, Shift to gallop; let go and the horse stops. A or D while standing turns it in place.
//
// The camera turns with the horse, so a turn doesn't swing the view around; the mouse looks around on top of that.
// While a bow is drawn the camera stops at the edges of Odin Horse's bow arc ("Bow Turn Left", "Bow Turn Right",
// "Bow Aim Up Down"), a few degrees inside it, so the crosshair can't point where an arrow can't go. The arc is
// measured from the saddle the way Odin Horse measures it (Player.m_attachPoint).
//
// Every piece has its own switch in the config so they can be compared mid-ride.
using System.Linq;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace PotatoRider
{
    internal static class Steering
    {
        internal static ConfigEntry<bool> Keys, CameraFollows, BowLock;
        internal static ConfigEntry<int> TurnPercent;
        internal static ConfigEntry<float> EdgeMargin;

        private static readonly AccessTools.FieldRef<Player, Transform> AttachPoint = AccessTools.FieldRefAccess<Player, Transform>("m_attachPoint");
        private static readonly AccessTools.FieldRef<Player, Quaternion> LookYaw = AccessTools.FieldRefAccess<Player, Quaternion>("m_lookYaw");
        private static readonly AccessTools.FieldRef<Character, Vector3> LookDir = AccessTools.FieldRefAccess<Character, Vector3>("m_lookDir");
        private static readonly AccessTools.FieldRef<Player, float> LookPitch = AccessTools.FieldRefAccess<Player, float>("m_lookPitch");

        internal static void Bind(ConfigFile config)
        {
            Keys = config.Bind("1 - Steering", "Steer with the keys", true, "Odin Horse: A and D turn the horse, hold W to walk and Shift to gallop, let go to stop. The mouse only moves the camera. Off: the base game's way, the horse goes where the camera looks.");
            TurnPercent = config.Bind("1 - Steering", "Turn speed percent", 100, new ConfigDescription("How fast A and D turn the horse, as a percent of its own turn speed (Odin Horse's Turn Speed settings).", new AcceptableValueRange<int>(25, 300)));
            CameraFollows = config.Bind("2 - Camera", "Turns with the horse", true, "The camera turns when the horse turns, so you keep your view. Off: the camera holds its direction while the horse turns under you.");
            BowLock = config.Bind("3 - Bow", "Camera stops at the bow arc", true, "While a bow is drawn on the horse, the camera stops at the edges of Odin Horse's bow arc, so the crosshair stays where arrows can go.");
            EdgeMargin = config.Bind("3 - Bow", "Margin inside the arc", 5f, new ConfigDescription("Degrees the camera stops short of the arc's edges. The arrow leaves from the bow hand, so it turns a little further than the camera.", new AcceptableValueRange<float>(0f, 20f)));
        }

        // ------------------------------------------------------------------ which mount
        private static Sadle Ridden(Player player)
        {
            if (!player || !player.IsAttached() || !(player.GetDoodadController() is Sadle saddle) || !saddle) return null;
            var mount = saddle.GetCharacter();
            return mount && mount.gameObject.name.Contains("rae_OdinHorse") ? saddle : null;
        }

        private static float Yaw(Vector3 v) => Mathf.Atan2(v.x, v.z) * Mathf.Rad2Deg;

        // ------------------------------------------------------------------ steering
        private static Sadle headingFor;
        private static float heading;

        [HarmonyPatch(typeof(Sadle), "ApplyControlls")]
        internal static class Keyboard
        {
            [HarmonyPriority(Priority.High)]
            private static void Prefix(Sadle __instance, ref Vector3 moveDir, ref Vector3 lookDir, bool run, ref bool block)
            {
                if (!Keys.Value || Ridden(Player.m_localPlayer) != __instance) { headingFor = null; return; }
                var mount = __instance.GetCharacter();
                float facing = Yaw(mount.transform.forward);
                if (headingFor != __instance) { headingFor = __instance; heading = facing; }

                float turn = Mathf.Clamp(moveDir.x, -1f, 1f);
                bool forward = moveDir.z > 0.5f;
                float rate = (run ? mount.m_runTurnSpeed : mount.m_turnSpeed) * TurnPercent.Value / 100f;
                heading += turn * rate * Time.deltaTime;
                // Never more than a quarter turn ahead of the horse, so letting go of A or D doesn't leave it turning on.
                heading = facing + Mathf.Clamp(Mathf.DeltaAngle(facing, heading), -25f, 25f);

                lookDir = Quaternion.Euler(0f, heading, 0f) * Vector3.forward;
                if (forward) moveDir = new Vector3(0f, 0f, 1f);                     // walk (Shift still gallops)
                else if (turn != 0f && !run) { moveDir = Vector3.zero; block = true; }  // turn in place
                else moveDir = new Vector3(0f, 0f, -1f);                             // let go: stop
            }
        }

        // ------------------------------------------------------------------ camera
        private static Sadle cameraFor;
        private static float lastFacing;

        [HarmonyPatch(typeof(Player), nameof(Player.SetMouseLook))]
        internal static class Look
        {
            private static void Postfix(Player __instance)
            {
                if (__instance != Player.m_localPlayer) return;
                var saddle = Ridden(__instance);
                var basis = saddle ? AttachPoint(__instance) : null;
                if (!basis) { cameraFor = null; return; }

                bool changed = false;
                float facing = Yaw(basis.forward);
                if (cameraFor != saddle) { cameraFor = saddle; lastFacing = facing; }
                float turned = Mathf.DeltaAngle(lastFacing, facing);
                lastFacing = facing;
                if (CameraFollows.Value && Keys.Value && turned != 0f)
                {
                    LookYaw(__instance) = Quaternion.Euler(0f, turned, 0f) * LookYaw(__instance);
                    changed = true;
                }

                if (BowLock.Value && __instance.IsDrawingBow() && Arc(out float left, out float right, out float pitch))
                {
                    // The camera's direction in the saddle's space, clamped like Odin Horse clamps the arrow.
                    Vector3 look = LookYaw(__instance) * Quaternion.Euler(LookPitch(__instance), 0f, 0f) * Vector3.forward;
                    Vector3 local = basis.InverseTransformDirection(look);
                    float m = EdgeMargin.Value;
                    float y = Yaw(local), p = Mathf.Atan2(local.y, new Vector2(local.x, local.z).magnitude) * Mathf.Rad2Deg;
                    float cy = Mathf.Clamp(y, -Mathf.Max(0f, left - m), Mathf.Max(0f, right - m));
                    float cp = Mathf.Clamp(p, -Mathf.Max(0f, pitch - m), Mathf.Max(0f, pitch - m));
                    if (cy != y || cp != p)
                    {
                        Vector3 world = basis.rotation * (Quaternion.Euler(-cp, cy, 0f) * Vector3.forward);
                        LookYaw(__instance) = Quaternion.Euler(0f, Yaw(world), 0f);
                        LookPitch(__instance) = -Mathf.Asin(Mathf.Clamp(world.y, -1f, 1f)) * Mathf.Rad2Deg;
                        changed = true;
                    }
                }

                if (changed)
                {
                    Traverse.Create(__instance).Method("UpdateEyeRotation").GetValue();
                    LookDir(__instance) = __instance.m_eye.forward;
                }
            }
        }

        // ------------------------------------------------------------------ Odin Horse's bow arc, read live
        private static ConfigEntry<float> arcLeft, arcRight, arcPitch;
        private static bool arcLooked;

        private static bool Arc(out float left, out float right, out float pitch)
        {
            if (!arcLooked)
            {
                arcLooked = true;
                if (Chainloader.PluginInfos.TryGetValue("Raelaziel.OdinHorse", out var info) && info.Instance)
                {
                    var c = info.Instance.Config;
                    ConfigEntry<float> Find(string key) => c.Keys.Where(k => k.Section == "Mounted Combat" && k.Key == key).Select(k => c[k] as ConfigEntry<float>).FirstOrDefault();
                    arcLeft = Find("Bow Turn Left"); arcRight = Find("Bow Turn Right"); arcPitch = Find("Bow Aim Up Down");
                }
                if (arcLeft == null || arcRight == null || arcPitch == null) Plugin.Log.LogWarning("Odin Horse's bow arc settings weren't found; the camera won't stop at the arc.");
            }
            left = arcLeft?.Value ?? 0f; right = arcRight?.Value ?? 0f; pitch = arcPitch?.Value ?? 0f;
            return arcLeft != null && arcRight != null && arcPitch != null;
        }
    }
}
