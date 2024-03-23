using LinkDotNet.Blog.Domain;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;

namespace LinkDotNet.Blog.Infrastructure.Persistence.MongoDB;

public static class MongoDBConnectionProvider
{
    public static IMongoDatabase Create(string connectionString, string databaseName)
    {
        var client = new MongoClient(connectionString);
        _ = BsonClassMap.RegisterClassMap<Entity>(cm =>
        {
            cm.AutoMap();
            _ = cm.MapIdProperty(e => e.Id);
        });
        return client.GetDatabase(databaseName);
    }
}
