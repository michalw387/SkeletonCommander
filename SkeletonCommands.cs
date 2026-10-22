using System.Collections.Generic;
using UnityEngine;

namespace ValheimSkeletonTracker
{
    // Skeleton actions shared by the chat/console command (/sk [list|follow|stay|dismiss]) and the hotkeys.
    internal static class SkeletonCommands
    {
        private static readonly List<string> Subcommands = new List<string> { "list", "follow", "stay", "dismiss" };

        public static void Register()
        {
            new Terminal.ConsoleCommand("sk",
                "[list|follow|stay|dismiss] - zarządzanie przyzwanymi szkieletami (Dead Raiser)",
                Run, optionsFetcher: () => Subcommands);
        }

        private static void Run(Terminal.ConsoleEventArgs args)
        {
            var player = Player.m_localPlayer;
            if (player == null)
            {
                args.Context.AddString("Brak gracza w świecie.");
                return;
            }

            string sub = args.Length > 1 ? args[1].ToLowerInvariant() : "list";
            switch (sub)
            {
                case "list":
                    List(args.Context, player);
                    break;
                case "follow":
                    args.Context.AddString(Follow(player));
                    break;
                case "stay":
                    args.Context.AddString(Stay(player));
                    break;
                case "dismiss":
                    args.Context.AddString(Dismiss(player));
                    break;
                default:
                    args.Context.AddString("Użycie: sk [list|follow|stay|dismiss]");
                    break;
            }
        }

        public static string Follow(Player player)
        {
            var skeletons = FindOwned(player);
            foreach (var skeleton in skeletons)
                SummonedSkeletons.SetFollowing(skeleton, player, follow: true);
            return $"Szkielety idą za tobą: {skeletons.Count}";
        }

        public static string Stay(Player player)
        {
            var skeletons = FindOwned(player);
            foreach (var skeleton in skeletons)
                SummonedSkeletons.SetFollowing(skeleton, player, follow: false);
            return $"Szkielety czekają w miejscu: {skeletons.Count}";
        }

        public static string Dismiss(Player player)
        {
            var skeletons = FindOwned(player);
            foreach (var skeleton in skeletons)
                SummonedSkeletons.Dismiss(skeleton);
            return $"Odwołano szkielety: {skeletons.Count}";
        }

        public static void List(Terminal terminal, Player player)
        {
            var skeletons = FindOwned(player);
            terminal.AddString($"Twoje szkielety: {skeletons.Count}");
            var playerPos = player.transform.position;
            for (int i = 0; i < skeletons.Count; i++)
            {
                var skeleton = skeletons[i];
                float distance = Vector3.Distance(playerPos, skeleton.transform.position);
                string state = SummonedSkeletons.IsFollowing(skeleton, player) ? "podąża" : "czeka";
                terminal.AddString($"  #{i + 1}  HP {skeleton.GetHealth():0}/{skeleton.GetMaxHealth():0}  {distance:0} m  {state}");
            }
        }

        private static List<Character> FindOwned(Player player)
        {
            var skeletons = new List<Character>();
            SummonedSkeletons.FindOwned(player, skeletons);
            return skeletons;
        }
    }
}
