using Microsoft.WindowsAzure.Storage.Table;
using System;

namespace OnCallBot.Models
{
    public class OnCallEntry : TableEntity
    {
        public OnCallEntry()
        {
        }

        public OnCallEntry(string team_id, string team_name, string user_id, string user_name) 
            : base(team_id, user_id)
        {
            TeamId = team_id;
            Team = team_name;
            UserId = user_id;
            UserName = user_name;
        }

        public string TeamId { get; set; }
        public string Team { get; set; }
        public string UserId { get; set; }
        public string UserName { get; set; }
        public string Phone { get; set; }
        public DateTime DateOn { get; set; }

        public string SlackString { get
            {
                return $"<@{UserId}|{UserName}> on call for <#{TeamId}|{Team}> since <!date^{ToUnixEpochSeconds(DateOn)}^{{date_long_pretty}}|{DateOn.ToString()} UTC> via {Phone}";
            }
        }

        private long ToUnixEpochSeconds(DateTime time)
        {
            //this is all available in framework 4.6 as DateTime.ToUnixEpochSeconds
            var seconds = time.Ticks / TimeSpan.TicksPerSecond;
            var unixEpochTime = new DateTime(1970, 1, 1).Ticks / TimeSpan.TicksPerSecond;
            return seconds - unixEpochTime;
        }
    }
}