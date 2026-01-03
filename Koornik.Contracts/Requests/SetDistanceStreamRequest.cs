namespace Koornik.Contracts.Requests;

/// <summary>
/// Request to enable or disable distance streaming for a device.
/// </summary>
public class SetDistanceStreamRequest
{
    /// <summary>
    /// Whether distance streaming should be enabled.
    /// </summary>
    public bool Enabled { get; set; }
}
