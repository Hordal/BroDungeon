using UnityEngine;

namespace DeckbuilderMonsterCombat
{
    public sealed class MonsterCombatDemo : MonoBehaviour
    {
        public GameObject[] monsters;
        private MonsterCombatActor actor;
        private int index;
        private void Start() { Select(0); }
        private void Select(int next)
        {
            if(monsters==null || monsters.Length==0) return;
            if(actor!=null) Destroy(actor.gameObject);
            index=(next+monsters.Length)%monsters.Length;
            var go=Instantiate(monsters[index],new Vector3(1.1f,0,0),Quaternion.identity);
            actor=go.GetComponent<MonsterCombatActor>();
            actor.PlaySpawn();
        }
        private void OnGUI()
        {
            GUI.Box(new Rect(12,12,Mathf.Min(Screen.width-24,820),126),"");
            if(GUI.Button(new Rect(24,24,84,28),"Previous"))Select(index-1);
            if(GUI.Button(new Rect(116,24,84,28),"Next"))Select(index+1);
            if(actor==null)return;
            GUI.Label(new Rect(214,28,600,24),(index+1)+" / "+monsters.Length+"   "+actor.monsterId+"   ["+actor.attackStyle+"]");
            string[] names={"Attack","Skill","Hit","Block","Death","Spawn"};
            for(int i=0;i<names.Length;i++) if(GUI.Button(new Rect(24+i*112,68,104,32),names[i]))
            {
                switch(i){case 0:actor.PlayAttack();break;case 1:actor.PlaySkill();break;case 2:actor.PlayHit();break;case 3:actor.PlayBlock();break;case 4:actor.PlayDeath();break;case 5:actor.PlaySpawn();break;}
            }
            GUI.Label(new Rect(24,108,760,22),"Single-image motion + procedural effects. Damage hooks are animation events.");
        }
    }
}
