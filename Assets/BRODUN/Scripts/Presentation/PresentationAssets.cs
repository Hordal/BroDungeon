using UnityEngine;
using UnityEngine.InputSystem;
namespace Brodun.Presentation
{
    [CreateAssetMenu(menuName="BRODUN/Presentation Assets")]
    public sealed class PresentationAssets : ScriptableObject
    {
        public Font font;
        public Font logoFont;
        public InputActionAsset inputActions;
        public Sprite titleBackground,mapBackground;
        public Sprite warrior,mage,priest;
        public Sprite buttonNormal,buttonHover,buttonPressed,buttonDisabled;
        public Sprite nodeNormal,nodeHover,nodeSelected,nodeLocked;
        public Sprite battle,boss,puzzle,roomEvent,shop,altar,rest,current,completed;
        public Sprite Portrait(CharacterId id) => id==CharacterId.Warrior?warrior:id==CharacterId.Mage?mage:priest;
        public Sprite RoomIcon(RoomNode n)
        {
            if(n.Type==RoomType.Boss)return boss;
            if(n.Type==RoomType.Puzzle)return puzzle;
            if(n.Type==RoomType.Event)return n.Event==EventKind.None?roomEvent:n.Event==EventKind.Shop?shop:n.Event==EventKind.Altar?altar:rest;
            return battle;
        }
    }
}
