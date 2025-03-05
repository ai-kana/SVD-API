namespace SVDAPI;

public class SVDClientFactory
{
    private readonly string _User;
    private readonly string _Key;
    private readonly string _EndPoint;
    public SVDClientFactory(string user, string key, string endPoint = "http://www.sshost.club:8080")
    {
        _User = user;
        _Key = key;
        _EndPoint = endPoint;
    }

    public async Task<SVDClient> CreateClientAsync()
    {
        SVDClient client = new(_User, _Key, _EndPoint);
        await client.UpdateTokenAsync();
        return client;
    }
}
