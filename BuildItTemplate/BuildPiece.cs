using System.Collections.Generic;

namespace BuildTemplate
{
    public class BuildPiece
    {
        public string PrefabName { get; set; }
        public string PrefabDescription { get; set; } = string.Empty;
        public string DisplayNameToken { get; set; }
        public bool Enabled { get; set; } = true;
        public string RequiredStation { get; set; } = string.Empty;

        public List<BuildPieceRequirement> Requirements { get; set; }

        public BuildMaterial Material { get; set; } = BuildMaterial.Stone;
        public string FuelItem { get; set; } = string.Empty;
    }
}
