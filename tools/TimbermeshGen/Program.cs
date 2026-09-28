using System.Globalization;
using System.IO.Compression;
using System.Numerics;
using System.Text;
using ProtoBuf;
using TimbermeshGen.Models;
using TimbermeshGen.Timbermesh;
using Timberborn.TimbermeshDTO;

// Offline tool for the Cable Power Transfer mod's models. Reads with the game's own DTO (Timberborn.TimbermeshDTO + the game's
// protobuf-net), so anything it writes is checked against exactly what the game will parse.
//
//   station <outDir>                         build the Power Transfer Station models (main + 4 stubs), verify them
//   info <file.timbermesh>                   nodes, per-material bounds, animation angles and spin axes
//   dump <file.timbermesh> [maxVerts]        raw nodes and vertex properties
//   obj <out.obj> <frame> <file>...          Blender-space OBJ of one or more models (Z up), animated nodes posed at frame

CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

switch (args.FirstOrDefault())
{
    case "station" when args.Length >= 2:
        WriteAndVerify(PowerTransferStationModel.Build(), Path.Combine(args[1], "PowerTransferStation.Folktails.Model.timbermesh"));
        foreach ((string face, Vector3 outward) in PowerTransferStationModel.StubFaces)
        {
            WriteAndVerify(PowerTransferStationModel.BuildStub(face, outward),
                Path.Combine(args[1], $"PowerTransferStation.Folktails.Stub{face}.Model.timbermesh"));
        }
        break;
    case "info" when args.Length >= 2:
        Info(args[1]);
        break;
    case "dump" when args.Length >= 2:
        Dump(args[1], args.Length > 2 ? int.Parse(args[2]) : 4);
        break;
    case "obj" when args.Length >= 4:
        ExportObj(args.Skip(3).ToArray(), args[1], int.Parse(args[2]));
        break;
    default:
        Console.WriteLine("usage: station <outDir> | info <file> | dump <file> [maxVerts] | obj <out.obj> <frame> <file>...");
        Environment.ExitCode = 1;
        break;
}

static void WriteAndVerify(TmModel model, string path)
{
    TimbermeshFile.Write(model, path);
    Info(path);
}

static Model Read(string path)
{
    using FileStream fs = File.OpenRead(path);
    if (fs.ReadByte() != 0x78 || fs.ReadByte() != 0x9C)
    {
        throw new Exception("Incorrect Zlib compression file header");
    }
    using DeflateStream ds = new DeflateStream(fs, CompressionMode.Decompress);
    return Serializer.Deserialize<Model>(ds);
}

