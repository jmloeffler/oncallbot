using Microsoft.WindowsAzure.Storage.Table;
using System;

namespace OnCallBot.Models
{
    public class OnCallEntry : TableEntity
    {
        public OnCallEntry()
        {
        }

        public OnCallEntry(string team, string name) 
            : base(team, name)
        {
            Team = team;
            Name = name;
        }

        public string Team { get; set; }
        public string Name { get; set; }
        public string Phone { get; set; }
        public DateTimeOffset? TimeOn { get; set; }
        public DateTimeOffset? TimeOff { get; set; }

        public string SlackString { get
            {
                return $"{Name} on call for #{Team} since {TimeOn.Value.ToString()} via {Phone}";
            }
        }
    }
}