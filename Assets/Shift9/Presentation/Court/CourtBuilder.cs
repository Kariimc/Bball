using System.Collections.Generic;
using Shift9.Sim.Core;
using UnityEngine;

namespace Shift9.Presentation.Court
{
    /// <summary>
    /// Procedurally builds a blockout of the court from <see cref="CourtMetrics"/> +
    /// <see cref="CourtPalette"/>: floor, painted keys, boundary/center/three-point lines, and
    /// both hoop assemblies (clear backboard + white square, orange rim, net, red-padded
    /// stanchion at z≈±53). Primitive-based — good for framing the camera and validating scale;
    /// detailed meshes and floor-logo decals are a later pass.
    ///
    /// Add to an empty GameObject and press Build (or it builds on Awake in play mode).
    /// </summary>
    public sealed class CourtBuilder : MonoBehaviour
    {
        [SerializeField] private CourtPalette _palette = CourtPalette.Default;
        [SerializeField] private float _stanchionSetback = CourtMetrics.DefaultStanchionSetback;
        [SerializeField] private int _arcSegments = 48;
        [SerializeField] private float _lineWidth = 0.17f;     // ~2 inch court lines
        [SerializeField] private bool _buildOnAwake = true;

        private const float LineY = 0.02f;  // lift lines above the floor to avoid z-fighting

        private void Awake()
        {
            if (_buildOnAwake) Build();
        }

        [ContextMenu("Build Court")]
        public void Build()
        {
            Clear();
            Transform root = transform;

            BuildFloorAndPaint(root);
            BuildLines(root);
            BuildHoop(root, home: true);
            BuildHoop(root, home: false);
        }

