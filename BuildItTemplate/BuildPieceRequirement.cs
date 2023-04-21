namespace RKsElevators
{
    public class BuildPieceRequirement
    {
        public string Item { get; set; }
        public int Amount { get; set; }
        public bool Recover { get; set; } = true;
    }
}