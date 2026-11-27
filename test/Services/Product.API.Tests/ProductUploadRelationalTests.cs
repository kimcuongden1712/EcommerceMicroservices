using Infrastructure.Common;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Product.API.Commons.Enums;
using Product.API.Entities;
using Product.API.Persistence;
using Product.API.Repositories;

namespace Product.API.Tests;

// SQLite exercises atomic SQL updates, not MySQL locking/timestamp behavior.
// Its test model stores UTC ticks because SQLite cannot compare DateTimeOffset natively.
public sealed class ProductUploadRelationalTests
{
    private sealed class SqliteProductContext(DbContextOptions<ProductContext> options):ProductContext(options)
    {
        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);
            var converter=new ValueConverter<DateTimeOffset,long>(d=>d.UtcTicks,t=>new DateTimeOffset(t,TimeSpan.Zero));
            foreach(var property in builder.Model.GetEntityTypes().SelectMany(e=>e.GetProperties()))
                if((Nullable.GetUnderlyingType(property.ClrType)??property.ClrType)==typeof(DateTimeOffset)){
                    property.SetValueConverter(converter);property.SetColumnType("INTEGER");
                }
        }
    }
    private sealed class Fixture:IDisposable
    {
        private readonly SqliteConnection _connection=new("Data Source=:memory:");
        public readonly ProductContext Context;
        public readonly ProductImageUploadRepository Repository;
        public readonly DateTimeOffset Now=DateTimeOffset.UtcNow;
        public Fixture()
        {
            _connection.Open();_connection.CreateCollation("utf8mb4_bin",string.CompareOrdinal);
            _connection.CreateFunction<string?,int?>("CHAR_LENGTH",value=>value?.Length,isDeterministic:true);
            Context=new SqliteProductContext(new DbContextOptionsBuilder<ProductContext>().UseSqlite(_connection).Options);
            Context.Database.EnsureCreated();
            Context.Categories.Add(new(){Id=1,Name="Category",Slug="category"});
            Context.Products.Add(new(){Id=1,CategoryId=1,Name="Product",Slug="product"});Context.SaveChanges();
            var unit=new UnitOfWork<ProductContext>(Context);
            Repository=new(Context,unit,new RepositoryBase<ProductImages,long,ProductContext>(Context,unit));
        }
        public ProductImageUploads Upload(ProductImageUploadStatus status=ProductImageUploadStatus.Pending)
        {
            var row=new ProductImageUploads{ProductId=1,StagingKey=Guid.NewGuid().ToString(),AssetKey=Guid.NewGuid().ToString("N"),ExpectedContentType="image/png",ExpectedSize=10,Status=status,CreatedAt=Now.AddMinutes(-1),ExpiresAt=Now.AddMinutes(10)};
            Context.ProductImageUploads.Add(row);Context.SaveChanges();return row;
        }
        public void Save()=>Context.SaveChanges();
        public void Dispose(){Context.Dispose();_connection.Dispose();}
    }

    [Theory]
    [InlineData("eligible",true)] [InlineData("stale",true)] [InlineData("expired",false)]
    [InlineData("future_retry",false)] [InlineData("fresh_claim",false)] [InlineData("wrong_status",false)]
    public async Task Pending_claim_updates_exactly_one_eligible_row_and_detaches_it(string scenario,bool expected)
    {
        using var f=new Fixture();var row=f.Upload();
        if(scenario=="expired")row.ExpiresAt=f.Now.AddSeconds(-1);
        if(scenario=="future_retry")row.NextAttemptAt=f.Now.AddHours(1);
        if(scenario=="fresh_claim")row.ProcessingStartedAt=f.Now;
        if(scenario=="stale")row.ProcessingStartedAt=f.Now.AddHours(-1);
        if(scenario=="wrong_status")row.Status=ProductImageUploadStatus.Ready;
        f.Save();var token=TestContext.Current.CancellationToken;
        var pending=await f.Repository.GetPendingUploadsAsync(f.Now,f.Now.AddMinutes(-10),10,token);Assert.Equal(expected,pending.Any(u=>u.Id==row.Id));
        Assert.Equal(expected,await f.Repository.TryClaimPendingUploadAsync(row.Id,f.Now,f.Now.AddMinutes(-10),token));
        Assert.Equal(expected?EntityState.Detached:EntityState.Unchanged,f.Context.Entry(row).State);
        f.Context.ChangeTracker.Clear();var persisted=await f.Repository.GetUploadByIdAsync(row.Id,true,token);
        Assert.NotNull(persisted);Assert.Equal(expected?1:0,persisted.AttemptCount);
        if(expected){Assert.Equal(f.Now,persisted.ProcessingStartedAt);Assert.False(await f.Repository.TryClaimPendingUploadAsync(row.Id,f.Now,f.Now.AddMinutes(-10),token));}
    }

    [Theory]
    [InlineData(ProductImageUploadStatus.Ready,false,null,true)]
    [InlineData(ProductImageUploadStatus.Ready,true,-1,true)]
    [InlineData(ProductImageUploadStatus.Rejected,false,null,true)]
    [InlineData(ProductImageUploadStatus.Expired,true,-1,true)]
    [InlineData(ProductImageUploadStatus.Cleaned,false,-1,true)]
    [InlineData(ProductImageUploadStatus.Ready,true,60,false)]
    [InlineData(ProductImageUploadStatus.Pending,false,null,false)]
    public async Task Cleanup_candidates_and_claims_honor_status_staging_and_retention(ProductImageUploadStatus status,bool stagingDeleted,int? retryMinutes,bool expected)
    {
        using var f=new Fixture();var row=f.Upload(status);row.StagingDeletedAt=stagingDeleted?f.Now.AddHours(-1):null;
        row.NextAttemptAt=retryMinutes.HasValue?f.Now.AddMinutes(retryMinutes.Value):null;f.Save();var token=TestContext.Current.CancellationToken;
        Assert.Equal(expected,(await f.Repository.GetCleanupUploadsAsync(10,token)).Any(u=>u.Id==row.Id));
        Assert.Equal(expected,await f.Repository.TryClaimCleanupUploadAsync(row.Id,f.Now,f.Now.AddMinutes(-10),token));
        f.Context.ChangeTracker.Clear();var saved=await f.Repository.GetUploadByIdAsync(row.Id,false,token);Assert.Equal(expected?1:0,saved!.CleanupAttempts);
        if(expected)Assert.False(await f.Repository.TryClaimCleanupUploadAsync(row.Id,f.Now,f.Now.AddMinutes(-10),token));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Referenced_ready_assets_cannot_be_claimed_as_orphans(bool referenced)
    {
        using var f=new Fixture();var row=f.Upload(ProductImageUploadStatus.Ready);row.StagingDeletedAt=f.Now.AddHours(-1);row.NextAttemptAt=f.Now.AddMinutes(-1);
        if(referenced)f.Context.ProductImages.Add(new(){ProductId=1,AssetKey=row.AssetKey!,AltText="Alt",Position=0});
        f.Save();var token=TestContext.Current.CancellationToken;
        Assert.Equal(!referenced,(await f.Repository.GetUnreferencedReadyUploadsAsync(f.Now,10,token)).Any(u=>u.Id==row.Id));
        Assert.Equal(!referenced,await f.Repository.TryClaimOrphanedReadyUploadAsync(row.Id,f.Now,f.Now.AddMinutes(-10),token));
        Assert.Equal(referenced?0:1,await f.Repository.CountActiveUploadSlotsAsync(1,f.Now,token));
        Assert.NotNull(await f.Repository.GetProductImageUploadAsync(1,row.Id,token));Assert.Null(await f.Repository.GetProductImageUploadAsync(2,row.Id,token));
    }

    [Theory]
    [InlineData(true)] [InlineData(false)]
    public async Task Expiration_only_changes_expired_pending_uploads(bool expired)
    {
        using var f=new Fixture();var row=f.Upload();row.ExpiresAt=expired?f.Now.AddMinutes(-1):f.Now.AddMinutes(1);f.Save();var token=TestContext.Current.CancellationToken;
        Assert.Equal(expired,(await f.Repository.GetExpiredUploadsAsync(10,token)).Any(u=>u.Id==row.Id));
        Assert.Equal(expired,await f.Repository.MarkExpiredAsync(row.Id,f.Now,token));
        f.Context.ChangeTracker.Clear();var saved=await f.Repository.GetUploadByIdAsync(row.Id,false,token);
        Assert.Equal(expired?ProductImageUploadStatus.Expired:ProductImageUploadStatus.Pending,saved!.Status);
        if(expired)Assert.False(await f.Repository.MarkExpiredAsync(row.Id,f.Now,token));
    }

    [Fact]
    public async Task Removed_asset_and_retry_schedule_are_persisted_without_stale_tracking()
    {
        using var f=new Fixture();var row=f.Upload(ProductImageUploadStatus.Ready);var token=TestContext.Current.CancellationToken;
        Assert.True(await f.Repository.MarkAssetUnreferencedForCleanupAsync(row.AssetKey!,f.Now.AddHours(1),token));
        Assert.False(await f.Repository.MarkAssetUnreferencedForCleanupAsync(row.AssetKey!,f.Now.AddHours(1),token));
        await f.Repository.ScheduleCleanupRetryAsync(row.Id,f.Now.AddHours(2),token);f.Context.ChangeTracker.Clear();
        var saved=await f.Repository.GetUploadByIdAsync(row.Id,false,token);Assert.Equal(ProductImageUploadStatus.Expired,saved!.Status);Assert.Equal(f.Now.AddHours(2),saved.NextAttemptAt);Assert.Null(saved.ProcessingStartedAt);
    }
}
