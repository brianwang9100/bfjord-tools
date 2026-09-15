using System;
using Bwork.Authoring.WaterSandbox;
using NUnit.Framework;
using UnityEngine;

namespace Bwork.Authoring.Editor.Tests
{
    public sealed class ConnectedWaterBankQueryTests
    {
        static ConnectedWaterField LongRiver()
        {
            return new ConnectedWaterField(new ConnectedWaterRecipe
            {
                bankFalloff=4,
                nodes=new[]
                {
                    new WaterNode{id="source",kind="source",position=Vector3.zero,radius=new Vector2(4,4)},
                    new WaterNode{id="mouth",kind="mouth",position=new Vector3(0,0,400),radius=new Vector2(4,4)},
                    new WaterNode{id="ocean",kind="ocean",position=new Vector3(0,0,470),radius=new Vector2(40,80)}
                },
                reaches=new[]
                {
                    new WaterReach{id="long",from="source",to="mouth",knots=new[]
                    {
                        new RiverKnot{position=Vector3.zero,handleIn=new Vector3(0,0,-100),handleOut=new Vector3(0,0,400f/3),width=8,foam=.1f},
                        new RiverKnot{position=new Vector3(0,0,400),handleIn=new Vector3(0,0,800f/3),handleOut=new Vector3(0,0,500),width=8,foam=.7f}
                    }}
                }
            });
        }

        [TestCase(30,30)]
        [TestCase(-30,30)]
        [TestCase(170,200)]
        [TestCase(-170,200)]
        public void WideBankQueryFindsReachBeyondItsIndexedCorridor(float x,float range)
        {
            var field=LongRiver();var point=new Vector2(x,200);
            float expected=Mathf.Abs(x)-4;
            // Sparse end nodes and finite ocean are all farther than the long reach midpoint.
            Assert.That(field.Sample(point).Distance,Is.GreaterThan(expected+1));
            var bank=field.SampleForBank(point,range);
            Assert.That(bank.Distance,Is.EqualTo(expected).Within(.001f));
            Assert.That(bank.Height,Is.EqualTo(0).Within(.001f));
            Assert.That(bank.BedDepth,Is.EqualTo(2).Within(.001f));
            Assert.That(bank.Flow.y,Is.EqualTo(1).Within(.001f));
            Assert.That(bank.Foam,Is.EqualTo(.4f).Within(.001f));
        }

        [Test]
        public void DeclaredRangeIsInclusiveAndOutsideHasNoBank()
        {
            var field=LongRiver();var point=new Vector2(30,200);
            Assert.That(field.SampleForBank(point,26).Distance,Is.EqualTo(26).Within(.001f));
            Assert.That(float.IsPositiveInfinity(field.SampleForBank(point,25).Distance),Is.True);
            Assert.That(field.SampleForBank(new Vector2(0,200),0).Distance,Is.EqualTo(-4).Within(.001f));
            Assert.That(field.SampleForBank(new Vector2(6,200),3).Distance,Is.EqualTo(field.Sample(new Vector2(6,200)).Distance));
            Assert.Throws<ArgumentException>(()=>field.SampleForBank(point,201));
            Assert.Throws<ArgumentException>(()=>field.SampleForBank(point,float.NaN));
        }
    }
}
