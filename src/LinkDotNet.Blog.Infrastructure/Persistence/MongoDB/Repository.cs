using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using LinkDotNet.Blog.Domain;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Linq;

namespace LinkDotNet.Blog.Infrastructure.Persistence.MongoDB;

public sealed class Repository<TEntity>(IMongoDatabase database) : IRepository<TEntity>
    where TEntity : Entity
{
    private readonly IMongoDatabase database = database;
    private IMongoCollection<TEntity> Collection => database.GetCollection<TEntity>(typeof(TEntity).Name);

    public async ValueTask<HealthCheckResult> PerformHealthCheckAsync()
    {
        try
        {
            var command = new BsonDocument("ping", 1);
            _ = await database.RunCommandAsync<BsonDocument>(command);

            return HealthCheckResult.Healthy("A healthy result.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy(exception: ex);
        }
    }

    public async ValueTask<TEntity> GetByIdAsync(string id)
    {
        var filter = Builders<TEntity>.Filter.Eq(e => e.Id, id);
        var result = await Collection.FindAsync(filter);
        return await result.FirstOrDefaultAsync();
    }

    public async ValueTask<IPagedList<TEntity>> GetAllAsync(
        Expression<Func<TEntity, bool>> filter = null,
        Expression<Func<TEntity, object>> orderBy = null,
        bool descending = true,
        int page = 1,
        int pageSize = int.MaxValue) =>
        await GetAllByProjectionAsync(s => s, filter, orderBy, descending, page, pageSize);

    public async ValueTask<IPagedList<TProjection>> GetAllByProjectionAsync<TProjection>(
        Expression<Func<TEntity, TProjection>> selector,
        Expression<Func<TEntity, bool>> filter = null,
        Expression<Func<TEntity, object>> orderBy = null,
        bool descending = true,
        int page = 1,
        int pageSize = int.MaxValue)
    {
        var query = Collection.AsQueryable();

        if (filter != null)
        {
            query = query.Where(filter);
        }

        if (orderBy != null)
        {
            query = descending ? query.OrderByDescending(orderBy) : query.OrderBy(orderBy);
        }

        var projectionQuery = query.Select(selector);
        return await projectionQuery.ToPagedListAsync(page, pageSize);
    }

    public async ValueTask StoreAsync(TEntity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        if (string.IsNullOrWhiteSpace(entity.Id))
        {
            entity.Id = ObjectId.GenerateNewId().ToString();
        }

        var filter = Builders<TEntity>.Filter.Eq(doc => doc.Id, entity.Id);
        var options = new ReplaceOptions { IsUpsert = true };
        _ = await Collection.ReplaceOneAsync(filter, entity, options);
    }

    public async ValueTask DeleteAsync(string id)
    {
        var filter = Builders<TEntity>.Filter.Eq(doc => doc.Id, id);
        _ = await Collection.DeleteOneAsync(filter);
    }

    public async ValueTask DeleteBulkAsync(IEnumerable<string> ids)
    {
        var filter = Builders<TEntity>.Filter.In(doc => doc.Id, ids);
        _ = await Collection.DeleteManyAsync(filter);
    }

    public async ValueTask StoreBulkAsync(IEnumerable<TEntity> records)
    {
        if (records.Any())
        {
            await Collection.InsertManyAsync(records);
        }
    }
}
