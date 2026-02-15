using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;
using System.Text;


namespace Payment.Protocol.Base
{
    public static class TlvReflectionMapper
    {
        private sealed record MapItem(byte Tag, PropertyInfo Prop, Type Type);
        private static readonly ConcurrentDictionary<Type, MapItem[]> _cache = new();

        // DTO -> TLVs
        public static IReadOnlyList<Tlv> ToTlvs(object obj, bool skipEmptyStrings, bool skipDefaultNumbers)
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
                    tlvs.Add(FrameWriter.Ascii(m.Tag, s));
                    continue;
                }

                // Guid
                if (value is Guid g)
                {
                    if (g == Guid.Empty) continue;
                    tlvs.Add(FrameWriter.Ascii(m.Tag, g.ToString("D")));
                    continue;
                }

                // enum -> name (OK/PARTIAL/FAILED)
                if (value.GetType().IsEnum)
                {
                    var name = value.ToString();
                    if (skipEmptyStrings && string.IsNullOrWhiteSpace(name)) continue;
                    tlvs.Add(FrameWriter.Ascii(m.Tag, name!));
                    continue;
                }

                // int/long -> ASCII digits
                if (value is int i)
                {
                    if (skipDefaultNumbers && i == 0) continue;
                    tlvs.Add(FrameWriter.Digits(m.Tag, i));
                    continue;
                }

                if (value is long l)
                {
                    if (skipDefaultNumbers && l == 0L) continue;
                    tlvs.Add(FrameWriter.Digits(m.Tag, l));
                    continue;
                }

                // byte[] -> raw TLV
                if (value is byte[] bytes)
                {
                    if (bytes.Length == 0) continue;
                    tlvs.Add(new Tlv(m.Tag, bytes));
                    continue;
                }

                throw new NotSupportedException(
                    $"TLV serialization does not support {type.Name}.{m.Prop.Name} ({m.Type}).");
            }

            return tlvs;
        }

        // TLVs -> DTO
        public static T FromTlvs<T>(IReadOnlyList<Tlv> tlvs) where T : new()
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

            return obj;
        }

        private static object? ConvertRaw(ReadOnlyMemory<byte> raw, Type targetType)
        {
            if (targetType == typeof(byte[]))
                return raw.ToArray();

            var s = Encoding.ASCII.GetString(raw.Span);

            if (targetType == typeof(string))
                return s;

            if (targetType == typeof(Guid))
                return Guid.TryParse(s, out var g) ? g : null;

            if (targetType == typeof(int) || targetType == typeof(int?))
                return int.TryParse(s, NumberStyles.None, CultureInfo.InvariantCulture, out var i) ? i : null;

            if (targetType == typeof(long) || targetType == typeof(long?))
                return long.TryParse(s, NumberStyles.None, CultureInfo.InvariantCulture, out var l) ? l : null;

            if (targetType.IsEnum)
                return Enum.TryParse(targetType, s, ignoreCase: true, out var e) ? e : null;

            var underlying = Nullable.GetUnderlyingType(targetType);
            if (underlying is not null && underlying.IsEnum)
                return Enum.TryParse(underlying, s, ignoreCase: true, out var e) ? e : null;

            throw new NotSupportedException($"Cannot convert TLV value to {targetType}");
        }

        private static MapItem[] BuildMap(Type type)
            => type.GetProperties(BindingFlags.Instance | BindingFlags.Public)
                   .Select(p => (p, attr: p.GetCustomAttribute<TlvTagAttribute>()))
                   .Where(x => x.attr is not null && x.p.CanRead)
                   .Select(x => new MapItem(x.attr!.Tag, x.p, x.p.PropertyType))
                   .OrderBy(x => x.Tag) // stable order helps debug hex dumps
                   .ToArray();
    }

}
