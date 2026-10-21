using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Game.Data.Loading;
using Game.Data.Strings;
using Game.Save.IO;
using Game.UI.Presenters;
using NUnit.Framework;

namespace Game.Tests.UI
{
    /// <summary>Real data and strings, and a Shell over a fresh temporary save folder.</summary>
    internal static class UiTestSupport
    {
        private static GameDatabase _db;
        private static StringTable _strings;

        public static string DataRoot()
        {
            string root = Data.Loading.DataRoot.Find(Directory.GetCurrentDirectory()) ?? Data.Loading.DataRoot.Find(AppContext.BaseDirectory);
            if (root == null) throw new InvalidOperationException("Repository Data/ folder not found from the test run directory.");
            return root;
        }

        public static GameDatabase Db()
        {
            if (_db == null)
            {
                var r = GameDataLoader.Load(new DirectoryDataSource(DataRoot()));
                Assert.IsTrue(r.IsSuccess, r.ToString());
                _db = r.Value;
            }
            return _db;
        }

        public static StringTable Strings()
        {
            if (_strings == null)
            {
                var r = GameDataLoader.LoadStrings(new DirectoryDataSource(DataRoot()));
                Assert.IsTrue(r.IsSuccess, r.ToString());
                _strings = r.Value;
            }
            return _strings;
        }

        public static IFileStore NewStore() =>
            new FileSystemFileStore(Path.Combine(Path.GetTempPath(), "acesso-ui-" + Guid.NewGuid().ToString("N")));

        public static readonly DateTime Now = new DateTime(2026, 10, 16, 12, 0, 0, DateTimeKind.Utc);

        public static CareerSession Open(IFileStore store)
        {
            var r = CareerSession.Open(Db(), Strings(), store, () => Now);
            Assert.IsTrue(r.IsSuccess, r.ToString());
            return r.Value;
        }

        public static ShellPresenter Shell(IFileStore store, ulong seed = 7)
        {
            var shell = new ShellPresenter(Open(store), new ShellNavigator(), () => seed);
            shell.Start(returningFromMatch: false);
            return shell;
        }

        /// <summary>New career through the screens: menu → Nova carreira → first club offered.</summary>
        public static ShellPresenter NewCareer(IFileStore store, ulong seed = 7)
        {
            var shell = Shell(store, seed);
            shell.NewCareer();
            Assert.AreEqual(ScreenId.ClubChoice, shell.Nav.Current);
            shell.ChooseClub(shell.ClubChoice().Options[0].ClubId);
            Assert.AreEqual(ScreenId.Hub, shell.Nav.Current);
            return shell;
        }

        /// <summary>Builds the model of the current screen (what the view would show).</summary>
        public static object CurrentModel(ShellPresenter shell)
        {
            switch (shell.Nav.Current)
            {
                case ScreenId.MainMenu: return shell.MainMenu();
                case ScreenId.ClubChoice: return shell.ClubChoice();
                case ScreenId.Hub: return shell.Hub();
                case ScreenId.PreMatch: return shell.PreMatch();
                case ScreenId.PostMatch: return shell.PostMatch();
                case ScreenId.Competitions: return shell.Competitions();
                case ScreenId.Calendar: return shell.Calendar();
                case ScreenId.Settings: return shell.Settings();
                case ScreenId.Board: return shell.Board();
                case ScreenId.Club: return shell.Club();
                case ScreenId.Finances: return shell.Finances();
                default: return shell.Placeholder(shell.Nav.Current);
            }
        }

        /// <summary>Every string reachable from a model (properties, lists, nested models).</summary>
        public static IEnumerable<string> Texts(object model)
        {
            if (model == null) yield break;
            if (model is string s) { yield return s; yield break; }
            if (model is IEnumerable list)
            {
                foreach (var item in list) foreach (var t in Texts(item)) yield return t;
                yield break;
            }
            var type = model.GetType();
            if (type.IsPrimitive || type.IsEnum) yield break;
            foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                foreach (var t in Texts(prop.GetValue(model))) yield return t;
        }

        /// <summary>No missing string key ("#key") and no unformatted placeholder in a model.</summary>
        public static void AssertTextsComplete(object model, string where)
        {
            foreach (var t in Texts(model))
            {
                bool colour = System.Text.RegularExpressions.Regex.IsMatch(t, "^#[0-9A-Fa-f]{6}$");
                Assert.IsFalse(!colour && t.StartsWith("#", StringComparison.Ordinal) && t.Length > 1 && char.IsLetter(t[1]), $"{where}: missing string '{t}'");
                Assert.IsFalse(t.Contains("{0}"), $"{where}: unformatted text '{t}'");
            }
        }
    }
}
