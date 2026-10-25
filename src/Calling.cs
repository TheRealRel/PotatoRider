// Calling your mount, and mounts that stay where you leave them (Valheim, PotatoRider).
//
// PotatoRider remembers the tame you last rode (saved on your character, Player.m_customData, so it survives a relog).
// The call key whistles and tells that tame to follow you, with the game's own tame command (Tameable.Command, which
// runs RPC_Command on whichever game owns the animal, so it works in multiplayer and on Mount Up's and Odin Horse's
// tames). RPC_Command toggles follow and stay, so the call reads the follow name the game keeps on the animal
// (ZDOVars.s_follow) first: already following you, nothing to send; following someone else, send it twice; staying,
// send it once. The animal answers with its own alerted call (BaseAI.m_alertedEffects: the wolf's howl, the Lox's
// shout, the Odin Horse's neigh). It works while the animal is loaded, which is as far as it can hear you; past that,
// a message says it's out of earshot.
//
// The game has no whistle sound (none among its 973 sound effects), so the whistle is made in code: two rising notes,
// the second with a slight warble and a breathy edge. It plays through the game's sound-effect mixer, borrowed from
// one of the game's animal calls, so it follows your volume settings. Only your own game plays it.
//
// Getting off a mount (Player.StopDoodadControl, while you still own the animal) gives it the game's stay: no follow
// target and a patrol point where it stands, the same as telling a tame to stay. It no longer wanders off or trails
// after you; calling it brings it back.
using System;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Audio;

namespace PotatoRider
{
    internal static class Calling
    {
        internal static ConfigEntry<bool> CallOn, StayOn;
        internal static ConfigEntry<KeyCode> CallKey;
        internal static ConfigEntry<int> WhistleVolume;
        private const string LastMount = "PotatoRider_LastMount", LastMountName = "PotatoRider_LastMountName";

        private static readonly Func<Player, bool> TakeInput =
            AccessTools.MethodDelegate<Func<Player, bool>>(AccessTools.Method(typeof(Player), "TakeInput"));

        internal static void Bind(ConfigFile config)
        {
            StayOn = config.Bind("4 - Mounting", "Stays where you get off", true, "A mount you get off stays on that spot, like telling a tame to stay, instead of wandering off or trailing after you.");
            CallOn = config.Bind("5 - Calling", "Call your mount", true, "The call key whistles for the tame you last rode, and it comes to you, as long as it's close enough to hear (in the area the game has loaded around you).");
            CallKey = config.Bind("5 - Calling", "Call key", KeyCode.J, "The key that whistles for your mount.");
            WhistleVolume = config.Bind("5 - Calling", "Whistle volume", 60, new ConfigDescription("How loud the whistle is, in percent, on top of your sound effects volume. 0 is silent.", new AcceptableValueRange<int>(0, 100)));
        }

        // ------------------------------------------------------------------ remembering the last mount
        [HarmonyPatch(typeof(Player), nameof(Player.StartDoodadControl))]
        internal static class Remember
        {
            private static void Postfix(Player __instance, IDoodadController shipControl)
            {
                if (__instance != Player.m_localPlayer || !(shipControl is Sadle saddle) || !saddle) return;
                var nview = saddle.GetCharacter() ? saddle.GetCharacter().GetComponent<ZNetView>() : null;
                if (!nview || !nview.IsValid()) return;
                var id = nview.GetZDO().m_uid;
                __instance.m_customData[LastMount] = id.UserID + ":" + id.ID;
                var tame = saddle.GetCharacter().GetComponent<Tameable>();
                __instance.m_customData[LastMountName] = tame ? tame.GetHoverName() : Localization.instance.Localize(saddle.GetCharacter().m_name);
            }
        }

        private static bool Remembered(Player player, out ZDOID id)
        {
            id = ZDOID.None;
            if (!player.m_customData.TryGetValue(LastMount, out var s)) return false;
            var parts = s.Split(':');
            if (parts.Length != 2 || !long.TryParse(parts[0], out long user) || !uint.TryParse(parts[1], out uint n)) return false;
            id = new ZDOID(user, n);
            return true;
        }

        // ------------------------------------------------------------------ staying where you get off
        [HarmonyPatch(typeof(Player), nameof(Player.StopDoodadControl))]
        internal static class Stay
        {
            private static void Prefix(Player __instance)
            {
                if (!StayOn.Value || __instance != Player.m_localPlayer || !(__instance.GetDoodadController() is Sadle saddle) || !saddle) return;
                var animal = saddle.GetCharacter();
                var ai = animal ? animal.GetComponent<MonsterAI>() : null;
                var nview = animal ? animal.GetComponent<ZNetView>() : null;
                if (!ai || !nview || !nview.IsValid() || animal.IsDead()) return;
                if (nview.IsOwner())
                {
                    ai.SetFollowTarget(null);
                    ai.SetPatrolPoint();
                    nview.GetZDO().Set(ZDOVars.s_follow, "");
                }
                else if (!string.IsNullOrEmpty(nview.GetZDO().GetString(ZDOVars.s_follow)))
                {
                    animal.GetComponent<Tameable>()?.Command(__instance, false);   // following: toggles to stay
                }
            }
        }

