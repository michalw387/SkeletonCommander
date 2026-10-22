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
        public const string PluginVersion = "1.6.0";

        private const float ScanInterval = 0.5f;
        private const string PinIconItem = "TrophySkeleton";
        private const string PinNameFollowing = "Skeleton";
        private const string PinNameStaying = "Skeleton (staying)";
        private const string GlowObjectName = "SkeletonTracker_Glow";

        private static readonly AccessTools.FieldRef<Minimap, bool> PinUpdateRequired =
            AccessTools.FieldRefAccess<Minimap, bool>("m_pinUpdateRequired");
        private static readonly Func<ConfigFile, Dictionary<ConfigDefinition, string>> ConfigOrphanedEntries =
            AccessTools.MethodDelegate<Func<ConfigFile, Dictionary<ConfigDefinition, string>>>(
                AccessTools.PropertyGetter(typeof(ConfigFile), "OrphanedEntries"));
        private static readonly AccessTools.FieldRef<Chat, float> ChatHideTimer =
            AccessTools.FieldRefAccess<Chat, float>("m_hideTimer");
        // False while typing in chat/console, in menus, the inventory or the large map.
        private static readonly Func<Player, bool> PlayerTakesInput =
            AccessTools.MethodDelegate<Func<Player, bool>>(AccessTools.Method(typeof(Player), "TakeInput"));

        internal static ConfigEntry<KeyboardShortcut> FollowKey;
        internal static ConfigEntry<KeyboardShortcut> StayKey;
        internal static ConfigEntry<KeyboardShortcut> DismissKey;
        internal static ConfigEntry<KeyboardShortcut> ListKey;
        internal static ConfigEntry<KeyboardShortcut> CycleNextSummonLoadoutKey;
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
        internal static ConfigEntry<SkeletonLoadout> NextSummonLoadout;

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
            // Written to BepInEx/config/<PluginGuid>.cfg on first launch. ConfigurationManager lists
            // sections in the order they are first bound here.
            const string hotkeyNote = " When aiming at one of your skeletons, affects only that one.";
            FollowKey = Config.Bind("Hotkeys", "Follow", new KeyboardShortcut(KeyCode.F, KeyCode.LeftAlt),
                "Skeletons follow you (like /sk follow)." + hotkeyNote);
            StayKey = Config.Bind("Hotkeys", "Stay", new KeyboardShortcut(KeyCode.G, KeyCode.LeftAlt),
                "Skeletons stay in place (like /sk stay)." + hotkeyNote);
            DismissKey = Config.Bind("Hotkeys", "Dismiss", new KeyboardShortcut(KeyCode.H, KeyCode.LeftAlt),
                "Dismisses skeletons (like /sk dismiss)." + hotkeyNote);
            ListKey = Config.Bind("Hotkeys", "List", new KeyboardShortcut(KeyCode.J, KeyCode.LeftAlt),
                "Lists your skeletons in chat (like /sk list).");
            CycleNextSummonLoadoutKey = Config.Bind("Hotkeys", "CycleNextSummonLoadout", new KeyboardShortcut(KeyCode.K, KeyCode.LeftAlt),
                "Switches the weapons of your next summoned skeletons (Summon.NextSummonLoadout): " +
                "Random -> SwordAndShield -> Bow.");

            NextSummonLoadout = Config.Bind("Summon", "NextSummonLoadout", SkeletonLoadout.Random,
                "Weapons of the skeletons you summon next: Random (game default), SwordAndShield or Bow. " +
                "Already summoned skeletons keep their weapons.");

            DeathNotifications = Config.Bind("Notifications", "Death", true,
                "On-screen message when one of your skeletons dies.");

            ShowCounter = Config.Bind("HUD", "ShowCounter", true,
                "Shows the skeleton counter below the minimap.");
            ShowDetails = Config.Bind("HUD", "ShowDetails", true,
                "Lists each skeleton below the counter: name, weapon, health, distance and whether it follows you.");
            CounterPositionY = Config.Bind("HUD", "CounterPositionY", 0.3f,
                new ConfigDescription("Vertical counter position as a fraction of screen height (0 = top, 1 = bottom).",
                    new AcceptableValueRange<float>(0f, 1f)));

            HealthBars = Config.Bind("HealthBars", "AlwaysShow", true,
                "Health bars above your skeletons' heads are always visible.");
            HealthBarDistance = Config.Bind("HealthBars", "MaxDistance", 40f,
                new ConfigDescription("Maximum distance (m) at which skeleton health bars are shown.",
                    new AcceptableValueRange<float>(10f, 100f)));
            HealthBarColors = Config.Bind("HealthBars", "ColorByHealth", true,
                "Colors skeleton health bars by HP: green > 60%, yellow > 30%, red below.");

            GlowEnabled = Config.Bind("Glow", "Enabled", true,
                "Subtle glow around your skeletons.");
            GlowColor = Config.Bind("Glow", "Color", new Color(0.4f, 0.85f, 1f, 1f),
                "Glow color (RGBA, hex).");
            GlowIntensity = Config.Bind("Glow", "Intensity", 1.2f,
                new ConfigDescription("Glow brightness.", new AcceptableValueRange<float>(0f, 5f)));
            GlowRange = Config.Bind("Glow", "Range", 3f,
                new ConfigDescription("Glow range (m).", new AcceptableValueRange<float>(0.5f, 10f)));

            // Keys renamed in 1.6.0 to make clear they apply to the next summon.
            MigrateRenamedEntry(CycleNextSummonLoadoutKey, "Hotkeys", "CycleLoadout");
            MigrateRenamedEntry(NextSummonLoadout, "Summon", "Loadout");

            SkeletonCommands.Register();
            _harmony = Harmony.CreateAndPatchAll(typeof(SkeletonPatches), PluginGuid);
        }

        private void Update()
        {
            HandleHotkeys();
            _hud.Update();

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
            else if (CycleNextSummonLoadoutKey.Value.IsDown())
            {
                // Setting the value also saves it to the config file.
                NextSummonLoadout.Value = (SkeletonLoadout)(((int)NextSummonLoadout.Value + 1) % 3);
                _hud.ShowNotice($"Next summon: {DescribeLoadout(NextSummonLoadout.Value)}");
            }
        }

        private static string DescribeLoadout(SkeletonLoadout loadout)
        {
            switch (loadout)
            {
                case SkeletonLoadout.SwordAndShield: return "sword & shield";
                case SkeletonLoadout.Bow: return "bow";
                default: return "random weapon";
            }
        }

        // Carries a value over from a key's old name, then drops the old key from the config file.
        // Values in the file that no Bind call claimed are kept by BepInEx as orphaned entries.
        private void MigrateRenamedEntry(ConfigEntryBase entry, string oldSection, string oldKey)
        {
            var orphaned = ConfigOrphanedEntries(Config);
            var oldDefinition = new ConfigDefinition(oldSection, oldKey);
            if (!orphaned.TryGetValue(oldDefinition, out var value))
                return;
            entry.SetSerializedValue(value);
            orphaned.Remove(oldDefinition);
            Config.Save();
        }

        private static void ShowMessage(Player player, string text)
        {
            player.Message(MessageHud.MessageType.Center, text);
        }

        internal static string GetHoverHints()
        {
            return HoverHint(FollowKey.Value, "Follow")
                + HoverHint(StayKey.Value, "Stay")
                + HoverHint(DismissKey.Value, "Dismiss");
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
            text.Append($"Skeletons: {_skeletons.Count}  ({following} following, {_skeletons.Count - following} staying)");
            if (ShowDetails.Value)
            {
                // Same numbering as the chat list (/sk list).
                for (int i = 0; i < _skeletons.Count; i++)
                    text.Append($"\n<size=80%>#{i + 1}  ").Append(SummonedSkeletons.Describe(_skeletons[i], player)).Append("</size>");
            }
            _hud.Show(text.ToString(), CounterPositionY.Value);
        }

        private void OnSkeletonDied(Character skeleton)
        {
            var player = Player.m_localPlayer;
            if (!DeathNotifications.Value || player == null)
                return;
            string name = SummonedSkeletons.GetName(skeleton);
            player.Message(MessageHud.MessageType.TopLeft, $"{name} ({SummonedSkeletons.GetWeaponName(skeleton)}) has died",
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
