using System;
using System.Linq;

namespace ModbusGetFree
{
    public partial class DataWindow
    {
        public readonly string AiId = Guid.NewGuid().ToString("N");
        DateTime? aiLastReadUtc;
        DateTime? aiLastAttemptUtc;
        string aiLastError;
        string aiReadIdentity;
        string aiReadSource;
        Guid aiReadSession;

        string AiIdentity()
        {
            return State.Unit + ":" + State.Function + ":" + State.Address + ":" + State.Quantity + ":" + EnronActive
                + ":" + string.Join(",", State.Values.Select(v => v.ToString()).ToArray());
        }

        void AiReadSucceeded()
        {
            aiLastReadUtc = DateTime.UtcNow;
            aiReadIdentity = AiIdentity();
            aiReadSource = host.Bus.IsDemo ? "demo" : "device";
            aiReadSession = host.Bus.SessionId;
        }

        public string AiQuality(DateTime now)
        {
            if (!aiLastReadUtc.HasValue) return "never_read";
            if (!host.Bus.Connected) return "disconnected";
            if (aiReadSession != host.Bus.SessionId || aiReadIdentity != AiIdentity()) return "unverified";
            if (aiLastError != null) return "read_error";
            if ((now - aiLastReadUtc.Value).TotalMilliseconds > Math.Max(3000.0, State.ScanRate * 3.0)) return "stale";
            return "fresh";
        }

        public object AiSnapshot(DateTime now)
        {
            return new {
                id = AiId, title = State.Title, slave = State.Unit, function = State.Function,
                startAddress = State.Address, addressBase = 0, scanRateMs = State.ScanRate,
                pollingDisabled = State.Disabled, source = aiReadSource ?? "none", quality = AiQuality(now),
                lastSuccessUtc = aiLastReadUtc.HasValue ? aiLastReadUtc.Value.ToString("o") : null,
                lastAttemptUtc = aiLastAttemptUtc.HasValue ? aiLastAttemptUtc.Value.ToString("o") : null,
                lastError = aiLastError, transactions = Tx, errors = Errors,
                values = GroupStarts().Select(i => {
                    var d = DefinitionAt(i);
                    return new { address = State.Address + i / RegisterStride,
                        name = i < State.Names.Length ? State.Names[i] : null, unit = (string)null,
                        rawWords = State.Values.Skip(i).Take(GroupWidth(d)).ToArray(),
                        displayValue = FormatGroup(i), format = IsBitArea ? "Bit" : ValueCodec.Formats[d.Format],
                        byteOrder = d.Order, scale = d.Scale, offset = d.Offset };
                }).ToArray()
            };
        }
    }
}
