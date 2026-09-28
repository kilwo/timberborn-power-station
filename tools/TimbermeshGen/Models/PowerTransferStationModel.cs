using System.Numerics;
using TimbermeshGen.Geometry;
using TimbermeshGen.Timbermesh;
using static TimbermeshGen.Models.Atlas;

namespace TimbermeshGen.Models
{
    /// <summary>
    /// The Folktails Power Transfer Station: a gearbox base with shaft stubs on all four sides, a slender wooden
    /// trestle, and a horizontal rope pulley on top. Unity model space: the 1x1x3 footprint spans X/Z 0..1, Y 0..3,
    /// origin at the block corner. See docs/station-model-spec.md.
    /// </summary>
    public static class PowerTransferStationModel
    {
        // Must match PowerTransferStationSpec in the blueprint (RopeAnchorPoint, PulleyRadius).
        public static readonly Vector3 PulleyCentre = new Vector3(0.5f, 2.85f, 0.5f);
        public const float RopeRadius = 0.175f;

        // Vanilla shaft axle: centre height 0.5, 0.22 square, face to face (measured from ClutchEngaged/PowerMeter).
        private static readonly Vector3 AxleCentre = new Vector3(0.5f, 0.5f, 0.5f);
        private const float AxleHalf = 0.11f;

        // Animation: one turn per loop. 96 frames at 24 fps = 4 s, so the pulley rim (r 0.175) moves at 0.275 blocks/s,
        // matching the zipline cable shader's scroll (_Speed 1.5 / _Density 5.5 = 0.273 blocks/s).
        private const int Frames = 96;
        private const float Framerate = 24f;

        private const float DeckTop = 0.09f;
        private const float HousingHalf = 0.21f;
        private const float HousingTop = 0.74f;
        private const float LidHalf = 0.23f;
        private const float LidTop = 0.8f;
        private const float PostBottomOffset = 0.33f;
        private const float PostTopOffset = 0.1f;
        private const float PostTop = 2.72f;
        private const float PostWidth = 0.075f;
        private const float PlatformHalf = 0.15f;
        private const float PlatformBottom = 2.7f;
        private const float PlatformTop = 2.77f;

        public static TmModel Build()
        {
            TmNode root = new TmNode { Name = "PowerTransferStation.Folktails.Model", Parent = -1 };
            BuildStatic().WriteTo(root);

            TmNode pulley = AnimatedNode("#Pulley", PulleyCentre, Vector3.UnitY, -1f);
            BuildPulley().WriteTo(pulley);

            TmNode axleX = AnimatedNode("#AxleX", AxleCentre, Vector3.UnitX, 1f);
            BuildAxle(Matrix4x4.Identity).WriteTo(axleX);

            TmNode axleZ = AnimatedNode("#AxleZ", AxleCentre, Vector3.UnitZ, 1f);
            BuildAxle(Matrix4x4.CreateRotationY(MathF.PI / 2)).WriteTo(axleZ);

            return new TmModel { Name = "", Nodes = new[] { root, pulley, axleX, axleZ } };
        }

