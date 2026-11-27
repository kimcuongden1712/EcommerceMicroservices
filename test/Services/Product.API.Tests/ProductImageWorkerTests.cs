using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Product.API.BackgroundServices;
using Product.API.Entities;
using Product.API.Repositories.Interfaces;
using Product.API.Services.V1.Interfaces;

namespace Product.API.Tests;

public sealed class ProductImageWorkerTests
{
    [Theory]
    [InlineData("normal")] [InlineData("empty")]
    [InlineData("cycle_error")] [InlineData("cycle_cancel")]
    [InlineData("item_error")] [InlineData("item_cancel")]
    public async Task Worker_processes_batches_and_stops_on_cancellation_without_waiting_for_next_interval(string scenario)
    {
        using var stop=new CancellationTokenSource();
        var repository=new Mock<IProductImageUploadRepository>();var processor=new Mock<IProductImageUploadProcessingService>();
        var uploads=new[]{new ProductImageUploads{Id=1,ProductId=1,StagingKey="a",ExpectedContentType="image/png"},new ProductImageUploads{Id=2,ProductId=1,StagingKey="b",ExpectedContentType="image/png"}};
        repository.Setup(r=>r.GetPendingUploadsAsync(It.IsAny<DateTimeOffset>(),It.IsAny<DateTimeOffset>(),10,It.IsAny<CancellationToken>()))
            .Returns((DateTimeOffset _,DateTimeOffset _,int _,CancellationToken ct)=>{
                if(scenario is "cycle_error" or "cycle_cancel"){
                    stop.Cancel();return Task.FromException<IReadOnlyList<ProductImageUploads>>(scenario=="cycle_error"?new IOException("Database failed"):new OperationCanceledException(ct));
                }
                return Task.FromResult<IReadOnlyList<ProductImageUploads>>(scenario=="empty"?Array.Empty<ProductImageUploads>():uploads);
            });
        processor.Setup(p=>p.ProcessUploadAsync(uploads[0],It.IsAny<CancellationToken>())).Returns((ProductImageUploads _,CancellationToken ct)=>{
            if(scenario=="item_cancel"){stop.Cancel();return Task.FromException(new OperationCanceledException(ct));}
            return scenario=="item_error"?Task.FromException(new IOException("Image failed")):Task.CompletedTask;
        });
        processor.Setup(p=>p.ProcessCleanupAsync(It.IsAny<CancellationToken>())).Callback(()=>stop.Cancel()).Returns(Task.CompletedTask);
        var services=new ServiceCollection();services.AddScoped(_=>repository.Object);services.AddScoped(_=>processor.Object);
        await using var provider=services.BuildServiceProvider();
        using var worker=new ProductImageUploadProcessor(provider.GetRequiredService<IServiceScopeFactory>(),NullLogger<ProductImageUploadProcessor>.Instance);
        await worker.StartAsync(stop.Token);
        Assert.NotNull(worker.ExecuteTask);await worker.ExecuteTask.WaitAsync(TimeSpan.FromSeconds(10),TestContext.Current.CancellationToken);
        if(scenario is "normal" or "item_error")processor.Verify(p=>p.ProcessUploadAsync(uploads[1],It.IsAny<CancellationToken>()),Times.Once);
        if(scenario is "normal" or "empty" or "item_error"){
            processor.Verify(p=>p.ProcessExpiredUploadsAsync(It.IsAny<CancellationToken>()),Times.Once);
            processor.Verify(p=>p.ProcessCleanupAsync(It.IsAny<CancellationToken>()),Times.Once);
        }
        else processor.Verify(p=>p.ProcessCleanupAsync(It.IsAny<CancellationToken>()),Times.Never);
    }
}
