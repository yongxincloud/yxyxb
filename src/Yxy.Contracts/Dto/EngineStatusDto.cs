namespace Yxy.Contracts.Dto
{
    public sealed class EngineStatusDto
    {
        public bool Installed { get; set; }

        public bool Running { get; set; }

        public string DisplayText { get; set; }

        public string LastError { get; set; }
    }
}
