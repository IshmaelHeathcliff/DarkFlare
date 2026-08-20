using System;
using System.Linq;
using UnityEditor.Localization;

namespace DarkFlare.Editor
{
    public static class PseudoLocalizationUtility
    {
        public static string Localize(string value)
        {
            var pseudo = LocalizationEditorSettings.GetPseudoLocales()
                .FirstOrDefault(locale => locale.Identifier.Code == "qps-ploc");

            if (pseudo == null)
            {
                throw new InvalidOperationException("未配置 qps-ploc 伪本地化语言");
            }

            return pseudo.GetPseudoString(value ?? string.Empty);
        }
    }
}
