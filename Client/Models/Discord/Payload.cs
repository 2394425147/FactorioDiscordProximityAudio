// ReSharper disable ClassCanBeSealed.Global
// ReSharper disable InconsistentNaming
// ReSharper disable UnusedAutoPropertyAccessor.Global
// ReSharper disable ClassNeverInstantiated.Global

#pragma warning disable CS8618

using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Client.Models.Discord;

public class Payload
{
    [JsonIgnore]
    private readonly JsonSerializerOptions _jsonSerializerOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public virtual string cmd   { get; set; }
    public virtual string evt   { get; set; }
    public virtual string nonce { get; set; }

    public enum OpCode
    {
        Handshake = 0,
        Frame     = 1,
        Close     = 2,
        Ping      = 3,
        Pong      = 4,
        Error     = 5 // Not used
    }

    public string Serialize()
    {
        // Without typecasting, the serializer omits derived properties
        return JsonSerializer.Serialize<object>(this, _jsonSerializerOptions);
    }

    public static byte[] Pack(OpCode opCode, string json)
    {
        var       jsonBytes = Encoding.UTF8.GetBytes(json);
        using var ms        = new MemoryStream();
        using var writer    = new BinaryWriter(ms);
        writer.Write((int)opCode);
        writer.Write(jsonBytes.Length);
        writer.Write(jsonBytes);
        return ms.ToArray();
    }

    public async Task Send(NamedPipeClientStream pipe, CancellationToken cancellationToken)
    {
        var packet = Pack(OpCode.Frame, Serialize());
        await pipe.WriteAsync(packet, cancellationToken);
        await pipe.FlushAsync(cancellationToken);
    }
}

public abstract class SendPayload<TArgs> : Payload
{
    public abstract override string cmd   { get; }
    public override          string evt   { get; set; }
    public override          string nonce { get; set; }
    public                   TArgs  args  { get; set; }
}

public abstract class ReceivePayload<TData> : Payload
{
    public override string cmd   { get; set; }
    public override string evt   { get; set; }
    public override string nonce { get; set; }
    public          TData? data  { get; set; }
}
