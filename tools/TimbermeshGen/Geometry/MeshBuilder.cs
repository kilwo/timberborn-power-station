using System.Numerics;
using TimbermeshGen.Timbermesh;

namespace TimbermeshGen.Geometry
{
    /// <summary>A rectangle in an UberAtlas texture, in Unity UV space (origin bottom-left).</summary>
    public readonly record struct UvRect(float U0, float V0, float U1, float V1)
    {
        public Vector2 At(float s, float t) => new Vector2(U0 + (U1 - U0) * s, V0 + (V1 - V0) * t);
    }

    /// <summary>A circle in an atlas texture, for planar-mapped discs (end grain, round plates).</summary>
    public readonly record struct UvCircle(float U, float V, float Radius);

    /// <summary>
    /// Builds one Timbermesh node's mesh in Unity model space (Y up, left-handed). Triangles are wound so that
    /// cross(b - a, c - a) points along the outward normal, which is Unity's front face (checked against vanilla
    /// models). Submeshes are grouped by material name.
    /// </summary>
    public sealed class MeshBuilder
    {
        private readonly List<Vector3> _positions = new List<Vector3>();
        private readonly List<Vector3> _normals = new List<Vector3>();
        private readonly List<Vector2> _uvs = new List<Vector2>();
        private readonly List<(string Material, List<int> Indices)> _submeshes = new List<(string, List<int>)>();

        public int VertexCount => _positions.Count;

        public IEnumerable<Vector3> Positions => _positions;

        public int Vertex(Vector3 position, Vector3 normal, Vector2 uv)
        {
            _positions.Add(position);
            _normals.Add(Vector3.Normalize(normal));
            _uvs.Add(uv);
            return _positions.Count - 1;
        }

        public void Triangle(string material, int a, int b, int c)
        {
            Vector3 face = Vector3.Cross(_positions[b] - _positions[a], _positions[c] - _positions[a]);
            if (face.LengthSquared() < 1e-14f)
            {
                return;
            }
            Vector3 normal = _normals[a] + _normals[b] + _normals[c];
            List<int> indices = Submesh(material);
            if (Vector3.Dot(face, normal) >= 0)
            {
                indices.AddRange(new[] { a, b, c });
            }
            else
            {
                indices.AddRange(new[] { a, c, b });
            }
        }

        /// <summary>A planar quad; corners in loop order, normal pointing away from <paramref name="inside"/>.</summary>
        public void Quad(string material, Vector3[] corners, Vector2[] uvs, Vector3 inside)
        {
            Vector3 normal = Vector3.Normalize(Vector3.Cross(corners[1] - corners[0], corners[3] - corners[0]));
            Vector3 centre = (corners[0] + corners[1] + corners[2] + corners[3]) / 4f;
            if (Vector3.Dot(normal, centre - inside) < 0)
            {
                normal = -normal;
            }
            int i0 = Vertex(corners[0], normal, uvs[0]);
            int i1 = Vertex(corners[1], normal, uvs[1]);
            int i2 = Vertex(corners[2], normal, uvs[2]);
            int i3 = Vertex(corners[3], normal, uvs[3]);
            Triangle(material, i0, i1, i2);
            Triangle(material, i0, i2, i3);
        }

        /// <summary>
        /// A convex six-sided solid. Corner index bits: 1 = +a, 2 = +b, 4 = +c for the solid's three local axes.
        /// Each face gets a whole atlas rect, with V running along the face's longer edge (the wood grain).
        /// </summary>
        public void Hexahedron(string material, Vector3[] corners, Func<int, UvRect> uvForFace, bool skipBottom = false)
        {
            int[][] faces =
            {
                new[] { 0, 2, 6, 4 }, new[] { 1, 3, 7, 5 }, // -a, +a
                new[] { 0, 1, 5, 4 }, new[] { 2, 3, 7, 6 }, // -b, +b
                new[] { 0, 1, 3, 2 }, new[] { 4, 5, 7, 6 }  // -c, +c
            };
            Vector3 inside = Vector3.Zero;
            foreach (Vector3 corner in corners)
            {
                inside += corner / 8f;
            }
            for (int f = 0; f < 6; f++)
            {
                Vector3[] quad = faces[f].Select(i => corners[i]).ToArray();
                Vector3 centre = (quad[0] + quad[1] + quad[2] + quad[3]) / 4f;
                if (skipBottom && Vector3.Normalize(centre - inside).Y < -0.9f)
                {
                    continue;
                }
                Quad(material, quad, FaceUvs(quad, uvForFace(f)), inside);
            }
        }

