using System;
using System.Linq;
using NUnit.Framework;

namespace DarkFlare.Tests
{
    public sealed class SettingsDataContractTests
    {
        [Test]
        public void DefaultsAndRoundTrip_AreValidAndPreserveAllDomains()
        {
            UserSettingsSnapshot snapshot = new UserSettingsSnapshot(
                UserLanguagePreference.English,
                0.8f,
                0.7f,
                0.6f,
                0.5f,
                true,
                "{\"bindings\":[]}",
                InputGlyphPreference.Gamepad,
                2560,
                1440,
                PreferredDisplayMode.Windowed,
                1.25f,
                1.5f,
                true,
                0.25f,
                true);

            SettingsDataValidationResult defaultResult = SettingsDataValidator.ValidateSnapshot(
                UserSettingsSnapshot.Default);
            SettingsDataValidationResult sourceResult = SettingsDataValidator.ValidateSnapshot(snapshot);
            UserSettingsSnapshot restored = UserSettingsSnapshot.FromDto(snapshot.ToDto());

            Assert.IsTrue(defaultResult.Succeeded);
            Assert.IsTrue(sourceResult.Succeeded);
            Assert.AreEqual(snapshot.Language, restored.Language);
            Assert.AreEqual(snapshot.MasterVolume, restored.MasterVolume);
            Assert.AreEqual(snapshot.BindingOverridesJson, restored.BindingOverridesJson);
            Assert.AreEqual(snapshot.GlyphPreference, restored.GlyphPreference);
            Assert.AreEqual(snapshot.DisplayWidth, restored.DisplayWidth);
            Assert.AreEqual(snapshot.DisplayMode, restored.DisplayMode);
            Assert.AreEqual(snapshot.TextScale, restored.TextScale);
            Assert.AreEqual(snapshot.HighContrast, restored.HighContrast);
        }

        [Test]
        public void Validate_RejectsInvalidEnumsRangesNonFiniteValuesAndOversizedBindings()
        {
            UserSettingsDocumentDto document = CreateValidDocument();
            document.Payload.Language = (UserLanguagePreference)99;
            document.Payload.Audio.MasterVolume = float.PositiveInfinity;
            document.Payload.Input.BindingOverridesJson = new string(
                'x',
                LocalSettingsFormat.MaximumBindingOverridesLength + 1);
            document.Payload.Display.Width = 1;
            document.Payload.Accessibility.TextScale = 3f;

            SettingsDataValidationResult result = SettingsDataValidator.ValidateDocument(document);

            Assert.IsFalse(result.Succeeded);
            Assert.That(
                result.Issues.Select(issue => issue.Code),
                Has.Member(SettingsDataIssueCode.NonFiniteNumber));
            Assert.That(
                result.Issues.Select(issue => issue.Code),
                Has.Member(SettingsDataIssueCode.LimitExceeded));
            Assert.That(
                result.Issues.Count(issue => issue.Code == SettingsDataIssueCode.InvalidValue),
                Is.GreaterThanOrEqualTo(3));
        }

        internal static UserSettingsDocumentDto CreateValidDocument(
            UserLanguagePreference language = UserLanguagePreference.Auto)
        {
            UserSettingsDataDto payload = UserSettingsSnapshot.Default
                .WithLanguage(language)
                .ToDto();
            return new UserSettingsDocumentDto
            {
                UpdatedUtc = new DateTimeOffset(
                    2026,
                    8,
                    19,
                    0,
                    0,
                    0,
                    TimeSpan.Zero).ToString("O"),
                Payload = payload,
            };
        }
    }
}
