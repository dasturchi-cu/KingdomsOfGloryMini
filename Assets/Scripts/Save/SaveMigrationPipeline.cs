using System;
using UnityEngine;

namespace KoG.MiniMvp.Save
{
    /// <summary>
    /// Migrates envelopes from older schema versions to <see cref="SaveSchema.CurrentVersion"/>.
    /// Add a step per breaking change — never mutate in place without bumping version.
    /// </summary>
    public static class SaveMigrationPipeline
    {
        public static SaveResult Migrate(SaveEnvelope envelope)
        {
            if (envelope == null)
                return SaveResult.Fail(SaveResultKind.Corrupt, "null");

            if (envelope.schemaVersion > SaveSchema.CurrentVersion)
            {
                return SaveResult.Fail(SaveResultKind.VersionUnsupported,
                    "future schema " + envelope.schemaVersion);
            }

            var current = envelope;
            while (current.schemaVersion < SaveSchema.CurrentVersion)
            {
                var next = MigrateOne(current);
                if (!next.Success)
                    return next;
                current = next.Envelope;
            }

            return SaveResult.Ok(current);
        }

        static SaveResult MigrateOne(SaveEnvelope from)
        {
            switch (from.schemaVersion)
            {
                case 0:
                    return MigrateV0ToV1(from);
                default:
                    return SaveResult.Fail(SaveResultKind.VersionUnsupported,
                        "no migrator for v" + from.schemaVersion);
            }
        }

        static SaveResult MigrateV0ToV1(SaveEnvelope from)
        {
            // v0 had payload inline without checksum/revision — fill defaults.
            var next = new SaveEnvelope
            {
                schemaVersion = 1,
                payloadType = string.IsNullOrEmpty(from.payloadType)
                    ? SaveSchema.PlayerCachePayloadType
                    : from.payloadType,
                playerId = from.playerId ?? string.Empty,
                revision = Math.Max(1, from.revision),
                writtenAtUtc = string.IsNullOrEmpty(from.writtenAtUtc)
                    ? DateTime.UtcNow.ToString("o")
                    : from.writtenAtUtc,
                encrypted = from.encrypted,
                payload = from.payload ?? string.Empty,
                checksumSha256 = string.IsNullOrEmpty(from.checksumSha256)
                    ? SaveChecksum.Sha256Hex(from.payload ?? string.Empty)
                    : from.checksumSha256
            };
            Debug.Log("[Save] Migrated envelope v0 → v1");
            return SaveResult.Ok(next);
        }
    }
}