        [ContextMenu("Clear")]
        public void Clear()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Transform child = transform.GetChild(i);
                if (Application.isPlaying) Destroy(child.gameObject);
                else DestroyImmediate(child.gameObject);
            }
        }

        // ---- Sections ----

        private void BuildFloorAndPaint(Transform root)
        {
            Quad("Floor", new Vector3(0f, 0f, 0f),
                new Vector3(SimConstants.CourtHalfWidth * 2f, SimConstants.CourtHalfLength * 2f, 1f),
                _palette.Floor, root);

            float paintW = SimConstants.PaintHalfWidth * 2f;
            for (int e = 0; e < 2; e++)
            {
                bool home = e == 0;
                Vector3 c = CourtMetrics.PaintCenter(home);
                Quad($"Paint_{(home ? "Home" : "Away")}",
                    new Vector3(c.x, 0.01f, c.z), new Vector3(paintW, SimConstants.PaintDepth, 1f),
                    _palette.Paint, root);
            }
        }

        private void BuildLines(Transform root)
        {
            Line("Boundary", CourtMetrics.BoundaryLoop(), true, root);
            Line("HalfCourt",
                new List<Vector3> { new Vector3(-SimConstants.CourtHalfWidth, 0, 0), new Vector3(SimConstants.CourtHalfWidth, 0, 0) },
                false, root);
            Line("CenterCircle", CourtMetrics.Circle(Vector3.zero, CourtMetrics.CenterCircleRadius, _arcSegments), true, root);

            for (int e = 0; e < 2; e++)
            {
                bool home = e == 0;
                string tag = home ? "Home" : "Away";
                Line($"Paint_{tag}", CourtMetrics.PaintLoop(home), true, root);
                Line($"FreeThrowCircle_{tag}",
                    CourtMetrics.Circle(new Vector3(0, 0, CourtMetrics.FreeThrowLineZ(home)), CourtMetrics.FreeThrowCircleRadius, _arcSegments),
                    true, root);
                Line($"ThreeArc_{tag}", CourtMetrics.ThreePointArc(home, _arcSegments), false, root);
                var corners = CourtMetrics.CornerThreeLines(home);
                for (int i = 0; i < corners.Length; i++)
                    Line($"ThreeCorner_{tag}_{i}", new List<Vector3> { corners[i].a, corners[i].b }, false, root);
            }
        }

        private void BuildHoop(Transform root, bool home)
        {
            string tag = home ? "Home" : "Away";
            float toCourt = -CourtMetrics.Sign(home); // +z for home points into the court
            Vector3 rim = CourtMetrics.RimCenter(home);

            // Backboard (clear glass) + white shooter's square just in front of it.
            Vector3 bb = CourtMetrics.BackboardCenter(home);
            Cube($"Backboard_{tag}", bb,
                new Vector3(CourtMetrics.BackboardWidth, CourtMetrics.BackboardHeight, 0.1f),
                _palette.BackboardGlass, root);
            Quad($"BackboardSquare_{tag}",
                new Vector3(0f, CourtMetrics.RimCenter(home).y + CourtMetrics.ShooterSquareHeight * 0.5f, bb.z + toCourt * 0.06f),
                new Vector3(CourtMetrics.ShooterSquareWidth, CourtMetrics.ShooterSquareHeight, 1f),
                _palette.BackboardSquare, root, faceZ: toCourt);

            // Rim (orange disc placeholder) + net (cone placeholder).
            Cylinder($"Rim_{tag}", new Vector3(rim.x, rim.y, rim.z),
                new Vector3(SimConstants.RimRadius * 2f, 0.04f, SimConstants.RimRadius * 2f), _palette.Rim, root);
            Cylinder($"Net_{tag}", new Vector3(rim.x, rim.y - 0.75f, rim.z),
                new Vector3(SimConstants.RimRadius * 1.7f, 0.75f, SimConstants.RimRadius * 1.7f), _palette.Net, root);

            // Stanchion: pole behind the baseline + red base pad + arm to the backboard.
            float poleZ = CourtMetrics.StanchionZ(home, _stanchionSetback);
            float poleTop = CourtMetrics.BackboardBottomY + CourtMetrics.BackboardHeight;
            Cube($"StanchionPole_{tag}", new Vector3(0f, poleTop * 0.5f, poleZ),
                new Vector3(1f, poleTop, 1f), _palette.Stanchion, root);
            Cube($"StanchionPad_{tag}", new Vector3(0f, 3.5f, poleZ - toCourt * 0.6f),
                new Vector3(3f, 7f, 1.4f), _palette.StanchionPad, root);
            float armZMid = (poleZ + bb.z) * 0.5f;
            Cube($"StanchionArm_{tag}", new Vector3(0f, poleTop, armZMid),
                new Vector3(0.6f, 0.6f, Mathf.Abs(poleZ - bb.z)), _palette.Stanchion, root);
        }

        // ---- Primitive helpers ----

        private GameObject Quad(string name, Vector3 pos, Vector3 scale, Color color, Transform parent, float faceZ = 0f)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localScale = scale;
            // A floor decal lies flat (faceZ == 0); a backboard square faces along ±z.
            go.transform.rotation = Mathf.Approximately(faceZ, 0f)
                ? Quaternion.Euler(90f, 0f, 0f)
                : Quaternion.LookRotation(new Vector3(0f, 0f, faceZ), Vector3.up);
            go.transform.localPosition = pos;
            Paint(go, color);
            return go;
        }

        private GameObject Cube(string name, Vector3 pos, Vector3 scale, Color color, Transform parent)
            => Primitive(PrimitiveType.Cube, name, pos, scale, color, parent);

        private GameObject Cylinder(string name, Vector3 pos, Vector3 scale, Color color, Transform parent)
            => Primitive(PrimitiveType.Cylinder, name, pos, scale, color, parent);

        private GameObject Primitive(PrimitiveType type, string name, Vector3 pos, Vector3 scale, Color color, Transform parent)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localScale = scale;
            go.transform.localPosition = pos;
            Paint(go, color);
            return go;
        }

        private void Line(string name, IList<Vector3> points, bool loop, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, LineY, 0f);

            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.loop = loop;
            lr.widthMultiplier = _lineWidth;
            lr.numCornerVertices = 2;
            lr.material = SolidMaterial(_palette.Line);
            lr.positionCount = points.Count;
            for (int i = 0; i < points.Count; i++) lr.SetPosition(i, points[i]);
        }

        private void Paint(GameObject go, Color color)
        {
            var r = go.GetComponent<Renderer>();
            if (r != null) r.sharedMaterial = MaterialUtil.Solid(color);
        }

        private static Material SolidMaterial(Color color) => MaterialUtil.Solid(color);
    }
}
