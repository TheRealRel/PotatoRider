// PotatoRider: riding and mounted combat fixes for Valheim (working name). Works with the Odin Horse's mounted combat
// and other riding mods. Everything here runs in your own game; the server doesn't need it.
using System;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using Jotunn.Utils;

namespace PotatoRider
{
    [BepInPlugin(Id, "PotatoRider", Version)]
    [BepInDependency(Jotunn.Main.ModGuid)]
    [BepInDependency("ishid4.mods.betterarchery", BepInDependency.DependencyFlags.SoftDependency)]
    [NetworkCompatibility(CompatibilityLevel.NotEnforced, VersionStrictness.None)]
    public class Plugin : BaseUnityPlugin
    {
        public const string Id = "potato.rider", Version = "1.0.0";
        internal static ManualLogSource Log;
        private Harmony harmony;

        private void LateUpdate() { Leap.Frame(); }

        private void Awake()
        {
            Log = Logger;
            harmony = new Harmony(Id);
            Patch(typeof(MountedCameraRoll.EyeLevel));
            Patch(typeof(MountedCameraRoll.TrueUp));
            Patch(typeof(RiddenHorse.HoldsBack));
            Patch(typeof(MountIcon.OwnIcon));
            Steering.Bind(Config);
            Patch(typeof(Steering.Keyboard));
            Patch(typeof(Steering.Turning));
            Patch(typeof(Steering.BuildUp));
            Patch(typeof(Steering.Look));
            RideAnywhere.Bind(Config);
            Patch(typeof(RideAnywhere.Mount));
            Patch(typeof(RideAnywhere.Prompt));
            Calling.Bind(Config);
            Patch(typeof(Calling.Remember));
            Patch(typeof(Calling.Stay));
            Patch(typeof(Calling.CallKeyPress));
            Patch(typeof(Smoothing.On));
            Patch(typeof(Smoothing.Off));
            Run("Better Archery's body turn leaves a mounted rider to the riding mod", () => MountedBowSpine.Enable(harmony));
        }

        private void Patch(Type type)
        {
            try { harmony.CreateClassProcessor(type).Patch(); }
            catch (Exception e) { Log.LogError($"{type.Name} is disabled: {e.GetBaseException().Message}"); }
        }

        private void Run(string name, Action action)
        {
            try { action(); }
            catch (Exception e) { Log.LogError($"{name}: disabled ({e.GetBaseException().Message})"); }
        }
    }
}
