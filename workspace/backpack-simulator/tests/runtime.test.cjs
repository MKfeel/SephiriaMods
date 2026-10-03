'use strict';
const assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path');
const M=require('../model.js'),R=require('../runtime-model.js'),D=require('../data/catalog.json');
let checks=0;
function eq(a,b,msg){assert.deepEqual(a,b,msg);checks++;}function ok(a,msg){assert.ok(a,msg);checks++;}
function fixture(profile='ember'){
 const state=M.makePreset(profile,D),r=new M.Model(D,state).evaluate(),p=M.profiles[profile];
 const specific={judge:1115,nebolax:528,'plasma-dagger':1201,'library-katana':417,guardian:1020,'armor-katana':424,'planet-fixed':126};
 return {protocol:1,requestId:'synthetic-test',width:6,capacity:state.capacity,profile,
  weapon:{entityId:specific[profile]||(p.fusion?503:1),type:p.weapon??0,attackWeight:r.combat.scenario.attackWeight},
  seed:42,cells:state.cells,definitions:[],patterns:{},fixedPatterns:[],
  nativeCounts:r.counts,nativeStats:r.combat.rawStats,nativeEffects:state.cells.flatMap((x,c)=>x&&D.items.find(d=>d.id===x.id).kind==='artifact'?[{uid:x.uid,level:r.level[c],active:r.active[c]}]:[]),
  matrices:{level:r.level,multiply:r.mult,disable:r.disableCount,ignore:r.ignoreCount},locks:{pins:[],paper:[],compass:[],rows:[]}};
}
async function main(){
 const rows=[];
 for(const id of Object.keys(M.profiles)){
  const input=fixture(id),prepared=R.prepare(input,D),out=await R.search(prepared,{stepsPerStart:120,starts:2,passes:1});
  ok(out.after.valid,'valid '+id);ok(R.acceptsResult(out.after,out.before),'primary and certified gain nonregression '+id);
  eq(out.cells.filter(Boolean).map(x=>x.uid).sort(),input.cells.filter(Boolean).map(x=>x.uid).sort(),'inventory preserved '+id);
  eq(out.after.objective,prepared.model.evaluate(out.cells).objective,'reported objective matches result '+id);
  eq(out.completedStarts,2,'zero budget completes configured starts '+id);
  rows.push({profile:id,before:out.before.total,after:out.after.total,evaluations:out.evaluations});
 }
 let s=fixture('judge');s.profile='auto';eq(R.identify(s,D).id,'judge','native judge identity');
 s=fixture('planet');s.profile='auto';s.weapon={entityId:506,type:7,attackWeight:1};const planet=R.identify(s,D);
 let t=fixture('sun');t.profile='auto';t.weapon=s.weapon;const sun=R.identify(t,D);
 eq(planet.id,'planet','same weapon can use planet');eq(sun.id,'sun','same weapon can use sun');
 s=fixture('judge');s.weapon.entityId=1;assert.throws(()=>R.prepare(s,D),/不匹配/);checks++;
 s=fixture();s.nativeEffects[0].level++;assert.throws(()=>R.prepare(s,D),/不一致/);checks++;
 s=fixture();s.definitions=[{id:1270,class:'Charm_NearLevelDamage',curves:{},mechanics:{},icon:'https://invalid.example/never'}];
 const merged=R.prepare(s,D);eq(merged.model.defs.get(1270).curves.allDamageBonusByLevel,D.items.find(x=>x.id===1270).curves.allDamageBonusByLevel,'partial runtime overrides retain exported curves');
 eq(merged.model.defs.get(1270).icon,D.items.find(x=>x.id===1270).icon,'snapshot cannot replace local sprite URLs');
 s=fixture();s.cells.find(Boolean).id=999999;assert.throws(()=>R.prepare(s,D),/未知物品/);checks++;
 s=fixture();s.cells[35]={...s.cells.find(x=>x&&D.items.find(d=>d.id===x.id).unique),uid:'duplicate'};assert.throws(()=>R.prepare(s,D),/重复唯一/);checks++;
 s=fixture();const a=s.cells.findIndex(Boolean),b=s.cells.findIndex((x,c)=>x&&c!==a);s.locks.pins=[[a,s.cells[a].uid]];
 let prepared=R.prepare(s,D),candidate=M.copy(s.cells);[candidate[a],candidate[b]]=[candidate[b],candidate[a]];
 eq(prepared.model.evaluate(candidate).valid,false,'pin legality, independent of score');
 s=fixture();s.nativeStats={...s.nativeStats,PHYSICAL_DAMAGE:(s.nativeStats.PHYSICAL_DAMAGE||0)+100,MAX_MP:240};
 prepared=R.prepare(s,D);eq(Math.round(prepared.before.combat.rawStats.MAX_MP),240,'native MP not double counted');
 eq(Math.round(prepared.before.combat.rawStats.PHYSICAL_DAMAGE),s.nativeStats.PHYSICAL_DAMAGE,'native element calibrated');
 const maxIndex=s.cells.findIndex(x=>x&&D.items.find(d=>d.id===x.id).kind==='artifact');s.cells[maxIndex].mark=2;
 prepared=R.prepare(s,D);const marked=await R.search(prepared,{stepsPerStart:600,starts:2,passes:1});
 ok(R.acceptsResult(marked.after,marked.before),'manual arrows outrank ordinary score');
 const weak={objective:[1,0,0,0,0,0,1e12,0]},strong={objective:[2,-50,-500,0,0,0,0,0]};ok(R.compare(strong,weak)>0,'no amount of score buys lost max activation');
 let cancelled=false;const stop=await R.search(prepared,{stepsPerStart:10000,starts:8,cancelled:()=>cancelled=true});eq(stop.cancelled,true,'cancellation supported');eq(stop.cells,prepared.state.cells,'cancel before search keeps bag');
 const budget=await R.search(prepared,{budgetMs:1,stepsPerStart:100000,starts:8});eq(budget.budgetReached,true,'positive budget respected');
 // Native patterns replace catalog patterns, including out-of-grid conditions.
 s=fixture();const tablet=s.cells.find(x=>x&&D.items.find(d=>d.id===x.id).kind==='tablet');
 s.patterns[tablet.uid]=Array.from({length:s.capacity},()=>Array.from({length:4},()=>({effects:[{cell:0,value:'100'}],conditions:[{cell:-1,value:'ITEM'}]})));
 prepared=R.prepare(s,D);eq(prepared.before.tabletActive[s.cells.findIndex(x=>x?.uid===tablet.uid)],false,'out-of-bounds ITEM stays unsatisfied');
 const report={date:'2026-10-02',checks,profiles:rows.length,scope:'Synthetic native-shaped snapshots; no running-game validation',rows};
 fs.writeFileSync(path.join(__dirname,'validation-runtime.json'),JSON.stringify(report,null,2));
 fs.writeFileSync(path.join(__dirname,'runtime-fixture.json'),JSON.stringify(fixture('judge'),null,2));
 console.log(JSON.stringify({checks,profiles:rows.length,scope:report.scope}));
}
if(require.main===module)main().catch(e=>{console.error(e);process.exitCode=1;});
module.exports={fixture};
