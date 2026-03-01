using Microsoft.Extensions.Logging;
using Payment.Protocol.Impl.Base;
using Payment.Protocol.Interface;
using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;
using System.Text;


namespace Payment.Protocol.Impl
{
    public class TlvMapper : ITlvMapper
    {
        private readonly ILogger<TlvMapper> _logger;
        private const int MaxTlvValueLength = 255;

        private sealed record MapItem(byte Tag, PropertyInfo Prop, Type Type);
        private static readonly ConcurrentDictionary<Type, MapItem[]> _cache = new();

        public TlvMapper(ILogger<TlvMapper> logger)
        {
            _logger = logger;
        }

        // DTO -> TLVs
        public IReadOnlyList<Tlv> ToTlvs(object obj, bool skipEmptyStrings, bool skipDefaultNumbers)
        {
            var type = obj.GetType();
            var map = _cache.GetOrAdd(type, BuildMap);

            var tlvs = new List<Tlv>(map.Length);

            foreach (var m in map)
            {
                var value = m.Prop.GetValue(obj);
                if (value is null) continue;

                // string
                if (value is string s)
                {
                    if (skipEmptyStrings && string.IsNullOrWhiteSpace(s)) continue;
                    ValidateStringLength(s, m.Tag, type.Name, m.Prop.Name);
                    tlvs.Add(Tlv.Ascii(m.Tag, s));
                    continue;
                }

                // Guid
                if (value is Guid g)
                {
                    if (g == Guid.Empty) continue;
                    var guidStr = g.ToString("D");
                    ValidateStringLength(guidStr, m.Tag, type.Name, m.Prop.Name);
                    tlvs.Add(Tlv.Ascii(m.Tag, guidStr));
                    continue;
                }

                // enum -> name (OK/PARTIAL/FAILED)
                if (value.GetType().IsEnum)
                {
                    var name = value.ToString();
                    if (skipEmptyStrings && string.IsNullOrWhiteSpace(name)) continue;
                    ValidateStringLength(name!, m.Tag, type.Name, m.Prop.Name);
                    tlvs.Add(Tlv.Ascii(m.Tag, name!));
                    continue;
                }

                // int/long -> ASCII digits
                if (value is int i)
                {
                    if (skipDefaultNumbers && i == 0) continue;
                    tlvs.Add(Tlv.Digits(m.Tag, i));
                    continue;
                }

                if (value is long l)
                {
                    if (skipDefaultNumbers && l == 0L) continue;
                    tlvs.Add(Tlv.Digits(m.Tag, l));
                    continue;
                }

                // DateTime -> "yyyyMMddHHmmss"
                if (value is DateTime dt)
                {
                    var date = dt.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
                    if (skipEmptyStrings && string.IsNullOrWhiteSpace(date)) continue;
                    tlvs.Add(Tlv.Ascii(m.Tag, date!));
                    continue;
                }

                // DateTimeOffset -> "yyyyMMddHHmmss" (choose local or UTC)
                if (value is DateTimeOffset dto)
                {
                    var date = dto.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
                    if (skipEmptyStrings && string.IsNullOrWhiteSpace(date)) continue;
                    tlvs.Add(Tlv.Ascii(m.Tag, date!));
                    continue;
                }

                if (value is bool b)
                {
                    tlvs.Add(Tlv.Ascii(m.Tag, b ? "1" : "0"));
                    continue;
                }

                // byte[] -> raw TLV
                if (value is byte[] bytes)
                {
                    if (bytes.Length == 0) continue;
                    if (bytes.Length > MaxTlvValueLength)
                        throw new ArgumentException(
                            $"Byte array too long for {type.Name}.{m.Prop.Name} (tag 0x{m.Tag:X2}): {bytes.Length} bytes (max {MaxTlvValueLength})");
                    tlvs.Add(new Tlv(m.Tag, bytes));
                    continue;
                }

                throw new NotSupportedException(
                    $"TLV serialization does not support {type.Name}.{m.Prop.Name} ({m.Type}).");
            }

            return tlvs;
        }
        // TLVs -> DTO
        public T FromTlvs<T>(IReadOnlyList<Tlv> tlvs) where T : new()
        {
            var type = typeof(T);
            var map = _cache.GetOrAdd(type, BuildMap);

            // tag -> value lookup (last wins)
            var dict = new Dictionary<byte, ReadOnlyMemory<byte>>(tlvs.Count);
            foreach (var t in tlvs)
                dict[t.Tag] = t.Value;

            var obj = new T();

            foreach (var m in map)
            {
                if (!m.Prop.CanWrite) continue;
                if (!dict.TryGetValue(m.Tag, out var raw)) continue;

                object? converted = ConvertRaw(raw, m.Type);
                if (converted is null) continue;

                m.Prop.SetValue(obj, converted);
            }

            //validate the required Properties
            return obj;
        }

