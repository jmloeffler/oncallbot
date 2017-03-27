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
            var token = formData["token"];
            var command = formData["command"];
            var channel_name = formData["channel_name"];
            var text = formData["text"];
            var user_name = formData["user_name"];

            var request = new SlackPost() { token = token, command = command, channel_name = channel_name, text = text, user_name = user_name };

            if (request.token != CloudConfigurationManager.GetSetting("SlackValidationToken"))
                throw new HttpResponseException(HttpStatusCode.Unauthorized);
            
            if (request.command == "/oncall")
                return NewOnCall(request);
            else if (request.command == "/offcall")
                return OffCall(request);
            else if (request.command == "/whosoncall")
                return OnCallList(request);
            else
                return new SlackResponse { response_type = "ephemeral", text = $"I did not understand.  Try /oncall, /offcall, or /whosoncall." };
        }

        [HttpDelete]
        public void Delete()
        {
            var table = GetTableReference();
            table.DeleteIfExists();
        }

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

            return new SlackResponse { response_type = "ephemeral", text = response_text };
        }

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

            return new SlackResponse { response_type = "ephemeral", text = response_text };
        }

        private static CloudTable GetTableReference()
        {
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(CloudConfigurationManager.GetSetting("oncall2_AzureStorageConnectionString"));
            var client = storageAccount.CreateCloudTableClient();
            var table = client.GetTableReference("OnCallEntries");
            table.CreateIfNotExists();

            return table;
        }

        private static SlackResponse OnCallList(SlackPost request)
        {
            var response_text = "";
            try
            {
                var table = GetTableReference();
                var query = table.CreateQuery<OnCallEntry>();
                var list = table.ExecuteQuery(query);

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
            return new SlackResponse { response_type = "ephemeral", text = response_text };
        }
    }
}
