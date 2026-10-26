using UnityEngine;

namespace Fishing.Game
{
    /// <summary>Bounded damped line spans with a smooth, laterally free hole crossing; never drives a collider.</summary>
    internal sealed class FishingLineView
    {
        private const int AirSegments = 10, WaterSegments = 40;
        private const float HoleClearance = .35f;
        private readonly LineRenderer _line;
        private readonly Span _air = new Span(), _water = new Span();
        private readonly Vector3[] _airPoints = new Vector3[AirSegments+1];
        private readonly Vector3[] _waterPoints = new Vector3[AirSegments+WaterSegments+1];
        private bool _throughHole;
        private float _time;

        public FishingLineView(LineRenderer line) { _line = line; }
        public void Reset() { _time = 0; _air.Reset(); _water.Reset(); }

        public void Draw(Vector3 start, Vector3 guide, Vector3 end, bool throughHole, float dt)
        {
            if (_throughHole != throughHole) { _air.Reset(); _water.Reset(); _throughHole = throughHole; }
            if (throughHole)
            {
                // The hole is an opening, not a pulley fixed at its centre. Only constrain an unsafe crossing.
                float crossing = Mathf.Clamp01((start.y - guide.y) / (start.y - end.y));
                guide.x = Mathf.Clamp(Mathf.Lerp(start.x, end.x, crossing), guide.x - HoleClearance, guide.x + HoleClearance);
            }
            if (dt > 0)
            {
                _time += dt;
                _air.Tick(start,throughHole ? guide : end,false,_time,dt);
                if (throughHole) _water.Tick(guide,end,true,_time,dt);
            }
            var points=throughHole ? _waterPoints : _airPoints;
            Vector3 airDelta=(throughHole ? guide : end)-start, waterDelta=end-guide;
            Vector3 tangent=throughHole ? (airDelta.normalized+waterDelta.normalized).normalized : airDelta.normalized;
            DrawSpan(_air,start,throughHole ? guide : end,airDelta,throughHole ? JoinTangent(tangent,airDelta) : airDelta,
                0,AirSegments,points,false);
            if (throughHole) DrawSpan(_water,guide,end,JoinTangent(tangent,waterDelta),waterDelta,
                AirSegments,WaterSegments,points,true);
            _line.positionCount=points.Length; _line.SetPositions(points);
        }

        private static Vector3 JoinTangent(Vector3 direction, Vector3 delta) => direction *
            Mathf.Min(delta.magnitude, 2 * Mathf.Abs(delta.y) / Mathf.Max(.001f, Mathf.Abs(direction.y)));

        private static void DrawSpan(Span span, Vector3 start, Vector3 end, Vector3 startTangent, Vector3 endTangent,
            int first, int segments, Vector3[] points, bool water)
        {
            for (int i=0; i<=segments; i++)
            {
                float t=(float)i/segments;
                // Concentrate the existing samples around the join, where a shallow line bends most.
                t=water ? t*t : 1-(1-t)*(1-t);
                float t2=t*t,t3=t2*t;
                // Shared tangent directions and zero-slope bow offsets remove the former hard corner.
                points[first+i]=(2*t3-3*t2+1)*start+(t3-2*t2+t)*startTangent+
                    (-2*t3+3*t2)*end+(t3-t2)*endTangent+(Vector3)span.Offset*(16*t2*(1-t)*(1-t));
            }
        }

        private sealed class Span
        {
            public Vector2 Offset { get; private set; }
            private Vector2 _velocity;
            private Vector3 _start, _end;
            private bool _initialized;
            public void Reset() { Offset=Vector2.zero; _velocity=Vector2.zero; _initialized=false; }
            public void Tick(Vector3 start, Vector3 end, bool water, float time, float dt)
            {
                Vector2 translation=_initialized ? (Vector2)((start-_start+end-_end)*(.5f/dt)) : Vector2.zero;
                float retrieval=_initialized ? Mathf.Max(0,(end.y-_end.y)/dt) : 0;
                _start=start; _end=end; _initialized=true;
                translation=Vector2.ClampMagnitude(translation,35);
                float frequency=water ? 4.5f+Mathf.Min(3,retrieval*.3f) : 3.5f;
                Vector2 force=water ? new Vector2(.35f*Mathf.Sin(time*.8f+end.y*.08f),0)-translation :
                    new Vector2(.1f*Mathf.Sin(time*.7f),-.95f)-translation*.35f;
                float maxOffset=Mathf.Min(water ? .45f : .24f,Vector3.Distance(start,end)*(water ? .035f : .06f));
                // Small explicit steps keep large acceptance dt values stable; pause never enters this path.
                float elapsed=Mathf.Min(dt,.1f);
                int steps=Mathf.Max(1,Mathf.CeilToInt(elapsed/(1f/120)));
                float step=elapsed/steps;
                for (int i=0; i<steps; i++)
                {
                    _velocity+=(force-Offset*(frequency*frequency)-_velocity*(2*frequency*(water ? .88f : .78f)))*step;
                    _velocity=Vector2.ClampMagnitude(_velocity,2);
                    Offset=Vector2.ClampMagnitude(Offset+_velocity*step,maxOffset);
                }
            }
        }
    }
}
