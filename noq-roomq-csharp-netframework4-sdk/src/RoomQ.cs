using System;
using Jose;
using static NoQ.RoomQ.ManagedHttpClient;
using NoQ.RoomQ.Exception;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Web;
using Newtonsoft.Json.Linq;
using System.Diagnostics;

namespace NoQ.RoomQ
{
    public class RoomQ
    {
        private readonly string clientID;
        private readonly string jwtSecret;
        private readonly string ticketIssuer;
        private readonly bool debug;
        private readonly string tokenName;
        private string token;
        private readonly string statusEndpoint;

        public RoomQ(string clientID, string jwtSecret, string ticketIssuer, string statusEndpoint, HttpContext httpContext = null, bool debug = false)
        {
            this.clientID = clientID;
            this.jwtSecret = jwtSecret;
            this.ticketIssuer = ticketIssuer;
            this.debug = debug;
            this.statusEndpoint = statusEndpoint;
            this.tokenName = "be_roomq_t_" + this.clientID;
            this.token = this.GetToken(httpContext);
        }

        private DateTime UnixToDateTime(long unixTime)
        {
            DateTime dt = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
            return dt.AddSeconds(unixTime);
        }

        /**
         * @return string|null
         */
        private string GetToken(HttpContext httpContext = null)
        {
            if (httpContext != null)
            {
                HttpRequest httpRequest = httpContext.Request;
                // Get noq_t from query string
                if (httpRequest.QueryString["noq_t"] != null)
                {
                    return httpRequest.QueryString["noq_t"];
                }
                // Get noq_t from cookies
                for (int i = 0; i < httpRequest.Cookies.Count; i++)
                {
                    if (httpRequest.Cookies[i].Name == this.tokenName)
                    {
                        return httpRequest.Cookies[i].Value;
                    }
                }

            }
            // Return emtpy token by default 
            return string.Empty;
        }

        public ValidationResult Validate(HttpContext httpContext, string returnUrl, string sessionId)
        {
            string token = this.token;
            var request = httpContext.Request;
            string currentUrl = (request.IsSecureConnection ? "https" : "http") + "://" + request.Url.Host + ":" + request.Url.Port + request.Url.LocalPath;
            bool needGenerateJWT = false;
            bool needRedirect = false;

            if (token == null || token.Length < 1)
            {
                needGenerateJWT = true;
                needRedirect = true;
                DebugPrint("no jwt");
            }
            else
            {
                DebugPrint("current jwt " + token);
                try
                {
                    var secret = Encoding.UTF8.GetBytes(this.jwtSecret);
                    JObject data = JObject.Parse(JWT.Decode(token, secret, JwsAlgorithm.HS256));
                    if (sessionId != null && data.TryGetValue("session_id", out _) && data["session_id"].ToString() != sessionId)
                    {
                        needGenerateJWT = true;
                        needRedirect = true;
                        DebugPrint("session id not match");
                    }
                    else if (data.TryGetValue("deadline", out _) && UnixToDateTime(data["deadline"].ToObject<long>()) < DateTime.UtcNow)
                    {
                        needRedirect = true;
                        DebugPrint("deadline exceed");
                    }
                    else if (data["type"].ToString() == "queue")
                    {
                        needRedirect = true;
                        DebugPrint("in queue");
                    }
                    else if (data["type"].ToString() == "self-sign")
                    {
                        needRedirect = true;
                        DebugPrint("self sign token");
                    }
                }
                catch (System.Exception e)
                {
                    needGenerateJWT = true;
                    needRedirect = true;
                    DebugPrint(e.Message);
                    DebugPrint("invalid secret");
                }
            }
            if (needGenerateJWT)
            {
                token = this.GenerateJWT(sessionId);
                DebugPrint("generating new jwt token");
                this.token = token;
            }
            HttpCookie cookie = new HttpCookie(this.tokenName);
            cookie.Value = this.token;
            cookie.Expires = DateTime.UtcNow.AddSeconds(12 * 60 * 60);
            httpContext.Response.Cookies.Add(cookie);
            if (needRedirect)
            {
                return this.RedirectToTicketIssuer(token, returnUrl ?? currentUrl);
            }
            else
            {
                return this.Enter(currentUrl);
            }
        }

        /**
         * @throws System.Exception
         * @throws InvalidTokenException|NotServingException
         */
        public void Extend(HttpContext httpContext, int duration)
        {
            string backend = this.GetBackend();
            try
            {
                ManagedHttpClient client = new ManagedHttpClient("https://" + backend);
                Dictionary<string, object> payload = new Dictionary<string, object>()
                    {
                        {"action", "beep"},
                        {"client_id", this.clientID},
                        {"id", this.token},
                        {"extend_serving_duration", duration * 60 }
                    };
                Response response = client.Post("/queue/" + this.clientID, payload: payload);
                DebugPrint(response.GetDeserializedContent<JObject>().ToString());
                if (!response.IsSuccess && response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    throw new InvalidApiKeyException();
                }
                else if (!response.IsSuccess && response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    throw new NotServingException();
                }
                else
                {
                    JObject json = response.GetDeserializedContent<JObject>();
                    string newToken = json["id"].ToString();
                    this.token = newToken;

                    HttpCookie cookie = new HttpCookie(this.tokenName);
                    cookie.Value = this.token;
                    cookie.Expires = DateTime.UtcNow.AddSeconds(12 * 60 * 60);
                    httpContext.Response.Cookies.Add(cookie);
                }
            }
            catch (System.Exception e)
            {
                throw e;
            }
        }

