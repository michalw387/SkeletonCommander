using System;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace ValheimSkeletonTracker
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class SkeletonTrackerPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "michal.valheim.skeletontracker";
        public const string PluginName = "Skeleton Tracker";
        public const string PluginVersion = "1.2.0";

        private const float ScanInterval = 0.5f;
        private const string PinIconItem = "TrophySkeleton";
        private const string PinNameFollowing = "Szkielet";
        private const string PinNameStaying = "Szkielet (czeka)";

        private static readonly AccessTools.FieldRef<Minimap, bool> PinUpdateRequired =
            AccessTools.FieldRefAccess<Minimap, bool>("m_pinUpdateRequired");
        private static readonly AccessTools.FieldRef<Chat, float> ChatHideTimer =
            AccessTools.FieldRefAccess<Chat, float>("m_hideTimer");
        // False while typing in chat/console, in menus, the inventory or the large map.
        private static readonly Func<Player, bool> PlayerTakesInput =
            AccessTools.MethodDelegate<Func<Player, bool>>(AccessTools.Method(typeof(Player), "TakeInput"));

        private ConfigEntry<KeyboardShortcut> _followKey;
        private ConfigEntry<KeyboardShortcut> _stayKey;
        private ConfigEntry<KeyboardShortcut> _dismissKey;
        private ConfigEntry<KeyboardShortcut> _listKey;
        private ConfigEntry<bool> _showCounter;
        private ConfigEntry<float> _counterPositionY;

        private readonly List<Character> _skeletons = new List<Character>();
        private readonly Dictionary<Character, Minimap.PinData> _pins = new Dictionary<Character, Minimap.PinData>();
        private readonly List<Character> _stale = new List<Character>();
        private Minimap _minimap;
        private Sprite _pinIcon;
        private int _followingCount;
        private float _timer;
        private GUIStyle _hudStyle;

        private void Awake()
        {
            // Written to BepInEx/config/<PluginGuid>.cfg on first launch.
            _followKey = Config.Bind("Hotkeys", "Follow", new KeyboardShortcut(KeyCode.F, KeyCode.LeftAlt),
                "Wszystkie szkielety idą za tobą (jak /sk follow).");
            _stayKey = Config.Bind("Hotkeys", "Stay", new KeyboardShortcut(KeyCode.G, KeyCode.LeftAlt),
                "Wszystkie szkielety czekają w miejscu (jak /sk stay).");
            _dismissKey = Config.Bind("Hotkeys", "Dismiss", new KeyboardShortcut(KeyCode.H, KeyCode.LeftAlt),
                "Odwołuje wszystkie szkielety (jak /sk dismiss).");
            _listKey = Config.Bind("Hotkeys", "List", new KeyboardShortcut(KeyCode.J, KeyCode.LeftAlt),
                "Wypisuje listę szkieletów na czacie (jak /sk list).");
            _showCounter = Config.Bind("HUD", "ShowCounter", true,
                "Pokazuje licznik szkieletów pod minimapą.");
            _counterPositionY = Config.Bind("HUD", "CounterPositionY", 0.3f,
                new ConfigDescription("Pionowa pozycja licznika jako ułamek wysokości ekranu (0 = góra, 1 = dół).",
                    new AcceptableValueRange<float>(0f, 1f)));

            SkeletonCommands.Register();
        }

        private void Update()
        {
            HandleHotkeys();

            _timer += Time.deltaTime;
            if (_timer < ScanInterval)
                return;
            _timer = 0f;

            var minimap = Minimap.instance;
            var player = Player.m_localPlayer;
            if (minimap != _minimap)
            {
                // New world/session: old pins went away with the previous Minimap.
                RemoveAllPins();
                _minimap = minimap;
            }
            if (minimap == null || player == null)
            {
                RemoveAllPins();
                _skeletons.Clear();
                _followingCount = 0;
                return;
            }

            SummonedSkeletons.FindOwned(player, _skeletons);

            _followingCount = 0;
            foreach (var skeleton in _skeletons)
            {
                bool following = SummonedSkeletons.IsFollowing(skeleton, player);
                if (following)
                    _followingCount++;

                var pos = skeleton.transform.position;
                string name = following ? PinNameFollowing : PinNameStaying;
                if (_pins.TryGetValue(skeleton, out var pin))
                {
                    if (pin.m_name == name)
                    {
                        pin.m_pos = pos;
                        continue;
                    }
                    // The pin label is created once with the marker, so a state change needs a new pin.
                    minimap.RemovePin(pin);
                }
                _pins[skeleton] = AddPin(minimap, pos, name);
            }

            _stale.Clear();
            foreach (var entry in _pins)
            {
                if (!_skeletons.Contains(entry.Key))
                    _stale.Add(entry.Key);
            }
            foreach (var skeleton in _stale)
            {
                minimap.RemovePin(_pins[skeleton]);
                _pins.Remove(skeleton);
            }

            PinUpdateRequired(minimap) = true;
        }

        private void HandleHotkeys()
        {
            var player = Player.m_localPlayer;
            if (player == null || !PlayerTakesInput(player))
                return;

            if (_followKey.Value.IsDown())
                ShowMessage(player, SkeletonCommands.Follow(player));
            else if (_stayKey.Value.IsDown())
                ShowMessage(player, SkeletonCommands.Stay(player));
            else if (_dismissKey.Value.IsDown())
                ShowMessage(player, SkeletonCommands.Dismiss(player));
            else if (_listKey.Value.IsDown() && Chat.instance != null)
            {
                SkeletonCommands.List(Chat.instance, player);
                // The chat window hides itself until opened; reset its timer so the list is visible.
                ChatHideTimer(Chat.instance) = 0f;
            }
        }

        private static void ShowMessage(Player player, string text)
        {
            player.Message(MessageHud.MessageType.Center, text);
        }

        private void OnGUI()
        {
            if (!_showCounter.Value || _skeletons.Count == 0 || Hud.instance == null || Hud.IsUserHidden())
                return;

            if (_hudStyle == null)
            {
                _hudStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.UpperRight,
                    fontSize = 18,
                    fontStyle = FontStyle.Bold
                };
            }

            int staying = _skeletons.Count - _followingCount;
            string text = $"Szkielety: {_skeletons.Count}  (podąża {_followingCount}, czeka {staying})";
            // Just below the small minimap in the top-right corner.
            var rect = new Rect(Screen.width - 420, Screen.height * _counterPositionY.Value, 400, 30);

            _hudStyle.normal.textColor = Color.black;
            GUI.Label(new Rect(rect.x + 1, rect.y + 1, rect.width, rect.height), text, _hudStyle);
            _hudStyle.normal.textColor = Color.white;
            GUI.Label(rect, text, _hudStyle);
        }

        private void OnDestroy()
        {
            RemoveAllPins();
        }

        private Minimap.PinData AddPin(Minimap minimap, Vector3 pos, string name)
        {
            var pin = minimap.AddPin(pos, Minimap.PinType.Icon3, name, save: false, isChecked: false);
            var icon = GetPinIcon();
            if (icon != null)
                pin.m_icon = icon;
            return pin;
        }

        // The skeleton trophy icon makes the pins easy to tell apart from regular map markers.
        private Sprite GetPinIcon()
        {
            if (_pinIcon == null && ObjectDB.instance != null)
            {
                var prefab = ObjectDB.instance.GetItemPrefab(PinIconItem);
                var itemDrop = prefab != null ? prefab.GetComponent<ItemDrop>() : null;
                if (itemDrop != null)
                    _pinIcon = itemDrop.m_itemData.GetIcon();
            }
            return _pinIcon;
        }

        private void RemoveAllPins()
        {
            if (_minimap != null)
            {
                foreach (var pin in _pins.Values)
                    _minimap.RemovePin(pin);
            }
            _pins.Clear();
        }
    }
}
