using UnityEngine;
using UnityEngine.UI;

namespace WhistlePOC
{
    [RequireComponent(typeof(CanvasRenderer), typeof(PoolMember))]
    public sealed class PooledSwordGraphic : MaskableGraphic
    {
        public enum EffectKind { Slash, DragTrail, PerfectRing }
        public EffectKind kind;
        public float lifetime = .45f;
        public float slashWidth = 12;
        public Color glowColor = Color.white;
        public WhistleGame Game;
        public WhistleGame.Cut Cut;
        VertexHelper mesh;
        public void ClearBinding() { Game = null; Cut = null; SetVerticesDirty(); }
        void Update() { if (Game != null) SetVerticesDirty(); }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); mesh = vh; if (Game == null) return;
            if (kind == EffectKind.PerfectRing)
            {
                float flash = Game.PerfectFlash;
                Ring(new Vector2(800, 450), 65 + (1 - flash) * 95, 2, Tint(flash * .7f)); return;
            }
            if (kind == EffectKind.DragTrail)
            {
                for (int i = 1; i < Game.SwordTrail.Count; i++)
                {
                    var a = Game.SwordTrail[i - 1]; var b = Game.SwordTrail[i]; if (b.start) continue;
                    float life = Mathf.Clamp01(1 - (Game.Clock - b.born) / .22f); float width = Mathf.Lerp(1, 6, life);
                    Line(a.position, b.position, width * 2.6f, Tint(life * .13f));
                    Line(a.position, b.position, width, Tint(life * .8f));
                    Line(a.position, b.position, width * .32f, Tint(life)); Ring(b.position, width * .35f, width * .3f, Tint(life * .65f));
                }
                return;
            }
            if (Cut == null || Cut.age < 0) return;
            float alpha = Mathf.Clamp01(1 - Cut.age / lifetime);
            if (Cut.sweep)
            {
                var tip = Vector2.Lerp(Cut.from, Cut.to, Mathf.Clamp01(Cut.age / .07f));
                float width = Mathf.Lerp(slashWidth, 2, Mathf.Clamp01(Cut.age / lifetime));
                Line(Cut.from, tip, width * 3, Tint(alpha * .12f)); Line(Cut.from, tip, width, Tint(alpha * .85f)); Line(Cut.from, tip, width * .3f, Tint(alpha));
            }
            for (int i = 0; Cut.hit && i < 7; i++)
            {
                var d = new Vector2(Mathf.Cos(i * 2.3f), Mathf.Sin(i * 2.3f));
                Line(Cut.to + d * (15 + Cut.age * 70), Cut.to + d * (30 + Cut.age * 110), 2, Tint(alpha));
            }
        }
        Color Tint(float alpha) { return new Color(glowColor.r, glowColor.g, glowColor.b, alpha * glowColor.a); }
        void Line(Vector2 a, Vector2 b, float width, Color c)
        {
            var n = new Vector2(-(b-a).y,(b-a).x).normalized * width/2; int i=mesh.currentVertCount;
            mesh.AddVert(a-n-new Vector2(800,450),c,Vector2.zero); mesh.AddVert(a+n-new Vector2(800,450),c,Vector2.zero);
            mesh.AddVert(b+n-new Vector2(800,450),c,Vector2.zero); mesh.AddVert(b-n-new Vector2(800,450),c,Vector2.zero);
            mesh.AddTriangle(i,i+1,i+2); mesh.AddTriangle(i,i+2,i+3);
        }
        void Ring(Vector2 center, float radius, float width, Color c)
        {
            for(int i=0;i<32;i++) { float a=i*Mathf.PI/16,b=(i+1)*Mathf.PI/16; Line(center+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius,center+new Vector2(Mathf.Cos(b),Mathf.Sin(b))*radius,width,c); }
        }
    }
}
