using Microsoft.Azure;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;
using OnCallBot.Models;
using System;
using System.Linq;
using System.Net;
using System.Net.Http.Formatting;
using System.Web.Http;

namespace OnCallBot.Controllers
{
    public class OnCallController : ApiController
    {
        // POST: api/OnCall
        public SlackResponse Post(FormDataCollection formData)
        {
            //extract the url-encoded form parameters
            var token = formData["token"];
            var command = formData["command"];
            var channel_name = formData["channel_name"];
            var text = formData["text"];
            var user_name = formData["user_name"];

            //verify that the request came from Slack and not some rando
            if (token != CloudConfigurationManager.GetSetting("SlackValidationToken"))
                throw new HttpResponseException(HttpStatusCode.Unauthorized);

            //route the request to the appropriate method
            var request = new SlackPost() { token = token, command = command, channel_name = channel_name, text = text, user_name = user_name };
            if (request.command == "/oncall")
                return NewOnCall(request);
            else if (request.command == "/offcall")
                return OffCall(request);
            else if (request.command == "/whosoncall")
                return OnCallList(request);
            else
                return new SlackResponse($"I did not understand.  Try /oncall, /offcall, or /whosoncall.");
        }
        
        /// <summary>
        /// Register the caller in the on-call system
        /// </summary>
        /// <param name="request">The request containing the payload of the Slack POST</param>
        /// <returns>The response to be returned to Slack</returns>
        private static SlackResponse NewOnCall(SlackPost request)
        {
            var response_text = "It shall be recorded";

            try
            {
                var table = GetTableReference();

                var onCall = new OnCallEntry(request.channel_name, request.user_name)
                {
                    Phone = request.text,
                    TimeOn = DateTimeOffset.Now.ToUniversalTime()
                };
                var insertOperation = TableOperation.Insert(onCall);
                table.Execute(insertOperation);
            }
            catch
            {
                response_text = "There was an error.  Use /whosoncall to verify the operation succeeded or report this to SRE.";
            }

            return new SlackResponse(response_text);
        }

        /// <summary>
        /// Remove the caller from the on-call system
        /// </summary>
        /// <param name="request">The request containing the payload of the Slack POST</param>
        /// <returns>The response to be returned to Slack</returns>
        private static SlackResponse OffCall(SlackPost request)
        {
            var response_text = "You are no longer on call.";

            try
            {
                var table = GetTableReference();

                var deleteOperation = TableOperation.Delete(new OnCallEntry(request.channel_name, request.user_name) { ETag = "*" });
                table.Execute(deleteOperation);
            }
            catch
            {
                response_text = "There was an error removing you from the on call list.  Are you on call?  Try /whosoncall to verify or report this error to SRE.";
            }

            return new SlackResponse(response_text);
        }

        /// <summary>
        /// Retrieve the list of those who are on call currently
        /// </summary>
        /// <param name="request">The request containing the payload of the Slack POST</param>
        /// <returns>The response to be returned to Slack</returns>
        private static SlackResponse OnCallList(SlackPost request)
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
            return new SlackResponse(response_text);
        }

        private static CloudTable GetTableReference()
        {
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(CloudConfigurationManager.GetSetting("oncall2_AzureStorageConnectionString"));
            var client = storageAccount.CreateCloudTableClient();
            var table = client.GetTableReference("OnCallEntries");
            table.CreateIfNotExists();

            return table;
        }
    }
}
