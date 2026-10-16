using NUnit.Framework;
using UnityEngine;

namespace EvolutionShop.Tests
{
    public sealed class FxCustomerMotionTests
    {
        static readonly string[] Types={"casual","bargain","collector","premium"};
        static readonly CustomerPhase[] Phases={CustomerPhase.Arriving,CustomerPhase.Browsing,CustomerPhase.ToCounter,CustomerPhase.Queued,CustomerPhase.Paid,CustomerPhase.Leaving};
        static float Difference(FxCustomerPose a,FxCustomerPose b)=>(a.LeftLegEuler-b.LeftLegEuler).magnitude+(a.RightLegEuler-b.RightLegEuler).magnitude+(a.LeftArmEuler-b.LeftArmEuler).magnitude+(a.RightArmEuler-b.RightArmEuler).magnitude+(a.UpperEuler-b.UpperEuler).magnitude;
        static void FiniteBounded(Vector3 p,float limit)
        {Assert.That(float.IsNaN(p.sqrMagnitude)||float.IsInfinity(p.sqrMagnitude),Is.False);Assert.That(p.magnitude,Is.LessThanOrEqualTo(limit));}

        [TestCase("casual")][TestCase("bargain")][TestCase("collector")][TestCase("premium")]
        public void StoppedFeetRemainPlantedAcrossAllPhasesAndReactions(string type)
        {
            foreach(var phase in Phases)foreach(FxCustomerReaction reaction in System.Enum.GetValues(typeof(FxCustomerReaction)))
                for(int i=0;i<=30;i++) {
                    var p=FxCustomerMotion.Sample(type,phase,i*.47f,0,i*.31f,reaction,i*.07f,Mathf.Sin(i),false);
                    Assert.That(p.LeftLegEuler,Is.EqualTo(Vector3.zero),phase+" / "+reaction);
                    Assert.That(p.RightLegEuler,Is.EqualTo(Vector3.zero),phase+" / "+reaction);
                }
        }
        [TestCase("casual")][TestCase("bargain")][TestCase("collector")][TestCase("premium")]
        public void LocomotionIsPeriodicContinuousAndOpposed(string type)
        {
            Assert.That(FxCustomerMotion.StrideLength(type),Is.InRange(.3f,2f));
            float movement=0;var previous=FxCustomerMotion.Sample(type,CustomerPhase.Arriving,0,1,0,FxCustomerReaction.None,0,0,false);
            for(int i=1;i<=720;i++) {
                float phase=i*Mathf.PI*2/360;
                var p=FxCustomerMotion.Sample(type,CustomerPhase.Arriving,phase,1,0,FxCustomerReaction.None,0,0,false);
                Assert.That((p.LeftLegEuler+p.RightLegEuler).magnitude,Is.LessThan(.001f));
                Assert.That(Difference(p,previous),Is.LessThan(4f),"No wrap or adjacent gait snap");
                movement+=(p.LeftLegEuler-previous.LeftLegEuler).magnitude;previous=p;
                FiniteBounded(p.LeftLegEuler,30);FiniteBounded(p.RightLegEuler,30);FiniteBounded(p.UpperEuler,20);
            }
            Assert.That(movement,Is.GreaterThan(80f),"Actual walking has visible cycles");
            var start=FxCustomerMotion.Sample(type,CustomerPhase.Arriving,0,1,0,FxCustomerReaction.None,0,0,false);
            Assert.That(Difference(start,previous),Is.LessThan(.001f));
        }
        [TestCase("casual")][TestCase("bargain")][TestCase("collector")][TestCase("premium")]
        public void BoundedReactionsSettleAndRepeatedQueueSamplesCannotInventAnEvent(string type)
        {
            foreach(FxCustomerReaction reaction in new[]{FxCustomerReaction.Accepted,FxCustomerReaction.Refused,FxCustomerReaction.Checkout}) {
                float duration=FxCustomerMotion.ReactionDuration(reaction);
                Assert.That(duration,Is.InRange(.15f,3f));float motion=0;
                var calm=FxCustomerMotion.Sample(type,CustomerPhase.Queued,0,0,0,FxCustomerReaction.None,0,0,false);
                for(int i=0;i<=100;i++) {
                    var p=FxCustomerMotion.Sample(type,CustomerPhase.Queued,0,0,0,reaction,duration*i/100,0,false);
                    FiniteBounded(p.LeftArmEuler,100);FiniteBounded(p.RightArmEuler,100);FiniteBounded(p.UpperEuler,20);
                    motion=Mathf.Max(motion,Difference(p,calm));
                }
                Assert.That(motion,Is.GreaterThan(.5f),"Meaningful local "+reaction);
                var settled=FxCustomerMotion.Sample(type,CustomerPhase.Queued,0,0,0,reaction,duration+10,0,false);
                Assert.That(Difference(calm,settled),Is.LessThan(.001f),"Expired action settles");
            }
            var q=FxCustomerMotion.Sample(type,CustomerPhase.Queued,0,0,1.1f,FxCustomerReaction.None,0,0,false);
            for(int i=0;i<100;i++)Assert.That(Difference(q,FxCustomerMotion.Sample(type,CustomerPhase.Queued,0,0,1.1f,FxCustomerReaction.None,0,0,false)),Is.Zero);
        }
        [Test] public void FourTypesHaveDifferentGesturesWithoutMutatingUnityRandom()
        {
            var state=Random.state;
            for(int a=0;a<Types.Length;a++)for(int b=a+1;b<Types.Length;b++) {
                float difference=0;
                foreach(var reaction in new[]{FxCustomerReaction.Accepted,FxCustomerReaction.Refused,FxCustomerReaction.Checkout})
                    for(int i=1;i<=9;i++)difference+=Difference(FxCustomerMotion.Sample(Types[a],CustomerPhase.Browsing,1,0,.4f,reaction,i*.08f,.3f,false),FxCustomerMotion.Sample(Types[b],CustomerPhase.Browsing,1,0,.4f,reaction,i*.08f,.3f,false));
                Assert.That(difference,Is.GreaterThan(1f),Types[a]+" / "+Types[b]);
            }
            Assert.That(Random.state,Is.EqualTo(state));
        }
        [TestCase("casual")][TestCase("bargain")][TestCase("collector")][TestCase("premium")]
        public void ReducedIdleAndReactionAreRestrainedButWalkingStillReads(string type)
        {
            var rest=FxCustomerMotion.Sample(type,CustomerPhase.Queued,0,0,0,FxCustomerReaction.None,0,0,true);
            var later=FxCustomerMotion.Sample(type,CustomerPhase.Queued,4,0,90,FxCustomerReaction.None,0,0,true);
            Assert.That(Difference(rest,later),Is.LessThan(.001f),"Reduced idle rests");
            var walk=FxCustomerMotion.Sample(type,CustomerPhase.Arriving,Mathf.PI*.5f,1,0,FxCustomerReaction.None,0,0,true);
            Assert.That(walk.LeftLegEuler.magnitude,Is.GreaterThan(1f),"Walking remains visible under reduced policy");
            foreach(var reaction in new[]{FxCustomerReaction.Accepted,FxCustomerReaction.Refused,FxCustomerReaction.Checkout}) {
                float ordinary=0,reduced=0;
                for(int i=0;i<50;i++) {
                    ordinary+=Difference(rest,FxCustomerMotion.Sample(type,CustomerPhase.Queued,0,0,0,reaction,i*.03f,0,false));
                    reduced+=Difference(rest,FxCustomerMotion.Sample(type,CustomerPhase.Queued,0,0,0,reaction,i*.03f,0,true));
                }
                Assert.That(reduced,Is.LessThanOrEqualTo(ordinary+.001f));
            }
        }
    }
}
