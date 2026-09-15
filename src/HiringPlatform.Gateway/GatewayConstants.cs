internal static class GatewayConstants
{
    public const string IdentityClient = "identity";
    public const string ApplicantClient = "applicant";
    public const string BlobClient = "blob";
    public const string RecruiterClient = "recruiter";
    public const string IdentityAddress = "https+http://identity-service";
    public const string ApplicantAddress = "https+http://applicant-service";
    public const string BlobAddress = "https+http://blob-service";
    public const string RecruiterAddress = "https+http://recruiter-service";
    public const string ApiPattern = "/api/{**path}";
}
