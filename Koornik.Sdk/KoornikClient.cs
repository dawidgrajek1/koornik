using Koornik.Contracts.Api;

namespace Koornik.Sdk;

public class KoornikClient(IDeviceApi devices, IZoneApi zones, ITransitionApi transitions)
{
    public IDeviceApi Devices { get; } = devices;
    public IZoneApi Zones { get; } = zones;
    public ITransitionApi Transitions { get; } = transitions;
}
