using System;
using System.IO;
using BepInEx;

namespace Gravegoods.Loot
{
    /// <summary>
    /// Where the active loot.txt text comes from.
    ///
    /// Loot is rolled by whichever client owns a chest's ZDO when it first loads, not by
    /// the server, so every client has to roll from the same tables. A client connected to
    /// a remote server therefore ignores its own file and waits for the server's copy;
    /// the server, a listen-server host, and single player use the local file.
    /// </summary>
    internal static class LootTables
    {
        internal const string FileName = "loot.txt";

        internal static string Directory => Path.Combine(Paths.ConfigPath, "Gravegoods");
        internal static string FilePath => Path.Combine(Directory, FileName);

        private static string _localText = "";
        private static string _serverText;

        private static string _parsedFrom;
        private static LootFile _parsed;

        /// <summary>Bumped whenever the active text changes, so cached pools rebuild.</summary>
        internal static int Revision { get; private set; }

        internal static string LocalText => _localText;

        private static bool UsesServerCopy => ZNet.instance != null && !ZNet.instance.IsServer();

        /// <summary>False only while a remote client is still waiting for the server's copy.</summary>
        internal static bool IsReady => !UsesServerCopy || _serverText != null;

        internal static string SourceDescription =>
            !UsesServerCopy ? $"local file ({FilePath})"
            : _serverText != null ? "server copy"
            : "waiting for server copy";

        internal static LootFile Current
        {
            get
            {
                var text = UsesServerCopy ? (_serverText ?? "") : _localText;
                if (_parsed == null || !ReferenceEquals(text, _parsedFrom))
                {
                    _parsed = LootFile.Parse(text);
                    _parsedFrom = text;
                    foreach (var warning in _parsed.Warnings)
                    {
                        GravegoodsPlugin.LogWarning(warning);
                    }
                }
                return _parsed;
            }
        }

        /// <summary>Reads loot.txt, writing the shipped default first if it does not exist.</summary>
        internal static void LoadLocal()
        {
            try
            {
                System.IO.Directory.CreateDirectory(Directory);
                if (!File.Exists(FilePath))
                {
                    File.WriteAllText(FilePath, DefaultLootFile.Text);
                    GravegoodsPlugin.LogInfo($"Wrote default loot tables to {FilePath}.");
                }
                _localText = File.ReadAllText(FilePath);
            }
            catch (Exception e)
            {
                // Fall back to the shipped text rather than rolling from nothing.
                GravegoodsPlugin.LogError($"Could not read {FilePath}, using built-in defaults: {e.Message}");
                _localText = DefaultLootFile.Text;
            }
            Revision++;
        }

        /// <summary>Called on a client as it connects, before any chest can load.</summary>
        internal static void BeginWaitingForServer()
        {
            _serverText = null;
            Revision++;
        }

        internal static void ReceiveServerCopy(string text)
        {
            _serverText = text ?? "";
            Revision++;
            GravegoodsPlugin.LogInfo($"Received loot tables from the server ({_serverText.Length} chars).");
        }
    }
}