        /**
         * @throws System.Exception
         * @throws InvalidTokenException|NotServingException
         */
        public long GetServing()
        {
            string backend = this.GetBackend();
            ManagedHttpClient client = new ManagedHttpClient("https://" + backend);
            try
            {
                Response response = client.Get("/rooms/" + this.clientID + "/servings/" + this.token);
                DebugPrint(response.GetDeserializedContent<JObject>().ToString());
                if (!response.IsSuccess && response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    throw new InvalidApiKeyException();
                }
                else if (!response.IsSuccess && response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    throw new NotServingException();
                }
                else
                {
                    DebugPrint(response.Content);
                    JObject json = response.GetDeserializedContent<JObject>();
                    return json["deadline"].ToObject<long>() / 1000;
                }
            }
            catch (System.Exception e)
            {
                throw e;
            }
        }

        /**
         * @throws System.Exception
         * @throws InvalidTokenException|NotServingException
         */
        public void DeleteServing(HttpContext httpContext)
        {
            string backend = this.GetBackend();

            try
            {
                ManagedHttpClient client = new ManagedHttpClient("https://" + backend);
                Dictionary<string, string> payload = new Dictionary<string, string>()
                {
                    {"action", "delete_serving"},
                    {"client_id", this.clientID},
                    {"id", this.token}
                };
                Response response = client.Post("/queue/" + this.clientID, payload: payload);
                DebugPrint(response.GetDeserializedContent<JObject>().ToString());
                if (!response.IsSuccess && response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    throw new InvalidApiKeyException();
                }
                else if (!response.IsSuccess && response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    throw new NotServingException();
                }
                else
                {
                    var secret = Encoding.UTF8.GetBytes(this.jwtSecret);
                    JObject data = JObject.Parse(JWT.Decode(this.token, secret, JwsAlgorithm.HS256));
                    var token = this.GenerateJWT(data["session_id"].ToString());
                    this.token = token;

                    HttpCookie cookie = new HttpCookie(this.tokenName);
                    cookie.Value = this.token;
                    cookie.Expires = DateTime.UtcNow.AddSeconds(12 * 60 * 60);
                    httpContext.Response.Cookies.Add(cookie);
                }
            }
            catch (System.Exception e)
            {
                throw e;
            }
        }

        private ValidationResult Enter(string currentUrl)
        {
            string urlWithoutToken = this.RemoveNoQToken(currentUrl);
            //redirect if url contain token
            if (urlWithoutToken != currentUrl)
            {
                return new ValidationResult(urlWithoutToken);
            }
            return new ValidationResult(null);
        }

        private ValidationResult RedirectToTicketIssuer(string token, string currentUrl)
        {
            string urlWithoutToken = this.RemoveNoQToken(currentUrl);
            Dictionary<string, string> _params = new Dictionary<string, string>()
            {
                {"noq_t", token },
                {"noq_c", this.clientID},
                {"noq_r", urlWithoutToken}
            };

            return new ValidationResult(this.ticketIssuer + "?" + QueryStringBuilder(_params));
        }

        private string GenerateJWT(string sessionID = null)
        {
            var data = new Dictionary<string, string>()
                {
                    { "room_id", this.clientID },
                    { "session_id", sessionID ?? Guid.NewGuid().ToString() },
                    { "type", "self-sign" }
                };
            var secret = Encoding.ASCII.GetBytes(this.jwtSecret);
            return JWT.Encode(data, secret, JwsAlgorithm.HS256);
        }

        private void DebugPrint(string message)
        {
            if (this.debug)
            {
                Debug.WriteLine($"[RoomQ] {message}");
            }
        }

        private string RemoveNoQToken(string currentUrl)
        {
            string updated = Regex.Replace(currentUrl, "/([&]*)(noq_t=[^&]*)/i", "");
            updated = Regex.Replace(updated, "/\\?&/i", "?");
            return Regex.Replace(updated, "/\\?$/i", "");
        }

        /**
         * TODO
         * @return 
         * @throws 
         * @throws QueueStoppedException
         */
        private string GetBackend()
        {
            ManagedHttpClient client = new ManagedHttpClient(this.statusEndpoint);
            Response response = client.Get("/" + this.clientID);
            JObject json = response.GetDeserializedContent<JObject>();
            string state = json["state"].ToString();
            if (state == "stopped")
            {
                throw new QueueStoppedException();
            }
            string backend = json["backend"].ToString();
            return backend;
        }
    }
}
