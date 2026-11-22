using Brodun.Presentation;
using UnityEditor;
using UnityEngine;

namespace Brodun.Editor
{
    public sealed class BattleBackgroundPreview : EditorWindow
    {
        private int theme;
        private int encounter;
        private static readonly string[] Themes = { "회랑", "수맥", "화로", "폭풍 (시안)" };
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
            EditorGUILayout.LabelField(BattleBackgrounds.ThemeName(theme + 1), EditorStyles.boldLabel);
            if (theme == 3)
                EditorGUILayout.HelpBox("폭풍은 질풍·번개 모티프 시안입니다. 새 구역이나 보스 편성을 추가하지 않습니다.", MessageType.Info);

            var sprite = BattleBackgrounds.Resolve(theme + 1, encounter == 1, null);
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
