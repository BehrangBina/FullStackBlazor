namespace Shared
{
    public record BookingResult(
        bool Success,
        string Message,
        string NewVersion,
        Seat[] SeatsSnapshot
    );
}
