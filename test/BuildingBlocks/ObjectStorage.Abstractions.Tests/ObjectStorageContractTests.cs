using System.Reflection;
using ObjectStorage.Abstractions.Interfaces;
using Xunit;

namespace ObjectStorage.Abstractions.Tests;

public sealed class ObjectStorageContractTests
{
    [Fact]
    public void Interface_exposes_the_required_async_operations_with_cancellation()
    {
        var expectedNames = new[]
        {
            "UploadAsync",
            "DownloadAsync",
            "GetMetadataAsync",
            "ExistsAsync",
            "DeleteAsync",
            "CopyAsync",
            "CreatePresignedUploadUrlAsync",
            "CreatePresignedDownloadUrlAsync"
        };
        var methods = typeof(IObjectStorage).GetMethods();

        Assert.Equal(
            expectedNames.OrderBy(name => name),
            methods.Select(method => method.Name).OrderBy(name => name));

        Assert.All(methods, method =>
        {
            var parameters = method.GetParameters();
            Assert.Equal(typeof(CancellationToken), parameters[^1].ParameterType);
            Assert.True(
                method.ReturnType == typeof(Task)
                || (method.ReturnType.IsGenericType
                    && method.ReturnType.GetGenericTypeDefinition() == typeof(Task<>)));
        });
    }

    [Fact]
    public void Abstractions_assembly_does_not_reference_provider_or_business_assemblies()
    {
        var referencedAssemblyNames = typeof(IObjectStorage).Assembly
            .GetReferencedAssemblies()
            .Select(assembly => assembly.Name ?? string.Empty);

        Assert.DoesNotContain(
            referencedAssemblyNames,
            name => name.StartsWith("Amazon", StringComparison.Ordinal)
                || name.StartsWith("AWSSDK", StringComparison.Ordinal)
                || name.StartsWith("Product", StringComparison.Ordinal)
                || name.Contains("Garage", StringComparison.OrdinalIgnoreCase));
    }
}
