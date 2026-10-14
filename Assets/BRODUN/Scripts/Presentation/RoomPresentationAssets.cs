using UnityEngine;
namespace Brodun.Presentation
{
    [CreateAssetMenu(menuName="BRODUN/Room Presentation")]
    public sealed class RoomPresentationAssets : ScriptableObject
    {
        public Sprite shop,altar,rest;
        public Sprite Background(RoomNode node)=>node.Event==EventKind.Shop?shop:node.Event==EventKind.Altar?altar:node.Event==EventKind.RestSite?rest:null;
    }
}
