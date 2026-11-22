using UnityEngine;

namespace DeckbuilderMonsterCombat
{
    // Procedural lines are generated at runtime; no external VFX packages needed.
    public sealed class MonsterCombatFx : MonoBehaviour
    {
        private LineRenderer[] lines;
        private Material material;
        private string style, element;
        private float height, elapsed;
        private bool skill, playing;
        private const float Duration = 0.48f;

        private void EnsureLines()
        {
            if (lines != null) return;
            material = new Material(Shader.Find("Sprites/Default"));
            material.hideFlags = HideFlags.DontSave;
            lines = new LineRenderer[7];
            for (int i=0;i<lines.Length;i++)
            {
                var child = new GameObject("CombatFX_"+i);
                child.transform.SetParent(transform, false);
                var line=child.AddComponent<LineRenderer>();
                line.sharedMaterial=material;
                line.useWorldSpace=false;
                line.sortingOrder=20;
                line.numCapVertices=4;
                line.numCornerVertices=3;
                line.enabled=false;
                lines[i]=line;
            }
        }
        public void PlayEffect(string attack, string arena, float bodyHeight, bool special)
        {
            style=attack; element=arena; height=bodyHeight; skill=special;
            elapsed=0f; playing=true; EnsureLines(); Draw(0f);
        }
        private void Update()
        {
            if (!playing) return;
            elapsed+=Time.deltaTime;
            if (elapsed>=Duration) { Clear(); return; }
            Draw(elapsed/Duration);
        }
        public void PreviewAt(string attack, string arena, float bodyHeight, bool special, float age)
        {
            style=attack; element=arena; height=bodyHeight; skill=special;
            EnsureLines();
            if (age<0f || age>=Duration) { Clear(); return; }
            Draw(age/Duration);
        }
        public void Clear()
        {
            playing=false;
            if (lines!=null) foreach(var line in lines) if(line!=null) line.enabled=false;
        }
        private void Stroke(int index, Vector3[] points, Color color, float width)
        {
            var line=lines[index]; line.enabled=true; line.positionCount=points.Length;
            line.SetPositions(points); line.startColor=color; line.endColor=color;
            line.startWidth=width; line.endWidth=width*.28f;
        }
        private void Draw(float t)
        {
            foreach(var line in lines) line.enabled=false;
            Color color=element=="fire" ? new Color(1f,.43f,.12f) : element=="frost" ? new Color(.5f,.86f,1f) : new Color(.74f,.52f,1f);
            color.a=(1f-t)*(skill?.95f:.8f);
            float k=height/2.4f;
            Vector3 origin=new Vector3(-.38f*k,height*.55f,0);
            bool ring=style=="Pulse" || style=="Bell" || style=="Spin" || (skill && style!="Dragon");
            if (ring)
            {
                for(int r=0;r<(skill?3:2);r++)
                {
                    var points=new Vector3[33];
                    float radius=(.25f+t*(skill?1.7f:1.2f)+r*.14f)*k;
                    for(int j=0;j<points.Length;j++) { float a=j*Mathf.PI*2/32; points[j]=origin+new Vector3(Mathf.Cos(a)*radius,Mathf.Sin(a)*radius*(style=="Bell"?.65f:1f),0); }
                    Stroke(r,points,color,.045f*k);
                }
            }
            else if(style=="Dragon")
            {
                origin=new Vector3(-height*.38f,height*.62f,0);
                for(int r=0;r<5;r++)
                {
                    var points=new Vector3[9];
                    for(int j=0;j<points.Length;j++)
                    {
                        float p=j/8f, length=(skill?3.1f:2f)*k*(.35f+.65f*t);
                        points[j]=origin+new Vector3(-p*length,(r-2)*p*.21f*k+Mathf.Sin(j*1.8f-t*15+r)*.045f*k,0);
                    }
                    Stroke(r,points,color,(skill?.14f:.09f)*k*(1-t*.5f));
                }
            }
            else if(style=="Cast" || style=="Fly" || style=="Sting")
            {
                Vector3 center=origin+Vector3.left*(.25f+t*2.5f)*k;
                var orb=new Vector3[17];
                for(int j=0;j<orb.Length;j++){float a=j*Mathf.PI*2/16;orb[j]=center+new Vector3(Mathf.Cos(a),Mathf.Sin(a),0)*.1f*k;}
                Stroke(0,orb,color,.075f*k);
                Stroke(1,new[]{center+Vector3.right*.55f*k,center},color,.045f*k);
            }
            else
            {
                for(int r=0;r<(style=="Tentacle"?3:2);r++)
                {
                    var points=new Vector3[15];
                    for(int j=0;j<points.Length;j++)
                    {
                        float p=j/14f;
                        points[j]=origin+new Vector3(-(.3f+p*.85f+t*.2f)*k,Mathf.Sin(p*Mathf.PI)*.5f*k-.25f*k+r*.14f*k,0);
                    }
                    Stroke(r,points,color,.065f*k);
                }
            }
        }
        private void OnDisable() { Clear(); }
        private void OnDestroy()
        {
            if(material!=null) { if(Application.isPlaying) Destroy(material); else DestroyImmediate(material); }
        }
    }
}