        public void Box(string material, Vector3 min, Vector3 max, Func<int, UvRect> uvForFace, bool skipBottom = false)
        {
            Vector3[] corners = new Vector3[8];
            for (int i = 0; i < 8; i++)
            {
                corners[i] = new Vector3((i & 1) != 0 ? max.X : min.X, (i & 2) != 0 ? max.Y : min.Y, (i & 4) != 0 ? max.Z : min.Z);
            }
            Hexahedron(material, corners, uvForFace, skipBottom);
        }

        /// <summary>A square-section beam from <paramref name="from"/> to <paramref name="to"/>.</summary>
        public void Beam(string material, Vector3 from, Vector3 to, float width, float height, Vector3 up, Func<int, UvRect> uvForFace)
        {
            Vector3 axis = Vector3.Normalize(to - from);
            Vector3 side = Vector3.Cross(up, axis);
            if (side.LengthSquared() < 1e-6f)
            {
                side = Vector3.Cross(Vector3.UnitX, axis);
            }
            side = Vector3.Normalize(side);
            Vector3 realUp = Vector3.Cross(axis, side);
            Vector3[] corners = new Vector3[8];
            for (int i = 0; i < 8; i++)
            {
                Vector3 end = (i & 1) != 0 ? to : from;
                corners[i] = end + realUp * ((i & 2) != 0 ? height / 2 : -height / 2) + side * ((i & 4) != 0 ? width / 2 : -width / 2);
            }
            Hexahedron(material, corners, uvForFace);
        }

        /// <summary>
        /// A surface of revolution about the vertical axis through <paramref name="centre"/>. The profile is (radius, y)
        /// points traversed counter-clockwise around the solid's cross-section (bottom outwards, up the outside,
        /// top inwards), so the outward normal is on the right of each edge.
        /// </summary>
        public void Revolve(Vector3 centre, IReadOnlyList<Vector2> profile, int segments, Func<int, RevolveBand> bandForEdge,
            float angleOffset = 0f)
        {
            for (int e = 0; e < profile.Count - 1; e++)
            {
                RevolveBand band = bandForEdge(e);
                if (band.Material == null)
                {
                    continue;
                }
                Vector2 p0 = profile[e];
                Vector2 p1 = profile[e + 1];
                Vector2 edge = p1 - p0;
                // Right-hand normal of the edge in (radius, y) space.
                Vector2 profileNormal = Vector2.Normalize(new Vector2(edge.Y, -edge.X));
                float rMax = profile.Max(p => p.X);
                if (band.Smooth)
                {
                    int[] ring0 = new int[segments + 1];
                    int[] ring1 = new int[segments + 1];
                    for (int s = 0; s <= segments; s++)
                    {
                        float angle = angleOffset + 2 * MathF.PI * s / segments;
                        Vector3 dir = new Vector3(MathF.Cos(angle), 0, MathF.Sin(angle));
                        Vector3 normal = dir * profileNormal.X + Vector3.UnitY * profileNormal.Y;
                        Vector3 v0 = centre + dir * p0.X + Vector3.UnitY * p0.Y;
                        Vector3 v1 = centre + dir * p1.X + Vector3.UnitY * p1.Y;
                        ring0[s] = Vertex(v0, normal, band.Uv(dir, p0.X, rMax, (float)s / segments, 0));
                        ring1[s] = Vertex(v1, normal, band.Uv(dir, p1.X, rMax, (float)s / segments, 1));
                    }
                    for (int s = 0; s < segments; s++)
                    {
                        Triangle(band.Material, ring0[s], ring0[s + 1], ring1[s + 1]);
                        Triangle(band.Material, ring0[s], ring1[s + 1], ring1[s]);
                    }
                }
                else
                {
                    for (int s = 0; s < segments; s++)
                    {
                        float a0 = angleOffset + 2 * MathF.PI * s / segments;
                        float a1 = angleOffset + 2 * MathF.PI * (s + 1) / segments;
                        float mid = (a0 + a1) / 2;
                        Vector3 d0 = new Vector3(MathF.Cos(a0), 0, MathF.Sin(a0));
                        Vector3 d1 = new Vector3(MathF.Cos(a1), 0, MathF.Sin(a1));
                        Vector3 dm = new Vector3(MathF.Cos(mid), 0, MathF.Sin(mid));
                        Vector3 normal = dm * profileNormal.X + Vector3.UnitY * profileNormal.Y;
                        // Each flat facet gets the whole U range, like a plank.
                        int i00 = Vertex(centre + d0 * p0.X + Vector3.UnitY * p0.Y, normal, band.Uv(d0, p0.X, rMax, 0, 0));
                        int i10 = Vertex(centre + d1 * p0.X + Vector3.UnitY * p0.Y, normal, band.Uv(d1, p0.X, rMax, 1, 0));
                        int i01 = Vertex(centre + d0 * p1.X + Vector3.UnitY * p1.Y, normal, band.Uv(d0, p1.X, rMax, 0, 1));
                        int i11 = Vertex(centre + d1 * p1.X + Vector3.UnitY * p1.Y, normal, band.Uv(d1, p1.X, rMax, 1, 1));
                        Triangle(band.Material, i00, i10, i11);
                        Triangle(band.Material, i00, i11, i01);
                    }
                }
            }
        }

