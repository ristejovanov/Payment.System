using Microsoft.Extensions.Logging;
using Payment.GT.Classes.Interface;
using Payment.Protocol;
using Payment.Protocol.Impl.Base;
using Payment.Shared.Dto;
using Payment.Shared.Enums;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Payment.GT.Classes
{
    public sealed class GatewayProcessor
    {
        private readonly IGatewayStateStore _store;
        private readonly IIssuerClient _issuer;
        private readonly ILogger<GatewayProcessor> _log;

        public GatewayProcessor(IGatewayStateStore store, IIssuerClient issuer, ILogger<GatewayProcessor> log)
        {
            _store = store;
            _issuer = issuer;
            _log = log;
        }

        public async Task<byte[]> HandleAsync(ParsedFrame req, CancellationToken ct)
        {
            return req.MsgType switch
            {
                0x70 => await HandleA70Async(req, ct),
                0x72 => await HandleA72Async(req, ct),
                0x01 => HandleHeartbeat(req), // if you define heartbeat msgType
                _ => BuildErrorResponse(req, "96", "UNSUPPORTED_MSG_TYPE")
            };
        }

        private async Task<byte[]> HandleA70Async(ParsedFrame req, CancellationToken ct)
        {
            var atmId = req.GetAsciiOrNull(Tags.AtmId) ?? "";
            var stanStr = req.GetAsciiOrNull(Tags.Stan) ?? "";
            var corr = req.GetAsciiOrNull(Tags.CorrelationId) ?? "";
            var isRepeat = req.GetAsciiOrNull(Tags.IsRepeat) ?? "0";

            if (string.IsNullOrWhiteSpace(atmId) || string.IsNullOrWhiteSpace(stanStr) || string.IsNullOrWhiteSpace(corr))
                return BuildA71(atmId, stanStr, corr, "96", null, "MISSING_FIELDS");

            if (!long.TryParse(stanStr, out var stan))
                return BuildA71(atmId, stanStr, corr, "96", null, "BAD_STAN");

            using var scope = _log.BeginScope(new Dictionary<string, object?>
            {
                ["atmId"] = atmId,
                ["stan"] = stan,
                ["correlationId"] = corr,
                ["isRepeat"] = isRepeat
            });

            // Idempotency: return same response if already processed
            var dedupeKey = $"A70:{atmId}:{stan}";
            if (_store.TryGetResponse(dedupeKey, out var cached))
            {
                _log.LogInformation("A70 dedup hit; returning cached A71");
                return cached;
            }

            var pan = req.GetAsciiOrNull(Tags.Pan) ?? "";
            var pinBlock = req.GetAsciiOrNull(Tags.PinBlock) ?? "";
            var amountStr = req.GetAsciiOrNull(Tags.AmountMinor) ?? "0";
            var currency = req.GetAsciiOrNull(Tags.Currency) ?? "EUR";

            if (!int.TryParse(amountStr, out var amountMinor))
                return BuildA71(atmId, stanStr, corr, "96", null, "BAD_AMOUNT");

            // issuer timeout 2s
            IssuerDecision decision;
            try
            {
                using var issuerCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                issuerCts.CancelAfter(TimeSpan.FromSeconds(2));
                decision = await _issuer.AuthorizeAsync(pan, pinBlock, amountMinor, currency, issuerCts.Token);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                decision = new IssuerDecision { Rc = "91", AuthCode = null, Message = "ISSUER_UNAVAILABLE" };
            }

            var status = decision.Rc == "00" ? ReservationStatus.Approved :
                         decision.Rc == "91" ? ReservationStatus.Timeout :
                         ReservationStatus.Declined;

            _store.UpsertReservation(new ReservationRecord(
                AtmId: atmId,
                Stan: stan,
                CorrelationId: corr,
                Status: status,
                Rc: decision.Rc,
                AuthCode: decision.AuthCode,
                Message: decision.Message,
                CreatedUtc: DateTimeOffset.UtcNow
            ));

            var resp = BuildA71(atmId, stanStr, corr, decision.Rc, decision.AuthCode, decision.Message);
            _store.StoreResponse(dedupeKey, resp);
            return resp;
        }

        private async Task<byte[]> HandleA72Async(ParsedFrame req, CancellationToken ct)
        {
            var atmId = req.GetAsciiOrNull(Tags.AtmId) ?? "";
            var stanStr = req.GetAsciiOrNull(Tags.Stan) ?? "";     // completion message stan
            var corr = req.GetAsciiOrNull(Tags.CorrelationId) ?? "";
            var originalStanStr = req.GetAsciiOrNull(Tags.OriginalStan) ?? "";

            if (string.IsNullOrWhiteSpace(atmId) || string.IsNullOrWhiteSpace(originalStanStr) || string.IsNullOrWhiteSpace(corr))
                return BuildA73(atmId, stanStr, corr, "96", "MISSING_FIELDS");

            if (!long.TryParse(originalStanStr, out var originalStan))
                return BuildA73(atmId, stanStr, corr, "96", "BAD_ORIGINAL_STAN");

            using var scope = _log.BeginScope(new Dictionary<string, object?>
            {
                ["atmId"] = atmId,
                ["correlationId"] = corr,
                ["originalStan"] = originalStan,
                ["completionStan"] = stanStr
            });

            // Idempotency: completion repeats should return same A73
            var dedupeKey = $"A72:{atmId}:{originalStan}";
            if (_store.TryGetResponse(dedupeKey, out var cached))
            {
                _log.LogInformation("A72 dedup hit; returning cached A73");
                return cached;
            }

            // Must reference existing approved reservation
            if (!_store.TryGetReservationByStan(atmId, originalStan, out var res) || res.Status != ReservationStatus.Approved)
            {
                var notFound = BuildA73(atmId, stanStr, corr, "25", "NOT_FOUND");
                _store.StoreResponse(dedupeKey, notFound);
                return notFound;
            }

            // In real world: finalize debit / reversal based on dispenseResult + dispensedAmountMinor.
            // Here we just accept and respond.
            var ok = BuildA73(atmId, stanStr, corr, "00", "COMPLETED");
            _store.StoreResponse(dedupeKey, ok);
            return ok;
        }

        private static byte[] CacheAndReturn(string key, byte[] resp) => resp;

        private static byte[] BuildA71(string atmId, string stan, string corr, string rc, string? authCode, string? msg)
        {
            // 4) Build TLVs for A70
            var tlvs = new List<Tlv>
            {
                FrameWriter.Ascii(Tags.AtmId, atmId),
                FrameWriter.Ascii(Tags.Stan, stan.ToString(CultureInfo.InvariantCulture)),
                FrameWriter.Ascii(Tags.CorrelationId, corr),
                FrameWriter.Ascii(Tags.Rc, rc),
            };

            if (!string.IsNullOrWhiteSpace(authCode))
                tlvs.Add(FrameWriter.Ascii(Tags.AuthCode, authCode));
            if (!string.IsNullOrWhiteSpace(msg))
                tlvs.Add(FrameWriter.Ascii(Tags.Message, msg));

            return FrameWriter.BuildFrame(MessageTypes.A71, tlvs);
        }

        private static byte[] BuildA73(string atmId, string stan, string corr, string rc, string? msg)
        {

            var tlvs = new List<Tlv>
            {
                FrameWriter.Ascii(Tags.AtmId, atmId),
                FrameWriter.Ascii(Tags.Stan, stan.ToString(CultureInfo.InvariantCulture)),
                FrameWriter.Ascii(Tags.CorrelationId, corr),
                FrameWriter.Ascii(Tags.Rc, rc),
            };

            if (!string.IsNullOrWhiteSpace(msg))
                tlvs.Add(FrameWriter.Ascii(Tags.Message, msg));

            return FrameWriter.BuildFrame(MessageTypes.A71, tlvs);
        }

        private static byte[] BuildErrorResponse(ParsedFrame req, string rc, string msg)
        {
            // respond with same correlation/atmId/stan if available; fall back if missing
            var atmId = req.GetAsciiOrNull(Tags.AtmId) ?? "";
            var stan = req.GetAsciiOrNull(Tags.Stan) ?? "";
            var corr = req.GetAsciiOrNull(Tags.CorrelationId) ?? "";
            return req.MsgType == 0x70 ? BuildA71(atmId, stan, corr, rc, null, msg)
                 : req.MsgType == 0x72 ? BuildA73(atmId, stan, corr, rc, msg);
        }

        private static byte[] HandleHeartbeat(ParsedFrame req)
        {
            // If you add heartbeat msgType, respond accordingly.
            return FrameWriter.BuildFrame(MessageTypes.Pong, tlvs: Array.Empty<Tlv>());
        }
    }

}
