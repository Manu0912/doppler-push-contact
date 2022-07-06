namespace Doppler.PushContact.Models
{
    public class MessageReport
    {
        public string Domain { get; set; }

        public int Sent { get; set; }

        public int Delivered { get; set; }

        public int NotDelivered { get; set; }

        public string Date { get; set; }

        public MessageReport(string domain, int sent, int delivered, int notDelivered, string date)
        {
            Domain = domain;
            Sent = sent;
            Delivered = delivered;
            NotDelivered = notDelivered;
            Date = date;
        }

        public MessageReport()
        {
        }
    }
}
