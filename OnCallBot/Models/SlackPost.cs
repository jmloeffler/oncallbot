namespace OnCallBot.Models
{
    public class SlackPost
    {
        public string token;
        public string team_id;
        public string team_domain;
        public string channel_id;
        public string channel_name;
        public string user_id;
        public string user_name;
        public string command;
        public string text;
        public string response_url;
    }

    public class SlackResponse
    {
        public SlackResponse(string text) : this("ephemeral", text)
        {}

        public SlackResponse(string responseType, string text)
        {
            this.response_type = responseType;
            this.text = text;
        }

        public string response_type { get; set; }
        public string text { get; set; }
    }
}