static void Info(string path)
{
    Model m = Read(path);
    Console.WriteLine($"{Path.GetFileName(path)}: {m.Nodes.Length} nodes, {new FileInfo(path).Length} bytes");
    foreach (Node n in m.Nodes)
    {
        Console.WriteLine($"node '{n.Name}' parent={n.Parent} pos={V(n.Position)} rot={Q(n.Rotation)} verts={n.VertexCount} " +
                          $"props=[{string.Join(",", n.VertexProperties.Select(p => p.Name))}]");
        VertexProperty pos = n.VertexProperties.FirstOrDefault(p => p.Name == "position");
        if (pos?.Data != null)
        {
            foreach (Timberborn.TimbermeshDTO.Mesh mesh in n.Meshes)
            {
                float[] mn = { 1e9f, 1e9f, 1e9f }, mx = { -1e9f, -1e9f, -1e9f };
                foreach (int i in mesh.Indices)
                {
                    for (int d = 0; d < 3; d++)
                    {
                        float f = BitConverter.ToSingle(pos.Data, (i * 3 + d) * 4);
                        mn[d] = Math.Min(mn[d], f);
                        mx[d] = Math.Max(mx[d], f);
                    }
                }
                Console.WriteLine($"   {mesh.Material,-32} tris={mesh.Indices.Count / 3,4} min=({mn[0]:0.###},{mn[1]:0.###},{mn[2]:0.###}) " +
                                  $"max=({mx[0]:0.###},{mx[1]:0.###},{mx[2]:0.###})");
            }
        }
        foreach (Timberborn.TimbermeshDTO.NodeAnimation a in n.NodeAnimations)
        {
            QuaternionFloat q0 = a.Frames[0].Rotation;
            QuaternionFloat qLast = a.Frames[^1].Rotation;
            double wrapDot = q0.X * qLast.X + q0.Y * qLast.Y + q0.Z * qLast.Z + q0.W * qLast.W;
            IEnumerable<string> angles = a.Frames.Where((_, i) => i % Math.Max(1, a.Frames.Count / 8) == 0).Select(f =>
            {
                QuaternionFloat q = f.Rotation;
                double dot = Math.Abs(q.X * q0.X + q.Y * q0.Y + q.Z * q0.Z + q.W * q0.W);
                return (2 * Math.Acos(Math.Min(1, dot)) * 180 / Math.PI).ToString("0");
            });
            // Frame pairs (i -> i+1, wrapping) whose quaternions lie in opposite hemispheres.
            IEnumerable<int> flips = Enumerable.Range(0, a.Frames.Count).Where(i =>
            {
                QuaternionFloat p = a.Frames[i].Rotation, q = a.Frames[(i + 1) % a.Frames.Count].Rotation;
                return p.X * q.X + p.Y * q.Y + p.Z * q.Z + p.W * q.W < 0;
            });
            Console.WriteLine($"   anim '{a.Name}' {a.Frames.Count} frames @ {a.Framerate} fps ({a.Length:0.##} s), " +
                              $"angle vs frame 0: {string.Join(" ", angles)}; dot(last, first)={wrapDot:0.###}; " +
                              $"sign flips after frames [{string.Join(",", flips)}]");
            // Rotation axis from frame 0 to frame 1 (direction of spin, right-hand rule on the quaternion).
            if (a.Frames.Count > 1)
            {
                QuaternionFloat f0 = a.Frames[0].Rotation, f1 = a.Frames[1].Rotation;
                Quaternion step = new Quaternion(f1.X, f1.Y, f1.Z, f1.W) * Quaternion.Inverse(new Quaternion(f0.X, f0.Y, f0.Z, f0.W));
                Vector3 axis = Vector3.Normalize(new Vector3(step.X, step.Y, step.Z) * MathF.Sign(step.W == 0 ? 1 : step.W));
                Console.WriteLine($"   spin axis per frame: ({axis.X:0.##},{axis.Y:0.##},{axis.Z:0.##})");
            }
        }
    }
}

static void Dump(string path, int maxVerts)
{
    Model m = Read(path);
    for (int i = 0; i < m.Nodes.Length; i++)
    {
        Node n = m.Nodes[i];
        Console.WriteLine($"[{i}] '{n.Name}' parent={n.Parent} pos={V(n.Position)} rot={Q(n.Rotation)} scale={V(n.Scale)} verts={n.VertexCount}");
        foreach (VertexProperty p in n.VertexProperties)
        {
            StringBuilder line = new StringBuilder($"    prop '{p.Name}' {p.ScalarType}x{p.ScalarTypeDimension} bytes={p.Data?.Length ?? 0}:");
            for (int v = 0; v < Math.Min(maxVerts, n.VertexCount); v++)
            {
                IEnumerable<string> values = Enumerable.Range(0, p.ScalarTypeDimension)
                    .Select(d => BitConverter.ToSingle(p.Data, (v * p.ScalarTypeDimension + d) * 4).ToString("0.###"));
                line.Append(" (").Append(string.Join(",", values)).Append(')');
            }
            Console.WriteLine(line);
        }
        foreach (Timberborn.TimbermeshDTO.Mesh mesh in n.Meshes)
        {
            Console.WriteLine($"    mesh material='{mesh.Material}' indices={mesh.Indices.Count} first=[{string.Join(",", mesh.Indices.Take(9))}]");
        }
        foreach (Timberborn.TimbermeshDTO.NodeAnimation a in n.NodeAnimations)
        {
            Console.WriteLine($"    nodeAnim '{a.Name}' fps={a.Framerate} frames={a.Frames.Count} f0={V(a.Frames[0].Position)} {Q(a.Frames[0].Rotation)} {V(a.Frames[0].Scale)}");
        }
    }
}

