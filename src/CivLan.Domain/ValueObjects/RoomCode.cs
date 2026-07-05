using CivLan.Domain.Exceptions;

namespace CivLan.Domain.ValueObjects;

public sealed record RoomCode
{
    public string Value { get; }

    private RoomCode(string value) => Value = value;

    public static RoomCode Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new DomainException("Room code is required.");

        var normalized = value.Trim().ToUpperInvariant();
        if (normalized.Length != 6)
            throw new DomainException("Room code must be 6 characters.");

        if (!normalized.All(c => char.IsLetterOrDigit(c)))
            throw new DomainException("Room code must be alphanumeric.");

        return new RoomCode(normalized);
    }

    public static RoomCode Generate(Random random)
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        var buffer = new char[6];
        for (var i = 0; i < buffer.Length; i++)
            buffer[i] = chars[random.Next(chars.Length)];

        return new RoomCode(new string(buffer));
    }

    public override string ToString() => Value;
}