        private object? ConvertRaw(ReadOnlyMemory<byte> raw, Type targetType)
        {
            if (targetType == typeof(byte[]))
                return raw.ToArray();

            var s = Encoding.ASCII.GetString(raw.Span);

            if (targetType == typeof(string))
                return s;

            if (targetType == typeof(Guid))
                return Guid.TryParse(s, out var g) ? g : null;

            if (targetType == typeof(Guid?))
                return Guid.TryParse(s, out var g2) ? g2 : (Guid?)null;

            if (targetType == typeof(int) || targetType == typeof(int?))
                return int.TryParse(s, NumberStyles.None, CultureInfo.InvariantCulture, out var i) ? i : null;

            if (targetType == typeof(long) || targetType == typeof(long?))
                return long.TryParse(s, NumberStyles.None, CultureInfo.InvariantCulture, out var l) ? l : null;

            // DateTime -> "yyyyMMddHHmmss" (FIXED: added deserialization)
            if (targetType == typeof(DateTime))
                return DateTime.TryParseExact(s, "yyyyMMddHHmmss",
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt) ? dt : (DateTime?)null;

            if (targetType == typeof(DateTime?))
                return DateTime.TryParseExact(s, "yyyyMMddHHmmss",
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt2) ? dt2 : (DateTime?)null;

            // DateTimeOffset -> "yyyyMMddHHmmss" (FIXED: added deserialization)
            if (targetType == typeof(DateTimeOffset))
                return DateTimeOffset.TryParseExact(s, "yyyyMMddHHmmss",
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out var dto) ? dto : (DateTimeOffset?)null;

            if (targetType == typeof(DateTimeOffset?))
                return DateTimeOffset.TryParseExact(s, "yyyyMMddHHmmss",
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out var dto2) ? dto2 : (DateTimeOffset?)null;

            // bool -> "1"/"0" (FIXED: added deserialization)
            if (targetType == typeof(bool))
                return s == "1" || s.Equals("true", StringComparison.OrdinalIgnoreCase);

            if (targetType == typeof(bool?))
            {
                if (s == "1" || s.Equals("true", StringComparison.OrdinalIgnoreCase))
                    return true;
                if (s == "0" || s.Equals("false", StringComparison.OrdinalIgnoreCase))
                    return false;
                return (bool?)null;
            }

            if (targetType.IsEnum)
                return Enum.TryParse(targetType, s, ignoreCase: true, out var e) ? e : null;

            var underlying = Nullable.GetUnderlyingType(targetType);
            if (underlying is not null && underlying.IsEnum)
                return Enum.TryParse(underlying, s, ignoreCase: true, out var e) ? e : null;

            throw new NotSupportedException($"Cannot convert TLV value to {targetType}");
        }


        private static void ValidateStringLength(string s, byte tag, string typeName, string propName)
        {
            var byteCount = Encoding.ASCII.GetByteCount(s);
            if (byteCount > MaxTlvValueLength)
            {
                throw new ArgumentException(
                    $"String too long for {typeName}.{propName} (tag 0x{tag:X2}): {byteCount} bytes (max {MaxTlvValueLength})");
            }
        }

        private static MapItem[] BuildMap(Type type)
        {
            var items = type.GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Select(p => (p, attr: p.GetCustomAttribute<TlvTagAttribute>()))
                .Where(x => x.attr is not null && x.p.CanRead)
                .Select(x => new MapItem(x.attr!.Tag, x.p, x.p.PropertyType))
                .OrderBy(x => x.Tag) // stable order helps debug hex dumps
                .ToArray();

            // VALIDATE: No duplicate tags (FIXED: added validation)
            var duplicates = items
                .GroupBy(x => x.Tag)
                .Where(g => g.Count() > 1)
                .ToArray();

            if (duplicates.Any())
            {
                var dupTags = string.Join(", ", duplicates.Select(g =>
                    $"0x{g.Key:X2} ({string.Join(", ", g.Select(i => i.Prop.Name))})"));
                throw new InvalidOperationException(
                    $"Type {type.Name} has duplicate TLV tags: {dupTags}");
            }

            return items;
        }
    }

}