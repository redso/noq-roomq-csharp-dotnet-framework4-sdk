# Install

> Download the latest package from Nuget manager

```shell
// Install via shell
PM> Install-Package NoQ.RoomQ.SDK -Version 1.0.0

// Search keywords: NoQ, RoomQ
```

# RoomQ Backend SDK - C#

The [RoomQ](https://www.noq.hk/en/roomq) Backend SDK is used for server-side integration to your server. It was developed with C#.

## High Level Logic

![The SDK Flow](https://raw.githubusercontent.com/redso/roomq.backend-sdk.nodejs/master/RoomQ-Backend-SDK-JS-high-level-logic-diagram.png)

1.  End user requests a page on your server
2.  The SDK verify if the request contain a valid ticket and in Serving state. If not, the SDK send him to the queue.
3.  End user obtain a ticket and wait in the queue until the ticket turns into Serving state.
4.  End user is redirected back to your website, now with a valid ticket
5.  The SDK verify if the request contain a valid ticket and in Serving state. End user stay in the requested page.
6.  The end user browses to a new page, and the SDK continue to check if the ticket is valid.

## How to integrate

### Prerequisite

To integrate with the SDK, you need to have the following information provided by RoomQ

1.  ROOM_ID
2.  ROOM_SECRET
3.  ROOMQ_TICKET_ISSUER
4.  ROOMQ_STATUS_API

### Major steps

To validate that the end user is allowed to access your site (has been through the queue) these steps are needed:

1.  Initialise RoomQ
2.  Determine if the current request page/path required to be protected by RoomQ
3.  Initialise Http Context Provider
4.  Validate the request
5.  If the end user should goes to the queue, set cache control
6.  Redirect user to queue

### Integration on specific path

It is recommended to integrate on the page/path which are selected to be provided. For the static files, e.g. images, css files, js files, ..., it is recommended to be skipped from the validation.
You can determine the requests type before pass it to the validation.

## Implementation Example

The following is an RoomQ integration example in C#.

```csharp
using System;
using System.Collections.Generic;
using System.Web;
using System.Web.Http;
using System.Diagnostics;
using NoQ.RoomQ;
using NoQ.RoomQ.Exception;

namespace WebApplication.Controllers
{
    public class RoomQController : ApiController
    {
        static readonly string ROOM_ID = "ROOM ID";
        static readonly string ROOM_SECRET = "ROOM Secret";
        static readonly string ROOMQ_TICKET_ISSUER = "TICKET ISSUER URL";
        static readonly string ROOMQ_STATUS_API = "STATUS API";

        private void Log(object msg)
        {
            Debug.WriteLine($"{msg}");
        }

        [HttpGet]
        [ActionName("get-ticket")]
        public Dictionary<string, object> GetTicket(string sessionID = null, string redirectURL = null)
        {
            Log("Get ticket");
            var response = new Dictionary<string, object>();
            try
            {
                var httpContext = HttpContext.Current;
                RoomQ roomQ = new RoomQ(ROOM_ID, ROOM_SECRET, ROOMQ_TICKET_ISSUER, ROOMQ_STATUS_API, httpContext: httpContext, debug: false);
                // Check if the request has valid ticket
                // If "session id" is null, SDK will generate UUID as "session id"
                ValidationResult validationResult = roomQ.Validate(httpContext, redirectURL, sessionID ?? "session id");
                response.Add("redirect", validationResult.NeedRedirect());
                if (validationResult.NeedRedirect())
                {
                    response.Add("url", validationResult.GetRedirectURL());
                }
                else
                {
                    // Retrieve the expiry time of the ticket
                    response.Add("serving", roomQ.GetServing());
                }
                Log(response);
                return response;
            }
            catch (NotServingException e)
            {
                // Ticket is not in serving state
                Log(e.Message);
                Log("Not Serving");
            }
            catch (InvalidTokenException e)
            {
                // Ticket is invalid
                Log(e.Message);
                Log("Other server issues");
            }
            catch (Exception e)
            {
                // Other server issues
                Log(e.Message);
                Log("Other server issues");
            }
            return response;
        }

        [HttpGet]
        [ActionName("get-serving")]
        public Dictionary<string, object> GetServing()
        {
            Log("Get serving");
            var httpContext = HttpContext.Current;
            RoomQ roomQ = new RoomQ(ROOM_ID, ROOM_SECRET, ROOMQ_TICKET_ISSUER, ROOMQ_STATUS_API, httpContext: httpContext, debug: true);
            var response = new Dictionary<string, object>();
            try
            {
                // Retrieve the expiry time of the ticket
                long serving = roomQ.GetServing();
                response.Add("serving", serving);
            }
            catch (NotServingException e)
            {
                // Ticket is not in serving state
                Log(e.Message);
                Log("Not Serving");
            }
            catch (InvalidTokenException e)
            {
                // Ticket is invalid
                Log(e.Message);
                Log("Other server issues");
            }
            catch (Exception e)
            {
                // Other server issues
                Log(e.Message);
                Log("Other server issues");
            }
            return response;
        }

        [HttpPut]
        [ActionName("extend-ticket")]
        public Dictionary<string, object> Extend()
        {
            Log("Extend");
            var httpContext = HttpContext.Current;
            RoomQ roomQ = new RoomQ(ROOM_ID, ROOM_SECRET, ROOMQ_TICKET_ISSUER, ROOMQ_STATUS_API, httpContext: httpContext, debug: true);
            var response = new Dictionary<string, object>();
            try
            {
                // Extend Ticket's expiry time
                // Please enable this feature in Web Portal as well
                roomQ.Extend(ref httpContext, 60);
                response.Add("serving", roomQ.GetServing());
            }
            catch (NotServingException e)
            {
                // Ticket is not in serving state
                Log(e.Message);
                Log("Not Serving");
            }
            catch (InvalidTokenException e)
            {
                // Ticket is invalid
                Log(e.Message);
                Log("Other server issues");
            }
            catch (Exception e)
            {
                // Other server issues
                Log(e.Message);
                Log("Other server issues");
            }
            return response;
        }

        [HttpDelete]
        [ActionName("delete-ticket")]
        public Dictionary<string, object> Delete()
        {
            Log("Delete");
            var httpContext = HttpContext.Current;
            RoomQ roomQ = new RoomQ(ROOM_ID, ROOM_SECRET, ROOMQ_TICKET_ISSUER, ROOMQ_STATUS_API, httpContext: httpContext, debug: true);
            var response = new Dictionary<string, object>();
            try
            {
                // Delete Ticket
                roomQ.DeleteServing(httpContext);
            }
            catch (NotServingException e)
            {
                // Ticket is not in serving state
                Log(e.Message);
                Log("Not Serving");
            }
            catch (InvalidTokenException e)
            {
                // Ticket is invalid
                Log(e.Message);
                Log("Other server issues");
            }
            catch (Exception e)
            {
                // Other server issues
                Log(e.Message);
                Log("Other server issues");
            }
            return response;
        }
    }
}
```

### Ajax calls

RoomQ doesn't support validate ticket in Ajax calls yet.

### Browser / CDN cache

If your responses are cached on browser or CDN, the new requests will not process by RoomQ.
In general, for the page / path integrated with RoomQ, you are not likely to cache the responses on CDN or browser.

### Hash of URL

As hash of URL will not send to server, hash information will be lost.

## Version Guidance

| Version | Nuget           | .Net Framework Version |
| ------- | --------------- | ---------------------- |
| 1.x     | `NoQ.RoomQ.SDK` | 4.5                    |
