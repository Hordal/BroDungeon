using System.Collections.Generic;
using System;
using System.Linq;
using Brodun.Flow;
using UnityEngine;
using UnityEngine.UI;
namespace Brodun.Presentation
{
    public sealed class MapPathsGraphic : MaskableGraphic
    {
        private FlowSession session;
        private Func<RoomNode,Vector2> placement;
        public void Bind(FlowSession value,Func<RoomNode,Vector2> positions=null){session=value;placement=positions;raycastTarget=false;SetVerticesDirty();}
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();if(session?.Run==null)return;
            var run=session.Run;
            foreach(var node in run.Map)foreach(string id in node.Next)
            {
                var next=DungeonRuntime.Node(run,id);
                var a=Position(node);var b=Position(next);var d=(b-a).normalized;
                bool available=node.Id==run.CurrentNode && DungeonRuntime.Available(run).Contains(id);
                bool visited=(node.Type==RoomType.Start||run.CompletedNodes.Contains(node.Id)) && (run.CompletedNodes.Contains(id)||run.CurrentNode==id);
                Color tint=available?new Color(.96f,.78f,.38f,1):visited?new Color(.25f,.78f,.67f,.95f):new Color(.48f,.55f,.56f,.8f);
                var perpendicular=new Vector2(-d.y,d.x)*(available||visited?3.2f:2.1f);
                a+=d*68;b-=d*68;int start=vh.currentVertCount;
                vh.AddVert(a-perpendicular,tint,Vector2.zero);vh.AddVert(a+perpendicular,tint,Vector2.zero);
                vh.AddVert(b+perpendicular,tint,Vector2.zero);vh.AddVert(b-perpendicular,tint,Vector2.zero);
                vh.AddTriangle(start,start+1,start+2);vh.AddTriangle(start+2,start+3,start);
            }
        }
        private Vector2 Position(RoomNode node) => placement!=null?placement(node):
            new Vector2((float)(node.X/100)*rectTransform.rect.width,(float)(1-node.Y/100)*rectTransform.rect.height)+rectTransform.rect.min;
    }
}
