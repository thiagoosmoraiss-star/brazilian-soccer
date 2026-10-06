using System.Numerics;
using Game.Match;
using NUnit.Framework;

namespace Game.Tests.Unit
{
    /// <summary>GAME_DESIGN §17: "Memória de sprint: 0,4 s após soltar se o analógico continua empurrado."</summary>
    public class InputIntentFilterTests
    {
        private const float Dt = 1f / 50f;
        private const float Memory = 0.4f;

        [Test]
        public void SprintHeld_StaysTrueWhileMoving()
        {
            var filter = new InputIntentFilter();
            var (_, sprint) = filter.Step(new Vector2(1f, 0f), rawSprintHeld: true, Memory, Dt);
            Assert.IsTrue(sprint);
        }

        [Test]
        public void SprintReleased_PersistsForTheMemoryWindow_ThenStops()
        {
            var filter = new InputIntentFilter();
            filter.Step(new Vector2(1f, 0f), rawSprintHeld: true, Memory, Dt); // establish sprint

            float elapsed = 0f;
            bool sprintAt039 = false, sprintAt041 = true;
            while (elapsed < Memory + 0.05f)
            {
                var (_, sprint) = filter.Step(new Vector2(1f, 0f), rawSprintHeld: false, Memory, Dt);
                elapsed += Dt;
                if (elapsed >= 0.35f && elapsed < Memory) sprintAt039 = sprint;
                if (elapsed >= Memory + 0.02f) sprintAt041 = sprint;
            }

            Assert.IsTrue(sprintAt039, "sprint must still be active just before the memory window ends.");
            Assert.IsFalse(sprintAt041, "sprint must stop once the memory window has fully elapsed.");
        }

        [Test]
        public void NotMoving_NeverSprints_EvenWithSprintHeld()
        {
            var filter = new InputIntentFilter();
            var (_, sprint) = filter.Step(Vector2.Zero, rawSprintHeld: true, Memory, Dt);
            Assert.IsFalse(sprint, "sprint only matters while actually moving.");
        }

        [Test]
        public void ReHoldingSprint_ResetsTheMemoryWindow()
        {
            var filter = new InputIntentFilter();
            filter.Step(new Vector2(1f, 0f), true, Memory, Dt);
            for (int i = 0; i < 15; i++) filter.Step(new Vector2(1f, 0f), false, Memory, Dt); // 0.3s coasting
            filter.Step(new Vector2(1f, 0f), true, Memory, Dt); // re-press before memory ran out

            float elapsed = 0f;
            bool stillSprinting = true;
            while (elapsed < Memory - 0.02f)
            {
                var (_, sprint) = filter.Step(new Vector2(1f, 0f), false, Memory, Dt);
                stillSprinting &= sprint;
                elapsed += Dt;
            }
            Assert.IsTrue(stillSprinting, "re-pressing sprint must restart the full memory window.");
        }
    }
}
