using System;
using System.Collections.Generic;
using TaleWorlds.Core;

namespace ProperShieldWalls
{
    /// <summary>
    /// Centralized weapon type → settings lookup. Eliminates duplicated switch
    /// statements across FriendlyFireCheckPatch and MeleeHitFriendlyBypassPatch.
    /// Adding a new weapon type = one entry here + MCM properties in Settings.cs.
    /// </summary>
    internal static class WeaponBypassConfig
    {
        internal struct WeaponSettings
        {
            public bool Enabled;
            public float BypassStart;
            public float BypassEnd;
        }

        private static readonly Dictionary<WeaponClass, Func<Settings, WeaponSettings>> Lookup
            = new Dictionary<WeaponClass, Func<Settings, WeaponSettings>>
        {
            // Polearms / Spears
            { WeaponClass.OneHandedPolearm, s => new WeaponSettings
                { Enabled = s.PolearmEnabled, BypassStart = s.PolearmBypassStart, BypassEnd = s.PolearmBypassEnd } },
            { WeaponClass.TwoHandedPolearm, s => new WeaponSettings
                { Enabled = s.PolearmEnabled, BypassStart = s.PolearmBypassStart, BypassEnd = s.PolearmBypassEnd } },
            { WeaponClass.LowGripPolearm, s => new WeaponSettings
                { Enabled = s.PolearmEnabled, BypassStart = s.PolearmBypassStart, BypassEnd = s.PolearmBypassEnd } },

            // Swords
            { WeaponClass.OneHandedSword, s => new WeaponSettings
                { Enabled = s.OneHandedSwordEnabled, BypassStart = s.OneHandedSwordBypassStart, BypassEnd = s.OneHandedSwordBypassEnd } },
            { WeaponClass.TwoHandedSword, s => new WeaponSettings
                { Enabled = s.TwoHandedSwordEnabled, BypassStart = s.TwoHandedSwordBypassStart, BypassEnd = s.TwoHandedSwordBypassEnd } },

            // Axes
            { WeaponClass.OneHandedAxe, s => new WeaponSettings
                { Enabled = s.OneHandedAxeEnabled, BypassStart = s.OneHandedAxeBypassStart, BypassEnd = s.OneHandedAxeBypassEnd } },
            { WeaponClass.TwoHandedAxe, s => new WeaponSettings
                { Enabled = s.TwoHandedAxeEnabled, BypassStart = s.TwoHandedAxeBypassStart, BypassEnd = s.TwoHandedAxeBypassEnd } },

            // Blunt
            { WeaponClass.Mace, s => new WeaponSettings
                { Enabled = s.MaceEnabled, BypassStart = s.MaceBypassStart, BypassEnd = s.MaceBypassEnd } },
            { WeaponClass.TwoHandedMace, s => new WeaponSettings
                { Enabled = s.MaceEnabled, BypassStart = s.MaceBypassStart, BypassEnd = s.MaceBypassEnd } },

            // Small weapons
            { WeaponClass.Dagger, s => new WeaponSettings
                { Enabled = s.DaggerEnabled, BypassStart = s.DaggerBypassStart, BypassEnd = s.DaggerBypassEnd } },

            // Throwables (melee use)
            { WeaponClass.Javelin, s => new WeaponSettings
                { Enabled = s.JavelinEnabled, BypassStart = s.JavelinBypassStart, BypassEnd = s.JavelinBypassEnd } },
            { WeaponClass.ThrowingAxe, s => new WeaponSettings
                { Enabled = s.ThrowingAxeEnabled, BypassStart = s.ThrowingAxeBypassStart, BypassEnd = s.ThrowingAxeBypassEnd } },
            { WeaponClass.ThrowingKnife, s => new WeaponSettings
                { Enabled = s.ThrowingKnifeEnabled, BypassStart = s.ThrowingKnifeBypassStart, BypassEnd = s.ThrowingKnifeBypassEnd } },
        };

        /// <summary>
        /// Get the bypass settings for a weapon class. Returns false if the weapon type is not configured.
        /// </summary>
        public static bool TryGetSettings(WeaponClass weaponClass, Settings settings, out WeaponSettings result)
        {
            if (Lookup.TryGetValue(weaponClass, out var getter))
            {
                result = getter(settings);
                return true;
            }
            result = default;
            return false;
        }

        /// <summary>
        /// Check if a weapon class has bypass enabled (used by FriendlyFireCheckPatch).
        /// </summary>
        public static bool IsWeaponEnabled(WeaponClass weaponClass, Settings settings)
        {
            return Lookup.TryGetValue(weaponClass, out var getter) && getter(settings).Enabled;
        }
    }
}
