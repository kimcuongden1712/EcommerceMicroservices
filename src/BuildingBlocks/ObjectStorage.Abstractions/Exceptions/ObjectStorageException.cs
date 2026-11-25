namespace ObjectStorage.Abstractions.Exceptions;

public sealed class ObjectStorageException : Exception
{
    public ObjectStorageException(
        string operation,
        string bucketName,
        string? objectKey,
        string message,
        Exception? innerException = null)
        : base(message, innerException)
    {
        Operation = operation;
        BucketName = bucketName;
        ObjectKey = objectKey;
    }

    public string Operation { get; }

    public string BucketName { get; }

    public string? ObjectKey { get; }
}
