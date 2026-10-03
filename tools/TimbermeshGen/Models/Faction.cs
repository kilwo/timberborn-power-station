using TimbermeshGen.Timbermesh;

namespace TimbermeshGen.Models
{
    /// <summary>
    /// Faction variants of a Folktails-built model: the same geometry and UVs with the faction's UberAtlas materials,
    /// as vanilla does (e.g. ShaftSupport/ShaftFrame.Folktails vs .IronTeeth: identical vertices and UVs, materials
    /// swapped). Material names from Blueprints/MaterialCollections (1.1.2.4).
    /// </summary>
    public static class Faction
    {
        public const string Folktails = "Folktails";
        public const string IronTeeth = "IronTeeth";

        public static readonly string[] All = { Folktails, IronTeeth };

        // Vanilla maps BaseWood_Brown -> BaseWood_DarkBrown and BaseWood_White -> BaseWood_Grey (shaft parts,
        // ImpermeablePowerShaft). LightBrown has no vanilla counterpart; Grey keeps the lid, rungs and platform a lighter
        // accent. PaintedMetal.IronTeeth has the same stripe layout as Folktails, in blue instead of yellow.
        private static readonly Dictionary<string, string> IronTeethMaterials = new Dictionary<string, string>
        {
            [Atlas.Brown] = "BaseWood_DarkBrown.IronTeeth",
            [Atlas.LightBrown] = "BaseWood_Grey.IronTeeth",
            [Atlas.White] = "BaseWood_Grey.IronTeeth",
            [Atlas.Metal] = "BaseMetal.IronTeeth",
            [Atlas.Painted] = "PaintedMetal.IronTeeth"
        };

        /// <summary>Converts a model built with Folktails materials and node names to <paramref name="faction"/>.</summary>
        public static TmModel Convert(TmModel model, string faction)
        {
            if (faction == Folktails)
            {
                return model;
            }
            if (faction != IronTeeth)
            {
                throw new ArgumentException($"unknown faction '{faction}'");
            }
            foreach (TmNode node in model.Nodes)
            {
                node.Name = node.Name.Replace("." + Folktails + ".", "." + faction + ".");
                foreach (TmMesh mesh in node.Meshes)
                {
                    if (!IronTeethMaterials.TryGetValue(mesh.Material, out string material))
                    {
                        throw new InvalidOperationException($"no {faction} material for '{mesh.Material}'");
                    }
                    mesh.Material = material;
                }
            }
            return model;
        }
    }
}
