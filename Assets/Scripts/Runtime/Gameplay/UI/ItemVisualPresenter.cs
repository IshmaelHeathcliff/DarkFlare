using UnityEngine;
using UnityEngine.UIElements;

namespace DarkFlare
{
    public static class ItemVisualPresenter
    {
        const string MissingIconClass = "item-icon--missing";

        public static Sprite GetSprite(string iconGuid)
        {
            return GameArchitectureProvider.RequireCurrent()
                .GetUtility<SpriteAssetLoader>()
                .GetSprite(iconGuid);
        }

        public static void ApplyIcon(VisualElement element, string iconGuid)
        {
            if (element == null)
            {
                return;
            }

            bool hasReference = !string.IsNullOrWhiteSpace(iconGuid);
            Sprite sprite = hasReference ? GetSprite(iconGuid) : null;
            element.EnableInClassList(MissingIconClass, hasReference && sprite == null);
            element.style.backgroundImage = sprite != null
                ? new StyleBackground(sprite)
                : new StyleBackground(StyleKeyword.Null);
        }
    }
}
