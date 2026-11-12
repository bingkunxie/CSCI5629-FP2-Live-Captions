using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using FP2.Captions;
public class MeetingFeatureTests
{
    private string Folder()=>Path.Combine(TestContext.CurrentContext.WorkDirectory??Environment.CurrentDirectory,"agenda-test-"+Guid.NewGuid().ToString("N"));
    [Test]
    public void UnicodeAgendaSurvivesSaveAndCompletionWithoutLosingOtherItems()
    {
        string dir=Folder();
        try{
            var store=new AgendaStore(Path.Combine(dir,"agenda.dat"));
            var a=MeetingAgenda.Replace(store.Load(),new[]{"确认会议目标","Discuss next steps"});
            store.Save(a);a=MeetingAgenda.SetDone(a,a.items[0].id,true);store.Save(a);
            var loaded=store.Load();
            Assert.That(loaded.items[0].text,Is.EqualTo("确认会议目标"));
            Assert.That(loaded.items[0].done,Is.True);Assert.That(loaded.items[1].done,Is.False);
        }finally{if(Directory.Exists(dir))Directory.Delete(dir,true);}
    }
    [Test]
    public void EditingAndReorderingPreserveUnchangedCompletion()
    {
        var a=MeetingAgenda.Replace(MeetingAgenda.Empty(),new[]{"First","Second"});
        a=MeetingAgenda.SetDone(a,a.items[0].id,true);
        var b=MeetingAgenda.Replace(a,new[]{"Second","First","New item"});
        Assert.That(b.items[1].id,Is.EqualTo(a.items[0].id));Assert.That(b.items[1].done,Is.True);
        Assert.That(b.items[2].done,Is.False);
    }
    [Test]
    public void LimitsAndStaleItemIdsCannotCorruptAgenda()
    {
        var a=MeetingAgenda.Replace(MeetingAgenda.Empty(),new[]{"Keep"});
        Assert.Throws<ArgumentException>(()=>MeetingAgenda.Replace(a,Enumerable.Repeat("Point",7)));
        Assert.Throws<ArgumentException>(()=>MeetingAgenda.Replace(a,new[]{new string('字',61)}));
        Assert.Throws<ArgumentException>(()=>MeetingAgenda.SetDone(a,"missing",true));
        Assert.That(a.items[0].done,Is.False);
    }
    [Test]
    public void CorruptedPrimaryRecoversPreviousAtomicSnapshot()
    {
        string dir=Folder();
        try{
            string path=Path.Combine(dir,"agenda.dat");var store=new AgendaStore(path);
            var a=MeetingAgenda.Replace(MeetingAgenda.Empty(),new[]{"Original"});store.Save(a);
            store.Save(MeetingAgenda.Replace(a,new[]{"Changed"}));File.WriteAllText(path,"broken");
            Assert.That(store.Load().items[0].text,Is.EqualTo("Original"));
        }finally{if(Directory.Exists(dir))Directory.Delete(dir,true);}
    }
    [Test]
    public void ReactionGateRejectsSpamDuplicatesUnknownAndStaleEvents()
    {
        var gate=new ReactionGate();
        Assert.That(gate.TrySend("like",0),Is.True);
        Assert.That(gate.TrySend("clap",1),Is.False);
        Assert.That(gate.TrySend("clap",2),Is.True);
        Assert.That(gate.TrySend("unknown",5),Is.False);
        Assert.That(gate.Accept("a","love",10,10.1),Is.True);
        Assert.That(gate.Accept("a","love",10,10.2),Is.False);
        Assert.That(gate.Accept("b","question",10,14),Is.False);
        Assert.That(gate.Accept("c","invalid",10,10),Is.False);
    }
}
