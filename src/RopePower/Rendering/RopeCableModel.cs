using System;
using System.Collections.Generic;
using Timberborn.BaseComponentSystem;
using Timberborn.Coordinates;
using Timberborn.Rendering;
using Timberborn.SelectionSystem;
using Timberborn.ZiplineSystem;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace RopePower.Rendering
{
    /// <summary>
    /// A rope loop: two strands on opposite sides of the pulleys, each a chain of vanilla zipline cable pieces following a
    /// parabolic sag (close to a catenary for slight sag). Reusing the cable template keeps the game's material, highlight,
    /// greyscale and the shader's _IsOperative motion. Per-piece placement copied from
    /// Timberborn.ZiplineSystem.ZiplineCableModel.UpdateModel (1.1.2.4).
    /// </summary>
    public class RopeCableModel
    {
        private static readonly int LengthId = Shader.PropertyToID("_Length");
        private static readonly int IsOperativeId = Shader.PropertyToID("_IsOperative");

        private readonly struct Piece
        {
            public readonly Cable Cable;
            public readonly MeshRenderer Renderer;

            public Piece(Cable cable, MeshRenderer renderer)
            {
                Cable = cable;
                Renderer = renderer;
            }
        }

        private readonly MaterialColorer _materialColorer;
        private readonly Highlighter _highlighter;
        private readonly RopeRendererSpec _spec;
        private readonly List<Piece> _firstStrand = new List<Piece>();
        private readonly List<Piece> _secondStrand = new List<Piece>();
        private readonly Vector3[] _points;

        /// <param name="createPiece">Instantiates one vanilla zipline cable template.</param>
        public RopeCableModel(MaterialColorer materialColorer, Highlighter highlighter, RopeRendererSpec spec,
                              Func<GameObject> createPiece)
        {
            _materialColorer = materialColorer;
            _highlighter = highlighter;
            _spec = spec;
            for (int i = 0; i < spec.SegmentsPerStrand; i++)
            {
                _firstStrand.Add(CreatePiece(createPiece()));
                _secondStrand.Add(CreatePiece(createPiece()));
            }
            _points = new Vector3[spec.SegmentsPerStrand + 1];
        }

        public bool IsOperative { get; private set; }

        /// <summary>Anchors are pulley centres in grid space (Z up); strands run <paramref name="pulleyRadius"/> to either side.</summary>
        public void Update(Vector3 startAnchor, Vector3 endAnchor, float pulleyRadius)
        {
            Vector3 start = CoordinateSystem.GridToWorld(startAnchor);
            Vector3 end = CoordinateSystem.GridToWorld(endAnchor);
            Vector3 horizontal = Vector3.ProjectOnPlane(end - start, Vector3.up);
            Vector3 side = horizontal.sqrMagnitude > 1e-6f
                ? Vector3.Cross(Vector3.up, horizontal.normalized) * pulleyRadius
                : Vector3.right * pulleyRadius;
            float sag = Mathf.Min((end - start).magnitude * _spec.SagPerLength, _spec.MaxSag);
            // The second strand runs end -> start on the other side, so the pair forms a loop (and scrolls opposite ways).
            PlaceStrand(start + side, end + side, sag, _firstStrand);
            PlaceStrand(end - side, start - side, sag, _secondStrand);
        }

        public void SetVisible(bool visible)
        {
            ForEachPiece(piece => piece.Cable.GameObject.SetActive(visible));
        }

        public void SetGreyscale(bool greyscale)
        {
            ForEachPiece(piece =>
            {
                if (greyscale)
                {
                    _materialColorer.EnableGrayscale(piece.Cable.GameObject);
                }
                else
                {
                    _materialColorer.DisableGrayscale(piece.Cable.GameObject);
                }
            });
        }

        /// <summary>Moving rope when powered (the zipline cable shader's _IsOperative).</summary>
        public void SetOperative(bool operative)
        {
            IsOperative = operative;
            ForEachPiece(piece => piece.Renderer.material.SetFloat(IsOperativeId, operative ? 1f : 0f));
        }

        /// <summary>Shadow only when either station is hidden by the level visibility slider, as ziplines do.</summary>
        public void SetShadowOnly(bool shadowOnly)
        {
            ShadowCastingMode mode = shadowOnly ? ShadowCastingMode.ShadowsOnly : ShadowCastingMode.On;
            ForEachPiece(piece => piece.Renderer.shadowCastingMode = mode);
        }

        public void Highlight(Color color)
        {
            ForEachPiece(piece => _highlighter.HighlightPrimary(piece.Cable, color));
        }

        public void Unhighlight()
        {
            ForEachPiece(piece => _highlighter.UnhighlightPrimary(piece.Cable));
        }

        public void Destroy()
        {
            ForEachPiece(piece => Object.Destroy(piece.Cable.GameObject));
            _firstStrand.Clear();
            _secondStrand.Clear();
        }

        private static Piece CreatePiece(GameObject root)
        {
            return new Piece(root.GetComponentSlow<Cable>(), root.GetComponentInChildren<MeshRenderer>());
        }

        private void PlaceStrand(Vector3 from, Vector3 to, float sag, List<Piece> strand)
        {
            int count = strand.Count;
            for (int i = 0; i <= count; i++)
            {
                float t = (float)i / count;
                _points[i] = Vector3.Lerp(from, to, t) + Vector3.down * (sag * 4f * t * (1f - t));
            }
            for (int i = 0; i < count; i++)
            {
                Vector3 span = _points[i + 1] - _points[i];
                float length = span.magnitude;
                Transform transform = strand[i].Cable.GameObject.transform;
                transform.position = _points[i] + 0.5f * span;
                transform.rotation = Quaternion.LookRotation(span.normalized, Vector3.up);
                transform.localScale = new Vector3(1f, 1f, length);
                Material material = strand[i].Renderer.material;
                material.SetFloat(LengthId, length);
                material.SetFloat(IsOperativeId, IsOperative ? 1f : 0f);
            }
        }

        private void ForEachPiece(Action<Piece> action)
        {
            foreach (Piece piece in _firstStrand)
            {
                action(piece);
            }
            foreach (Piece piece in _secondStrand)
            {
                action(piece);
            }
        }
    }
}
