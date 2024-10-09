namespace AasxServerStandardBib.Models
{
    public class NotificationMessage
    {
        public string Type { get; set; }
        public object Payload { get; set; }
        public string Upn { get; set; }
        public string DeviceId { get; set; }
        public string BrokerId { get; set; }
        public string AssetId { get; set; }
        public string TenantId { get; set; }
        public string SubscriptionId { get; set; }
        public string ApplicationId { get; set; }
        public string ProjectId { get; set; }
        public string TargetId { get; set; }
        // public string DeviceTemplateId { get; set; }
        // public string AssetTemplateId { get; set; }
        // public string TableId { get; set; }
        public NotificationMessage()
        {
        }
    }
}
