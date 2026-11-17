using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Xml.Linq;
using NUnit.Framework;

namespace SoilRelocation.Tests.Project
{
    [TestFixture]
    public sealed class ReleasePackageTests
    {
        private const string SoilRelocationPackageId = "escarval.soilrelocation";
        private const string HarmonyPackageId = "brrainz.harmony";
        private const string HistoricalAssemblyPath = "1.6/Assemblies/SoilRelocation.dll";

        private const string HistoricalAssemblySha256 =
            "DC855571D9F87490E887F5BD552C94B4B1E0718417F910F4AA34CB273718C264";

        private static readonly string[] _translatedLanguages = ["Russian", "Ukrainian"];

        private static readonly string[] _requiredAssets =
        [
            "mod/About/ModIcon.png",
            "mod/About/Preview.png",
            "mod/Textures/Things/Item/Resource/SoilRelocation/PortableTerrain.png",
            "mod/Textures/UI/Designators/SoilRelocation/DigTerrain.png"
        ];

        [Test]
        public void AboutMetadata_DeclaresReleaseRuntimeContract()
        {
            string root = FindProjectRoot();
            string aboutPath = Path.Combine(root, "mod", "About", "About.xml");
            var document = XDocument.Load(aboutPath);
            XElement metadata = document.Root;

            Assert.That(metadata, Is.Not.Null, "About.xml has no document root.");
            Assert.That(ValueOf(metadata, "packageId"), Is.EqualTo(SoilRelocationPackageId));

            string[] supportedVersions = metadata.Element("supportedVersions")
                                             ?.Elements("li")
                                             .Select(element => element.Value.Trim())
                                             .ToArray() ??
                                         [];
            Assert.That(supportedVersions, Is.EquivalentTo(["1.6", "1.7"]));

            List<XElement> dependencies = metadata.Element("modDependencies")?.Elements("li").ToList() ?? [];
            Assert.That(dependencies, Has.Count.EqualTo(1), "Only Harmony is a required runtime dependency.");

            XElement harmony = dependencies.Single();
            Assert.That(ValueOf(harmony, "packageId"), Is.EqualTo(HarmonyPackageId));
            Assert.That(ValueOf(harmony, "displayName"), Is.EqualTo("Harmony"));
            Assert.That(ValueOf(harmony, "steamWorkshopUrl"), Is.Not.Empty);
            Assert.That(ValueOf(harmony, "downloadUrl"), Is.Not.Empty);

            string[] loadAfter = metadata.Element("loadAfter")
                                     ?.Elements("li")
                                     .Select(element => element.Value.Trim())
                                     .ToArray() ??
                                 [];
            Assert.That(loadAfter, Does.Contain(HarmonyPackageId));
        }

        [Test]
        public void LoadFolders_SelectsTheSharedRootAndOneVersionDirectory()
        {
            string loadFoldersPath = Path.Combine(FindProjectRoot(), "mod", "LoadFolders.xml");
            XElement loadFolders = XDocument.Load(loadFoldersPath).Root;

            Assert.That(loadFolders, Is.Not.Null);
            Assert.That(loadFolders.Name, Is.EqualTo(XName.Get("loadFolders")));
            Assert.That(
                loadFolders.Elements().Select(element => element.Name),
                Is.EquivalentTo([XName.Get("v1.6"), XName.Get("v1.7")]));

            AssertLoadFolder(loadFolders.Element("v1.6"), "1.6");
            AssertLoadFolder(loadFolders.Element("v1.7"), "Current");
        }

