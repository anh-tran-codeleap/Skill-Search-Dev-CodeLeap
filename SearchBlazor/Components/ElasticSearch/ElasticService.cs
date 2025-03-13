using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Elastic.Clients.Elasticsearch;
using Microsoft.Extensions.Options;
using SearchBlazor.Components.ElasticSearch.Configuration;

namespace SearchBlazor.Components.ElasticSearch
{
    public class ElasticService
    {
        private readonly ElasticsearchClient _client;
        private readonly ElasticSettings _settings;
        public ElasticService(IOptions<ElasticSettings> optionsMonitor)
        {
            _settings = optionsMonitor.Value;
            var clientSettings = new ElasticsearchClientSettings(new Uri(_settings.Url))
                            .DefaultIndex(_settings.DefaultIndex);
            _client = new ElasticsearchClient(clientSettings);
        }
        public async Task CreateIndexIfNotExistsAsync(string indexName)
        {
            var existsResponse = await _client.Indices.ExistsAsync(indexName);
            if (!existsResponse.Exists)
            {
                await _client.Indices.CreateAsync(indexName);
            }
        }

    }
}