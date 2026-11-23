using Brodun.Presentation;
using UnityEditor;
using UnityEngine;

namespace Brodun.Editor
{
    public sealed class BattleBackgroundPreview : EditorWindow
    {
        private int theme;
        private int encounter;
        private static readonly string[] Themes = { "회랑", "불", "서리", "공허" };
        private static readonly string[] Encounters = { "일반 / 엘리트", "보스" };

        [MenuItem("BRODUN/Art/Battle Background Pack")]
        public static void Open()
        {
            var window = GetWindow<BattleBackgroundPreview>("Battle Backgrounds");
            window.minSize = new Vector2(640, 400);
        }

        private void OnGUI()
        {
            theme = GUILayout.Toolbar(theme, Themes);
            encounter = GUILayout.Toolbar(encounter, Encounters);
            var selected=DungeonThemes.All[theme];
            EditorGUILayout.LabelField(selected.Name, EditorStyles.boldLabel);

            var sprite = BattleBackgrounds.Resolve(selected.Id, encounter == 1, null);
            if (sprite == null)
            {
                EditorGUILayout.HelpBox("배경 Sprite를 찾지 못했습니다. PNG와 .meta의 임포트 완료 여부를 확인하세요.", MessageType.Error);
                return;
            }
            EditorGUILayout.LabelField(sprite.texture.width + " × " + sprite.texture.height + " · 일반전 / 보스전 전환");
            Rect rect = GUILayoutUtility.GetRect(1, 10000, 220, 10000, GUILayout.ExpandHeight(true));
            GUI.DrawTexture(rect, sprite.texture, ScaleMode.ScaleToFit);
            if (GUILayout.Button("프로젝트에서 선택"))
                Selection.activeObject = sprite;
        }
    }
}
