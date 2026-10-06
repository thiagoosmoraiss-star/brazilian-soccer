using Game.Career.Economy;
using Game.Career.Market;
using Game.Career.Season;
using Game.Save;
using NUnit.Framework;

namespace Game.Tests.Save
{
    /// <summary>B8 acceptance (TEST_PLAN "Save"): "invariantes (contrato único, elenco, saldo = livro, tabelas)".</summary>
    public class SaveValidatorTests
    {
        private static CareerSimulator NewCareer(ulong seed) =>
            CareerSimulator.Start(SaveTestData.Db(), seed, new Game.Simulation.QuickSim.QuickSim(SaveTestData.Db()).Simulate,
                new TransferWindow(), new EconomySystem());

        [Test]
        public void ARealCareer_AlwaysValidates()
        {
            var career = NewCareer(61);
            for (int s = 0; s < 3; s++) career.PlaySeason();
            Assert.IsTrue(SaveValidator.Validate(career.State).IsSuccess);
        }

        [Test]
        public void DuplicateContractForTheSamePlayer_FailsValidation()
        {
            var career = NewCareer(62);
            var world = career.State.World;
            var player = world.Players[0];
            var existing = world.ContractOf(player.Id);
            Assert.IsNotNull(existing, "fixture needs a contracted player.");
            world.ContractList.Add(new Game.Career.World.Contract
            {
                Id = career.State.Ids.Next(),
                PlayerId = player.Id,
                ClubId = existing.ClubId,
                Wage = existing.Wage,
                StartYear = existing.StartYear,
                EndYear = existing.EndYear,
            });

            var result = SaveValidator.Validate(career.State);
            Assert.IsFalse(result.IsSuccess);
            StringAssert.Contains("more than one active contract", result.ToString());
        }

        [Test]
        public void BalanceNotMatchingTheLedger_FailsValidation()
        {
            var career = NewCareer(63);
            career.State.World.Clubs[0].Balance += 1;

            var result = SaveValidator.Validate(career.State);
            Assert.IsFalse(result.IsSuccess);
            StringAssert.Contains("does not match the ledger sum", result.ToString());
        }

        [Test]
        public void DanglingClubReference_FailsValidation()
        {
            var career = NewCareer(64);
            career.State.ManagedClubId = new Game.Core.Ids.Id(999_999);

            var result = SaveValidator.Validate(career.State);
            Assert.IsFalse(result.IsSuccess);
            StringAssert.Contains("does not exist", result.ToString());
        }
    }
}
