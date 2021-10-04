using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Text;
using System.Web;
using Newtonsoft.Json;

namespace NoQ.RoomQ
{
    public class ManagedHttpClient
    {
        private readonly string baseURL = null;

        public enum METHOD
        {
            GET, PUT, POST, DELETE
        }

        public ManagedHttpClient(string baseURL)
        {
            this.baseURL = baseURL;
        }

        public static string UrlEncode(string s)
        {
            return HttpUtility.UrlEncode(s);
        }

        private delegate HttpClient OnHttpClientReady(HttpClient client);

        private void RequestHeaderHandler(HttpClient client, Dictionary<string, string> headers)
        {
            client.DefaultRequestHeaders.Clear();
            foreach (KeyValuePair<string, string> entry in headers)
                client.DefaultRequestHeaders.Add(entry.Key, entry.Value);
        }

        private string QueryStringHandler(Dictionary<string, string> query)
        {
            return QueryStringBuilder(query);
        }

        private HttpClient GetHttpClient(ref string path, Dictionary<string, string> query = null, Dictionary<string, string> headers = null)
        {
            HttpClient client = new HttpClient();
            if (headers != null) this.RequestHeaderHandler(client, headers);
            if (query != null) path += "?" + this.QueryStringHandler(query);
            return client;
        }

        private Response GetResponse(Task<HttpResponseMessage> response)
        {
            try
            {
                HttpResponseMessage responseMessage = response.Result;
                string content = responseMessage.Content.ReadAsStringAsync().Result;
                return new Response(content, response.Result.IsSuccessStatusCode, response.Result.StatusCode);
            }
            catch (System.Exception e)
            {
                return new Response(e.Message, false, HttpStatusCode.InternalServerError);
            }

        }

        public class Response
        {
            public string Content { get; }
            public bool IsSuccess { get; }
            public HttpStatusCode StatusCode { get; }

            public Response(string content, bool isSuccess, HttpStatusCode statusCode)
            {
                this.Content = content;
                this.IsSuccess = isSuccess;
                this.StatusCode = statusCode;
            }

            public T GetDeserializedContent<T>()
            {
                return JsonConvert.DeserializeObject<T>(this.Content);                
            }
        }

        public Response Get(string path, Dictionary<string, string> query = null, Dictionary<string, string> headers = null)
        {
            using (HttpClient client = this.GetHttpClient(ref path, query: query, headers: headers))
            {
                Task<HttpResponseMessage> response = client.GetAsync(this.baseURL + path);
                return this.GetResponse(response);
            }
        }

        public Response Put<T>(string path, T payload, Dictionary<string, string> query = null, Dictionary<string, string> headers = null)
        {
            using (HttpClient client = this.GetHttpClient(ref path, query: query, headers: headers))
            {
                HttpContent content = null;
                if (payload != null)
                    content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");
                Task<HttpResponseMessage> response = client.PutAsync(this.baseURL + path, content);
                return this.GetResponse(response);
            }
        }

        public Response Post<T>(string path, T payload, Dictionary<string, string> query = null, Dictionary<string, string> headers = null)
        {
            using (HttpClient client = this.GetHttpClient(ref path, query: query, headers: headers))
            {
                HttpContent content = null;
                if (payload != null)
                    content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");
                Task<HttpResponseMessage> response = client.PostAsync(this.baseURL + path, content);
                return this.GetResponse(response);
            }
        }

        public Response Delete(string path, Dictionary<string, string> query = null, Dictionary<string, string> headers = null)
        {
            using (HttpClient client = this.GetHttpClient(ref path, query: query, headers: headers))
            {
                Task<HttpResponseMessage> response = client.DeleteAsync(this.baseURL + path);
                return this.GetResponse(response);
            }
        }

        public static string QueryStringBuilder(Dictionary<string, string> query)
        {
            var _query = HttpUtility.ParseQueryString(string.Empty);
            foreach (KeyValuePair<string, string> entry in query)
                _query[entry.Key] = entry.Value;
            return _query.ToString();
        }
    }
}
