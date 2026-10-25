using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace SkeletonCommander
{
    internal enum HudStyle
    {
        Bars,
        Text
    }

    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class SkeletonCommanderPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "xmordax.valheim.skeletoncommander";
        public const string PluginName = "Skeleton Commander";
        public const string PluginVersion = "1.9.0";
        // Config files of earlier GUIDs (the mod was called Skeleton Tracker before), newest first;
        // the first one found is taken over on first launch.
        private static readonly string[] OldConfigFileNames =
        {
            "michal.valheim.skeletoncommander.cfg",
            "michal.valheim.skeletontracker.cfg"
        };

        private const float ScanInterval = 0.5f;
        private const string PinIconItem = "TrophySkeleton";
        private const string PinNameFollowing = "Skeleton";
        private const string PinNameStaying = "Skeleton (staying)";
        private const string GlowObjectName = "SkeletonCommander_Glow";

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
        internal static ConfigEntry<bool> ShowStateSummary;
        internal static ConfigEntry<bool> ShowTeam;
        internal static ConfigEntry<bool> ShowName;
        internal static ConfigEntry<bool> ShowWeapon;
        internal static ConfigEntry<bool> ShowHealth;
        internal static ConfigEntry<bool> ShowDistance;
        internal static ConfigEntry<bool> ShowState;
        internal static ConfigEntry<bool> ShowShield;
        internal static ConfigEntry<float> CounterPositionY;
        internal static ConfigEntry<float> HudFontSize;
        internal static ConfigEntry<HudStyle> Style;
        internal static ConfigEntry<bool> HealthBars;
        internal static ConfigEntry<float> HealthBarDistance;
        internal static ConfigEntry<bool> HealthBarColors;
        internal static ConfigEntry<bool> GlowEnabled;
        internal static ConfigEntry<Color> GlowColor;
        internal static ConfigEntry<float> GlowIntensity;
        internal static ConfigEntry<float> GlowRange;
        internal static ConfigEntry<bool> DeathNotifications;
        internal static ConfigEntry<bool> TeamEnabled;
        internal static ConfigEntry<TeamSlot>[] TeamSlots;
        internal static ConfigEntry<bool> DismissWeakest;
        internal static ConfigEntry<float> DismissBeyondDistance;
        internal static ConfigEntry<bool> KeepNearOtherPlayers;

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

        internal static BepInEx.Logging.ManualLogSource Log;

        private void Awake()
        {
            Log = Logger;
            MigrateOldConfigFile();

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

            TeamEnabled = Config.Bind("Summon", "TeamEnabled", true,
                "Gives new summons the weapons set by TeamSlot1-4. Off: skeletons get random weapons as in the base game.");
            // Each new summon gets the weapon its team is missing. The summon limit is 1-4 depending on the
            // Dead Raiser's upgrade level; a limit of N uses slots 1..N.
            var slotDefaults = new[] { TeamSlot.SwordAndShield, TeamSlot.Bow, TeamSlot.SwordAndShield, TeamSlot.Bow };
            TeamSlots = slotDefaults.Select((value, i) => Config.Bind("Summon", $"TeamSlot{i + 1}", value,
                $"Weapons of team member {i + 1}, used from a summon limit of {i + 1} (Dead Raiser upgrade level). " +
                "New summons get the weapon the team is missing; Random accepts any weapon. " +
                "Already summoned skeletons keep their weapons.")).ToArray();
            DismissWeakest = Config.Bind("Summon", "DismissWeakest", true,
                "Over the summon limit, the skeleton with the least HP is dismissed (the oldest on a tie), so a new summon " +
                "replaces it with the weapon its team slot needs. Off: the oldest is dismissed as in the base game.");
            DismissBeyondDistance = Config.Bind("Summon", "DismissBeyondDistance", 100f,
                new ConfigDescription("Skeletons farther away than this (in meters) from you, and from other players with KeepNearOtherPlayers, are " +
                    "dismissed, also when you teleport " +
                    "or die far from them. Skeletons left in an unloaded area would otherwise not count toward the summon limit. " +
                    "Skeletons leaving the area the game simulates around you (which depends on the Simulation distance " +
                    "graphics setting) are dismissed even when closer.",
                    new AcceptableValueRange<float>(20f, 120f)));
            KeepNearOtherPlayers = Config.Bind("Summon", "KeepNearOtherPlayers", true,
                "Far skeletons are kept while another player is near them, so they keep fighting alongside that player " +
                "(e.g. after you die). They don't count toward your summon limit while you are away; when you come back " +
                "to more skeletons than your limit, the extra ones are dismissed. Off: far skeletons are dismissed even " +
                "when other players are near them.");

            DeathNotifications = Config.Bind("Notifications", "Death", true,
                "On-screen message when one of your skeletons dies.");

            ShowCounter = Config.Bind("HUD", "ShowCounter", true,
                "Shows the skeleton counter below the minimap.");
            ShowDetails = Config.Bind("HUD", "ShowDetails", true,
                "Lists each skeleton below the counter: name, weapon, health, distance and whether it follows you.");
            ShowStateSummary = Config.Bind("HUD", "ShowStateSummary", true,
                "Shows how many skeletons are following you and how many are staying next to the counter.");
            ShowTeam = Config.Bind("HUD", "ShowTeam", true,
                "Shows how many skeletons of each type your team (Summon.TeamSlot1-4) has and wants.");
            ShowName = Config.Bind("HUD", "ShowName", true, "Shows each skeleton's name in the detailed list.");
            ShowWeapon = Config.Bind("HUD", "ShowWeapon", true, "Shows each skeleton's weapon in the detailed list.");
            ShowHealth = Config.Bind("HUD", "ShowHealth", true, "Shows each skeleton's health in the detailed list.");
            ShowDistance = Config.Bind("HUD", "ShowDistance", true, "Shows each skeleton's distance from you in the detailed list.");
            ShowState = Config.Bind("HUD", "ShowState", true,
                "Shows whether each skeleton is following you or staying in the detailed list.");
            ShowShield = Config.Bind("HUD", "ShowShield", true,
                "Shows the barrier from the Staff of Protection (how much is left and for how long) in the detailed list.");
            CounterPositionY = Config.Bind("HUD", "CounterPositionY", 0.3f,
                new ConfigDescription("Vertical counter position as a fraction of screen height (0 = top, 1 = bottom).",
                    new AcceptableValueRange<float>(0f, 1f)));
            HudFontSize = Config.Bind("HUD", "FontSize", 18f,
                new ConfigDescription("Font size of the skeleton counter and list; the whole box scales with it.",
                    new AcceptableValueRange<float>(10f, 40f)));
            Style = Config.Bind("HUD", "Style", HudStyle.Bars,
                "How the detailed list shows each skeleton: Bars (a panel with an HP bar per skeleton) or Text (one line each).");

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
            // While teleporting the player is still at the old place; TeleportTo already checked the destination.
            if (!player.IsTeleporting())
                DismissFarSkeletons(player, player.transform.position, _skeletons);
            DismissOverLimit(player, _skeletons);
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

            if (IsPressed(FollowKey.Value))
                ShowMessage(player, SkeletonCommands.Follow(player, only));
            else if (IsPressed(StayKey.Value))
                ShowMessage(player, SkeletonCommands.Stay(player, only));
            else if (IsPressed(DismissKey.Value))
                ShowMessage(player, SkeletonCommands.Dismiss(player, only));
            else if (IsPressed(ListKey.Value) && Chat.instance != null)
            {
                SkeletonCommands.List(Chat.instance, player);
                // The chat window hides itself until opened; reset its timer so the list is visible.
                ChatHideTimer(Chat.instance) = 0f;
            }
        }

        // KeyboardShortcut.IsDown() fails while any other key is held (e.g. W while running),
        // so only the shortcut's own keys are checked.
        private static bool IsPressed(KeyboardShortcut shortcut)
        {
            return shortcut.MainKey != KeyCode.None
                && UnityInput.Current.GetKeyDown(shortcut.MainKey)
                && shortcut.Modifiers.All(UnityInput.Current.GetKey);
        }

        private void MigrateOldConfigFile()
        {
            if (File.Exists(Config.ConfigFilePath))
                return;
            foreach (string fileName in OldConfigFileNames)
            {
                string oldPath = Path.Combine(Paths.ConfigPath, fileName);
                if (!File.Exists(oldPath))
                    continue;
                File.Move(oldPath, Config.ConfigFilePath);
                Config.Reload();
                return;
            }
        }

        private static void ShowMessage(Player player, string text)
        {
            player.Message(MessageHud.MessageType.Center, text);
        }

        internal static IEnumerable<ConfigEntry<KeyboardShortcut>> Hotkeys => new[]
        {
            FollowKey, StayKey, DismissKey, ListKey
        };

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
            text.Append($"<align=center>Skeletons: {_skeletons.Count}");
            if (ShowStateSummary.Value)
                text.Append($"  ({following} following, {_skeletons.Count - following} staying)");
            text.Append("</align>");
            if (ShowTeam.Value && TeamEnabled.Value)
                text.Append($"\n<align=center><size=80%>{SkeletonLoadouts.DescribeTeam(_skeletons)}</size></align>");
            List<SkeletonFrameData> frames = null;
            if (ShowDetails.Value && Style.Value == HudStyle.Bars)
                frames = _skeletons.Select((skeleton, i) => DescribeFrame(skeleton, i, player)).ToList();
            else if (ShowDetails.Value)
            {
                // Same numbering as the chat list (/sk list).
                for (int i = 0; i < _skeletons.Count; i++)
                    text.Append($"\n<size=80%>#{i + 1}  ").Append(SummonedSkeletons.Describe(_skeletons[i], player,
                        ShowName.Value, ShowWeapon.Value, ShowHealth.Value, ShowDistance.Value, ShowState.Value,
                        shield: ShowShield.Value)).Append("</size>");
            }
            _hud.Show(text.ToString(), CounterPositionY.Value, HudFontSize.Value, frames);
        }

        // Header: number and name (gold), then the other enabled parts; the HP bar replaces the HP text.
        private static SkeletonFrameData DescribeFrame(Character skeleton, int index, Player player)
        {
            string name = ShowName.Value ? $" {SummonedSkeletons.GetName(skeleton)}" : "";
            string details = SummonedSkeletons.Describe(skeleton, player, name: false, weapon: ShowWeapon.Value,
                health: false, distance: ShowDistance.Value, state: ShowState.Value, padded: false, shield: false);
            bool hasShield = SummonedSkeletons.TryGetShield(skeleton, out float shield, out float maxShield, out float shieldSeconds);
            return new SkeletonFrameData
            {
                Header = $"<color=#FFC74D>#{index + 1}{name}</color>  <size=90%>{details}</size>",
                ShowBar = ShowHealth.Value,
                Health = skeleton.GetHealth(),
                MaxHealth = skeleton.GetMaxHealth(),
                BarColor = SummonedSkeletons.GetHealthColor(skeleton),
                ShowShield = ShowShield.Value && hasShield,
                Shield = shield,
                MaxShield = maxShield,
                ShieldSeconds = shieldSeconds
            };
        }

        // A skeleton in an area that unloads keeps existing but is no longer found, so it would not count
        // toward the summon limit. Dismiss skeletons too far from where the player is or is about to be
        // while they are still loaded. Dismissed ones are removed from the list.
        internal static void DismissFarSkeletons(Player player, Vector3 center, List<Character> skeletons)
        {
            float maxDistance = DismissBeyondDistance.Value;
            int dismissed = skeletons.RemoveAll(skeleton =>
            {
                var position = skeleton.transform.position;
                if (IsNear(position, center, maxDistance))
                    return false;
                // The server hands a skeleton to a player it is near, whose game then keeps simulating it.
                var nview = skeleton.GetComponent<ZNetView>();
                if (KeepNearOtherPlayers.Value && ((nview.GetZDO().HasOwner() && !nview.IsOwner())
                    || Player.GetAllPlayers().Any(other => other != player && IsNear(position, other.transform.position, maxDistance))))
                    return false;
                float distance = SummonedSkeletons.HorizontalDistance(position, center);
                Log.LogInfo($"Dismissing {SummonedSkeletons.GetName(skeleton)} at {distance:0} m " +
                    $"(owner: {(nview.IsOwner() ? "you" : nview.GetZDO().HasOwner() ? "another player" : "none")})");
                SummonedSkeletons.Dismiss(skeleton);
                return true;
            });
            if (dismissed > 0)
                player.Message(MessageHud.MessageType.TopLeft, dismissed == 1
                    ? "Skeleton too far away - dismissed"
                    : $"{dismissed} skeletons too far away - dismissed");
        }

        // Outside the active area of a player that player gives up the skeleton, and the area may unload soon after.
        private static bool IsNear(Vector3 position, Vector3 center, float maxDistance)
        {
            return SummonedSkeletons.HorizontalDistance(position, center) <= maxDistance
                && ZNetScene.InActiveArea(position, center);
        }

        // Skeletons left in an unloaded area don't count when a new one is summoned, so coming back to
        // them can put the player above the limit. The newest skeleton carries the current limit.
        private static void DismissOverLimit(Player player, List<Character> skeletons)
        {
            if (skeletons.Count == 0)
                return;
            int maxInstances = SummonedSkeletons.GetMaxInstances(skeletons[0]);
            if (maxInstances <= 0)
                return;
            int dismissed = SummonedSkeletons.DismissExcess(skeletons, maxInstances);
            if (dismissed > 0)
                player.Message(MessageHud.MessageType.TopLeft, dismissed == 1
                    ? "Skeleton over the summon limit - dismissed"
                    : $"{dismissed} skeletons over the summon limit - dismissed");
        }

        internal static void DismissFarSkeletons(Player player, Vector3 center)
        {
            var skeletons = new List<Character>();
            SummonedSkeletons.FindOwned(player, skeletons);
            DismissFarSkeletons(player, center, skeletons);
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
