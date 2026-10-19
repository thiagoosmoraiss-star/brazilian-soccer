using System;
using System.Collections.Generic;

namespace Game.UI.Presenters
{
    /// <summary>The Shell's screens (TECHNICAL_SPEC §15; X-95 says which sub-stage brings each one).</summary>
    public enum ScreenId
    {
        MainMenu,
        ClubChoice,
        Hub,
        PreMatch,
        PostMatch,
        Squad,
        Player,
        Lineup,
        Market,
        Competitions,
        Calendar,
        Board,
        Settings,
    }

    /// <summary>Bottom bar (GAME_DESIGN §13): Clube · Elenco · Mercado · Competições · Diretoria.</summary>
    public enum Tab
    {
        Club,
        Squad,
        Market,
        Competitions,
        Board,
    }

    /// <summary>
    /// Stack navigation of the Shell (TECHNICAL_SPEC §15 "navegação por pilha sem recarregar"): a tab resets the stack
    /// to its root; screens are pushed on top (Calendar, Settings, Pre-Match…) and Back pops them.
    /// </summary>
    public sealed class ShellNavigator
    {
        private readonly List<ScreenId> _stack = new List<ScreenId>();

        public event Action Changed;

        public ScreenId Current => _stack.Count == 0 ? ScreenId.MainMenu : _stack[_stack.Count - 1];
        public Tab Tab { get; private set; } = Tab.Club;
        public bool CanGoBack => _stack.Count > 1;
        public int Depth => _stack.Count;

        /// <summary>The bottom bar shows on the career screens (not on the menus, the club choice or around a match).</summary>
        public bool ShowsTabBar
        {
            get
            {
                switch (Current)
                {
                    case ScreenId.Hub:
                    case ScreenId.Squad:
                    case ScreenId.Player:
                    case ScreenId.Market:
                    case ScreenId.Competitions:
                    case ScreenId.Calendar:
                    case ScreenId.Board:
                        return true;
                    default:
                        return false;
                }
            }
        }

        public static ScreenId TabRoot(Tab tab)
        {
            switch (tab)
            {
                case Tab.Squad: return ScreenId.Squad;
                case Tab.Market: return ScreenId.Market;
                case Tab.Competitions: return ScreenId.Competitions;
                case Tab.Board: return ScreenId.Board;
                default: return ScreenId.Hub;
            }
        }

        public void Reset(ScreenId root)
        {
            _stack.Clear();
            _stack.Add(root);
            if (root == ScreenId.Hub) Tab = Tab.Club;
            Changed?.Invoke();
        }

        public void SelectTab(Tab tab)
        {
            Tab = tab;
            _stack.Clear();
            _stack.Add(TabRoot(tab));
            Changed?.Invoke();
        }

        public void Push(ScreenId screen)
        {
            _stack.Add(screen);
            Changed?.Invoke();
        }

        /// <summary>Replaces the top screen (Pre-Match → Post-Match after Simular).</summary>
        public void Replace(ScreenId screen)
        {
            if (_stack.Count == 0) _stack.Add(screen);
            else _stack[_stack.Count - 1] = screen;
            Changed?.Invoke();
        }

        public bool Back()
        {
            if (!CanGoBack) return false;
            _stack.RemoveAt(_stack.Count - 1);
            Changed?.Invoke();
            return true;
        }
    }
}
