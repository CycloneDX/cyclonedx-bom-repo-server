namespace CycloneDX.BomRepoServer.Options
{
    public class AllowedMethodsOptions
    {
        public bool Get { get; set; } = false;
        public bool Post { get; set; } = false;
        public bool Delete { get; set; } = false;
    }
}