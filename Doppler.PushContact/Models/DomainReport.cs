namespace Doppler.PushContact.Models
{
    public class DomainReport
    {
        public string Domain { get; set; }

        public int PushContactQuantity { get; set; }

        public bool is_push_feature_enable { get; set; }

        public DomainReport(string domain, int pushContactQuantity)
        {
            Domain = domain;
            PushContactQuantity = pushContactQuantity;
        }

        public DomainReport()
        {
        }
    }
}

