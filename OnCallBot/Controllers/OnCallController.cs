using Microsoft.Azure;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;
using Newtonsoft.Json;
using OnCallBot.Models;
using System;
using System.Linq;
using System.Net;
using System.Net.Http.Formatting;
using System.Threading.Tasks;
using System.Web.Hosting;
using System.Web.Http;

namespace OnCallBot.Controllers
{
    public class OnCallController : ApiController
    {
        // POST: api/OnCall
        public IHttpActionResult Post(FormDataCollection formData)
        {
            //parse the request
            var request = ParseFormData(formData);

            //verify that the request came from Slack and not some rando
            if (request.token != CloudConfigurationManager.GetSetting("SlackValidationToken"))
                throw new HttpResponseException(HttpStatusCode.Unauthorized);

            //route the request based on the command
            if (request.command == "/oncall")
            {
                HostingEnvironment.QueueBackgroundWorkItem(ct => NewOnCall(request));
                return Ok();
            }
            else if (request.command == "/offcall")
            {
                HostingEnvironment.QueueBackgroundWorkItem(ct => OffCall(request));
                return Ok();
            }
            else if (request.command == "/whosoncall")
            {
                HostingEnvironment.QueueBackgroundWorkItem(ct => OnCallList(request));
                return Ok();
            }
            else
            {
                return BadRequest($"I did not understand.  Try /oncall, /offcall, or /whosoncall.");
            }
        }

        private static SlackPost ParseFormData(FormDataCollection formData)
        {
            //extract the url-encoded form parameters
            var token = formData["token"];
            var command = formData["command"];
            var channel_id = formData["channel_id"];
            var channel_name = formData["channel_name"];
            var text = formData["text"];
            var user_name = formData["user_name"];
            var user_id = formData["user_id"];
            var response_url = formData["response_url"];

            //route the request to the appropriate method
            return new SlackPost()
                {
                    token = token,
                    command = command,
                    channel_id = channel_id,
                    channel_name = channel_name,
                    text = text,
                    user_id = user_id,
                    user_name = user_name,
                    response_url = response_url
                };
        }

        /// <summary>
        /// Register the caller in the on-call system
        /// </summary>
        /// <param name="request">The request containing the payload of the Slack POST</param>
        private static async Task NewOnCall(SlackPost request)
        {
            var response_text = $"You are now on call for <#{request.channel_id}|{request.channel_name}>";

            try
            {
                var table = GetTableReference();

                var onCall = new OnCallEntry(request.channel_id, request.channel_name, request.user_id, request.user_name)
                {
                    Phone = request.text,
                    DateOn = DateTime.UtcNow
                };
                var insertOperation = TableOperation.Insert(onCall);
                await table.ExecuteAsync(insertOperation);
            }
            catch
            {
                response_text = "There was an error.  Use /whosoncall to verify the operation succeeded or report this to SRE.";
            }

            await PostSlackMessage(request, new SlackResponse(response_text));
        }

        /// <summary>
        /// Remove the caller from the on-call system
        /// </summary>
        /// <param name="request">The request containing the payload of the Slack POST</param>
        private static async Task OffCall(SlackPost request)
        {
            var response_text = $"You are no longer on call for <#{request.channel_id}|{request.channel_name}>.";

            try
            {
                var table = GetTableReference();

                var deleteOperation = TableOperation.Delete(new OnCallEntry(request.channel_id, request.channel_name, request.user_id, request.user_name) { ETag = "*" });
                await table.ExecuteAsync(deleteOperation);
            }
            catch
            {
                response_text = "There was an error removing you from the on call list.  Are you on call?  Try /whosoncall to verify or report this error to SRE.";
            }

            await PostSlackMessage(request, new SlackResponse(response_text));
        }

        /// <summary>
        /// Retrieve the list of those who are on call currently
        /// </summary>
        /// <param name="request">The request containing the payload of the Slack POST</param>
        private static async Task OnCallList(SlackPost request)
        {
            var response_text = "";
            try
            {
                var table = GetTableReference();
                var query = table.CreateQuery<OnCallEntry>();
                var list = table.ExecuteQuery(query);

                //get the slack-formatted strings and join them into a single string, delimited by newline
                response_text = string.Join("\n", list.Select(l => l.SlackString));
                if (string.IsNullOrWhiteSpace(response_text))
                {
                    response_text = "Nobody is currently listed as on call.";
                }
            }
            catch
            {
                response_text = "There was an error retrieving the on-call list.  Try again or report this to SRE";
            }
            await PostSlackMessage(request, new SlackResponse(response_text));
        }

        private static CloudTable GetTableReference()
        {
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(CloudConfigurationManager.GetSetting("oncall2_AzureStorageConnectionString"));
            var client = storageAccount.CreateCloudTableClient();
            var table = client.GetTableReference("OnCallEntries");
            table.CreateIfNotExists();

            return table;
        }

        private static async Task PostSlackMessage(SlackPost userPost, SlackResponse message)
        {
            using (var client = new WebClient())
            {
                client.Headers.Add(HttpRequestHeader.ContentType, "application/json");
                await client.UploadStringTaskAsync(new Uri(userPost.response_url), JsonConvert.SerializeObject(message));
            }
        }
    }
}
