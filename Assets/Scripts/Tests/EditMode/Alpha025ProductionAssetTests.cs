using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Localization.Tables;
using UnityEngine.UIElements;

namespace DarkFlare.Tests
{
    public sealed class Alpha025ProductionAssetTests
    {
        const string GlyphRoot = "Assets/Art/UI/InputGlyphs";

        static readonly string[] GlyphNames =
        {
            "keyboard_keycap",
            "gamepad_button_south",
            "gamepad_button_east",
            "gamepad_button_west",
            "gamepad_button_north",
            "gamepad_start",
            "gamepad_dpad",
            "gamepad_left_stick",
        };

        static readonly string[] RequiredSettingsKeys =
        {
            "settings.open",
            "settings.title",
            "settings.audio.title",
            "settings.input.title",
            "settings.input.restore_defaults",
            "settings.accessibility.title",
            "settings.accessibility.reduce_motion",
            "settings.status.saved",
            "settings.input.rebind_listening",
            "flow.modal.confirm",
        };

        [Test]
        public void AudioAssets_HaveValidMixerAddressablesCueAndProvenance()
        {
            const string configPath = "Assets/Audio/AudioServiceConfiguration.asset";
            const string clipPath = "Assets/Audio/UI/ui_confirm.wav";
            AudioServiceConfiguration configuration =
                AssetDatabase.LoadAssetAtPath<AudioServiceConfiguration>(configPath);

            Assert.IsNotNull(configuration);
            Assert.IsTrue(configuration.IsValid());
            Assert.IsTrue(configuration.TryGetCue(
                AudioCueIds.UiConfirm,
                out AudioCueDefinition cue));
            Assert.AreEqual(AudioCategory.UI, cue.Category);
            Assert.IsFalse(cue.Loop);
            Assert.Greater(cue.MaxConcurrency, 0);
            AudioMixer mixer = configuration.Mixer;
            Assert.IsNotNull(mixer);
            CollectionAssert.AreEquivalent(
                new[] { "Master", "Music", "SFX", "UI" },
                mixer.FindMatchingGroups(string.Empty).Select(group => group.name));

            foreach (string parameter in new[]
            {
                AudioServiceConfiguration.MasterVolumeParameter,
                AudioServiceConfiguration.MusicVolumeParameter,
                AudioServiceConfiguration.SoundEffectsVolumeParameter,
                AudioServiceConfiguration.UiVolumeParameter,
            })
            {
                Assert.IsTrue(mixer.GetFloat(parameter, out _), parameter);
            }

            string configGuid = AssetDatabase.AssetPathToGUID(configPath);
            string clipGuid = AssetDatabase.AssetPathToGUID(clipPath);
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            Assert.AreEqual(
                AudioServiceConfiguration.Address,
                settings.FindAssetEntry(configGuid, true)?.address);
            Assert.AreEqual(
                "audio/ui.confirm",
                settings.FindAssetEntry(clipGuid, true)?.address);
            Assert.AreEqual(clipGuid, cue.Clip.AssetGUID);
            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(clipPath);
            Assert.IsNotNull(clip);
            Assert.That(clip.length, Is.InRange(0.6f, 0.7f));
            string provenance = File.ReadAllText(
                Path.GetFullPath("Assets/Audio/UI/README.md"));
            StringAssert.Contains("2026-08-24", provenance);
            StringAssert.Contains("Unity MCP", provenance);
        }

        [Test]
        public void SettingsUi_ContainsSharedPageAndRequiredControls()
        {
            VisualTreeAsset tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                "Assets/UI/ApplicationShell.uxml");
            Assert.IsNotNull(tree);
            TemplateContainer root = tree.CloneTree();
            Assert.IsNotNull(root.Q<Button>("front-end-settings"));
            Assert.IsNotNull(root.Q<VisualElement>("application-settings"));
            Assert.IsNotNull(root.Q<Slider>("settings-master-volume"));
            Assert.IsNotNull(root.Q<DropdownField>("settings-glyph-preference"));
            Assert.IsNotNull(root.Q<VisualElement>("settings-binding-list"));
            Assert.IsNotNull(root.Q<Toggle>("settings-reduce-motion"));
        }

        [Test]
        public void Localization_ContainsSettingsEntriesInBothProductionLocales()
        {
            var collection = LocalizationEditorSettings.GetStringTableCollection("ui");
            Assert.IsNotNull(collection);

            foreach (string key in RequiredSettingsKeys)
            {
                Assert.IsNotNull(collection.SharedData.GetEntry(key), key);

                foreach (var locale in LocalizationEditorSettings.GetLocales())
                {
                    StringTable table = collection.GetTable(locale.Identifier) as StringTable;
                    Assert.IsNotNull(table, locale.Identifier.Code);
                    StringTableEntry entry = table.GetEntry(key);
                    Assert.IsNotNull(entry, $"{locale.Identifier.Code}:{key}");
                    Assert.IsFalse(
                        string.IsNullOrWhiteSpace(entry.Value),
                        $"{locale.Identifier.Code}:{key}");
                }
            }
        }

        [Test]
        public void GlyphFamily_UsesOneSingleSpritePerAuditedCanvas()
        {
            foreach (string name in GlyphNames)
            {
                string path = $"{GlyphRoot}/{name}.png";
                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);

                Assert.IsNotNull(importer, path);
                Assert.AreEqual(TextureImporterType.Sprite, importer.textureType, path);
                Assert.AreEqual(SpriteImportMode.Single, importer.spriteImportMode, path);
                Assert.AreEqual(64f, importer.spritePixelsPerUnit, path);
                Assert.IsFalse(importer.mipmapEnabled, path);
                Assert.IsNotNull(texture, path);
                Assert.AreEqual(64, texture.width, path);
                Assert.AreEqual(64, texture.height, path);
                Assert.IsNotNull(sprite, path);
            }

            string audit = File.ReadAllText(Path.GetFullPath(
                $"{GlyphRoot}/input-glyphs.audit.json"));
            StringAssert.Contains("darkflare-input-glyphs", audit);

            foreach (string name in GlyphNames)
            {
                StringAssert.Contains($"\"name\": \"{name}\"", audit);
            }
        }
    }
}
