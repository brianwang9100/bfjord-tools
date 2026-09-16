using System;
using System.Linq;
using System.IO;
using Bwork.Authoring.Editor.Rocks;
using NUnit.Framework;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace Bwork.Authoring.Editor.Tests
{
    public sealed class RockShowcaseTests
    {
        static RockVariant[] Variants()=>RockShowcaseCommand.VariantIds.Select(id=>new RockVariant{id=id,displayName=id,boundsSize=new[]{3f,2f,3f}}).ToArray();
        [Test] public void AllNineAreDeterministicSeparatedAndBuried()
        {
            var first=RockShowcaseCommand.Plan(Variants(),194,90,80,(x,z)=>10,(p,r)=>false);
            var second=RockShowcaseCommand.Plan(Variants(),194,90,80,(x,z)=>10,(p,r)=>false);
            Assert.That(first.Select(p=>p.id).Distinct().Count(),Is.EqualTo(9));
            CollectionAssert.AreEqual(first.Select(p=>(p.id,p.position,p.yaw,p.scale)),second.Select(p=>(p.id,p.position,p.yaw,p.scale)));
            foreach(var p in first){Assert.That(p.position.y,Is.InRange(9.68f,9.92f));Assert.That(p.scale,Is.InRange(.65f,1.4f));}
            for(int i=0;i<9;i++)for(int j=i+1;j<9;j++)Assert.That(Vector2.Distance(new Vector2(first[i].position.x,first[i].position.z),new Vector2(first[j].position.x,first[j].position.z)),Is.GreaterThan(12));
        }
        [Test] public void AnotherSeedVariesYawScaleAndSpacingWithoutDroppingVariants()
        {
            var a=RockShowcaseCommand.Plan(Variants(),1,90,80,(x,z)=>0,(p,r)=>false);var b=RockShowcaseCommand.Plan(Variants(),2,90,80,(x,z)=>0,(p,r)=>false);
            CollectionAssert.AreEqual(a.Select(p=>p.id),b.Select(p=>p.id));Assert.That(a.Zip(b,(x,y)=>x.yaw!=y.yaw&&x.scale!=y.scale&&x.position!=y.position).All(v=>v),Is.True);
        }
        [Test] public void CliPlacementReportSerializesAsPlainCoordinates()
        {
            var placement=RockShowcaseCommand.Plan(Variants(),194,80,145,(x,z)=>10,(p,r)=>false)[0];
            var json=JObject.Parse(JsonConvert.SerializeObject(placement.Report()));
            Assert.That(json["id"].Value<string>(),Is.EqualTo(placement.id));
            Assert.That(json["position"].Type,Is.EqualTo(JTokenType.Array));
            CollectionAssert.AreEqual(new[]{placement.position.x,placement.position.y,placement.position.z},json["position"].Values<float>());
        }
        [Test] public void ProtectedHolesAndUnevenGroundRejectBeforePublication()
        {
            Assert.Throws<InvalidDataException>(()=>RockShowcaseCommand.Plan(Variants(),1,90,80,(x,z)=>0,(p,r)=>true));
            Assert.Throws<InvalidDataException>(()=>RockShowcaseCommand.Plan(Variants(),1,90,80,(x,z)=>float.NaN,(p,r)=>false));
            Assert.Throws<InvalidDataException>(()=>RockShowcaseCommand.Plan(Variants(),1,90,80,(x,z)=>x*10,(p,r)=>false));
        }
    }
}