        /// <summary>Appends another builder's geometry, transformed (used to build parts in a local frame).</summary>
        public void Append(MeshBuilder other, Matrix4x4 transform)
        {
            int offset = _positions.Count;
            for (int i = 0; i < other._positions.Count; i++)
            {
                _positions.Add(Vector3.Transform(other._positions[i], transform));
                _normals.Add(Vector3.Normalize(Vector3.TransformNormal(other._normals[i], transform)));
                _uvs.Add(other._uvs[i]);
            }
            foreach ((string material, List<int> indices) in other._submeshes)
            {
                Submesh(material).AddRange(indices.Select(i => i + offset));
            }
        }

        public void WriteTo(TmNode node)
        {
            Vector4[] tangents = ComputeTangents();
            node.VertexCount = _positions.Count;
            node.VertexProperties.Add(TmVertexProperty.Floats("position", 3, _positions.SelectMany(p => new[] { p.X, p.Y, p.Z })));
            node.VertexProperties.Add(TmVertexProperty.Floats("normal", 3, _normals.SelectMany(n => new[] { n.X, n.Y, n.Z })));
            node.VertexProperties.Add(TmVertexProperty.Floats("tangent", 4, tangents.SelectMany(t => new[] { t.X, t.Y, t.Z, t.W })));
            // Vertex colour multiplies albedo in the vanilla shaders; white = unshaded, as most vanilla wood parts.
            node.VertexProperties.Add(TmVertexProperty.Floats("color", 4, _positions.SelectMany(_ => new[] { 1f, 1f, 1f, 1f })));
            node.VertexProperties.Add(TmVertexProperty.Floats("uv0", 2, _uvs.SelectMany(uv => new[] { uv.X, uv.Y })));
            foreach ((string material, List<int> indices) in _submeshes)
            {
                node.Meshes.Add(new TmMesh { Material = material, Indices = indices.ToList() });
            }
        }

        public IEnumerable<(string Material, IReadOnlyList<int> Indices)> Submeshes =>
            _submeshes.Select(s => (s.Material, (IReadOnlyList<int>)s.Indices));

        public IReadOnlyList<Vector3> Normals => _normals;

        public IReadOnlyList<Vector2> Uvs => _uvs;

