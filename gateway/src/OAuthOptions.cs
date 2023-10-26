public class OAuthOptions
{
    public OAuthOptions(string clientID, string clientSecret, string jwtKey, string domain)
    {
        ClientID = clientID;
        ClientSecret = clientSecret;
        JwtKey = jwtKey;
        Domain = domain;
    }

    public string ClientID { get; set; }
    public string ClientSecret { get; set; }
    public string JwtKey { get; set; }
    public string Domain { get; set; }
}