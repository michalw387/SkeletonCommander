using System.Collections.Generic;

namespace ValheimSkeletonTracker
{
    // Skeleton actions shared by the chat/console command (/sk [list|follow|stay|dismiss]) and the hotkeys.
    // Actions apply to all of the player's skeletons, or only to `only` when given.
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

        public static string Follow(Player player, Character only = null)
        {
            var skeletons = Targets(player, only);
            foreach (var skeleton in skeletons)
                SummonedSkeletons.SetFollowing(skeleton, player, follow: true);
            return only != null
                ? $"{SummonedSkeletons.GetName(only)} idzie za tobą"
                : $"Szkielety idą za tobą: {skeletons.Count}";
        }

        public static string Stay(Player player, Character only = null)
        {
            var skeletons = Targets(player, only);
            foreach (var skeleton in skeletons)
                SummonedSkeletons.SetFollowing(skeleton, player, follow: false);
            return only != null
                ? $"{SummonedSkeletons.GetName(only)} czeka w miejscu"
                : $"Szkielety czekają w miejscu: {skeletons.Count}";
        }

        public static string Dismiss(Player player, Character only = null)
        {
            var skeletons = Targets(player, only);
            foreach (var skeleton in skeletons)
                SummonedSkeletons.Dismiss(skeleton);
            return only != null
                ? $"Odwołano: {SummonedSkeletons.GetName(only)}"
                : $"Odwołano szkielety: {skeletons.Count}";
        }

        public static void List(Terminal terminal, Player player)
        {
            var skeletons = Targets(player, null);
            terminal.AddString($"Twoje szkielety: {skeletons.Count}");
            for (int i = 0; i < skeletons.Count; i++)
                terminal.AddString($"  #{i + 1}  {SummonedSkeletons.Describe(skeletons[i], player)}");
        }

        private static List<Character> Targets(Player player, Character only)
        {
            if (only != null)
                return new List<Character> { only };
            var skeletons = new List<Character>();
            SummonedSkeletons.FindOwned(player, skeletons);
            return skeletons;
        }
    }
}
