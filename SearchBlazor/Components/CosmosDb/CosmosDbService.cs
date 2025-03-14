using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;

namespace SearchBlazor.Components.CosmosDb
{
    public class CosmosDbService
    {
        private readonly CosmosClient _cosmosClient;
        private readonly Container _container;
        private readonly string _databaseId;
        IConfiguration configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.Development.json")
            .Build();
        public CosmosDbService()
        {
            var account = configuration["CosmosDb:Account"];
            var key = configuration["CosmosDb:Key"];
            _databaseId = configuration["CosmosDb:DatabaseId"];
            // var containerId = configuration["CosmosDb:ContainerId"];

            _cosmosClient = new CosmosClient(account, key);
            //  _container = cosmosClient.GetContainer(databaseId, containerId);
        }
        public Container GetContainer(string containerId)
        {
            return _cosmosClient.GetContainer(_databaseId, containerId);
        }

        public async Task AddItemAsync<T>(string containerId, T item, string partitionKey)
        {
            var container = GetContainer(containerId);
            await container.CreateItemAsync(item, new PartitionKey(partitionKey));
        }


        public async Task<List<T>> QueryItemsAsync<T>(string containerId, string partitionKey, string query)
        {
            var container = GetContainer(containerId);
            var queryDef = new QueryDefinition(query);
            var iterator = container.GetItemQueryIterator<T>(queryDef);

            List<T> results = new();
            while (iterator.HasMoreResults)
            {
                FeedResponse<T> response = await iterator.ReadNextAsync();
                results.AddRange(response);

                // foreach (var item in await iterator.ReadNextAsync())
                // {
                //     results.Add(item);
                // }
            }
            return results;
        }

    }
}