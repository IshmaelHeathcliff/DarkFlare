using Sirenix.OdinInspector;
using UnityEngine;

namespace DarkFlare
{
    [CreateAssetMenu(menuName = "DarkFlare/Data/Tags/Tag Definition", fileName = "TagDefinition")]
    public class TagDefinition : ScriptableObject
    {
        [SerializeField]
        [LabelText("稳定ID")]
        string _id = string.Empty;

        [SerializeField]
        [LabelText("中文名")]
        string _displayName = string.Empty;

        [SerializeField]
        [TextArea]
        [LabelText("说明")]
        string _description = string.Empty;

        public string Id => _id;

        public string DisplayName => _displayName;

        public string Description => _description;
    }
}

