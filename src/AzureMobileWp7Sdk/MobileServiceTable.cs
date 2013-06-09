using System;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;

namespace AzuraMobileSdk
{
    public class MobileServiceTable<TItem> : MobileServiceTable
    {
        public MobileServiceTable(IMobileServiceClient client, string tableName)
            : base(client, tableName)
        {
        }

        public Task<TItem[]> Get(MobileServiceQuery query)
        {
            return base.Get(query).ContinueWith(res => res.Result.ToObject<TItem[]>(MobileServiceClient.Serializer));
        }

        public async Task<TItem> Insert(TItem item)
        {
            var jobject = JObject.FromObject(item, MobileServiceClient.Serializer);

            var res = Insert(jobject);
            return await res.ContinueWith(task => JsonConvert.DeserializeObject<TItem>(task.Result));
        }

        public Task<TItem> Update(TItem item)
        {
            var jobject = JObject.FromObject(item, MobileServiceClient.Serializer);

            var res = Update(jobject);
            return res.ContinueWith(task => JsonConvert.DeserializeObject<TItem>(task.Result));
        }
    }

    public class MobileServiceTable
    {
        private readonly IMobileServiceClient _client;
        private readonly string _tableName;

        public MobileServiceTable(IMobileServiceClient client, string tableName)
        {
            _client = client;
            _tableName = tableName;
        }

        public Task<JArray> Get(MobileServiceQuery query)
        {
            var tableUrl = "tables/" + _tableName;
            if (query != null)
            {
                var queryString = query.ToString();
                if (queryString.Length > 0)
                {
                    tableUrl += "?" + queryString;
                }
            }

            return _client.Get(tableUrl).ContinueWith(res => JArray.Parse(res.Result));
        }

        public async Task<string> Insert(JObject item)
        {
            var tableUrl = "tables/" + _tableName;

            item.Remove("id");

            var nullProperties = item.Properties().Where(p => p.Value.Type == JTokenType.Null).ToArray();
            foreach (var nullProperty in nullProperties)
            {
                item.Remove(nullProperty.Name);
            }

            return await _client.Post(tableUrl, item);
        }

        public Task<string> Update(JObject updates)
        {
            JToken idToken;

            if (updates.TryGetValue("id", out idToken) == false)
            {
                throw new Exception("missing [id] field");
            }

            var id = idToken.Value<object>().ToString();
            var tableUrl = "tables/" + _tableName + "/" + id;

            return _client.Patch(tableUrl, updates);
        }

        public Task Delete(object id)
        {
            var tableUrl = "tables/" + _tableName + "/" + id;
            return _client.Delete(tableUrl);
        }
    }
}
