using UnityEngine;
using UnityEngine.UI;

// Bundled vector-like UI artwork: independent of platform emoji fonts.
public sealed class ReactionGraphic : MaskableGraphic
{
    public string Kind = "like";
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Color gold=new Color32(255,201,67,255), ink=new Color32(32,52,65,255);
        void Poly(Color c, params Vector2[] points)
        {
            int start=vh.currentVertCount;
            foreach(var p in points) vh.AddVert(new Vector3(rectTransform.rect.center.x+p.x*rectTransform.rect.width/100,rectTransform.rect.center.y+p.y*rectTransform.rect.height/100),c,Vector2.zero);
            for(int i=1;i<points.Length-1;i++)vh.AddTriangle(start,start+i,start+i+1);
        }
        void Box(float x,float y,float w,float h,Color c) => Poly(c,new Vector2(x-w/2,y-h/2),new Vector2(x+w/2,y-h/2),new Vector2(x+w/2,y+h/2),new Vector2(x-w/2,y+h/2));
        void Circle(float x,float y,float r,Color c)
        {
            var points=new Vector2[26];points[0]=new Vector2(x,y);
            for(int i=0;i<25;i++){float a=i*Mathf.PI*2/24;points[i+1]=new Vector2(x+Mathf.Cos(a)*r,y+Mathf.Sin(a)*r);}
            Poly(c,points);
        }
        if(Kind=="love")
        {
            Color pink=new Color32(248,100,137,255);
            Circle(-17,15,23,pink);Circle(17,15,23,pink);
            Poly(pink,new Vector2(-38,4),new Vector2(38,4),new Vector2(0,-43));
        }
        else if(Kind=="question")
        {
            Circle(0,0,43,new Color32(116,209,242,255));
            Poly(ink,new Vector2(-21,22),new Vector2(-13,31),new Vector2(14,31),new Vector2(24,20),new Vector2(24,8),new Vector2(6,-6),new Vector2(6,-14),new Vector2(-5,-14),new Vector2(-5,0),new Vector2(12,14),new Vector2(12,19),new Vector2(7,22));
            Circle(0,-27,6,ink);
        }
        else if(Kind=="clap")
        {
            Poly(new Color32(237,162,56,255),new Vector2(-34,-30),new Vector2(-43,2),new Vector2(-14,35),new Vector2(0,27),new Vector2(13,-12),new Vector2(-8,-37));
            Poly(gold,new Vector2(-6,-37),new Vector2(-15,5),new Vector2(13,38),new Vector2(27,31),new Vector2(40,-9),new Vector2(19,-38));
            Box(-38,37,4,13,Color.white);Box(0,44,4,10,Color.white);Box(41,32,4,13,Color.white);
        }
        else
        {
            Box(-29,-14,17,42,new Color32(76,163,241,255));
            Box(5,-8,45,45,gold);
            Poly(gold,new Vector2(-14,12),new Vector2(-17,38),new Vector2(-8,45),new Vector2(2,38),new Vector2(0,9));
            for(int i=0;i<4;i++){Box(21,11-i*12,26,10,gold);Circle(34,11-i*12,5,gold);}
            Circle(-29,-26,3,Color.white);
        }
    }
    public void SetKind(string kind){Kind=kind;SetVerticesDirty();}
}
