using System;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using JetBrains.Annotations;
using UnityEngine;

namespace HideUI
{
    [BepInPlugin(ModGUID, ModName, ModVersion)]
    public class HideUIPlugin : BaseUnityPlugin
    {
        internal const string ModName = "HideUI";
        internal const string ModVersion = "1.0.0";
        internal const string Author = "Azumatt";
        private const string ModGUID = Author + "." + ModName;
        private static string ConfigFileName = ModGUID + ".cfg";
        private static string ConfigFileFullPath = Paths.ConfigPath + Path.DirectorySeparatorChar + ConfigFileName;
        private readonly Harmony _harmony = new(ModGUID);
        public static readonly ManualLogSource HideUILogger = BepInEx.Logging.Logger.CreateLogSource(ModName);


        public enum Toggle
        {
            On = 1,
            Off = 0
        }

        public void Awake()
        {
            hideUiHotkey = Config.Bind("1 - General", "Hide UI Hotkey", new KeyboardShortcut(KeyCode.H, KeyCode.LeftControl), new ConfigDescription("Hotkey to toggle on and off the UI.", null, new AcceptableShortcuts()));
            shouldHideUI = Config.Bind("1 - General", "Hide UI", Toggle.Off, "If on, the UI will be hidden. If off, the UI will be shown. The hotkey automatically toggles this value.");

            Assembly assembly = Assembly.GetExecutingAssembly();
            _harmony.PatchAll(assembly);
            SetupWatcher();
        }

        private void Update()
        {
            if (!Global.code || Global.code.uiGameMenu == null || Global.code.uiCombat == null) return;
            if (hideUiHotkey.Value.IsKeyDown())
            {
                shouldHideUI.Value = shouldHideUI.Value == Toggle.On ? Toggle.Off : Toggle.On;
            }

            if (shouldHideUI.Value == Toggle.On && !Global.code.uiGameMenu.hideUI)
            {
                HideUI();
            }
        }

        public void HideUI()
        {
            Global.code.uiCombat.gameObject.SetActive(Global.code.uiGameMenu.hideUI);
            Global.code.uiGameMenu.hideUI = !Global.code.uiGameMenu.hideUI;
        }

        private void OnDestroy()
        {
            Config.Save();
        }

        private void SetupWatcher()
        {
            FileSystemWatcher watcher = new(Paths.ConfigPath, ConfigFileName);
            watcher.Changed += ReadConfigValues;
            watcher.Created += ReadConfigValues;
            watcher.Renamed += ReadConfigValues;
            watcher.IncludeSubdirectories = true;
            watcher.SynchronizingObject = ThreadingHelper.SynchronizingObject;
            watcher.EnableRaisingEvents = true;
        }

        private void ReadConfigValues(object sender, FileSystemEventArgs e)
        {
            if (!File.Exists(ConfigFileFullPath)) return;
            try
            {
                HideUILogger.LogDebug("ReadConfigValues called");
                Config.Reload();
            }
            catch
            {
                HideUILogger.LogError($"There was an issue loading your {ConfigFileName}");
                HideUILogger.LogError("Please check your config entries for spelling and format!");
            }
        }


        #region ConfigOptions

        private static ConfigEntry<KeyboardShortcut> hideUiHotkey = null!;
        private static ConfigEntry<Toggle> shouldHideUI = null!;

        private class ConfigurationManagerAttributes
        {
            [UsedImplicitly] public int? Order = null!;
            [UsedImplicitly] public bool? Browsable = null!;
            [UsedImplicitly] public string? Category = null!;
            [UsedImplicitly] public Action<ConfigEntryBase>? CustomDrawer = null!;
        }

        internal ConfigEntry<T> TextEntryConfig<T>(string group, string name, T value, string desc)
        {
            ConfigurationManagerAttributes attributes = new()
            {
                CustomDrawer = TextAreaDrawer
            };
            return Config.Bind(group, name, value, new ConfigDescription(desc, null, attributes));
        }

        internal static void TextAreaDrawer(ConfigEntryBase entry)
        {
            GUILayout.ExpandHeight(true);
            GUILayout.ExpandWidth(true);
            entry.BoxedValue = GUILayout.TextArea((string)entry.BoxedValue, GUILayout.ExpandWidth(true),
                GUILayout.ExpandHeight(true));
        }

        class AcceptableShortcuts : AcceptableValueBase
        {
            public AcceptableShortcuts() : base(typeof(KeyboardShortcut))
            {
            }

            public override object Clamp(object value) => value;
            public override bool IsValid(object value) => true;

            public override string ToDescriptionString() =>
                "# Acceptable values: " + string.Join(", ", UnityInput.Current.SupportedKeyCodes);
        }

        #endregion
    }

    public static class KeyboardExtensions
    {
        public static bool IsKeyDown(this KeyboardShortcut shortcut)
        {
            return shortcut.MainKey != KeyCode.None && Input.GetKeyDown(shortcut.MainKey) &&
                   shortcut.Modifiers.All(Input.GetKey);
        }

        public static bool IsKeyHeld(this KeyboardShortcut shortcut)
        {
            return shortcut.MainKey != KeyCode.None && Input.GetKey(shortcut.MainKey) &&
                   shortcut.Modifiers.All(Input.GetKey);
        }
    }
}