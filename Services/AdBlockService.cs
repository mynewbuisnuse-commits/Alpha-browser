namespace AlphaBrowser.Services;

public static class AdBlockService
{
    public static readonly string[] BlockedHosts = new[]
    {
        "doubleclick.net", "googlesyndication.com", "googleadservices.com",
        "adservice.google.com", "adsystem.amazon.com", "amazon-adsystem.com",
        "scorecardresearch.com", "outbrain.com", "taboola.com", "criteo.com",
        "criteo.net", "adnxs.com", "pubmatic.com", "rubiconproject.com",
        "openx.net", "moatads.com", "advertising.com", "adsrvr.org",
        "yieldmo.com", "zedo.com", "ads.yahoo.com", "facebook.net",
        "connect.facebook.net", "analytics.google.com", "google-analytics.com",
        "googletagmanager.com", "googletagservices.com", "hotjar.com",
        "mixpanel.com", "segment.io", "quantserve.com", "chartbeat.com",
        "newrelic.com", "branch.io", "adcolony.com", "tapad.com",
        "media.net", "popads.net", "propellerads.com", "revcontent.com",
        "exoclick.com", "trafficjunky.net", "juicyads.com", "snigelweb.com",
        "smartadserver.com"
    };

    public static bool ShouldBlockUrl(string url)
    {
        if (string.IsNullOrEmpty(url)) return false;
        if (!Uri.TryCreate(url, UriKind.Absolute, out var u)) return false;
        var host = u.Host.ToLowerInvariant();
        foreach (var bad in BlockedHosts)
            if (host == bad || host.EndsWith("." + bad))
                return true;
        return false;
    }

    public const string CosmeticCss = @"
        iframe[src*='doubleclick'],
        iframe[src*='googlesyndication'],
        iframe[src*='adservice'],
        div[id^='google_ads_'],
        ins.adsbygoogle,
        div[class*='ad-container'],
        div[class*='advert'],
        div[id*='banner-ad'],
        div[id*='taboola-'],
        div[id*='outbrain-'] { display:none !important; visibility:hidden !important; height:0 !important; }";

    public static string InjectCssScript() =>
        "(function(){try{var s=document.createElement('style');s.textContent=" +
        System.Text.Json.JsonSerializer.Serialize(CosmeticCss) +
        ";(document.head||document.documentElement).appendChild(s);}catch(e){}})();";
}
