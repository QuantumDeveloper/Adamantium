using Adamantium.Mathematics;
using NUnit.Framework;

namespace Adamantium.MathTests
{
    public class Vector2DTests
    {
        [Test]
        public void IsVectorsNotCollinear()
        {
            var vector = new Vector2(1, 0);
            var vector2 = new Vector2(1, -26.94);
            var isCollinear = vector.IsCollinear(vector2);
            Assert.IsFalse(isCollinear, "Vectors are collinear");
        }
        
        [Test]
        public void IsVectorsCollinear()
        {
            var vector = new Vector2(5, 50);
            var vector2 = new Vector2(6, 60);
            var isCollinear = vector.IsCollinear(vector2);
            Assert.IsTrue(isCollinear, "Vectors are NOT collinear");
        }
    }
}