        // ------------------------------------------------------------------ calling
        [HarmonyPatch(typeof(Player), "Update")]
        internal static class CallKeyPress
        {
            private static void Postfix(Player __instance)
            {
                if (!CallOn.Value || __instance != Player.m_localPlayer || !Input.GetKeyDown(CallKey.Value)) return;
                if (!TakeInput(__instance) || __instance.IsDead()) return;
                Call(__instance);
            }
        }

        private static void Call(Player player)
        {
            if (WhistleVolume.Value > 0) Whistle(player);
            if (!Remembered(player, out var id)) { Say(player, "You haven't ridden anything yet", "nothing remembered"); return; }
            var go = ZNetScene.instance ? ZNetScene.instance.FindInstance(id) : null;
            if (!go)
            {
                // Not loaded near you. Your game may not even know of it (after logging in, it only hears of what's
                // nearby), so it's out of earshot either way, named as it was when you last rode it.
                player.m_customData.TryGetValue(LastMountName, out var name);
                Say(player, (string.IsNullOrEmpty(name) ? "Your mount" : name) + " is out of earshot", "not loaded");
                return;
            }
            var animal = go.GetComponent<Character>();
            var tame = go.GetComponent<Tameable>();
            var nview = go.GetComponent<ZNetView>();
            if (!animal || animal.IsDead() || !tame || !nview || !nview.IsValid() || !animal.IsTamed()) { Say(player, "Your mount can't come", "not a living tame"); return; }
            if (player.GetDoodadController() is Sadle riding && riding && riding.GetCharacter() == animal) return;   // already on it

            string following = nview.GetZDO().GetString(ZDOVars.s_follow);
            if (following != player.GetPlayerName())
            {
                if (!string.IsNullOrEmpty(following)) tame.Command(player, false);   // following someone else: to stay first
                tame.Command(player, false);                                         // then to follow you
            }
            var ai = go.GetComponent<BaseAI>();
            if (ai != null) ai.m_alertedEffects.Create(go.transform.position, go.transform.rotation, go.transform);
            Say(player, tame.GetHoverName() + " is coming", "following was '" + following + "'");
        }

        private static void Say(Player player, string text, string why)
        {
            player.Message(MessageHud.MessageType.Center, text);
            Plugin.Log.LogInfo("Call: " + text + " (" + why + ")");
        }

        // ------------------------------------------------------------------ the whistle, made in code
        private static AudioClip whistle;
        private static AudioMixerGroup mixer;
        private static bool mixerLooked;

        private static void Whistle(Player player)
        {
            if (!whistle) whistle = MakeWhistle();
            if (!mixerLooked && ZNetScene.instance)
            {
                mixerLooked = true;
                foreach (var name in new[] { "sfx_wolf_alerted", "sfx_lox_alerted", "sfx_boar_alerted" })
                {
                    var p = ZNetScene.instance.GetPrefab(name);
                    var src = p ? p.GetComponentInChildren<AudioSource>(true) : null;
                    if (src && src.outputAudioMixerGroup) { mixer = src.outputAudioMixerGroup; break; }
                }
            }
            var go = new GameObject("PotatoRider_Whistle");
            go.transform.position = player.GetHeadPoint();
            var audio = go.AddComponent<AudioSource>();
            audio.clip = whistle;
            audio.outputAudioMixerGroup = mixer;
            audio.spatialBlend = 0.5f;
            float loud = 0.55f * WhistleVolume.Value / 100f;
            audio.volume = mixer ? loud : loud * AudioMan.GetSFXVolume();
            audio.Play();
            UnityEngine.Object.Destroy(go, whistle.length + 0.2f);
        }

        private static AudioClip MakeWhistle()
        {
            const int rate = 44100;
            // Two notes: a short rising one, a gap, then a longer one rising higher with a slight warble.
            (float start, float length, float f0, float f1, float warble)[] notes =
            {
                (0.00f, 0.13f, 2250f, 2700f, 0f),
                (0.19f, 0.32f, 2450f, 3150f, 22f),
            };
            float total = 0.55f;
            var data = new float[(int)(rate * total)];
            var rng = new System.Random(7);
            float breath = 0f;
            foreach (var n in notes)
            {
                double phase = 0;
                int s0 = (int)(n.start * rate), len = (int)(n.length * rate);
                for (int i = 0; i < len && s0 + i < data.Length; i++)
                {
                    float t = i / (float)rate, u = i / (float)len;
                    float f = n.f0 + (n.f1 - n.f0) * Mathf.SmoothStep(0f, 1f, Mathf.Min(1f, u * 1.6f)) - (u > 0.8f ? (u - 0.8f) * 900f : 0f);
                    f += n.warble * Mathf.Sin(2f * Mathf.PI * 6f * t);
                    phase += 2 * Math.PI * f / rate;
                    float env = Mathf.Min(1f, t / 0.015f) * Mathf.Min(1f, (n.length - t) / 0.04f);
                    breath = breath * 0.85f + (float)(rng.NextDouble() * 2 - 1) * 0.15f;   // soft, filtered air
                    data[s0 + i] += env * (0.5f * (float)Math.Sin(phase) + 0.04f * breath);
                }
            }
            var clip = AudioClip.Create("PotatoRider_Whistle", data.Length, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
