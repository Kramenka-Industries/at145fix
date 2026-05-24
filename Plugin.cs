using System;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

// Drop-in performance-optimized rebuild of "QoL AT-145 Accuracy Fix"
// (com.raksaputra.qol145fix). Gameplay behavior and all original config
// entries/defaults/descriptions are preserved verbatim.
//
// Changes vs. the original:
//   1. Removed SceneManager.sceneLoaded hook + ApplyToPrefabAndDump, which
//      ran Resources.FindObjectsOfTypeAll<GameObject>() + a LINQ .name scan
//      over every loaded object on every scene load. The per-instance
//      StartMissile postfix already patches every launched missile, so the
//      prefab pass was redundant. This removes the single largest cost.
//   2. Per-launch logging is gated behind a new "Debug/Verbose Logging"
//      config (default false); interpolated strings are not built when off.
//   3. Reflection field access uses cached Harmony FieldRef delegates
//      (zero boxing, no per-launch FieldInfo.GetValue/SetValue).

namespace Qol145Fix
{
    [BepInPlugin("com.raksaputra.qol145fix", "QoL AT-145 Accuracy Fix", "1.0.1")]
    public class Qol145FixPlugin : BaseUnityPlugin
    {
        public static ConfigEntry<float> LoftAmount;
        public static ConfigEntry<bool> OverrideMotor;
        public static ConfigEntry<float> MotorBurnTime;
        public static ConfigEntry<float> MotorThrust;
        public static ConfigEntry<float> MotorFuelMass;
        public static ConfigEntry<bool> VerboseLogging;
        public static ManualLogSource Log;

        private void Awake()
        {
            Log = Logger;

            LoftAmount = Config.Bind<float>("General", "AT-145 Loft Amount", 0.5f,
                "Restores top-attack capability to the AT-145. 0.5 is vanilla/standard.");
            OverrideMotor = Config.Bind<bool>("General", "Override Motor Parameters", true,
                "If true, overrides the motor properties to restore vanilla flight characteristics (improves accuracy significantly).");
            MotorBurnTime = Config.Bind<float>("General", "Motor Burn Time", 6.5f,
                "Burn time of the AT-145 engine in seconds. Vanilla is 6.5s. QoL mod sets to 1.0s.");
            MotorThrust = Config.Bind<float>("General", "Motor Thrust", 950f,
                "Thrust of the AT-145 engine in Newtons. Vanilla is ~950N. QoL mod sets to 5000N.");
            MotorFuelMass = Config.Bind<float>("General", "Motor Fuel Mass", 3.5f,
                "Fuel mass of the AT-145 engine in kg. Vanilla is ~3.5kg. QoL mod sets to 1.0kg.");
            VerboseLogging = Config.Bind<bool>("Debug", "Verbose Logging", false,
                "Logs every AT-145 launch with before/after values. Off by default to avoid per-launch log I/O and string allocation.");

            Logger.LogInfo("QoL AT-145 Accuracy Fix loaded! (perf-optimized: no per-scene prefab scan, gated logging, no boxed reflection)");

            new Harmony("com.raksaputra.qol145fix").PatchAll();
        }
    }

    [HarmonyPatch(typeof(Missile), "StartMissile")]
    public static class Missile_StartMissile_Patch
    {
        // All reflection resolved exactly once at type load. Zero per-launch
        // allocation: FieldRef delegates return by ref (no float boxing,
        // no FieldInfo.GetValue/SetValue).
        private static readonly AccessTools.FieldRef<Missile, MissileSeeker> SeekerRef =
            AccessTools.FieldRefAccess<Missile, MissileSeeker>("seeker");

        private static readonly AccessTools.FieldRef<OpticalSeeker, float> LoftRef =
            AccessTools.FieldRefAccess<OpticalSeeker, float>("loftAmount");

        // Missile.motors is Motor[]; Motor is a private nested reference type.
        // GetValue returns the array reference (no element boxing).
        private static readonly FieldInfo MotorsField =
            AccessTools.Field(typeof(Missile), "motors");

        private static readonly Type MotorType =
            AccessTools.Inner(typeof(Missile), "Motor");

        private static readonly AccessTools.FieldRef<object, float> ThrustRef =
            AccessTools.FieldRefAccess<float>(MotorType, "thrust");

        private static readonly AccessTools.FieldRef<object, float> BurnTimeRef =
            AccessTools.FieldRefAccess<float>(MotorType, "burnTime");

        private static readonly AccessTools.FieldRef<object, float> FuelMassRef =
            AccessTools.FieldRefAccess<float>(MotorType, "fuelMass");

        public static void Postfix(Missile __instance)
        {
            // Cheap reject first: one marshalled name fetch, ordinal substring.
            // Runs for every missile launch of every type; everything below
            // only executes for AT-145 (Missile_G2G).
            string name = __instance.name;
            if (name == null || !name.Equals("Missile_G2G"))
                return;

            bool verbose = Qol145FixPlugin.VerboseLogging.Value;

            // Loft (top-attack restore)
            OpticalSeeker seeker = SeekerRef(__instance) as OpticalSeeker;
            if (seeker != null)
            {
                float newLoft = Qol145FixPlugin.LoftAmount.Value;
                if (verbose)
                {
                    float old = LoftRef(seeker);
                    Qol145FixPlugin.Log?.LogInfo($"[qol145fix] {name} loftAmount {old} -> {newLoft}");
                }
                LoftRef(seeker) = newLoft;
            }

            // Motor override (motors[0] only — preserves original behavior)
            if (!Qol145FixPlugin.OverrideMotor.Value)
                return;

            if (!(MotorsField.GetValue(__instance) is Array motors) || motors.Length == 0)
                return;

            object motor = motors.GetValue(0);
            if (motor == null)
                return;

            float thrust = Qol145FixPlugin.MotorThrust.Value;
            float burn = Qol145FixPlugin.MotorBurnTime.Value;
            float fuel = Qol145FixPlugin.MotorFuelMass.Value;

            if (verbose)
            {
                float oThrust = ThrustRef(motor);
                float oBurn = BurnTimeRef(motor);
                float oFuel = FuelMassRef(motor);
                Qol145FixPlugin.Log?.LogInfo(
                    $"[qol145fix] {name} motor Thrust {oThrust}->{thrust}, BurnTime {oBurn}->{burn}, FuelMass {oFuel}->{fuel}");
            }

            ThrustRef(motor) = thrust;
            BurnTimeRef(motor) = burn;
            FuelMassRef(motor) = fuel;
        }
    }
}
