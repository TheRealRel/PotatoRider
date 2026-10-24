// While attached to anything but a ship, the camera stays level when a riding mod tips the rider (Valheim, PotatoRider).
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;

namespace PotatoRider
{
    // ------------------------------------------------------------------ mounted camera roll
    // The camera points the way your character's eye points. The game levels the eye every frame, but riding mods tip
    // the rider over during attacks after that (the Odin Horse's lance and bow swings), and the eye tips with the
    // rider, rolling the camera onto its side. While attached to anything but a ship, the eye is levelled again just
    // before the camera reads it, and the camera's tilt uses true up instead of the rider's body. Ships work as before.
    internal static class MountedCameraRoll
    {
        private static readonly Action<Player> LevelEye =
            AccessTools.MethodDelegate<Action<Player>>(AccessTools.Method(typeof(Player), "UpdateEyeRotation"));
        private static GameObject level;

        private static bool Mounted(Player player) => player && player.IsAttached() && !player.IsAttachedToShip();

        [HarmonyPatch(typeof(GameCamera), "UpdateCamera")]
        internal static class EyeLevel
        {
            private static void Prefix()
            {
                var player = Player.m_localPlayer;
                if (Mounted(player)) LevelEye(player);
            }
        }

        [HarmonyPatch(typeof(GameCamera), "ApplyCameraTilt")]
        internal static class TrueUp
        {
            private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) =>
                Transpilers.CallInstead(instructions, x => x.Calls(AccessTools.Method(typeof(Character), nameof(Character.GetVisual))),
                    AccessTools.Method(typeof(TrueUp), nameof(UpFrom)), 1, "Valheim's camera tilt changed.");

            private static GameObject UpFrom(Character character)
            {
                if (character is Player player && Mounted(player))
                {
                    if (!level)
                    {
                        level = new GameObject("PotatoTweaks_Level");
                        UnityEngine.Object.DontDestroyOnLoad(level);
                    }
                    return level;
                }
                return character.GetVisual();
            }
        }
    }


    internal static class Transpilers
    {
        // Turns the instructions that match into calls of the replacement; any other number of matches than expected
        // means the game changed, so the patch fails at startup with the given reason instead of misbehaving.
        internal static List<CodeInstruction> CallInstead(IEnumerable<CodeInstruction> instructions, Func<CodeInstruction, bool> match,
            MethodInfo replacement, int expected, string changed)
        {
            var code = instructions.ToList();
            var matches = code.Where(match).ToList();
            if (matches.Count != expected) throw new InvalidOperationException(changed);
            foreach (var instruction in matches)
            {
                instruction.opcode = OpCodes.Call;
                instruction.operand = replacement;
            }
            return code;
        }
    }
}
