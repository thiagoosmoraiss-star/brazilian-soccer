using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace Game.Tests.Unit
{
    /// <summary>
    /// Enforces ARCHITECTURE.md §2-3: pure assemblies never reference Unity, and every assembly only
    /// depends on what the table allows (asmdefs for Unity, csproj for the .NET solution, and the compiled IL).
    /// </summary>
    public class ArchitectureTests
    {
        private static readonly Dictionary<string, string[]> PureAllowed = new Dictionary<string, string[]>
        {
            ["Core"] = new string[0],
            ["Data"] = new[] { "Core" },
            ["Rules"] = new[] { "Core", "Data" },
            ["Match"] = new[] { "Core", "Data", "Rules" },
            ["Match.AI"] = new[] { "Core", "Data", "Rules", "Match" },
            ["Simulation"] = new[] { "Core", "Data", "Rules" },
            ["Career"] = new[] { "Core", "Data", "Rules", "Match", "Simulation" },
            ["Career.Market"] = new[] { "Core", "Data", "Rules", "Career" },
            ["Career.Economy"] = new[] { "Core", "Data", "Rules", "Career" },
            ["Save"] = new[] { "Core", "Data", "Career", "Career.Market", "Career.Economy" },
        };

        private static readonly Dictionary<string, string[]> UnityAllowed = new Dictionary<string, string[]>
        {
            ["App"] = PureAllowed.Keys.Concat(new[] { "UI", "Presentation", "Input", "Audio" }).ToArray(),
            ["Input"] = new[] { "Core", "Match" },
            ["Presentation"] = new[] { "Core", "Data", "Match" },
            ["Audio"] = new[] { "Core", "Match" },
            ["UI"] = new[] { "Core", "Data", "Career", "Career.Market", "Career.Economy", "Simulation" },
        };

        private static string AsmdefPath(string name) =>
            Path.Combine(TestPaths.GameSources(), name, name + ".asmdef");

        private static string ReadAsmdef(string name)
        {
            string path = AsmdefPath(name);
            Assert.IsTrue(File.Exists(path), "Missing asmdef: " + path);
            return File.ReadAllText(path);
        }

        // Minimal extraction (test-only): avoids depending on a JSON library while D-11 is pending.
        private static string[] References(string asmdefJson)
        {
            var m = Regex.Match(asmdefJson, "\"references\"\\s*:\\s*\\[(?<body>[^\\]]*)\\]");
            Assert.IsTrue(m.Success, "asmdef without references array");
            return Regex.Matches(m.Groups["body"].Value, "\"([^\"]+)\"").Cast<Match>().Select(x => x.Groups[1].Value).ToArray();
        }

        private static bool NoEngineReferences(string asmdefJson) =>
            Regex.IsMatch(asmdefJson, "\"noEngineReferences\"\\s*:\\s*true");

        [Test]
        public void PureAsmdefs_HaveNoEngineReferences_AndAllowedDependencies()
        {
            foreach (var kv in PureAllowed)
            {
                string json = ReadAsmdef(kv.Key);
                Assert.IsTrue(NoEngineReferences(json), kv.Key + ".asmdef must set noEngineReferences: true");
                var illegal = References(json).Except(kv.Value).ToArray();
                Assert.IsEmpty(illegal, $"{kv.Key} references forbidden assemblies: {string.Join(", ", illegal)}");
            }
        }

        [Test]
        public void UnityAsmdefs_OnlyDependOnAllowedAssemblies()
        {
            foreach (var kv in UnityAllowed)
            {
                var illegal = References(ReadAsmdef(kv.Key)).Except(kv.Value).ToArray();
                Assert.IsEmpty(illegal, $"{kv.Key} references forbidden assemblies: {string.Join(", ", illegal)}");
            }
        }

        [Test]
        public void ExplicitlyForbiddenEdges_AreAbsent()
        {
            var match = References(ReadAsmdef("Match"));
            var simulation = References(ReadAsmdef("Simulation"));
            CollectionAssert.DoesNotContain(match, "Career");
            CollectionAssert.DoesNotContain(match, "Simulation");
            CollectionAssert.DoesNotContain(simulation, "Match");
            Assert.IsEmpty(References(ReadAsmdef("Core")), "Core depends on nothing.");
        }

        [Test]
        public void PureSources_DoNotMentionUnityEngine()
        {
            foreach (var name in PureAllowed.Keys)
            {
                string dir = Path.Combine(TestPaths.GameSources(), name);
                foreach (var file in Directory.GetFiles(dir, "*.cs", SearchOption.AllDirectories))
                {
                    string text = File.ReadAllText(file);
                    Assert.IsFalse(text.Contains("UnityEngine") || text.Contains("UnityEditor"),
                        "Pure source references Unity: " + file);
                }
            }
        }

        [Test]
        public void CompiledPureAssemblies_DoNotReferenceUnity()
        {
            foreach (var name in PureAllowed.Keys)
            {
                Assembly asm;
                try
                {
                    asm = Assembly.Load(new AssemblyName(name));
                }
                catch (FileNotFoundException)
                {
                    // Unity does not compile an asmdef without scripts; acceptable only while the folder is empty.
                    string dir = Path.Combine(TestPaths.GameSources(), name);
                    Assert.IsEmpty(Directory.GetFiles(dir, "*.cs", SearchOption.AllDirectories), name + " has sources but no assembly.");
                    continue;
                }
                var refs = asm.GetReferencedAssemblies().Select(r => r.Name).ToArray();
                var unity = refs.Where(r => r.StartsWith("UnityEngine", StringComparison.Ordinal) ||
                                            r.StartsWith("UnityEditor", StringComparison.Ordinal)).ToArray();
                Assert.IsEmpty(unity, $"{name} references Unity: {string.Join(", ", unity)}");
                var gameRefs = refs.Where(r => PureAllowed.ContainsKey(r)).Except(PureAllowed[name]).ToArray();
                Assert.IsEmpty(gameRefs, $"{name} references forbidden assemblies: {string.Join(", ", gameRefs)}");
            }
        }

        [Test]
        public void DotnetProjects_MirrorAllowedDependencies()
        {
            string dotnet = Path.Combine(TestPaths.RepositoryRoot(), "dotnet");
            if (!Directory.Exists(dotnet)) Assert.Inconclusive("dotnet/ solution not present.");
            foreach (var kv in PureAllowed)
            {
                string csproj = Path.Combine(dotnet, kv.Key, kv.Key + ".csproj");
                Assert.IsTrue(File.Exists(csproj), "Missing project: " + csproj);
                var refs = Regex.Matches(File.ReadAllText(csproj), "ProjectReference Include=\"[^\"]*[\\\\/](?<n>[^\\\\/\"]+)\\.csproj\"")
                    .Cast<Match>().Select(m => m.Groups["n"].Value).ToArray();
                var illegal = refs.Except(kv.Value).ToArray();
                Assert.IsEmpty(illegal, $"dotnet/{kv.Key} references forbidden projects: {string.Join(", ", illegal)}");
                CollectionAssert.AreEquivalent(kv.Value, refs, $"dotnet/{kv.Key} must mirror {kv.Key}.asmdef");
                CollectionAssert.AreEquivalent(References(ReadAsmdef(kv.Key)), refs, $"dotnet/{kv.Key} vs asmdef");
            }
        }
    }
}