// Unity (Y up, left-handed) -> Blender (Z up, right-handed): (x, y, z) -> (-x, -z, y), the Timbermesh plugin's
// convention (vanilla example models sit at negative Blender X/Y). The mirror flips winding, so faces are reversed.
static void ExportObj(string[] paths, string objPath, int frame)
{
    StringBuilder obj = new StringBuilder();
    int vertexBase = 1;
    foreach (string path in paths)
    {
        vertexBase = AppendObj(Read(path), obj, vertexBase, frame);
    }
    File.WriteAllText(objPath, obj.ToString());
    Console.WriteLine($"wrote {objPath}");
}

static int AppendObj(Model m, StringBuilder obj, int vertexBase, int frame)
{
    Matrix4x4[] world = new Matrix4x4[m.Nodes.Length];
    for (int i = 0; i < m.Nodes.Length; i++)
    {
        Node n = m.Nodes[i];
        QuaternionFloat rotation = n.NodeAnimations.Count > 0
            ? n.NodeAnimations[0].Frames[frame % n.NodeAnimations[0].Frames.Count].Rotation
            : n.Rotation;
        Matrix4x4 local = Matrix4x4.CreateScale(n.Scale.X, n.Scale.Y, n.Scale.Z)
                          * Matrix4x4.CreateFromQuaternion(new Quaternion(rotation.X, rotation.Y, rotation.Z, rotation.W))
                          * Matrix4x4.CreateTranslation(n.Position.X, n.Position.Y, n.Position.Z);
        world[i] = n.Parent >= 0 ? local * world[n.Parent] : local;
        if (n.VertexCount == 0)
        {
            continue;
        }
        VertexProperty pos = n.VertexProperties.First(p => p.Name == "position");
        VertexProperty nor = n.VertexProperties.First(p => p.Name == "normal");
        VertexProperty uv = n.VertexProperties.First(p => p.Name == "uv0");
        obj.AppendLine($"o {n.Name.TrimStart('#')}");
        for (int v = 0; v < n.VertexCount; v++)
        {
            Vector3 p = Vector3.Transform(ReadVector3(pos, v), world[i]);
            Vector3 q = Vector3.Normalize(Vector3.TransformNormal(ReadVector3(nor, v), world[i]));
            obj.AppendLine($"v {-p.X:0.#####} {-p.Z:0.#####} {p.Y:0.#####}");
            obj.AppendLine($"vn {-q.X:0.#####} {-q.Z:0.#####} {q.Y:0.#####}");
            obj.AppendLine($"vt {BitConverter.ToSingle(uv.Data, v * 8):0.#####} {BitConverter.ToSingle(uv.Data, v * 8 + 4):0.#####}");
        }
        foreach (Timberborn.TimbermeshDTO.Mesh mesh in n.Meshes)
        {
            obj.AppendLine($"usemtl {mesh.Material}");
            for (int t = 0; t < mesh.Indices.Count; t += 3)
            {
                int a = mesh.Indices[t] + vertexBase, b = mesh.Indices[t + 1] + vertexBase, c = mesh.Indices[t + 2] + vertexBase;
                obj.AppendLine($"f {a}/{a}/{a} {c}/{c}/{c} {b}/{b}/{b}");
            }
        }
        vertexBase += n.VertexCount;
    }
    return vertexBase;
}

static Vector3 ReadVector3(VertexProperty p, int v) =>
    new Vector3(BitConverter.ToSingle(p.Data, v * 12), BitConverter.ToSingle(p.Data, v * 12 + 4), BitConverter.ToSingle(p.Data, v * 12 + 8));

static string V(Vector3Float v) => v == null ? "null" : $"({v.X:0.###},{v.Y:0.###},{v.Z:0.###})";

static string Q(QuaternionFloat q) => q == null ? "null" : $"({q.X:0.###},{q.Y:0.###},{q.Z:0.###},{q.W:0.###})";