        private static MeshBuilder BuildStatic()
        {
            MeshBuilder mesh = new MeshBuilder();

            // Deck: five planks running along X.
            const float deckMin = 0.08f, deckMax = 0.92f, gap = 0.01f;
            float plankWidth = (deckMax - deckMin - 4 * gap) / 5;
            for (int i = 0; i < 5; i++)
            {
                float z0 = deckMin + i * (plankWidth + gap);
                int plank = i;
                mesh.Box(Brown, new Vector3(deckMin, 0, z0), new Vector3(deckMax, DeckTop, z0 + plankWidth), _ => Plank8(plank * 3 + 1),
                    skipBottom: true);
            }

            // Gearbox housing, lid, and a metal band near the bottom.
            mesh.Box(Brown, new Vector3(0.5f - HousingHalf, DeckTop, 0.5f - HousingHalf),
                new Vector3(0.5f + HousingHalf, HousingTop, 0.5f + HousingHalf), f => LongPlank(f), skipBottom: true);
            mesh.Box(LightBrown, new Vector3(0.5f - LidHalf, HousingTop, 0.5f - LidHalf), new Vector3(0.5f + LidHalf, LidTop, 0.5f + LidHalf),
                f => Plank8(f + 2));
            const float bandOut = HousingHalf + 0.012f;
            mesh.Box(Metal, new Vector3(0.5f - bandOut, 0.14f, 0.5f - bandOut), new Vector3(0.5f + bandOut, 0.18f, 0.5f + bandOut),
                _ => MetalSlice, skipBottom: true);

            // Bearing collars where the axles leave the housing (octagonal metal rings).
            Vector2[] collar = { new Vector2(0.12f, 0), new Vector2(0.17f, 0), new Vector2(0.17f, 0.03f), new Vector2(0.12f, 0.03f) };
            foreach (Vector3 outward in new[] { Vector3.UnitX, -Vector3.UnitX, Vector3.UnitZ, -Vector3.UnitZ })
            {
                MeshBuilder ring = new MeshBuilder();
                ring.Revolve(Vector3.Zero, collar, 8, _ => RevolveBand.Wrapped(Metal, PlainMetal, smooth: false), MathF.PI / 8);
                mesh.Append(ring, AlignY(outward, AxleCentre + outward * HousingHalf));
            }

            // Trestle: four tapered corner posts from the deck to the top platform.
            Vector3[] corners = { new Vector3(-1, 0, -1), new Vector3(1, 0, -1), new Vector3(1, 0, 1), new Vector3(-1, 0, 1) };
            for (int i = 0; i < 4; i++)
            {
                Vector3 bottom = PostPoint(corners[i], DeckTop);
                Vector3 top = PostPoint(corners[i], PostTop);
                int plank = i;
                mesh.Beam(Brown, bottom, top, PostWidth, PostWidth, Vector3.UnitX, _ => LongPlank(plank));
            }

            // Rungs on each face, and diagonal braces between them.
            float[] rungs = { 1.05f, 1.7f, 2.3f };
            for (int level = 0; level < rungs.Length; level++)
            {
                for (int i = 0; i < 4; i++)
                {
                    Vector3 a = PostPoint(corners[i], rungs[level]);
                    Vector3 b = PostPoint(corners[(i + 1) % 4], rungs[level]);
                    int plank = level + i;
                    mesh.Beam(LightBrown, a, b, 0.05f, 0.05f, Vector3.UnitY, _ => Plank8(plank));
                }
            }
            for (int level = 0; level < rungs.Length - 1; level++)
            {
                for (int i = 0; i < 4; i++)
                {
                    bool rising = (level + i) % 2 == 0;
                    Vector3 a = PostPoint(corners[i], rising ? rungs[level] : rungs[level + 1]);
                    Vector3 b = PostPoint(corners[(i + 1) % 4], rising ? rungs[level + 1] : rungs[level]);
                    // Set the brace slightly outside the post centre line so it reads in front of the drive shaft.
                    Vector3 faceOut = Vector3.Normalize(new Vector3((a + b).X / 2 - 0.5f, 0, (a + b).Z / 2 - 0.5f)) * 0.01f;
                    int plank = level * 4 + i;
                    mesh.Beam(Brown, a + faceOut, b + faceOut, 0.04f, 0.035f, Vector3.UnitY, _ => Plank8(plank + 5));
                }
            }

            // Top platform carrying the pulley, and a bearing where the drive shaft leaves the lid.
            mesh.Box(LightBrown, new Vector3(0.5f - PlatformHalf, PlatformBottom, 0.5f - PlatformHalf),
                new Vector3(0.5f + PlatformHalf, PlatformTop, 0.5f + PlatformHalf), f => Plank8(f + 1));
            Vector2[] bearing = { new Vector2(0.03f, 0), new Vector2(0.085f, 0), new Vector2(0.085f, 0.035f), new Vector2(0.03f, 0.035f) };
            mesh.Revolve(new Vector3(0.5f, LidTop, 0.5f), bearing, 8, _ => RevolveBand.Wrapped(Metal, PlainMetal, smooth: false),
                MathF.PI / 8);
            mesh.Revolve(new Vector3(0.5f, PlatformTop, 0.5f), bearing, 8, _ => RevolveBand.Wrapped(Metal, PlainMetal, smooth: false),
                MathF.PI / 8);
            return mesh;
        }