        [Test]
        public void SourcePackage_ContainsOnlyThePublishedHistoricalAssembly()
        {
            string root = FindProjectRoot();
            string modRoot = Path.Combine(root, "mod");
            string[] assemblyPaths = Directory.EnumerateFiles(modRoot, "*", SearchOption.AllDirectories)
                .Where(file => string.Equals(Path.GetExtension(file), ".dll", StringComparison.OrdinalIgnoreCase))
                .Select(file => MakeRelativePath(modRoot, file))
                .ToArray();

            Assert.That(
                assemblyPaths,
                Is.EqualTo([HistoricalAssemblyPath]),
                "Source content must only ship the frozen 1.6 DLL. The current DLL is added by explicit deployment.");

            using FileStream assembly = File.OpenRead(Path.Combine(modRoot, HistoricalAssemblyPath));
            Assert.That(Convert.ToHexString(SHA256.HashData(assembly)), Is.EqualTo(HistoricalAssemblySha256));

            var project = XElement.Load(Path.Combine(root, "src", "SoilRelocation", "SoilRelocation.csproj"));
            XElement[] historicalVersions = project.Elements("ItemGroup").Elements("HistoricalModVersion").ToArray();
            Assert.That(
                historicalVersions.Select(version => (string)version.Attribute("Include")),
                Is.EqualTo(["1.6"]),
                "DeployToGame must declare the preserved RimWorld 1.6 assembly in the production project.");
            Assert.That(
                (string)historicalVersions[0].Attribute("Sha256"),
                Is.EqualTo(HistoricalAssemblySha256).IgnoreCase);
        }

        [Test]
        public void SourcePackage_KeepsSharedContentAtTheRoot()
        {
            string modRoot = Path.Combine(FindProjectRoot(), "mod");
            string[] modDirectories = Directory.EnumerateDirectories(modRoot).Select(Path.GetFileName).ToArray();
            string[] generatedDirectories = ["Assemblies", "Current", "1.7"];

            Assert.That(modDirectories, Is.SupersetOf(["About", "Defs", "Patches", "Languages", "Textures"]));
            Assert.That(modDirectories.Intersect(generatedDirectories, StringComparer.OrdinalIgnoreCase), Is.Empty);
        }

        [Test]
        public void RequiredReleaseAssets_ArePresent()
        {
            string root = FindProjectRoot();
            var missing = new List<string>();

            foreach (string relativePath in _requiredAssets)
            {
                string path = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));

                if (!File.Exists(path))
                {
                    missing.Add(relativePath);
                }
            }

