﻿using Raven.Client.Documents;

namespace LinkDotNet.Blog.Infrastructure.Persistence.RavenDb;

public static class RavenDbConnectionProvider
{
    public static IDocumentStore Create(string url, string databaseName)
    {
        var documentStore = new DocumentStore
        {
            Urls = [url],
            Database = databaseName,
            Conventions = { IdentityPartsSeparator = '-' },
        };
        _ = documentStore.Initialize();
        return documentStore;
    }
}