        /// <summary>Pulley wheel, hub, straps and drive shaft, in the #Pulley node's frame (origin at the pulley centre).</summary>
        private static MeshBuilder BuildPulley()
        {
            MeshBuilder mesh = new MeshBuilder();

            // Wheel profile, counter-clockwise around the cross-section: bottom face, lower rim, V groove (rope centre
            // at r = 0.175, rope radius ~0.018), upper rim, top face.
            Vector2[] wheel =
            {
                new Vector2(0.05f, -0.06f), new Vector2(0.215f, -0.06f), new Vector2(0.215f, -0.03f), new Vector2(0.158f, -0.008f),
                new Vector2(0.158f, 0.008f), new Vector2(0.215f, 0.03f), new Vector2(0.215f, 0.06f), new Vector2(0.05f, 0.06f)
            };
            mesh.Revolve(Vector3.Zero, wheel, 24, e => e switch
            {
                0 or 6 => RevolveBand.Planar(Brown, EndGrain),
                1 or 5 => RevolveBand.Wrapped(Brown, BarkSlice),
                _ => RevolveBand.Wrapped(Metal, MetalSlice)
            });

            // Octagonal hub cap with a ring-plate top.
            Vector2[] hub = { new Vector2(0.065f, 0.055f), new Vector2(0.065f, 0.1f), new Vector2(0, 0.1f) };
            mesh.Revolve(Vector3.Zero, hub, 8, e => e == 0
                ? RevolveBand.Wrapped(Metal, PlainMetal, smooth: false)
                : RevolveBand.Planar(Metal, RingPlate), MathF.PI / 8);

            // Four yellow straps across the top face, so the spin is visible from the game camera.
            for (int i = 0; i < 4; i++)
            {
                MeshBuilder strap = new MeshBuilder();
                strap.Box(Painted, new Vector3(0.06f, 0.058f, -0.0175f), new Vector3(0.205f, 0.072f, 0.0175f), _ => YellowStripe,
                    skipBottom: true);
                mesh.Append(strap, Matrix4x4.CreateRotationY(i * MathF.PI / 2));
            }

            // Drive shaft down to the gearbox lid; it turns with the pulley.
            Vector2[] shaft = { new Vector2(0.045f, LidTop - PulleyCentre.Y), new Vector2(0.045f, -0.06f) };
            mesh.Revolve(Vector3.Zero, shaft, 8, _ => RevolveBand.Wrapped(White, LongPlank(2), smooth: false), MathF.PI / 8);
            return mesh;
        }

        /// <summary>A face-to-face axle along local X (0.22 square), with metal bands on the visible stubs.</summary>
        private static MeshBuilder BuildAxle(Matrix4x4 orientation)
        {
            MeshBuilder axle = new MeshBuilder();
            axle.Box(White, new Vector3(-0.5f, -AxleHalf, -AxleHalf), new Vector3(0.5f, AxleHalf, AxleHalf), f => LongPlank(f + 1));
            const float band = AxleHalf + 0.012f;
            foreach (float x in new[] { -0.4f, 0.4f })
            {
                axle.Box(Metal, new Vector3(x - 0.02f, -band, -band), new Vector3(x + 0.02f, band, band), _ => MetalSlice);
            }
            MeshBuilder oriented = new MeshBuilder();
            oriented.Append(axle, orientation);
            return oriented;
        }

        private static TmNode AnimatedNode(string name, Vector3 pivot, Vector3 axis, float direction)
        {
            TmNode node = new TmNode { Name = name, Parent = 0, Position = TmVector3.From(pivot) };
            TmNodeAnimation animation = new TmNodeAnimation { Name = "Default", Framerate = Framerate };
            // One full turn per loop; direction +1/-1 is the sign of the angle about the axis. The pulley uses -1:
            // with RopeCableModel's strands (side = Cross(up, towardsPartner)) and the cable shader scrolling towards
            // each piece's far end, rope leaves every station on +side and arrives on -side, so the rim moves that way.
            for (int i = 0; i < Frames; i++)
            {
                float angle = direction * 2 * MathF.PI * i / Frames;
                animation.Frames.Add(new TmNodeAnimationFrame
                {
                    Position = TmVector3.From(pivot),
                    Rotation = TmQuaternion.From(Quaternion.CreateFromAxisAngle(axis, angle)),
                    Scale = new TmVector3 { X = 1, Y = 1, Z = 1 }
                });
            }
            node.NodeAnimations.Add(animation);
            return node;
        }

        private static Vector3 PostPoint(Vector3 corner, float y)
        {
            float t = (y - DeckTop) / (PostTop - DeckTop);
            float offset = PostBottomOffset + (PostTopOffset - PostBottomOffset) * t;
            return new Vector3(0.5f + corner.X * offset, y, 0.5f + corner.Z * offset);
        }

        /// <summary>Rotation taking +Y to <paramref name="direction"/>, then translation (System.Numerics row vectors).</summary>
        private static Matrix4x4 AlignY(Vector3 direction, Vector3 position)
        {
            Vector3 y = Vector3.Normalize(direction);
            Vector3 x = Vector3.Normalize(Vector3.Cross(y, MathF.Abs(y.Y) < 0.9f ? Vector3.UnitY : Vector3.UnitX));
            Vector3 z = Vector3.Cross(x, y);
            return new Matrix4x4(
                x.X, x.Y, x.Z, 0,
                y.X, y.Y, y.Z, 0,
                z.X, z.Y, z.Z, 0,
                position.X, position.Y, position.Z, 1);
        }
    }
}
