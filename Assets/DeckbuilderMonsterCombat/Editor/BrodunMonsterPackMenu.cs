using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DeckbuilderMonsterCombat.Editor
{
    public static class BrodunMonsterPackMenu
    {
        [MenuItem("BRODUN/Monsters/Open 57 Monster Motion Demo")]
        public static void OpenDemo()
        {
            if(EditorApplication.isPlaying){Debug.LogWarning("Stop Play Mode before opening the monster demo.");return;}
            if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
            EditorSceneManager.OpenScene(MonsterCombatBuilder.Root+"/Scenes/MonsterCombatDemo.unity");
        }

        [MenuItem("BRODUN/Monsters/Validate 57 Monster Motion Pack")]
        public static void Validate(){MonsterCombatBuilder.Validate();}

        public static void BatchValidate()
        {
            try{Validate();EditorApplication.Exit(0);}
            catch(Exception error){Debug.LogException(error);EditorApplication.Exit(1);}
        }
    }
}
