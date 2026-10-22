using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
        public const string PluginVersion = "1.3.0";

        private const float ScanInterval = 0.5f;
        private const string PinIconItem = "TrophySkeleton";
        private const string PinNameFollowing = "Szkielet";
        private const string PinNameStaying = "Szkielet (czeka)";
        private const string GlowObjectName = "SkeletonTracker_Glow";

        private static readonly AccessTools.FieldRef<Minimap, bool> PinUpdateRequired =
            AccessTools.FieldRefAccess<Minimap, bool>("m_pinUpdateRequired");
        private static readonly AccessTools.FieldRef<Chat, float> ChatHideTimer =
            AccessTools.FieldRefAccess<Chat, float>("m_hideTimer");
        // False while typing in chat/console, in menus, the inventory or the large map.
        private static readonly Func<Player, bool> PlayerTakesInput =
            AccessTools.MethodDelegate<Func<Player, bool>>(AccessTools.Method(typeof(Player), "TakeInput"));

        internal static ConfigEntry<KeyboardShortcut> FollowKey;
        internal static ConfigEntry<KeyboardShortcut> StayKey;
        internal static ConfigEntry<KeyboardShortcut> DismissKey;
        internal static ConfigEntry<KeyboardShortcut> ListKey;
        internal static ConfigEntry<bool> ShowCounter;
        internal static ConfigEntry<bool> ShowDetails;
        internal static ConfigEntry<float> CounterPositionY;
        internal static ConfigEntry<bool> HealthBars;
        internal static ConfigEntry<float> HealthBarDistance;
        internal static ConfigEntry<bool> HealthBarColors;
        internal static ConfigEntry<bool> GlowEnabled;
        internal static ConfigEntry<Color> GlowColor;
        internal static ConfigEntry<float> GlowIntensity;
        internal static ConfigEntry<float> GlowRange;
        internal static ConfigEntry<bool> DeathNotifications;

        // The local player's skeletons as of the last scan; read by the Harmony patches.
        private static readonly HashSet<Character> Owned = new HashSet<Character>();

        private readonly List<Character> _skeletons = new List<Character>();
        private readonly Dictionary<Character, Minimap.PinData> _pins = new Dictionary<Character, Minimap.PinData>();
        private readonly List<Character> _stale = new List<Character>();
        private readonly HashSet<Character> _deathHooked = new HashSet<Character>();
        private readonly SkeletonHud _hud = new SkeletonHud();
        private Harmony _harmony;
        private Minimap _minimap;
        private Sprite _pinIcon;
        private float _timer;

        internal static bool IsOwned(Character character)
        {
            return character != null && Owned.Contains(character);
        }

        private void Awake()
        {
            // Written to BepInEx/config/<PluginGuid>.cfg on first launch.
            const string hotkeyNote = " Gdy celujesz w swojego szkieleta, działa tylko na niego.";
            FollowKey = Config.Bind("Hotkeys", "Follow", new KeyboardShortcut(KeyCode.F, KeyCode.LeftAlt),
                "Szkielety idą za tobą (jak /sk follow)." + hotkeyNote);
            StayKey = Config.Bind("Hotkeys", "Stay", new KeyboardShortcut(KeyCode.G, KeyCode.LeftAlt),
                "Szkielety czekają w miejscu (jak /sk stay)." + hotkeyNote);
            DismissKey = Config.Bind("Hotkeys", "Dismiss", new KeyboardShortcut(KeyCode.H, KeyCode.LeftAlt),
                "Odwołuje szkielety (jak /sk dismiss)." + hotkeyNote);
            ListKey = Config.Bind("Hotkeys", "List", new KeyboardShortcut(KeyCode.J, KeyCode.LeftAlt),
                "Wypisuje listę szkieletów na czacie (jak /sk list).");

            ShowCounter = Config.Bind("HUD", "ShowCounter", true,
                "Pokazuje licznik szkieletów pod minimapą.");
            ShowDetails = Config.Bind("HUD", "ShowDetails", true,
                "Pod licznikiem pokazuje każdego szkieleta: imię, broń, zdrowie, odległość i czy podąża.");
            CounterPositionY = Config.Bind("HUD", "CounterPositionY", 0.3f,
                new ConfigDescription("Pionowa pozycja licznika jako ułamek wysokości ekranu (0 = góra, 1 = dół).",
                    new AcceptableValueRange<float>(0f, 1f)));

            HealthBars = Config.Bind("HealthBars", "AlwaysShow", true,
                "Paski zdrowia nad głowami twoich szkieletów są zawsze widoczne.");
            HealthBarDistance = Config.Bind("HealthBars", "MaxDistance", 40f,
                new ConfigDescription("Maksymalna odległość (m), z której widać paski zdrowia szkieletów.",
                    new AcceptableValueRange<float>(10f, 100f)));
            HealthBarColors = Config.Bind("HealthBars", "ColorByHealth", true,
                "Koloruje paski zdrowia nad szkieletami według HP: zielony > 60%, żółty > 30%, czerwony poniżej.");

            GlowEnabled = Config.Bind("Glow", "Enabled", true,
                "Delikatna poświata wokół twoich szkieletów.");
            GlowColor = Config.Bind("Glow", "Color", new Color(0.4f, 0.85f, 1f, 1f),
                "Kolor poświaty (RGBA w zapisie szesnastkowym).");
            GlowIntensity = Config.Bind("Glow", "Intensity", 1.2f,
                new ConfigDescription("Jasność poświaty.", new AcceptableValueRange<float>(0f, 5f)));
            GlowRange = Config.Bind("Glow", "Range", 3f,
                new ConfigDescription("Zasięg poświaty (m).", new AcceptableValueRange<float>(0.5f, 10f)));

            DeathNotifications = Config.Bind("Notifications", "Death", true,
                "Komunikat na ekranie, gdy któryś z twoich szkieletów zginie.");

            SkeletonCommands.Register();
            _harmony = Harmony.CreateAndPatchAll(typeof(SkeletonPatches), PluginGuid);
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
                Owned.Clear();
                _hud.Hide();
                return;
            }

            SummonedSkeletons.FindOwned(player, _skeletons);
            Owned.Clear();
            Owned.UnionWith(_skeletons);
            _deathHooked.RemoveWhere(c => c == null);

            int following = 0;
            foreach (var skeleton in _skeletons)
            {
                bool isFollowing = SummonedSkeletons.IsFollowing(skeleton, player);
                if (isFollowing)
                    following++;

                UpdatePin(minimap, skeleton, isFollowing);
                UpdateGlow(skeleton);
                if (_deathHooked.Add(skeleton))
                    skeleton.m_onDeath += () => OnSkeletonDied(skeleton);
            }
            RemoveStalePins(minimap);
            PinUpdateRequired(minimap) = true;

            UpdateHud(player, following);
        }

        private void HandleHotkeys()
        {
            var player = Player.m_localPlayer;
            if (player == null || !PlayerTakesInput(player))
                return;

            // Aiming at one of our skeletons narrows the action to just that one.
            var hovered = player.GetHoverCreature();
            var only = IsOwned(hovered) ? hovered : null;

            if (FollowKey.Value.IsDown())
                ShowMessage(player, SkeletonCommands.Follow(player, only));
            else if (StayKey.Value.IsDown())
                ShowMessage(player, SkeletonCommands.Stay(player, only));
            else if (DismissKey.Value.IsDown())
                ShowMessage(player, SkeletonCommands.Dismiss(player, only));
            else if (ListKey.Value.IsDown() && Chat.instance != null)
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

        internal static string GetHoverHints()
        {
            return HoverHint(FollowKey.Value, "Podążaj")
                + HoverHint(StayKey.Value, "Czekaj")
                + HoverHint(DismissKey.Value, "Odwołaj");
        }

        private static string HoverHint(KeyboardShortcut key, string action)
        {
            if (key.MainKey == KeyCode.None)
                return "";
            string keys = string.Join(" + ", key.Modifiers.Concat(new[] { key.MainKey }));
            return $"\n[<color=yellow><b>{keys}</b></color>] {action}";
        }

        private void UpdateHud(Player player, int following)
        {
            if (!ShowCounter.Value || _skeletons.Count == 0)
            {
                _hud.Hide();
                return;
            }

            var text = new StringBuilder();
            text.Append($"Szkielety: {_skeletons.Count}  (podąża {following}, czeka {_skeletons.Count - following})");
            if (ShowDetails.Value)
            {
                foreach (var skeleton in _skeletons)
                    text.Append("\n<size=80%>").Append(SummonedSkeletons.Describe(skeleton, player)).Append("</size>");
            }
            _hud.Show(text.ToString(), CounterPositionY.Value);
        }

        private void OnSkeletonDied(Character skeleton)
        {
            var player = Player.m_localPlayer;
            if (!DeathNotifications.Value || player == null)
                return;
            string name = SummonedSkeletons.GetName(skeleton);
            player.Message(MessageHud.MessageType.TopLeft, $"{name} ({SummonedSkeletons.GetWeaponName(skeleton)}) zginął",
                0, GetPinIcon());
        }

        private void UpdatePin(Minimap minimap, Character skeleton, bool following)
        {
            var pos = skeleton.transform.position;
            string name = following ? PinNameFollowing : PinNameStaying;
            if (_pins.TryGetValue(skeleton, out var pin))
            {
                if (pin.m_name == name)
                {
                    pin.m_pos = pos;
                    return;
                }
                // The pin label is created once with the marker, so a state change needs a new pin.
                minimap.RemovePin(pin);
            }

            pin = minimap.AddPin(pos, Minimap.PinType.Icon3, name, save: false, isChecked: false);
            var icon = GetPinIcon();
            if (icon != null)
                pin.m_icon = icon;
            _pins[skeleton] = pin;
        }

        private void RemoveStalePins(Minimap minimap)
        {
            _stale.Clear();
            foreach (var entry in _pins)
            {
                if (!Owned.Contains(entry.Key))
                    _stale.Add(entry.Key);
            }
            foreach (var skeleton in _stale)
            {
                minimap.RemovePin(_pins[skeleton]);
                _pins.Remove(skeleton);
            }
        }

        private static void UpdateGlow(Character skeleton)
        {
            var glow = skeleton.transform.Find(GlowObjectName);
            if (!GlowEnabled.Value)
            {
                if (glow != null)
                    Destroy(glow.gameObject);
                return;
            }

            if (glow == null)
            {
                glow = new GameObject(GlowObjectName).transform;
                glow.SetParent(skeleton.transform, worldPositionStays: false);
                glow.localPosition = Vector3.up * 1.2f;
                var newLight = glow.gameObject.AddComponent<Light>();
                newLight.type = LightType.Point;
                newLight.shadows = LightShadows.None;
            }

            // Applied every scan so config changes take effect immediately.
            var light = glow.GetComponent<Light>();
            light.color = GlowColor.Value;
            light.intensity = GlowIntensity.Value;
            light.range = GlowRange.Value;
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

        private void OnDestroy()
        {
            RemoveAllPins();
            _hud.Destroy();
            foreach (var skeleton in Owned)
            {
                var glow = skeleton != null ? skeleton.transform.Find(GlowObjectName) : null;
                if (glow != null)
                    Destroy(glow.gameObject);
            }
            Owned.Clear();
            _harmony?.UnpatchSelf();
        }
    }
}
