namespace GreekReaderAPI.Services;

public interface IEmailThrottle
{
    bool TryAcquire(string normalisedAddress);
}
