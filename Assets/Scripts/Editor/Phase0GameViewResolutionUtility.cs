using System;
using System.Linq;
using System.Reflection;
using UnityEditor;

namespace DarkFlare.Editor
{
    public static class Phase0GameViewResolutionUtility
    {
        const BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        public static void SetResolution(int width, int height)
        {
            object group = GetStandaloneGroup();
            int index = FindResolution(group, width, height);

            if (index < 0)
            {
                AddResolution(group, width, height);
                index = FindResolution(group, width, height);
            }

            if (index < 0)
            {
                throw new InvalidOperationException($"无法创建 Game View 分辨率 {width}×{height}");
            }

            Type gameViewType = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView", true);
            EditorWindow gameView = EditorWindow.GetWindow(gameViewType);
            PropertyInfo selectedSizeIndex = gameViewType.GetProperty("selectedSizeIndex", InstanceFlags)
                ?? throw new MissingMemberException(gameViewType.FullName, "selectedSizeIndex");
            selectedSizeIndex.SetValue(gameView, index);
            gameView.Repaint();
        }

        static object GetStandaloneGroup()
        {
            Assembly editorAssembly = typeof(EditorWindow).Assembly;
            Type sizesType = editorAssembly.GetType("UnityEditor.GameViewSizes", true);
            Type singletonType = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
            PropertyInfo instanceProperty = singletonType.GetProperty("instance", BindingFlags.Static | BindingFlags.Public)
                ?? throw new MissingMemberException(singletonType.FullName, "instance");
            object sizes = instanceProperty.GetValue(null);
            MethodInfo getGroup = sizesType.GetMethod("GetGroup", InstanceFlags)
                ?? throw new MissingMemberException(sizesType.FullName, "GetGroup");
            return getGroup.Invoke(sizes, new object[] { GameViewSizeGroupType.Standalone });
        }

        static int FindResolution(object group, int width, int height)
        {
            Type groupType = group.GetType();
            MethodInfo getTotalCount = groupType.GetMethod("GetTotalCount", InstanceFlags)
                ?? throw new MissingMemberException(groupType.FullName, "GetTotalCount");
            MethodInfo getGameViewSize = groupType.GetMethod("GetGameViewSize", InstanceFlags)
                ?? throw new MissingMemberException(groupType.FullName, "GetGameViewSize");
            int count = (int)getTotalCount.Invoke(group, null);

            for (int i = 0; i < count; i++)
            {
                object size = getGameViewSize.Invoke(group, new object[] { i });
                Type sizeType = size.GetType();
                int sizeWidth = (int)sizeType.GetProperty("width", InstanceFlags).GetValue(size);
                int sizeHeight = (int)sizeType.GetProperty("height", InstanceFlags).GetValue(size);

                if (sizeWidth == width && sizeHeight == height)
                {
                    return i;
                }
            }

            return -1;
        }

        static void AddResolution(object group, int width, int height)
        {
            Assembly editorAssembly = typeof(EditorWindow).Assembly;
            Type sizeType = editorAssembly.GetType("UnityEditor.GameViewSize", true);
            Type sizeModeType = editorAssembly.GetType("UnityEditor.GameViewSizeType", true);
            object fixedResolution = Enum.Parse(sizeModeType, "FixedResolution");
            ConstructorInfo constructor = sizeType
                .GetConstructors(InstanceFlags)
                .FirstOrDefault(item => item.GetParameters().Length == 4)
                ?? throw new MissingMethodException(sizeType.FullName, ".ctor");
            object size = constructor.Invoke(new[]
            {
                fixedResolution,
                (object)width,
                height,
                $"Phase 0 {width}×{height}",
            });
            MethodInfo addCustomSize = group.GetType().GetMethod("AddCustomSize", InstanceFlags)
                ?? throw new MissingMemberException(group.GetType().FullName, "AddCustomSize");
            addCustomSize.Invoke(group, new[] { size });
        }
    }
}
