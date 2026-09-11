namespace Faultline.Sdk;

public class FaultlineOptions
{
    /// <summary>Ingestion API base URL, e.g. https://faultline.internal.bistec.com</summary>
    public string ServerUrl { get; set; } = default!;

    /// <summary>Project public key (from the Faultline dashboard).</summary>
    public string ProjectKey { get; set; } = default!;

    public string Environment { get; set; } = "production";
    public string? Release { get; set; }
}
