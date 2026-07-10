using System;
using System.IO;
using System.Text;
using MCM.Abstractions.Base.Global;

namespace ProperShieldWalls
{
    /// <summary>
    /// File-backed diagnostic sink.
    ///
    /// Debug.Print is captured by nothing on this machine, and the on-screen InformationManager
    /// feed scrolls away faster than it can be read mid-battle. The one instrument that has to
    /// survive a real fight therefore writes to disk.
    ///
    /// Bounded on purpose: MeleeHitCallback runs on every melee collision in the mission. An
    /// unbounded append here would itself become a per-collision log storm.
    /// </summary>
    internal static class Diagnostics
    {
        private const int MaxLinesPerMission = 400;

        private static string _path;
        private static int _lines;
        private static bool _capAnnounced;
        private static bool _sinkBroken;

        /// <summary>Successful AI swing-to-overhead rewrites this mission. A remap is otherwise silent.</summary>
        internal static int RemapCount;

        internal static bool Enabled
        {
            get
            {
                var settings = GlobalSettings<Settings>.Instance;
                return settings != null && settings.DiagnosticLogging;
            }
        }

        internal static void Reset()
        {
            _lines = 0;
            _capAnnounced = false;
            RemapCount = 0;
        }

        internal static void Write(string line)
        {
            if (_sinkBroken) return;

            if (_lines >= MaxLinesPerMission)
            {
                if (_capAnnounced) return;
                _capAnnounced = true;
                line = string.Format(
                    "[PSW] diagnostic cap reached ({0} lines); silent until next mission.", MaxLinesPerMission);
            }
            _lines++;

            try
            {
                if (_path == null)
                {
                    string dir = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                        "Mount and Blade II Bannerlord");
                    Directory.CreateDirectory(dir);
                    _path = Path.Combine(dir, "PSW_diag.log");
                }

                File.AppendAllText(_path, line + Environment.NewLine, Encoding.UTF8);
            }
            catch
            {
                // A diagnostic must never take the game down. Latch off rather than retry
                // once per collision for the rest of the mission.
                _sinkBroken = true;
            }
        }
    }
}
