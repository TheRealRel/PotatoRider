// Riding the Odin Horse with the keys instead of the mouse (Valheim, PotatoRider).
//
// The base game's saddle sends the horse wherever the camera looks while W or Shift is held; A and D do nothing.
// Here the saddle is always told to go straight ahead, the way the horse faces, and A and D turn the horse itself at
// its own turn speed (Odin Horse's "Turn Speed" walking, "Running Turn Speed" galloping), easing in and out over a
// quarter second. Turning the horse directly, rather than pointing the saddle off to one side, keeps it walking the
// way it faces (a creature walks toward its target and turns at full speed only when that target is 90 degrees away,
// so an offset target makes it crab sideways) and stops the turn as soon as the key is let go.
// Hold W to walk, Shift to gallop; let go and the horse stops. A or D while standing turns it in place.
//
// The camera turns with the horse, so a turn doesn't swing the view around; the mouse looks around on top of that.
// The horse's facing only changes on physics steps (50 a second, in steps of about 0.8 degrees), so the camera eases
// after it instead of copying each step, which made the whole view judder.
// While a bow is drawn the camera stops at the edges of Odin Horse's bow arc ("Bow Turn Left", "Bow Turn Right",
// "Bow Aim Up Down"), so the crosshair can't point where an arrow can't go. The arc is measured from the saddle the
// way Odin Horse measures it (Player.m_attachPoint); measured in game, an arrow at the edge leaves along the crosshair.
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
            TurnPercent = config.Bind("1 - Steering", "Turn speed percent", 100, new ConfigDescription("How fast A and D turn the horse, as a percent of its own turn speed (Odin Horse's Turn Speed and Running Turn Speed: 75 and 120 degrees a second by default).", new AcceptableValueRange<int>(25, 300)));
            CameraFollows = config.Bind("2 - Camera", "Turns with the horse", true, "The camera turns when the horse turns, so you keep your view. Off: the camera holds its direction while the horse turns under you.");
            BowLock = config.Bind("3 - Bow", "Camera stops at the bow arc", true, "While a bow is drawn on the horse, the camera stops at the edges of Odin Horse's bow arc, so the crosshair stays where arrows can go.");
            EdgeMargin = config.Bind("3 - Bow", "Margin inside the arc", 0f, new ConfigDescription("Degrees the camera stops short of the arc's edges. 0 stops it right at the edge, where arrows still leave along the crosshair.", new AcceptableValueRange<float>(0f, 20f)));
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
        private enum Speed { Stop, Walk, Run, Turn, NoChange }   // the same order as Sadle.Speed
        private static readonly AccessTools.FieldRef<Sadle, Vector3> ControlDir = AccessTools.FieldRefAccess<Sadle, Vector3>("m_controlDir");
        private static readonly System.Reflection.FieldInfo SpeedField = AccessTools.Field(typeof(Sadle), "m_speed");
        private static Speed SpeedOf(Sadle saddle) => (Speed)System.Convert.ToInt32(SpeedField.GetValue(saddle));

        private static Sadle steered;
        private static float turnInput, turnEased;

        private static Vector3 Ahead(Character mount)
        {
            Vector3 f = mount.transform.forward; f.y = 0f;
            return f.sqrMagnitude > 0.0001f ? f.normalized : Vector3.forward;
        }

        [HarmonyPatch(typeof(Sadle), "ApplyControlls")]
        internal static class Keyboard
        {
            [HarmonyPriority(Priority.High)]
            private static void Prefix(Sadle __instance, ref Vector3 moveDir, ref Vector3 lookDir, bool run, ref bool block)
            {
                if (!Keys.Value || Ridden(Player.m_localPlayer) != __instance) { if (steered == __instance) steered = null; return; }
                steered = __instance;
                turnInput = Mathf.Clamp(moveDir.x, -1f, 1f);
                bool forward = moveDir.z > 0.5f;
                lookDir = Ahead(__instance.GetCharacter());                         // always straight ahead; A and D turn the horse
                if (forward) moveDir = new Vector3(0f, 0f, 1f);                     // walk (Shift still gallops)
                else if (turnInput != 0f && !run)
                {
                    var now = SpeedOf(__instance);
                    if (now == Speed.Walk || now == Speed.Run) moveDir = new Vector3(0f, 0f, -1f);  // stop first: a moving saddle ignores "turn"
                    else { moveDir = Vector3.zero; block = true; }                  // then turn in place
                }
                else moveDir = new Vector3(0f, 0f, -1f);                             // let go: stop
            }
        }

        // The game turns a creature in Character.UpdateRotation, every physics step, and returns the turn rate, which
        // becomes the animator's turn_speed. The Odin Horse's movement blend uses turn_speed across and forward_speed
        // up: walk-left and walk-right at turn -1/+1 with no forward speed, gallop-left and gallop-right at the gallop
        // end. So doing the turn here, and reporting it, plays those legs when turning in place and blends them in
        // while walking. (Turning from Sadle.UpdateRiding ran only at the creature-AI rate, 20 times a second, in
        // 3.75-degree jumps, and reported no turn, so the legs stood still.) Runs where the horse is simulated; the
        // rider owns it while riding.
        private static readonly AccessTools.FieldRef<Character, Vector3> MoveDir = AccessTools.FieldRefAccess<Character, Vector3>("m_moveDir");

        [HarmonyPatch(typeof(Character), "UpdateRotation")]
        internal static class Turning
        {
            private static bool Prefix(Character __instance, float dt, ref float __result)
            {
                var saddle = steered;
                if (!saddle || !Keys.Value || __instance != saddle.GetCharacter() || !saddle.HaveValidUser()) return true;
                turnEased = Mathf.MoveTowards(turnEased, turnInput, dt / 0.25f);
                bool running = SpeedOf(saddle) == Speed.Run;
                float rate = (running ? __instance.m_runTurnSpeed : __instance.m_turnSpeed) * TurnPercent.Value / 100f;
                float angle = turnEased * rate * dt;
                if (angle != 0f)
                {
                    var turn = Quaternion.Euler(0f, angle, 0f);
                    __instance.transform.rotation = turn * __instance.transform.rotation;
                    if (MoveDir(__instance) != Vector3.zero) MoveDir(__instance) = turn * MoveDir(__instance);  // keep walking the way it faces
                    ControlDir(saddle) = Ahead(__instance);                                                   // and so does the saddle
                }
                __result = turnEased * rate * Mathf.Deg2Rad;   // what the game reports: radians a second, signed
                return false;                                  // the rider steers; nothing else turns the horse
            }
        }

        // ------------------------------------------------------------------ camera
        private static Sadle cameraFor;
        private static float lastFacing, owed;
        private const float CameraEase = 0.06f;   // seconds; the horse turns in 20 ms steps

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
                if (cameraFor != saddle) { cameraFor = saddle; lastFacing = facing; owed = 0f; }
                owed += Mathf.DeltaAngle(lastFacing, facing);
                lastFacing = facing;
                if (!CameraFollows.Value || !Keys.Value) owed = 0f;
                if (owed != 0f)
                {
                    // Ease after the horse's stepped turn; a big sudden turn (a jump, a knock-back) is taken at once.
                    float step = Mathf.Abs(owed) > 30f ? owed : owed * (1f - Mathf.Exp(-Time.deltaTime / CameraEase));
                    if (Mathf.Abs(owed - step) < 0.001f) step = owed;
                    owed -= step;
                    LookYaw(__instance) = Quaternion.Euler(0f, step, 0f) * LookYaw(__instance);
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
