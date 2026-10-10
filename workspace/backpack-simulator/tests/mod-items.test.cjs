'use strict';
const assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path');
const M=require('../model.js'),R=require('../runtime-model.js'),D=require('../data/catalog.json');
let checks=0;
const ok=(value,message)=>{assert.ok(value,message);checks++;};
const eq=(a,b,message)=>{assert.deepEqual(a,b,message);checks++;};
function fixture(mark=0){
 const capacity=12,levels=[0,3,-1,...Array(9).fill(0)];
 return {protocol:1,requestId:'mod-item-20008',width:6,capacity,profile:'physical-sword',seed:42,
  weapon:{entityId:1,type:0,attackWeight:1},
  cells:Array.from({length:capacity},(_,c)=>c===0?{uid:'mod',id:20008,rotation:0,enchant:0,mark,locked:false,nativeActive:true}:null),
  definitions:[{id:20008,class:'SPModCharm',kind:'artifact',name:'Mod fixture',maxLevel:3,criteria:'',categories:[],
   stats:[{statusID:'PHYSICAL_DAMAGE',valuesByLevel:[999999,999999,999999,999999]}],attackable:true,curves:{damageByLevel:[999999]},mechanics:{damageRatio:999999}}],
  matrices:{level:levels,multiply:Array(capacity).fill(1),disable:Array(capacity).fill(0),ignore:Array(capacity).fill(0)},
  nativeCounts:{},nativeEffects:[{uid:'mod',level:99,active:false}],patterns:{},fixedPatterns:[],locks:{pins:[],paper:[],compass:[],rows:[]}};
}
const moved=(cells,target)=>{const out=M.copy(cells);[out[0],out[target]]=[out[target],out[0]];return out;};
async function main(){
 let input=fixture(),p=R.prepare(input,D),d=p.model.defs.get(20008);
 ok(d.modelIgnored,'unregistered mod item becomes neutral');eq(d.stats,[],'custom stats ignored');eq(d.curves,{},'custom damage curves ignored');
 eq(p.ignoredItems.map(x=>x.id),[20008],'ignored item reported');ok(!D.items.some(x=>x.id===20008),'base catalog unchanged');
 eq(p.model.evaluate(moved(input.cells,1)).total,p.before.total,'unmarked mod item has no level/damage preference');
 let out=await R.search(p,{starts:2,stepsPerStart:400,passes:4});eq(out.cells,input.cells,'neutral item keeps position on ties');
 eq(out.after.objective,p.model.evaluate(out.cells).objective,'cached and complete objectives match');
 for(const mark of [1,2,3,4]){
  input=fixture(mark);if(mark===3){input.matrices.level[0]=-1;input.matrices.level[2]=0;}
  p=R.prepare(input,D);const target=mark===4?2:1,candidate=p.model.evaluate(moved(input.cells,target));
  ok(R.compare(candidate,p.before)>0,'explicit arrow improves objective '+mark);
  out=await R.search(p,{starts:2,stepsPerStart:600,passes:8});ok(R.compare(out.after,p.before)>0,'search respects arrow '+mark);
  eq(out.cells.filter(Boolean).map(x=>[x.uid,x.id]),[['mod',20008]],'mod identity preserved '+mark);
 }
 input=fixture(2);input.locks.pins=[[0,'mod']];p=R.prepare(input,D);out=await R.search(p,{starts:2,stepsPerStart:200,passes:4});eq(out.cells[0].uid,'mod','pin preserved for mod item');
 input=fixture();input.definitions[0].criteria='CharmActivateCriteria_SideEnd';p=R.prepare(input,D);eq(p.model.evaluate(moved(input.cells,7)).active[7],false,'native side placement retained');
 input=fixture();input.definitions[0].criteria='CustomUnknownCriteria';p=R.prepare(input,D);eq(p.before.active[0],true,'unknown criterion retains native activation as fixed input');
 input=fixture();input.definitions[0].kind='misc';p=R.prepare(input,D);eq(p.model.defs.get(20008).kind,'misc','misc mod item still occupies inventory');
 input=fixture();input.definitions[0].class='StoneTablet';input.definitions[0].kind='tablet';input.definitions[0].rotatable=true;
 input.patterns.mod=Array.from({length:12},()=>Array.from({length:4},()=>({effects:[{cell:1,value:'1'}],conditions:[]})));input.matrices.level[1]=4;
 p=R.prepare(input,D);eq(p.before.level[1],4,'mod tablet native geometry retained');
 const known=D.items.find(x=>x.kind==='artifact'&&!x.unique&&x.criteria===''&&!x.weapon&&x.maxLevel>=1);
 input=fixture();input.cells[0].id=known.id;input.definitions[0].id=known.id;p=R.prepare(input,D);ok(p.model.defs.get(known.id).modelIgnored,'mod replacing vanilla class is neutral');
 input=fixture();input.cells[0].id=known.id;Object.assign(input.definitions[0],{id:known.id,class:known.class,modelIgnored:true});p=R.prepare(input,D);ok(p.model.defs.get(known.id).modelIgnored,'same-named class in mod assembly is neutral');
 eq(p.before.combat.channels,p.model.evaluate(moved(input.cells,1)).combat.channels,'reused vanilla ID does not trigger native combat rules');
 input=fixture();input.cells[1]={...input.cells[0],uid:'mod-copy'};input.definitions[0].unique=true;p=R.prepare(input,D);eq(p.state.cells.filter(Boolean).length,2,'duplicate mod unique instances preserved');
 input=fixture();input.definitions=[];assert.throws(()=>R.prepare(input,D),/未知物品/);checks++;
 input=fixture();input.cells[0].rotation=4;assert.throws(()=>R.prepare(input,D),/旋转/);checks++;
 const report={version:'3.0.1',checks,scope:'Offline native-shaped inputs; ID 20008, neutral scoring, explicit arrows, geometry and identity'};
 fs.writeFileSync(path.join(__dirname,'validation-mod-items-3.0.1.json'),JSON.stringify(report,null,2));
 fs.writeFileSync(path.join(__dirname,'mod-item-20008-input.json'),JSON.stringify(fixture(2),null,2));
 console.log(JSON.stringify(report));
}
main().catch(error=>{console.error(error);process.exitCode=1;});
