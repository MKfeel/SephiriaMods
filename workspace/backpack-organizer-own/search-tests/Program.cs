using System;
using System.Collections.Generic;
using System.Diagnostics;
namespace SephiriaBackpackOrganizer {
 public partial class InventorySorter {
  class Setting<T> { public T ValueField; public T Value => ValueField; public Setting(T value) { ValueField=value; } }
  class Settings { public Setting<int> SearchTimeBudgetMs=new(0); public Setting<bool> EnableSmartStart=new(true); }
  class Slot { public bool hasItem; public int instanceID, rotation; public object charm; }
  class Info { public bool isCharm,isStele,tabletRotatable,isHourglass,isRayShard,isWhitePaper,isMagicBook; public int enchant,maxLevel,manualPriorityRank; public bool weaponOk=true; }
  class Chain { public List<int> instanceIDs=new(); }
  class SearchContext { public List<Slot> original; public Dictionary<int,Info> itemByInstance=new(); public int[] cellLevel=new int[6],mysticFactor={1,1,1,1,1,1}; public bool[] disabled=new bool[6],ignore=new bool[6]; public bool cancelled,searchBudgetReached; public int width=3,annealEvaluations,annealStarts,annealStartsCompleted; public List<Chain> compassChains=new(); }
  Settings plugin=new();
  static List<Slot> CloneSlots(List<Slot> a) { var b=new List<Slot>(); foreach(var s in a)b.Add(new Slot {hasItem=s.hasItem,instanceID=s.instanceID,rotation=s.rotation,charm=s.charm}); return b; }
  static void CopySlots(List<Slot>a,List<Slot>b) { var copy=CloneSlots(a); b.Clear(); b.AddRange(copy); }
  static void SwapSlots(List<Slot>a,int i,int j) {(a[i],a[j])=(a[j],a[i]);}
  static bool CriteriaSatisfied(SearchContext c,Info i,List<Slot>s,bool b,int n)=>true;
  static bool RestoreCompassBindings(SearchContext c,List<Slot>s)=>true;
  static bool CompassBindingsSatisfied(SearchContext c,List<Slot>s)=>true;
  static long CreateSearchDeadline(int ms)=>ms<=0?0:Stopwatch.GetTimestamp()+ms*Stopwatch.Frequency/1000;
  static bool SearchDeadlineReached(long d)=>d>0 && Stopwatch.GetTimestamp()>=d;
  static List<Slot> BuildSmartStart(SearchContext c)=>CloneSlots(c.original);
  static void ScrambleForSearch(SearchContext c,List<Slot>s,Random r) {foreach(var x in s)if(x.instanceID==1)x.rotation=r.Next(4);}
  static void Mutate(SearchContext c,List<Slot>s,Random r) {SwapSlots(s,r.Next(s.Count),r.Next(s.Count));}
  // Synthetic landscape: only moving AND rotating the tablet supplies the marked charm.
  static double EvaluateLayout(SearchContext c,List<Slot>s) {
   Array.Clear(c.cellLevel);
   if(s[2].instanceID==1 && s[2].rotation==3 && s[3].instanceID==2)c.cellLevel[3]=5;
   return c.cellLevel[3]*10000;
  }
  static void Check(bool ok,string why) {if(!ok)throw new Exception(why);}
  public static void Main() {
   for(int trial=0;trial<3;trial++) {
    var sorter=new InventorySorter(); var c=new SearchContext();
    c.original=new(); for(int i=0;i<6;i++)c.original.Add(new Slot {hasItem=i<2,instanceID=i<2?i+1:0,charm=i==1?new object():null});
    c.itemByInstance[1]=new Info {isStele=true,tabletRotatable=true}; c.itemByInstance[2]=new Info {isCharm=true,maxLevel=5,manualPriorityRank=1};
    var result=sorter.RunHybridSearch(c,c.original,out var before,out var after);
    Check(after==50000,"known optimum not found"); Check(c.annealStartsCompleted==8 && !c.searchBudgetReached,"zero budget truncated full search");
    Check(c.original[0].instanceID==1 && c.original[0].rotation==0,"input mutated");
    Check(result.FindAll(s=>s.hasItem).Count==2,"items lost");
    c.cancelled=true; c.annealStarts=0;
    sorter.RunHybridSearch(c,c.original,out before,out after); Check(c.annealStarts==0,"cancellation ignored");
    c.cancelled=false; sorter.plugin.SearchTimeBudgetMs.ValueField=1;
    sorter.RunHybridSearch(c,c.original,out before,out after); Check(c.searchBudgetReached,"positive budget ignored");
   }
   Console.WriteLine("PASS 18 actual-search harness checks; synthetic model, not game validation");
  }
 }
}
