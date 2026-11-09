using System.Net;
using System.Text;

namespace aberaTech.Server.Training;

/// <summary>
/// Mostafa Saad's sheet in memory, for `make e2e` (Training:Fake,
/// Development only) and the tests: a few rows in his layout, at levels
/// around a 900 rating, of each importance.
/// </summary>
public sealed class FakeSaadSheet : HttpMessageHandler
{
    public const string Csv = """"
        Code,Category,level,Sol,Category ID,Importance,Can't solve,Tried to solve
        CF1-D2-A,"greedy, ""quoted""",1.5,Editorial,[32],p5,,
        CF2-D2-B,implementation,2,,[1],p4,,
        CF3-D2-B,math,2.25,,[1],p3 v2,,
        CF4-D2-C,dp,2.5,,[16],p3,,
        CF5-D2-C,dp,2.75,,[16],p4,,
        CF6-D2-C,graphs,2.9,,[46],p3,,
        CF7-D2-C,greedy,3,,[32],p5,,
        CF8-D2-C,sorting,3.25,,[9],p3,,
        CF9-D2-D,binary search,3.5,,[9],p4,,
        SPOJ METEORS,parallel binary search,3.5,,[9],p3,,
        Atcoder087-ARC-C,constructive,3.75,,[1],p4,,
        CF10-D2-D,"line
        break",4.5,,[1],p3,,
        CF11-D2-E,trees,5,,[50],p5,,
        CF12-D2-E,trees,5,,[50],p2,,
        CF13-D2-E,boring,4,,[1],,,
        UVA 11722,probability,2.5,,[1],p3,,
        """";

    public List<Uri> Requests { get; } = [];

    public HttpStatusCode? Down { get; set; }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        lock (Requests) Requests.Add(request.RequestUri!);
        if (Down is { } down) return Task.FromResult(new HttpResponseMessage(down));
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(Csv, Encoding.UTF8, "text/csv"),
        });
    }
}
