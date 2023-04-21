using System.Collections.Generic;

namespace RKsElevators
{
    public class BuildPiece
    {
        public string PrefabName { get; set; }
        public string PrefabDescription { get; set; } = string.Empty;
        public string DisplayNameToken { get; set; }
        public bool Enabled { get; set; } = true;
        public string RequiredStation { get; set; } = string.Empty;

        public List<BuildPieceRequirement> Requirements { get; set; }

        public BuildMaterial Material { get; set; } = BuildMaterial.Wood;
        public string FuelItem { get; set; } = string.Empty;
    }
}
