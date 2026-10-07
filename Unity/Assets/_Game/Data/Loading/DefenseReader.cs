using Game.Core.Results;
using Game.Data.Match;

namespace Game.Data.Loading
{
    /// <summary>Strict reader + semantic checks for Data/Balance/defense.json.</summary>
    public static class DefenseReader
    {
        public const int SupportedSchemaVersion = 1;
        public const string InvalidDefense = "INVALID_DEFENSE";

        public static Result<DefenseDefinition> Read(string json)
        {
            var j = new StrictJson(GameDataLoader.DefenseFile);
            var root = j.ParseRoot(json, SupportedSchemaVersion, "tackle", "control");
            if (root == null) return Result<DefenseDefinition>.Fail(j.Errors);
            var d = new DefenseDefinition();

            var t = j.Object(root, "tackle", "root");
            if (t != null)
            {
                j.Keys(t, "tackle", "shieldMeters", "reTackleCooldownSeconds", "dispossessedStunSeconds", "possessionSecureSeconds");
                d.ShieldMeters = j.Float(t, "shieldMeters", "tackle");
                d.ReTackleCooldownSeconds = j.Float(t, "reTackleCooldownSeconds", "tackle");
                d.DispossessedStunSeconds = j.Float(t, "dispossessedStunSeconds", "tackle");
                d.PossessionSecureSeconds = j.Float(t, "possessionSecureSeconds", "tackle");
            }
            var c = j.Object(root, "control", "root");
            if (c != null)
            {
                j.Keys(c, "control", "switchHysteresis", "postManualLockSeconds", "beatenDistance", "behindBallPenalty", "autoSwitchCooldownSeconds", "intentionAlignment");
                d.SwitchHysteresis = j.Float(c, "switchHysteresis", "control");
                d.PostManualLockSeconds = j.Float(c, "postManualLockSeconds", "control");
                d.BeatenDistance = j.Float(c, "beatenDistance", "control");
                d.BehindBallPenalty = j.Float(c, "behindBallPenalty", "control");
                d.AutoSwitchCooldownSeconds = j.Float(c, "autoSwitchCooldownSeconds", "control");
                d.IntentionAlignment = j.Float(c, "intentionAlignment", "control");
            }
            if (!j.Ok) return Result<DefenseDefinition>.Fail(j.Errors);

            bool ok = d.ShieldMeters >= 0f && d.ReTackleCooldownSeconds >= 0f && d.DispossessedStunSeconds >= 0f && d.PossessionSecureSeconds >= 0f
                      && d.SwitchHysteresis >= 0f && d.SwitchHysteresis < 1f && d.PostManualLockSeconds >= 0f
                      && d.BeatenDistance > 0f && d.BehindBallPenalty >= 0f && d.AutoSwitchCooldownSeconds >= 0f
                      && d.IntentionAlignment > -1f && d.IntentionAlignment < 1f;
            if (!ok) { j.Fail(InvalidDefense, "defense values invalid (switchHysteresis 0-1, beatenDistance > 0, others >= 0)."); return Result<DefenseDefinition>.Fail(j.Errors); }
            return Result<DefenseDefinition>.Ok(d);
        }
    }
}
