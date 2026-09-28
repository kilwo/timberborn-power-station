using TimbermeshGen.Geometry;

namespace TimbermeshGen.Models
{
    /// <summary>
    /// Regions of the vanilla UberAtlas textures (Unity UV space, origin bottom-left), read off the example textures
    /// packed in StreamingAssets/Modding/TimberbornExampleModels.blend and cross-checked against vanilla UVs.
    /// </summary>
    public static class Atlas
    {
        public const string Brown = "BaseWood_Brown.Folktails";
        public const string LightBrown = "BaseWood_LightBrown.Folktails";
        public const string White = "BaseWood_White.Folktails";
        public const string Metal = "BaseMetal.Folktails";
        public const string Painted = "PaintedMetal.Folktails";

        private const float Plank = 1f / 16f;
        private const float Inset = 0.006f;

        /// <summary>One of the four long planks in the top-middle of the BaseWood atlas (grain along V).</summary>
        public static UvRect LongPlank(int i) =>
            new UvRect(0.5f + Plank * (i & 3) + Inset, 0.505f, 0.5f + Plank * ((i & 3) + 1) - Inset, 0.995f);

        /// <summary>One of the eight planks in the bottom-left of the BaseWood atlas (grain along V).</summary>
        public static UvRect Plank8(int i) =>
            new UvRect(Plank * (i & 7) + Inset, 0.01f, Plank * ((i & 7) + 1) - Inset, 0.49f);

        /// <summary>A thin horizontal slice of the fibrous log area (top-left of BaseWood), for rims.</summary>
        public static UvRect BarkSlice { get; } = new UvRect(0.02f, 0.62f, 0.48f, 0.66f);

        /// <summary>Large end-grain rings (BaseWood).</summary>
        public static UvCircle EndGrain { get; } = new UvCircle(0.688f, 0.189f, 0.055f);

        /// <summary>Plain worn panel (BaseMetal).</summary>
        public static UvRect PlainMetal { get; } = new UvRect(0.52f, 0.27f, 0.74f, 0.49f);

        /// <summary>Thin slice of plain metal, for bands wrapped around a circumference.</summary>
        public static UvRect MetalSlice { get; } = new UvRect(0.52f, 0.36f, 0.74f, 0.40f);

        /// <summary>Round plate with an engraved ring (BaseMetal).</summary>
        public static UvCircle RingPlate { get; } = new UvCircle(0.875f, 0.627f, 0.11f);

        /// <summary>A yellow painted stripe (PaintedMetal.Folktails).</summary>
        public static UvRect YellowStripe { get; } = new UvRect(0.04f, 0.05f, 0.085f, 0.95f);
    }
}
