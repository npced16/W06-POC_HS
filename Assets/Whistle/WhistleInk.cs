using UnityEngine;
using UnityEngine.UI;

namespace WhistlePOC
{
    // HUD framing and trails over the first person 3D camera.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class WhistleInk : MaskableGraphic
    {
        public WhistleGame Game;
        VertexHelper mesh;
        Color paper = new Color(.83f, .83f, .83f), ink = new Color(.065f, .065f, .065f), accent = new Color(.95f, .95f, .95f);
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); mesh = vh; if (Game == null) return;
            bool light = Game.Revealed;
            Box(800, 812, 1600, 176, ink);
            Box(800, 87, 1600, 174, ink);
            Line(new Vector2(80, 724), new Vector2(1520, 724), 1, paper * .35f);
            Line(new Vector2(80, 174), new Vector2(1520, 174), 1, paper * .35f);
            if (Game.HitFlash > .25f) Box(800, 450, 1600, 900, new Color(accent.r, accent.g, accent.b, Game.HitFlash * .12f));
            if (light)
            {
                Ring(Game.Cursor, 13, 1.5f, accent);
                Line(Game.Cursor + new Vector2(-20, 0), Game.Cursor + new Vector2(20, 0), 1, accent);
                Line(Game.Cursor + new Vector2(0, -20), Game.Cursor + new Vector2(0, 20), 1, accent);
            }
        }
        void Box(float x, float y, float w, float h, Color c) { Quad(new Vector2(x-w/2,y-h/2),new Vector2(x-w/2,y+h/2),new Vector2(x+w/2,y+h/2),new Vector2(x+w/2,y-h/2),c); }
        void Line(Vector2 a, Vector2 b, float width, Color c) { var n = new Vector2(-(b-a).y,(b-a).x).normalized * width/2; Quad(a-n,a+n,b+n,b-n,c); }
        void Ring(Vector2 center, float r, float width, Color c) { for(int i=0;i<32;i++) { float a=i*Mathf.PI/16,b=(i+1)*Mathf.PI/16; Line(center+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*r,center+new Vector2(Mathf.Cos(b),Mathf.Sin(b))*r,width,c); } }
        void Disc(Vector2 center, float r, Color c) { for(int i=0;i<8;i++) { float a=i*Mathf.PI/4,b=(i+1)*Mathf.PI/4; Tri(center,center+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*r,center+new Vector2(Mathf.Cos(b),Mathf.Sin(b))*r,c); } }
        void Quad(Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color color) { int i=mesh.currentVertCount; Vert(a,color); Vert(b,color); Vert(c,color); Vert(d,color); mesh.AddTriangle(i,i+1,i+2); mesh.AddTriangle(i,i+2,i+3); }
        void Tri(Vector2 a, Vector2 b, Vector2 c, Color color) { int i=mesh.currentVertCount; Vert(a,color); Vert(b,color); Vert(c,color); mesh.AddTriangle(i,i+1,i+2); }
        void Vert(Vector2 p, Color color) { mesh.AddVert(p-new Vector2(800,450),color,Vector2.zero); }
    }
}




