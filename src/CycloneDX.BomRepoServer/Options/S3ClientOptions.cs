namespace CycloneDX.BomRepoServer.Options;

public class S3ClientOptions
{
    public string Endpoint { get; set; } = string.Empty;
    public string AccessKey { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;
    public string BucketName { get; set; } = string.Empty;
    public bool UseHttp { get; set; }
    public bool ForcePathStyle { get; set; }
}