        private List<int> Submesh(string material)
        {
            foreach ((string name, List<int> indices) in _submeshes)
            {
                if (name == material)
                {
                    return indices;
                }
            }
            List<int> list = new List<int>();
            _submeshes.Add((material, list));
            return list;
        }

        // Quad corners -> atlas rect, with V along the longer edge.
        private static Vector2[] FaceUvs(Vector3[] quad, UvRect rect)
        {
            float a = Vector3.Distance(quad[0], quad[1]);
            float b = Vector3.Distance(quad[0], quad[3]);
            return a >= b
                ? new[] { rect.At(0, 0), rect.At(0, 1), rect.At(1, 1), rect.At(1, 0) }
                : new[] { rect.At(0, 0), rect.At(1, 0), rect.At(1, 1), rect.At(0, 1) };
        }

        // Per-vertex tangents from UV derivatives. Unity: binormal = cross(normal, tangent.xyz) * tangent.w.
        private Vector4[] ComputeTangents()
        {
            Vector3[] tan = new Vector3[_positions.Count];
            Vector3[] bitan = new Vector3[_positions.Count];
            foreach ((string _, List<int> indices) in _submeshes)
            {
                for (int i = 0; i < indices.Count; i += 3)
                {
                    int a = indices[i], b = indices[i + 1], c = indices[i + 2];
                    Vector3 e1 = _positions[b] - _positions[a], e2 = _positions[c] - _positions[a];
                    Vector2 d1 = _uvs[b] - _uvs[a], d2 = _uvs[c] - _uvs[a];
                    float det = d1.X * d2.Y - d2.X * d1.Y;
                    if (MathF.Abs(det) < 1e-12f)
                    {
                        continue;
                    }
                    float r = 1f / det;
                    Vector3 t = (e1 * d2.Y - e2 * d1.Y) * r;
                    Vector3 bt = (e2 * d1.X - e1 * d2.X) * r;
                    foreach (int v in new[] { a, b, c })
                    {
                        tan[v] += t;
                        bitan[v] += bt;
                    }
                }
            }
            Vector4[] result = new Vector4[_positions.Count];
            for (int v = 0; v < result.Length; v++)
            {
                Vector3 n = _normals[v];
                Vector3 t = tan[v] - n * Vector3.Dot(n, tan[v]);
                if (t.LengthSquared() < 1e-12f)
                {
                    t = Vector3.Cross(n, MathF.Abs(n.Y) < 0.9f ? Vector3.UnitY : Vector3.UnitX);
                }
                t = Vector3.Normalize(t);
                float w = Vector3.Dot(Vector3.Cross(n, t), bitan[v]) < 0 ? -1f : 1f;
                result[v] = new Vector4(t, w);
            }
            return result;
        }
    }

    /// <summary>Material and UV mapping for one band of a <see cref="MeshBuilder.Revolve"/> profile.</summary>
    public sealed class RevolveBand
    {
        private readonly UvRect _wrap;
        private readonly UvCircle? _planar;

        private RevolveBand(string material, bool smooth, UvRect wrap, UvCircle? planar)
        {
            Material = material;
            Smooth = smooth;
            _wrap = wrap;
            _planar = planar;
        }

        public string Material { get; }

        public bool Smooth { get; }

        public static RevolveBand None { get; } = new RevolveBand(null, true, default, null);

        /// <summary>U runs around the circumference, V along the band.</summary>
        public static RevolveBand Wrapped(string material, UvRect rect, bool smooth = true) => new RevolveBand(material, smooth, rect, null);

        /// <summary>Top-down projection onto a circle in the atlas (for flat caps).</summary>
        public static RevolveBand Planar(string material, UvCircle circle) => new RevolveBand(material, true, default, circle);

        public Vector2 Uv(Vector3 dir, float radius, float maxRadius, float around, float along)
        {
            if (_planar is UvCircle c)
            {
                float k = radius / maxRadius * c.Radius;
                return new Vector2(c.U + dir.X * k, c.V + dir.Z * k);
            }
            return _wrap.At(around, along);
        }
    }
}
