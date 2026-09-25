using Timberborn.BaseComponentSystem;
using Timberborn.Coordinates;
using Timberborn.Rendering;
using Timberborn.SelectionSystem;
using Timberborn.ZiplineSystem;
using UnityEngine;

namespace RopePower.Rendering
{
    /// <summary>
    /// A rope loop drawn as two straight strands on opposite sides of the line between the pulleys.
    /// Interim visual (Phase 4) reusing the vanilla zipline cable model; placement copied from
    /// Timberborn.ZiplineSystem.ZiplineCableModel.UpdateModel (1.1.2.4). Phase 5 replaces it with a sagging, animated rope.
    /// </summary>
    public class RopeCableModel
    {
        private static readonly int LengthId = Shader.PropertyToID("_Length");
        private static readonly int IsOperativeId = Shader.PropertyToID("_IsOperative");

        private readonly MaterialColorer _materialColorer;
        private readonly Highlighter _highlighter;
        private readonly Cable _first;
        private readonly Cable _second;
        private readonly MeshRenderer _firstRenderer;
        private readonly MeshRenderer _secondRenderer;

        public RopeCableModel(MaterialColorer materialColorer, Highlighter highlighter, GameObject firstStrand, GameObject secondStrand)
        {
            _materialColorer = materialColorer;
            _highlighter = highlighter;
            _first = firstStrand.GetComponentSlow<Cable>();
            _second = secondStrand.GetComponentSlow<Cable>();
            _firstRenderer = firstStrand.GetComponentInChildren<MeshRenderer>();
            _secondRenderer = secondStrand.GetComponentInChildren<MeshRenderer>();
        }

        /// <summary>Anchors are in grid space (Z up), as PowerTransferStation.RopeAnchorPoint.</summary>
        public void Update(Vector3 startAnchor, Vector3 endAnchor)
        {
            Vector3 start = CoordinateSystem.GridToWorld(startAnchor);
            Vector3 end = CoordinateSystem.GridToWorld(endAnchor);
            PlaceStrand(start, end, _first.GameObject, _firstRenderer);
            PlaceStrand(end, start, _second.GameObject, _secondRenderer);
        }

        public void SetVisible(bool visible)
        {
            _first.GameObject.SetActive(visible);
            _second.GameObject.SetActive(visible);
        }

        public void SetGreyscale(bool greyscale)
        {
            if (greyscale)
            {
                _materialColorer.EnableGrayscale(_first.GameObject);
                _materialColorer.EnableGrayscale(_second.GameObject);
            }
            else
            {
                _materialColorer.DisableGrayscale(_first.GameObject);
                _materialColorer.DisableGrayscale(_second.GameObject);
            }
        }

        public void Highlight(Color color)
        {
            _highlighter.HighlightPrimary(_first, color);
            _highlighter.HighlightPrimary(_second, color);
        }

        public void Unhighlight()
        {
            _highlighter.UnhighlightPrimary(_first);
            _highlighter.UnhighlightPrimary(_second);
        }

        public void Destroy()
        {
            Object.Destroy(_first.GameObject);
            Object.Destroy(_second.GameObject);
        }

        private static void PlaceStrand(Vector3 start, Vector3 end, GameObject strand, MeshRenderer meshRenderer)
        {
            // Offsets each strand to its right-hand side, so the two strands form a loop.
            (Vector3 from, Vector3 to) = ZiplineCalculator.CalculateWorldConnections(start, end);
            Vector3 span = to - from;
            float length = span.magnitude;
            strand.transform.position = from + 0.5f * span;
            strand.transform.rotation = Quaternion.LookRotation(span.normalized, Vector3.up);
            strand.transform.localScale = new Vector3(1f, 1f, length);
            meshRenderer.material.SetFloat(LengthId, length);
            meshRenderer.material.SetFloat(IsOperativeId, 0f);
        }
    }
}
