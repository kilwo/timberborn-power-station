using System.IO.Compression;
using ProtoBuf;

namespace TimbermeshGen.Timbermesh
{
    // Writable mirror of Timberborn.TimbermeshDTO (game 1.1.2.4). The game's DTO classes have get-only
    // properties, so we serialize these instead; member numbers and types must match the game's exactly.
    // Every written file is read back with the game's own DTO (see Program.Verify).

    [ProtoContract]
    public class TmModel
    {
        [ProtoMember(1)] public int Version { get; set; }
        [ProtoMember(2)] public string Name { get; set; }
        [ProtoMember(3)] public TmNode[] Nodes { get; set; }
    }

    [ProtoContract]
    public class TmNode
    {
        [ProtoMember(1)] public int Parent { get; set; } = -1;
        [ProtoMember(2)] public string Name { get; set; }
        [ProtoMember(3)] public TmVector3 Position { get; set; } = new TmVector3();
        [ProtoMember(4)] public TmQuaternion Rotation { get; set; } = new TmQuaternion { W = 1 };
        [ProtoMember(5)] public TmVector3 Scale { get; set; } = new TmVector3 { X = 1, Y = 1, Z = 1 };
        [ProtoMember(6)] public int VertexCount { get; set; }
        [ProtoMember(7)] public List<TmVertexProperty> VertexProperties { get; set; } = new List<TmVertexProperty>();
        [ProtoMember(8)] public List<TmMesh> Meshes { get; set; } = new List<TmMesh>();
        [ProtoMember(10)] public List<TmNodeAnimation> NodeAnimations { get; set; } = new List<TmNodeAnimation>();
    }

    [ProtoContract]
    public class TmMesh
    {
        [ProtoMember(1)] public List<int> Indices { get; set; } = new List<int>();
        [ProtoMember(2)] public string Material { get; set; }
    }

    // Same values as Timberborn.TimbermeshDTO.ScalarType.
    public enum TmScalarType
    {
        Unspecified,
        UnsignedByte,
        UnsignedInt,
        Int,
        Float,
        Double
    }

    [ProtoContract]
    public class TmVertexProperty
    {
        [ProtoMember(1)] public string Name { get; set; }
        [ProtoMember(2)] public TmScalarType ScalarType { get; set; }
        [ProtoMember(3)] public int ScalarTypeDimension { get; set; }
        [ProtoMember(4)] public byte[] Data { get; set; }

        public static TmVertexProperty Floats(string name, int dimension, IEnumerable<float> values)
        {
            List<byte> bytes = new List<byte>();
            foreach (float value in values)
            {
                bytes.AddRange(BitConverter.GetBytes(value));
            }
            return new TmVertexProperty
            {
                Name = name, ScalarType = TmScalarType.Float, ScalarTypeDimension = dimension, Data = bytes.ToArray()
            };
        }
    }

    [ProtoContract]
    public class TmNodeAnimation
    {
        [ProtoMember(1)] public string Name { get; set; }
        [ProtoMember(2)] public float Framerate { get; set; }
        [ProtoMember(3)] public List<TmNodeAnimationFrame> Frames { get; set; } = new List<TmNodeAnimationFrame>();
    }

    [ProtoContract]
    public class TmNodeAnimationFrame
    {
        // The game dereferences all three (NodeAnimationCache), so always set them.
        [ProtoMember(1)] public TmVector3 Position { get; set; }
        [ProtoMember(2)] public TmQuaternion Rotation { get; set; }
        [ProtoMember(3)] public TmVector3 Scale { get; set; }
    }

    [ProtoContract]
    public class TmVector3
    {
        [ProtoMember(1)] public float X { get; set; }
        [ProtoMember(2)] public float Y { get; set; }
        [ProtoMember(3)] public float Z { get; set; }

        public static TmVector3 From(System.Numerics.Vector3 v) => new TmVector3 { X = v.X, Y = v.Y, Z = v.Z };
    }

    [ProtoContract]
    public class TmQuaternion
    {
        [ProtoMember(1)] public float X { get; set; }
        [ProtoMember(2)] public float Y { get; set; }
        [ProtoMember(3)] public float Z { get; set; }
        [ProtoMember(4)] public float W { get; set; }

        public static TmQuaternion From(System.Numerics.Quaternion q) => new TmQuaternion { X = q.X, Y = q.Y, Z = q.Z, W = q.W };
    }

    public static class TimbermeshFile
    {
        // TimbermeshReader expects a zlib stream: header 0x78 0x9C, raw deflate, then (ignored) Adler-32.
        public static void Write(TmModel model, string path)
        {
            using MemoryStream payload = new MemoryStream();
            Serializer.Serialize(payload, model);
            byte[] data = payload.ToArray();

            using FileStream file = File.Create(path);
            file.WriteByte(0x78);
            file.WriteByte(0x9C);
            using (DeflateStream deflate = new DeflateStream(file, CompressionLevel.Optimal, leaveOpen: true))
            {
                deflate.Write(data, 0, data.Length);
            }
            uint adler = Adler32(data);
            file.WriteByte((byte)(adler >> 24));
            file.WriteByte((byte)(adler >> 16));
            file.WriteByte((byte)(adler >> 8));
            file.WriteByte((byte)adler);
        }

        private static uint Adler32(byte[] data)
        {
            uint a = 1, b = 0;
            foreach (byte x in data)
            {
                a = (a + x) % 65521;
                b = (b + a) % 65521;
            }
            return (b << 16) | a;
        }
    }
}
