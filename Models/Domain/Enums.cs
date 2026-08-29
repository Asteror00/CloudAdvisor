namespace CloudAdvisor.Models.Domain
{
    public enum SessionStatus
    {
        Pending,
        Processing,
        Completed,
        Failed
    }

    public enum FeatureType
    {
        Controller,
        DbContext,
        AuthMiddleware,
        FileHandling,
        BackgroundService,
        ApiEndpoint
    }

    public enum ServiceCategory
    {
        Compute,
        Storage,
        Database,
        Networking,
        Identity
    }

    public enum RecommendationPriority
    {
        Required,
        Recommended,
        Optional
    }
}
