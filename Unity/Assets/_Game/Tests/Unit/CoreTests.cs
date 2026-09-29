using Game.Core.Ids;
using Game.Core.Math;
using Game.Core.Results;
using NUnit.Framework;

namespace Game.Tests.Unit
{
    public class CoreTests
    {
        [Test]
        public void IdAllocator_IsMonotonic_AndNeverReuses()
        {
            var alloc = new IdAllocator();
            var a = alloc.Next();
            var b = alloc.Next();
            Assert.AreEqual(1, a.Value);
            Assert.AreEqual(2, b.Value);
            Assert.AreNotEqual(a, b);

            var resumed = new IdAllocator(alloc.LastIssued);
            Assert.AreEqual(3, resumed.Next().Value);
            Assert.IsTrue(Id.None.IsNone);
        }

        [Test]
        public void Result_ReportsSuccessAndErrors()
        {
            Assert.IsTrue(Result.Success.IsSuccess);
            var fail = Result<int>.Fail("CODE", "message");
            Assert.IsFalse(fail.IsSuccess);
            Assert.AreEqual("CODE", fail.Errors[0].Code);
            Assert.Throws<System.InvalidOperationException>(() => { var _ = fail.Value; });
            Assert.AreEqual(7, Result<int>.Ok(7).Value);
        }

        [Test]
        public void MathUtil_Basics()
        {
            Assert.AreEqual(5f, MathUtil.Lerp(0f, 10f, 0.5f));
            Assert.AreEqual(0.25f, MathUtil.InverseLerp(0f, 4f, 1f));
            Assert.AreEqual(0f, MathUtil.InverseLerp(3f, 3f, 1f));
            Assert.AreEqual(1f, MathUtil.Clamp01(2f));
        }
    }
}
