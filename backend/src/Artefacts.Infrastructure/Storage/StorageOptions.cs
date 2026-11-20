using System.ComponentModel.DataAnnotations;

namespace Artefacts.Infrastructure.Storage;

public enum StorageProvider
{
    S3,
    FileSystem,
}

public sealed class StorageOptions : IValidatableObject
{
    public const string Section = "Storage";

    public StorageProvider Provider { get; set; } = StorageProvider.S3;
    public S3StorageOptions S3 { get; set; } = new();
    public FileSystemStorageOptions FileSystem { get; set; } = new();

    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (Provider == StorageProvider.S3)
        {
            if (string.IsNullOrWhiteSpace(S3.ServiceUrl)) yield return Missing("Storage:S3:ServiceUrl");
            if (string.IsNullOrWhiteSpace(S3.Bucket)) yield return Missing("Storage:S3:Bucket");
            if (string.IsNullOrWhiteSpace(S3.AccessKey)) yield return Missing("Storage:S3:AccessKey");
            if (string.IsNullOrWhiteSpace(S3.SecretKey)) yield return Missing("Storage:S3:SecretKey");
        }
        else if (string.IsNullOrWhiteSpace(FileSystem.Root))
        {
            yield return Missing("Storage:FileSystem:Root");
        }
    }

    private static ValidationResult Missing(string key) => new($"Configuration value '{key}' is required.", [key]);
}

public sealed class S3StorageOptions
{
    /// <summary>S3 endpoint, e.g. http://localhost:8333 (SeaweedFS). Leave the AWS default for real S3.</summary>
    public string ServiceUrl { get; set; } = "";
    public string Bucket { get; set; } = "";
    public string AccessKey { get; set; } = "";
    public string SecretKey { get; set; } = "";
    public string Region { get; set; } = "us-east-1";
    public bool ForcePathStyle { get; set; } = true;
}

public sealed class FileSystemStorageOptions
{
    public string Root { get; set; } = "";
}
