using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using NUnit.Framework;

namespace SoilRelocation.Tests.Project
{
    [TestFixture]
    public sealed class ReleasePackageTests
    {
        private const string SoilRelocationPackageId = "escarval.soilrelocation";
        private const string HarmonyPackageId = "brrainz.harmony";

        private static readonly string[] _languages = { "English", "Russian", "Ukrainian" };

        private static readonly string[] _requiredAssets =
        {
            "mod/About/ModIcon.png",
            "mod/About/Preview.png",
            "mod/Textures/Things/Item/Resource/SoilRelocation/PortableTerrain.png",
            "mod/Textures/UI/Designators/SoilRelocation/DigTerrain.png"
        };

        [Test]
        public void AboutMetadata_DeclaresReleaseRuntimeContract()
        {
            string root = FindProjectRoot();
            string aboutPath = Path.Combine(root, "mod", "About", "About.xml");
            var document = XDocument.Load(aboutPath);
            XElement metadata = document.Root;

            Assert.That(metadata, Is.Not.Null, "About.xml has no document root.");
            Assert.That(ValueOf(metadata, "packageId"), Is.EqualTo(SoilRelocationPackageId));

            string[] supportedVersions = metadata.Element("supportedVersions")?.Elements("li")
                .Select(element => element.Value.Trim()).ToArray() ?? Array.Empty<string>();
            Assert.That(supportedVersions, Does.Contain("1.6"));

            List<XElement> dependencies = metadata.Element("modDependencies")?.Elements("li").ToList() ??
                                          new List<XElement>();
            Assert.That(dependencies, Has.Count.EqualTo(1), "Only Harmony is a required runtime dependency.");

            XElement harmony = dependencies.Single();
            Assert.That(ValueOf(harmony, "packageId"), Is.EqualTo(HarmonyPackageId));
            Assert.That(ValueOf(harmony, "displayName"), Is.EqualTo("Harmony"));
            Assert.That(ValueOf(harmony, "steamWorkshopUrl"), Is.Not.Empty);
            Assert.That(ValueOf(harmony, "downloadUrl"), Is.Not.Empty);

            string[] loadAfter = metadata.Element("loadAfter")?.Elements("li")
                .Select(element => element.Value.Trim()).ToArray() ?? Array.Empty<string>();
            Assert.That(loadAfter, Does.Contain(HarmonyPackageId));
        }

        [Test]
        public void Package_DoesNotBundleHarmonyAssembly()
        {
            string modRoot = Path.Combine(FindProjectRoot(), "mod");
            string[] bundledHarmony = Directory.GetFiles(modRoot, "0Harmony.dll", SearchOption.AllDirectories);

            Assert.That(
                bundledHarmony,
                Is.Empty,
                "Harmony is an external required dependency and must not be bundled by Soil Relocation.");
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

            foreach (string language in _languages)
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
            {
                TestContext.CurrentContext.TestDirectory,
                Directory.GetCurrentDirectory()
            };

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
            string normalizedRoot = Path.GetFullPath(root).TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
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