            Assert.That(missing, Is.Empty, "Missing required release assets: " + string.Join(", ", missing));
        }

        [Test]
        public void LocalizationPackages_HaveMatchingFileAndKeyParity()
        {
            string languagesRoot = Path.Combine(FindProjectRoot(), "mod", "Languages");
            string englishRoot = Path.Combine(languagesRoot, "English");

            Assert.That(Directory.Exists(englishRoot), Is.True, "English localization package is missing.");

            Dictionary<string, HashSet<string>> englishKeys = ReadLocalizationPackage(englishRoot);

            foreach (string definitionFile in Directory.GetFiles(
                         Path.Combine(FindProjectRoot(), "mod", "Defs"),
                         "*.xml",
                         SearchOption.AllDirectories))
            {
                XElement[] definitions = XElement.Load(definitionFile)
                    .Elements()
                    .Where(definition => definition.Element("defName") != null)
                    .ToArray();

                foreach (IGrouping<string, XElement> typeGroup in definitions.GroupBy(definition =>
                             definition.Name.LocalName))
                {
                    var keys = new HashSet<string>(StringComparer.Ordinal);
                    foreach (XElement definition in typeGroup)
                    {
                        foreach (XElement field in definition.Elements()
                                     .Where(field =>
                                         field.Name.LocalName is "label"
                                             or "description"
                                             or "reportString"
                                             or "verb"
                                             or "gerund"
                                             or "labelShort"
                                             or "pawnLabel"
                                             or "gerundLabel"))
                        {
                            Assert.That(field.Value.Trim(), Is.Not.Empty, field.ToString());
                            keys.Add(ValueOf(definition, "defName") + "." + field.Name.LocalName);
                        }
                    }

                    if (keys.Count > 0)
                    {
                        englishKeys.Add($"DefInjected/{typeGroup.Key}/{Path.GetFileName(definitionFile)}", keys);
                    }
                }
            }

            foreach (string language in _translatedLanguages)
            {
                string languageRoot = Path.Combine(languagesRoot, language);
                Assert.That(Directory.Exists(languageRoot), Is.True, language + " localization package is missing.");

                Dictionary<string, HashSet<string>> actual = ReadLocalizationPackage(languageRoot);
                Assert.That(
                    actual.Keys.OrderBy(value => value).ToArray(),
                    Is.EqualTo(englishKeys.Keys.OrderBy(value => value).ToArray()),
                    language + " localization file layout differs from English.");

                foreach (KeyValuePair<string, HashSet<string>> expectedFile in englishKeys)
                {
                    Assert.That(
                        actual[expectedFile.Key].OrderBy(value => value).ToArray(),
                        Is.EqualTo(expectedFile.Value.OrderBy(value => value).ToArray()),
                        language + " localization keys differ from English in " + expectedFile.Key + ".");
                }
            }
        }

        [Test]
        public void ModXmlFiles_AreParseable()
        {
            string modRoot = Path.Combine(FindProjectRoot(), "mod");
            string[] xmlFiles = Directory.GetFiles(modRoot, "*.xml", SearchOption.AllDirectories);
            var failures = new List<string>();

            foreach (string file in xmlFiles)
            {
                try
                {
                    _ = XDocument.Load(file);
                }
                catch (Exception exception)
                {
                    failures.Add(ToProjectRelative(file) + ": " + exception.Message);
                }
            }

            Assert.That(failures, Is.Empty, "Invalid mod XML: " + string.Join(" | ", failures));
        }

        private static void AssertLoadFolder(XElement version, string assemblyDirectory)
        {
            XElement[] entries = version.Elements().ToArray();
            Assert.That(entries.Select(entry => entry.Name), Is.EqualTo([XName.Get("li"), XName.Get("li")]));
            Assert.That(entries.SelectMany(entry => entry.Attributes()), Is.Empty);
            Assert.That(entries.Select(entry => entry.Value.Trim()), Is.EqualTo(["/", assemblyDirectory]));
        }

        private static Dictionary<string, HashSet<string>> ReadLocalizationPackage(string languageRoot)
        {
            var result = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
            string[] files = Directory.GetFiles(languageRoot, "*.xml", SearchOption.AllDirectories);

            foreach (string file in files)
            {
                string relative = MakeRelativePath(languageRoot, file);
                var document = XDocument.Load(file);
                XElement root = document.Root;

                Assert.That(root, Is.Not.Null, relative + " has no document root.");
                Assert.That(root.Name.LocalName, Is.EqualTo("LanguageData"), relative + " has an unexpected root.");

                var keys = root.Elements().Select(element => element.Name.LocalName).ToList();
                var uniqueKeys = new HashSet<string>(keys, StringComparer.Ordinal);

                Assert.That(
                    uniqueKeys.Count,
                    Is.EqualTo(keys.Count),
                    relative + " contains duplicate top-level localization keys.");

                result.Add(relative, uniqueKeys);
            }

            return result;
        }

        private static string FindProjectRoot()
        {
            string[] starts =
            [
                TestContext.CurrentContext.TestDirectory,
                Directory.GetCurrentDirectory()
            ];

            foreach (string start in starts)
            {
                if (string.IsNullOrEmpty(start))
                {
                    continue;
                }

                var directory = new DirectoryInfo(start);

                while (directory != null)
                {
                    string about = Path.Combine(directory.FullName, "mod", "About", "About.xml");
                    string solution = Path.Combine(directory.FullName, "src", "SoilRelocation.sln");

                    if (File.Exists(about) && File.Exists(solution))
                    {
                        return directory.FullName;
                    }

                    directory = directory.Parent;
                }
            }

            throw new DirectoryNotFoundException(
                "Could not locate the Soil Relocation repository root from the test directory or current directory.");
        }

        private static string ValueOf(XElement parent, string elementName)
        {
            return parent.Element(elementName)?.Value.Trim() ?? string.Empty;
        }

        private static string MakeRelativePath(string root, string file)
        {
            string normalizedRoot =
                Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
                Path.DirectorySeparatorChar;
            string normalizedFile = Path.GetFullPath(file);

            if (!normalizedFile.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
            {
                return normalizedFile.Replace(Path.DirectorySeparatorChar, '/');
            }

            return normalizedFile.Substring(normalizedRoot.Length).Replace(Path.DirectorySeparatorChar, '/');
        }

        private static string ToProjectRelative(string file)
        {
            return MakeRelativePath(FindProjectRoot(), file);
        }
    }
}