(function(root){
'use strict';
// Native level curves feed a deterministic steady-state surrogate. Frequencies,
// uptime and enemy resistance are explicit assumptions, not combat observations.
const clamp=(v,a,b)=>Math.max(a,Math.min(b,v));
const value=(a,level,fallback=0)=>Array.isArray(a)&&a.length?Number(a[clamp(level,0,a.length-1)])||0:fallback;
const elements=['PHYSICAL','FIRE','ICE','LIGHTNING'];
const elementalDamageKeys=elements.map(e=>e+'_DAMAGE'),elementalAmpKeys=elements.map(e=>e+'_DAMAGE_AMP');
const conversionKeys=elements.map(from=>elements.map(to=>from+'_TO_'+to));
const elementNames=['物理','火焰','冰霜','闪电'];
const handled=new Set(['Charm_StatusInstance','Charm_Basic','Charm_WhitePaper','Charm_UpCharmDamage','Charm_Magic','Charm_SummonGreenBat','Charm_NearLevelDamage','Charm_PlanetModule','Charm_CompanionChaos','Charm_RightSpellCooldownHelper','Charm_FireIce','Charm_WoodenBox','Charm_3Elemental_ByRow','Charm_AddStatByDefense','Charm_Wings','Charm_AddStatByAnotherStat','Charm_MPMultipleCast','Charm_CriticalChanceIncreaseWithTablets','Charm_CritAndRanged','Charm_DebuffDamage','Charm_WarmGlove','Charm_FireFly','Charm_IncreaseAllDamageByHP','Charm_MiniBossFight','Charm_FrozenEgg','Charm_KirinHorn','Charm_Freeze','Charm_ScytheOfBerut','Charm_FlamePlanet','Charm_TheTyphoonSheetmusic','Charm_LightningPouch','Charm_Lightning_BasicAttack','Charm_AirSlash','Charm_IceSpear','Charm_IceHammer','Charm_IceSword','Charm_RockElephant']);
const guideHits={3013:8,3020:4,3022:35,3023:14,3028:30,3034:2,3036:10};
function sustainWithLeech(supply,reserve,demand,gatedDamage,freeDamage,mpSteal){
 // UnitAvatar.ApplyDamage: HealMpFloat(actualDamage * MPSteal / 1000).
 // Solve uptime * demand = base supply + uptime * damage * steal, capped at 1.
 const ratio=Math.max(0,mpSteal)/1000,netDemand=demand-gatedDamage*ratio;
 const uptime=demand<=0||netDemand<=0?1:clamp((supply+reserve+freeDamage*ratio)/netDemand,0,1);
 return {uptime,mpPerSecond:(freeDamage+gatedDamage*uptime)*ratio};
}
const scenarioSpec={
 duration:{name:'战斗时长 / 秒',min:1,max:600,default:30},
 actions:{name:'基础动作 / 秒',min:.05,max:50,default:2},
 application:{name:'武器额外施加减益 / 秒',min:0,max:100,default:1},
 attackWeight:{name:'每动作攻击权重',min:.05,max:5,default:1},
 hitRate:{name:'多段命中比例',min:.05,max:1,default:.8},
 uptime:{name:'条件效果覆盖率',min:0,max:1,default:.8},
 enemyReduction:{name:'敌人减伤比例',min:0,max:.95,default:.4},
 manaRatio:{name:'平均当前 MP 比例',min:0,max:1,default:.5},
 externalMPRegen:{name:'额外回蓝 / 秒',min:0,max:100,default:0},
 mpDemand:{name:'武器耗蓝 / 秒',min:0,max:200,default:0},
 lastShare:{name:'末击伤害占比',min:0,max:1,default:.35},
 dashShare:{name:'冲刺攻击占比',min:0,max:1,default:.5},
 dashRate:{name:'基础冲刺 / 秒',min:0,max:20,default:1},
 teaStacks:{name:'红茶叶袋击杀层数',min:0,max:3,default:0},
 debuffKinds:{name:'目标减益种类数',min:0,max:12,default:3},
 baseHP:{name:'背包外最大 HP',min:1,max:2000,default:70},
 baseMP:{name:'背包外最大 MP',min:1,max:2000,default:50},
 basePhysical:{name:'背包外物理属性',min:0,max:5000,default:20},
 baseFire:{name:'背包外火属性',min:0,max:5000,default:20},
 baseIce:{name:'背包外冰属性',min:0,max:5000,default:20},
 baseLightning:{name:'背包外雷属性',min:0,max:5000,default:20},
 baseCritical:{name:'背包外暴击率 / %',min:0,max:200,default:0},
 baseDefense:{name:'背包外防御',min:0,max:1000,default:0},
 companionRate:{name:'同伴基础攻击 / 秒',min:.05,max:10,default:1},
};
function scenario(profile,overrides={}){
 const s=Object.fromEntries(Object.entries(scenarioSpec).map(([k,d])=>[k,d.default]));
 for(const k of ['actions','application','attackWeight','lastShare'])if(profile[k]!==undefined)s[k]=profile[k];
 s.mpDemand=profile.mpUse||0;s.criticalPolicy='build';s.nearTarget=true;s.elite=true;
 return {...s,...overrides};
}
function validateScenario(input={}){
 if(!input||typeof input!=='object'||Array.isArray(input))throw Error('场景参数必须是对象。');
 const out={};
 for(const [k,d] of Object.entries(scenarioSpec))if(input[k]!==undefined){const n=input[k];if(typeof n!=='number'||!Number.isFinite(n)||n<d.min||n>d.max)throw Error(d.name+' 超出范围。');out[k]=n;}
 if(input.criticalPolicy!==undefined){if(!['build','normal','disabled'].includes(input.criticalPolicy))throw Error('暴击策略无效。');out.criticalPolicy=input.criticalPolicy;}
 for(const k of ['nearTarget','elite'])if(input[k]!==undefined){if(typeof input[k]!=='boolean')throw Error('场景开关必须为布尔值。');out[k]=input[k];}
 return out;
}
function attackable(d,weapon=null){
 if(!d?.attackable)return false;
 if(d.class==='Charm_Magic')return !!d.skillEffect?.damagePercentByLevel;
 if(d.class==='Charm_EchoOfTheGlacier')return weapon===0;
 return true;
}
function needleRoot(c,cells,defs,weapon){
 const seen=new Set();let k=c;
 while(k>=0&&k<cells.length&&cells[k]){
  if(seen.has(k))return -1;seen.add(k);
  const d=defs.get(cells[k].id);if(d.class!=='Charm_UpCharmDamage')return attackable(d,weapon)?k:-1;
  const x=k%6+(d.mechanics.xOffset||0),y=Math.floor(k/6)+(d.mechanics.yOffset??-1);
  k=x<0||x>=6||y<0?-1:y*6+x;
 }
 return -1;
}
function elementalStats(s,stats){
 const raw=[s.basePhysical,s.baseFire,s.baseIce,s.baseLightning],amps=Array(4),destinations=Array(4),percents=Array(4);
 for(let i=0;i<4;i++){
  raw[i]+=stats[elementalDamageKeys[i]]||0;amps[i]=1+(stats[elementalAmpKeys[i]]||0)/100;
  let destination=-1,percent=0;
  for(let j=0;j<4;j++){const v=stats[conversionKeys[i][j]]||0;if(i!==j&&v>percent){percent=v;destination=j;}}
  destinations[i]=destination;percents[i]=percent;
 }
 const convert=bonus=>{
  const amplified=Array(4),out=Array(4);
  for(let i=0;i<4;i++){amplified[i]=Math.trunc((raw[i]+(bonus?bonus[i]:0))*amps[i]);out[i]=destinations[i]>=0&&amplified[i]>20?20:amplified[i];}
  for(let i=0;i<4;i++)if(destinations[i]>=0&&amplified[i]>20)out[destinations[i]]+=Math.trunc((amplified[i]-20)*percents[i]/100);
  return out;
 };
 // UnitAvatar.SelectElementalConversion chooses one largest conversion per
 // source (first element wins ties). Conversion removes the source's excess
 // over 20. Highest-element bonus is selected after conversion, then the
 // resulting raw bonus participates in amplification/conversion as native.
 const initial=convert(null),highest=Math.max(...initial),bonus=Math.max(0,stats.HIGHEST_ELEMENTAL_DAMAGE||0);
 return (bonus===0?initial:convert(initial.map(x=>x>0&&x===highest?bonus:0))).map(x=>Math.max(0,x));
}
function compileContext(catalog,state,defs,profile=null){
 const keys=new Set(['CRITICAL','DEFENSE','FIRE_DAMAGE','ICE_DAMAGE','LIGHTNING_DAMAGE','PHYSICAL_DAMAGE','MP_REGEN','CRITICAL_DAMAGE_RATE','FINAL_DAMAGE','MAGIC_DAMAGE_BONUS','ATTACK_SPEED']);
 for(const x of state.cells)if(x)for(const st of defs.get(x.id).stats||[])keys.add(st.statusID);
 for(const [id,v] of state.runtime?.statOffsetEntries||Object.entries(state.runtime?.statOffsets||{}))if(v!==0)keys.add(id);
 const combos=[];for(const cat of catalog.categories)for(const t of cat.combo.addStatByCombo||[]){const stats=t.status.map(token=>{const [k,v]=token.split('/');keys.add(k);return [k,Number(v)||0];});combos.push({id:cat.id,threshold:t.comboCount,stats});}
 // Fixed-shape numeric accumulators avoid a different V8 object shape for
 // every item permutation. JSON-quoted keys are data, never executable input.
 const makeStats=Function('return {'+[...keys].map(k=>JSON.stringify(k)+':0').join(',')+'}');
 const ids=state.cells.filter(Boolean).map(x=>x.id),uniqueIds=ids.filter(id=>defs.get(id).unique);
 const recordsByUid=new Map();
 const effectClasses=new Set([...evaluate.toString().matchAll(/case '(Charm_[^']+)'/g)].map(m=>m[1]));effectClasses.add('Charm_UpCharmDamage');
 for(const inst of state.cells)if(inst){const d=defs.get(inst.id);if(d.kind==='artifact'&&!d.modelIgnored)recordsByUid.set(inst.uid,{c:0,inst,d,lv:0,active:false,staticPlan:null,
  effect:effectClasses.has(d.class),planet:d.class==='Charm_SummonGreenBat',companion:/^Charm_(Summon|Companion)/.test(d.class)&&!['Charm_SummonGreenBat','Charm_CompanionChaos'].includes(d.class)&&!!d.curves.damageByLevel,magic:d.class==='Charm_Magic'});}
 const context={makeStats,combos,recordsByUid,records:[],byId:new Map(),owners:new Map(),activeRecords:[],planets:[],companions:[],magics:[],effects:[],goals:null,aggregate:null,
  distinct:new Set(ids).size===ids.length,distinctUnique:new Set(uniqueIds).size===uniqueIds.length,incremental:false};
 // Native static status contributions are integers. Within a proven safe sum
 // bound, subtract/add updates are exact regardless of candidate visit order.
 // Keep combos, conditional effects, external offsets and conversions outside
 // this accumulator: those can be fractional and retain their original order.
 let bound=0;
 for(const x of state.cells)if(x&&defs.get(x.id).kind==='artifact'&&!defs.get(x.id).modelIgnored){
  const d=defs.get(x.id),groups=d.stats||[];
  for(const st of groups){const values=st.valuesByLevel||[];if(values.some(v=>!Number.isSafeInteger(v)))bound=Infinity;else bound+=Math.max(0,...values.map(Math.abs));}
  const levels=Number.isInteger(d.maxLevel)&&d.maxLevel>=0&&d.maxLevel<=64?Array.from({length:d.maxLevel+1},(_,lv)=>groups.map(st=>value(st.valuesByLevel,lv))):[];
  const plan={groups,levels,active:false,level:null,values:null};recordsByUid.get(x.uid).staticPlan=plan;
 }
 if(bound<2**48){
  const base=makeStats(),scratch=makeStats(),dirty=[];context.incremental=true;
  context.makeStats=()=>{for(const key of dirty)scratch[key]=base[key]||0;dirty.length=0;return scratch;};
  context.touch=key=>dirty.push(key);
  context.updateStatic=r=>{
   const plan=r.staticPlan;if(plan.active===r.active&&plan.level===r.lv)return;
   const next=r.active?(plan.levels[r.lv]||plan.groups.map(st=>value(st.valuesByLevel,r.lv))):null,previous=plan.values;
   for(let i=0;i<plan.groups.length;i++){const key=plan.groups[i].statusID,delta=(next?.[i]||0)-(previous?.[i]||0);base[key]+=delta;scratch[key]=base[key];}
   plan.active=r.active;plan.level=r.lv;plan.values=next;
  };
  if(profile&&context.distinctUnique){
   // Only these native classes have no level/position behavior beyond their
   // status values. Explicit item reads and build goals retain their own level
   // identity. Equal integer sums are thus the same input to the full scorer.
   const queried=new Set([...evaluate.toString().matchAll(/\b(?:has|rec|curve)\((\d+)/g)].map(m=>Number(m[1])));
   for(const id of Object.keys(profile.core))queried.add(Number(id));queried.add(profile.receiver);
   const indices=new Map(),items=new Map();
   for(const r of recordsByUid.values())if(!queried.has(r.d.id)&&!r.d.attackable&&['Charm_StatusInstance','Charm_HomingMagic'].includes(r.d.class)&&!Object.values(r.d.curves||{}).some(Array.isArray)){
    const terms=r.staticPlan.groups.map(st=>{if(!indices.has(st.statusID))indices.set(st.statusID,indices.size);return [indices.get(st.statusID),st.valuesByLevel];});
    items.set(r.inst.uid,{terms,levels:r.staticPlan.levels});
   }
   if(items.size>1)context.aggregate={items,size:indices.size};
  }
 }
 return context;
}
function evaluate(catalog,state,cells,layout,profile,defs,resolvedScenario=null,context=null){
 const s=resolvedScenario||scenario(profile,state.scenario),n=cells.length,stats=context?context.makeStats():{},notes=[],warnings=[],links=[],unsupported=[];
 const add=(id,v)=>{if(context?.incremental)context.touch(id);stats[id]=(stats[id]||0)+v;},get=id=>stats[id]||0,pct=id=>Math.max(0,1+get(id)/100);
 const records=context?context.records:[],byId=context?context.byId:new Map(),owners=context?context.owners:new Map();
 if(context){records.length=0;owners.clear();context.activeRecords.length=0;context.planets.length=0;context.companions.length=0;context.magics.length=0;context.effects.length=0;}
 // Registration order is not present in an offline save. Preserve input instance
 // order across candidate moves; never choose the highest-level unique copy.
 if(context?.scoreOnly&&context.distinctUnique){/* No ownership ambiguity in this fixed item set. */}
 else if(context?.distinct){for(let c=0;c<n;c++)if(cells[c]&&layout.active[c])owners.set(cells[c].id,cells[c].uid);}
 else{const positions=new Map(cells.filter(Boolean).map(x=>[x.uid,x]));
  const activeUids=new Set(cells.filter((x,c)=>x&&layout.active[c]).map(x=>x.uid));
  for(const uid of state.uniqueOrder||state.cells.filter(Boolean).map(x=>x.uid)){const x=positions.get(uid);if(x&&activeUids.has(uid)&&!owners.has(x.id))owners.set(x.id,x.uid);}}
 for(let c=0;c<n;c++){
  const inst=cells[c];if(!inst)continue;
  let record=context?context.recordsByUid.get(inst.uid):null;const d=context?record?.d:defs.get(inst.id);if(d?.kind!=='artifact'||d.modelIgnored)continue;
  const active=layout.active[c]&&(!d.unique||context?.distinctUnique||owners.get(d.id)===inst.uid);
  if(context){record.c=c;record.inst=inst;record.lv=layout.effective[c];record.active=active;
   if(record.effect&&(active||d.class==='Charm_UpCharmDamage'))context.effects.push(record);
   if(active){context.activeRecords.push(record);if(record.planet)context.planets.push(record);if(record.companion)context.companions.push(record);if(record.magic)context.magics.push(record);}}
  else record={c,inst,d,lv:layout.effective[c],active};
  // Compiled records are stable objects. With a fixed inventory, previous
  // winners also receive their current active flag before any byId consumer.
  // Updating each active copy in cell order preserves the last-active rule.
  records.push(record);if(record.active||!context&&!byId.has(d.id))byId.set(d.id,record);
  if(context?.incremental)context.updateStatic(record);
  else if(record.active)for(const st of d.stats)add(st.statusID,value(st.valuesByLevel,record.lv));
 }
 const has=id=>!!byId.get(id)?.active,rec=id=>{const r=byId.get(id);return r?.active?r:null;};
 const curve=(id,key,fallback=0)=>{const r=rec(id);return r?value(r.d.curves[key]||r.d.mechanics[key],r.lv,fallback):0;};
 if(context){for(const t of context.combos)if((layout.counts[t.id]||0)>=t.threshold)for(const [key,num] of t.stats)add(key,num);}
 else for(const cat of catalog.categories)for(const t of cat.combo.addStatByCombo||[])if((layout.counts[cat.id]||0)>=t.comboCount)for(const token of t.status){const [key,num]=token.split('/');add(key,Number(num)||0);}
 add('CRITICAL',s.baseCritical*100);add('DEFENSE',s.baseDefense);
 const localDamage=Array(n).fill(0),localCooldown=Array(n).fill(0),localCost=Array(n).fill(0),chaosRows=new Set();
 const tabletCount=cells.filter(x=>x&&defs.get(x.id).kind==='tablet').length;
 for(const r of context?context.effects:records){
  const {d,c,lv}=r,m=d.mechanics||{},v=key=>value(d.curves[key]||m[key],lv);
  if(d.class==='Charm_UpCharmDamage'){
   const target=needleRoot(c,cells,defs,state.weapon);if(target>=0){const td=defs.get(cells[target].id),needleLevel=clamp(layout.level[c],0,d.maxLevel),bonus=value(d.curves.damageBonusByLevel,needleLevel)+(td.rarity<=m.maxRarity?value(d.curves.dependencyDamageBonusByLevel,needleLevel):0);localDamage[target]+=bonus;links.push({from:c,to:target,type:'needle',value:bonus});}
   continue;
  }
  if(!r.active)continue;
  switch(d.class){
   case 'Charm_FireIce':add(c%6<3?'FIRE_DAMAGE':'ICE_DAMAGE',v('mainStat'));add(c%6<3?'ICE_DAMAGE':'FIRE_DAMAGE',v('oppositeStat'));break;
   case 'Charm_WoodenBox':{const count=cells.slice(0,6).filter(x=>x&&defs.get(x.id).kind==='artifact').length;for(const e of elements.slice(1))add(e+'_DAMAGE',count*v('apPerQuickSlotCharmByLevel'));break;}
   case 'Charm_3Elemental_ByRow':add(elements[Math.floor(c/6)%4]+'_DAMAGE',v('addElementalStatByLevel'));break;
   case 'Charm_IncreaseMPRegen':add('MP_REGEN',v('addMPRegenByLevel'));break;
   case 'Charm_CriticalChanceIncreaseWithTablets':add('CRITICAL',tabletCount*v('criticalBonusByLevel'));break;
   case 'Charm_CritAndRanged':if(s.nearTarget)add('CRITICAL_DAMAGE_RATE',m.critDamageOnApplied);break;
   case 'Charm_DebuffDamage':add('FINAL_DAMAGE',v('additionalDamage')*s.debuffKinds);break;
   case 'Charm_WarmGlove':add('FINAL_DAMAGE',v('damageBonusByLevel')*s.uptime);break;
   case 'Charm_FireFly':add('LIGHTNING_DAMAGE',v('lightningDamageByLevel')*s.uptime);break;
   case 'Charm_IncreaseAllDamageByHP':add('FINAL_DAMAGE',v('damagePercentByLevel')*s.uptime);break;
   case 'Charm_MiniBossFight':add('FINAL_DAMAGE',v('addDamageByLevel')*s.teaStacks);break;
   case 'Charm_RightSpellCooldownHelper':if(c%6<5&&cells[c+1]&&defs.get(cells[c+1].id).class==='Charm_Magic'){const bonus=v('cooldownRecoveryByLevel');localCooldown[c+1]+=bonus;links.push({from:c,to:c+1,type:'hourglass',value:bonus});}break;
   case 'Charm_ReduceMPCost':if(c%6>0&&cells[c-1]&&defs.get(cells[c-1].id).class==='Charm_Magic'){const bonus=v('reducePercentByLevel');localCost[c-1]+=bonus;links.push({from:c,to:c-1,type:'cost',value:bonus});}break;
   case 'Charm_CompanionChaos':chaosRows.add(Math.floor(c/6));break;
  }
  if(!context&&!handled.has(d.class)&&![1040,1080,1025,1185,1090,1062,1069].includes(d.id)&&d.effects.length)unsupported.push({id:d.id,name:d.name,class:d.class,statsOnly:!!d.stats.length});
 }
 add('FINAL_DAMAGE',layout.harmonyTotal);
 // Game snapshots subtract the original modeled contribution before adding
 // this fixed residual. Never add the full current panel to backpack stats.
 for(const [id,v] of state.runtime?.statOffsetEntries||Object.entries(state.runtime?.statOffsets||{}))if(v!==0)add(id,v);
 if(has(1053))for(const e of elements.slice(1))add(e+'_DAMAGE',Math.floor(get('DEFENSE')/10)*curve(1053,'addByLevel'));
 if(has(1291))add('MAGIC_DAMAGE_BONUS',Math.floor(get('EVASION')/Math.max(1,curve(1291,'perBaseStatByLevel'))));
 if(has(1305))add('ATTACK_SPEED',Math.floor(get('DEBUFF_DAMAGE')/Math.max(1,curve(1305,'perBaseStatByLevel'))));
 if(has(1143))add('FINAL_DAMAGE',Math.floor(Math.max(0,get('ATTACK_SPEED'))/Math.max(1,curve(1143,'attackSpeedUnitByLevel'))));
 const rawStats=context?null:{...stats};
 const el=elementalStats(s,stats);
 const element=e=>e==='HIGHEST'?Math.max(...el):e==='TRI'?(el[1]+el[2]+el[3])/3:el[Math.max(0,elements.indexOf(e))];
 const mpBase=Math.trunc(s.baseMP+get('MAX_MP'));
 const maxHP=Math.max(1,(s.baseHP+get('MAX_HP'))*pct('FINAL_HP')),maxMP=Math.max(1,mpBase+Math.trunc(mpBase*get('FINAL_MP')/100));
 const noCrit=s.criticalPolicy==='disabled'||(s.criticalPolicy==='build'&&profile.noCrit);
 function critical(e,{magic=false,follower=false,extra=0,last=false,sun=false}={}){
  if(noCrit)return 1;
  let chance=get('CRITICAL')/100,bonus=50+get('CRITICAL_DAMAGE_RATE');
  if(follower){chance*=get('FOLLOWER_CRITICAL_CONTRIBUTE')/100;bonus=50+get('CRITICAL_DAMAGE_RATE')*get('FOLLOWER_CRITICAL_CONTRIBUTE')/100;}
  if(['ICE','CHAOS','ICE_LIGHTNING'].includes(e))chance+=curve(1150,'criticalChanceByLevel');
  if(['LIGHTNING','CHAOS','ICE_LIGHTNING'].includes(e))chance+=curve(1170,'addCriticalByLevel');
  if(e==='FIRE'||e==='CHAOS')bonus+=curve(1185,'addCriticalDamageByLevel');
  if(magic)chance+=get('MAGIC_CRITICAL')/100;if(sun)chance+=get('FLAME_SWORD_CRITICAL');
  if(last)chance+=curve(1025,'criticalBonusPercentByLevel');
  chance+=extra;const execution=has(1173)&&(!follower||get('FOLLOWER_CRITICAL_CONTRIBUTE')>0);
  return Math.max(0,1+(clamp(chance,0,100)+(execution?clamp(chance-100,0,100):0))*bonus/10000);
 }
 const speed=Math.max(.05,pct('ATTACK_SPEED')),actions=s.actions*(profile.speed||s.autoMixed?speed:1),dash=s.dashRate*pct('DASH_RECOVERY_SPEED');
 const allDamage=1+(get('FINAL_DAMAGE')+(s.elite?get('ELITE_DAMAGE'):0))/100;
 const trueDamage=Math.max(0,get('TRUE_DAMAGE'));
 const defenseFactor=extra=>clamp(1-s.enemyReduction*(1-clamp(get('IGNORE_DEFENSE')+extra,0,100)/100),.01,1);
 const damage=(raw,e,options={})=>Math.max(0,raw)*Math.max(0,allDamage+(options.follower?get('FOLLOWER_DAMAGE')/100:0))*(options.debuff?pct('DEBUFF_DAMAGE'):1)*critical(e,options)*defenseFactor(options.ignore||0)*(has(1151)?.98:1)+trueDamage;
 let mpDemand=s.mpDemand*Math.max(0,1-get('SPECIAL_ATTACK_COST_REDUCTION')/100);
 let mpSupply=Math.max(0,get('MP_REGEN')*.1+s.externalMPRegen);const mpReserve=maxMP*s.manaRatio/s.duration;
 const sustain=demand=>demand<=0?1:clamp((mpSupply+mpReserve)/demand,0,1);
 const channels=[],itemOutput=Array(n).fill(0),metrics={};
 function channel(key,name,output,detail){const v=Number.isFinite(output)?Math.max(0,output):0;if(!context||s.autoMixed||profile.kind==='balanced')channels.push({key,name,value:v,detail});return v;}
 const knownOutput=new Set();
 const activeRecords=context?context.activeRecords:records.filter(r=>r.active),planets=context?context.planets:activeRecords.filter(r=>r.d.class==='Charm_SummonGreenBat');
 const companions=context?context.companions:activeRecords.filter(r=>/^Charm_(Summon|Companion)/.test(r.d.class)&&!['Charm_SummonGreenBat','Charm_CompanionChaos'].includes(r.d.class)&&r.d.curves.damageByLevel);
 const debuffRate=cat=>s.application+((layout.counts[cat]||0)>=2?((layout.counts[cat]||0)>=6?1.25:.5):0);
 const blue=get('BLUE_BURN_CHANGE')>0,plasma=get('PLASMA_ACTIVE')>0;
 const burnElement=blue&&el[2]>el[1]?'ICE':'FIRE';
 const burnBase=blue?Math.max(el[1],el[2])+Math.min(el[1],el[2])*.25:el[1];
 const plasmaBase=blue?(Math.max(el[1],el[2])+el[3])/2+Math.min(el[1],el[2])*.25:el[1]+el[3];
 const burnTick=(plasma?plasmaBase:burnBase)*(get('BURN_EVO')>0?.28:.18)*(1+(get('BURN_DAMAGE')+(plasma?get('ELECTRIC_DAMAGE'):0))/100)*(plasma?pct('PLASMA_DAMAGE'):1);
 metrics.burnTick=burnTick;
 let output=0;
 if(['weapon','balanced'].includes(profile.kind)){
  const e=s.autoMixed?'PHYSICAL':profile.element||'PHYSICAL',base=element(e)*(1+(get('FINAL_WEAPONDAMAGE')+(profile.defenseRatio?get('DEFENSE')*profile.defenseRatio*100:0))/100);
  const mode=profile.mode,basic=1+get('BASIC_ATTACK_DAMAGE')/100,special=1+get('SPECIAL_ATTACK_DAMAGE')/100,dashBonus=1+get('DASH_ATTACK_DAMAGE')/100;
  const modeBonus=mode==='special'?special:mode==='dash'?dashBonus:mode==='hybrid'?basic*(1-s.dashShare)+dashBonus*s.dashShare:basic;
  const lastBoost=curve(1080,'damagePercentByLevel');
  const hit=(damage(base*modeBonus,e)*(1-s.lastShare)+damage(base*modeBonus*(1+lastBoost/100),e,{last:true})*s.lastShare);
  output=channel('weapon','武器攻击',hit*(mode==='dash'?dash:actions)*(1+(profile.evasionRatio||0)*get('EVASION')/100)*sustain(mpDemand),'属性 × 对应攻击加成 × 期望暴击 × 设定动作频率');
 }
 if(['burn','plasma','freeze','shock'].includes(profile.kind)){
  const usePlasma=plasma&&['plasma','burn','shock'].includes(profile.kind);
  const rate=debuffRate(profile.kind==='freeze'?'GLACIER':profile.kind==='shock'?'MAGITECH':'EMBER');
  const burnStacks=Math.min(Math.max(0,2+get('BURN_STACK')+(usePlasma?get('ELECTRIC_STACK'):0)),rate*(1+get('BURN_ADD'))*Math.min(s.duration,4*pct('DEBUFF_DURATION')));
  metrics.burnStacks=burnStacks;
  if(['burn','plasma'].includes(profile.kind)&&(!profile.kind.includes('plasma')||plasma))output+=channel('burn','持续灼烧',damage(burnTick*burnStacks,burnElement,{debuff:true})*2*pct('BURN_SPEED')*sustain(mpDemand),'单跳 × 实际可维持层数 × 跳速');
  if(profile.kind==='freeze'){
   const rateWithClaw=rate*(1+curve(1145,'freezePercents')/100),threshold=Math.max(1,5-get('FREEZE_THRESHOLD'));
   output=channel('freeze','冻结爆发',damage(el[2]*5*pct('FREEZE_DAMAGE'),'ICE',{debuff:true})*rateWithClaw/threshold*s.uptime,'施加频率 × 蓝爪额外层数 ÷ 冻结门槛；覆盖率包含抗性与空窗');metrics.freezeThreshold=threshold;
  }
  if(profile.kind==='shock'||usePlasma){
   const interval=get('ELECTRIC_QUICKNESS')>0?1:2,shockRate=debuffRate('MAGITECH'),stack=Math.min(2+get('ELECTRIC_STACK')+(usePlasma?get('BURN_STACK'):0),shockRate*interval*pct('DEBUFF_DURATION'));
   const base=usePlasma?Math.floor((el[1]+el[3])/2)*pct('PLASMA_DAMAGE')*(1+(get('BURN_DAMAGE')+get('ELECTRIC_DAMAGE'))/100):el[3]*1.8;
   const triggered=shockRate*clamp(get('ELECTRIC_LUCK'),0,100)/100*pct('DEBUFF_DURATION');
   output+=channel('shock','触电结算',((stack>0?damage(base*stack,'LIGHTNING',{debuff:true})/interval:0)+damage(base,'LIGHTNING',{debuff:true})*triggered)*sustain(mpDemand),'普通定时结算 + 指南针强化触发；强化部分计持续时间');metrics.shockStacks=stack;
  }
  if(profile.kind==='plasma'&&!plasma)warnings.push('等离子没有启用：需要启用等离子头盔。');
 }
 let planetOutput=0,planetHits=0;
 for(const r of planets){
  const {d,c,lv}=r,dt=d.planet||{},frequency=(pct('PLANET_ATTACK_SPEED')+(get('SUPERPLANET')>0?actions*s.attackWeight*.95:0))/Math.max(.1,dt.fireIntervalTimer?.time||1)*(dt.fireCount||1);
  const e=['PHYSICAL','FIRE','ICE','LIGHTNING','CHAOS'][dt.elementalType]||'PHYSICAL';
  const borrowed=has(1295)?burnTick*([1,2,3,4,4][d.rarity]||1):0;
  const raw=(value(d.curves.damageByLevel,lv)+borrowed)*pct('PLANET_DAMAGE')*(layout.planetTargets.includes(c)?1.5:1)*(1+localDamage[c]/100);
  const total=damage(raw,e)*frequency*s.hitRate;itemOutput[c]=total;planetOutput+=total;planetHits+=frequency*s.hitRate;knownOutput.add(d.id);
 }
 if(profile.kind==='planet')output=channel('planet','行星射击',planetOutput*sustain(mpDemand),profile.burnPlanet?'银河动作加速；灼星只借用单跳，不借层数和跳速':'原生发射间隔、发射数、银河和望远镜');
 let companionOutput=0,companionHits=0,companionCloudHits=0;
 for(const r of companions){const frequency=s.companionRate*(1+curve(1090,'attackSpeedPercentByLevel')/100)*s.uptime,e=chaosRows.has(Math.floor(r.c/6))?'CHAOS':({1198:'ICE',1199:'FIRE',1200:'LIGHTNING'}[r.d.id]||'PHYSICAL'),out=damage(value(r.d.curves.damageByLevel,r.lv)*(1+localDamage[r.c]/100),e,{follower:true})*frequency;companionOutput+=out;companionHits+=frequency;if(['LIGHTNING','CHAOS'].includes(e))companionCloudHits+=Math.min(frequency,10);itemOutput[r.c]=out;knownOutput.add(r.d.id);}
 if(profile.kind==='companion')output=channel('companion','同伴攻击',companionOutput,'原生基础伤害；同排混沌转化；玩家暴击按指南书比例继承');
 const canThreshold=has(1292)?curve(1292,'multipleCastMPThresholdByLevel'):null,multicast=canThreshold!==null&&maxMP>=canThreshold?1+(rec(1292).d.mechanics.multicast||1):1;
 metrics.multicast=multicast;metrics.multicastThreshold=canThreshold;
 if(canThreshold!==null&&multicast===1)warnings.push('浇水壶未双发：最大 MP '+maxMP.toFixed(0)+' / 当前门槛 '+canThreshold+'。');
 let magicOutput=0,casts=0,magicDemand=0;
 // Per-cast hit counts are guide estimates and are multiplied by editable hitRate.
 for(const r of context?context.magics:activeRecords.filter(r=>r.d.class==='Charm_Magic')){
  const {d,c,lv}=r,sk=d.skill||{},ef=d.skillEffect||{};if(!ef.damagePercentByLevel)continue;
  if(profile.fusion&&![3002,3027,3011].includes(d.id))continue;
  const e=ef.relatedDamage?.replace('Damage','').toUpperCase()||'FIRE';
  let rate=(1+(get('COOLDOWN_RECOVERY_SPEED')+localCooldown[c])/100)/Math.max(.2,sk.cooldownTime||1);
  if(profile.fusion)rate=Math.min(rate,actions);
  const isBolt=ef.class==='ActiveSkill_Bolt',hitCount=guideHits[d.id]||value(ef.fireCountByLevel,lv,ef.fireCount||1);
  const rawCost=value(sk.mpCostsByLevel,lv)*Math.max(0,1+(-get('MAGIC_COST_REDUCE')-localCost[c]+(isBolt&&has(1062)?curve(1062,'additionalCostPercent'):0))/100);
  const roundedCost=rawCost%1===.5?Math.round(rawCost/2)*2:Math.round(rawCost);
  const cost=profile.fusion?0:roundedCost;
  let raw=(value(ef.defaultDamageByLevel,lv)+element(e)*value(ef.damagePercentByLevel,lv)/100)*pct('MAGIC_DAMAGE_BONUS')*(1+localDamage[c]/100);
  if(profile.fusion)raw*=pct('BASIC_ATTACK_DAMAGE');
  let horn=1;if(isBolt&&has(1062))horn=3*curve(1062,'multiShotDamageRatioByLevel',1);
  if(!(horn>0))horn=1;
  const out=damage(raw,e,{magic:true,extra:value(ef.criticalChanceBonusByLevel,lv)})*rate*hitCount*horn*multicast*s.hitRate;
  magicOutput+=out;casts+=rate*multicast;magicDemand+=rate*cost;itemOutput[c]=out;knownOutput.add(d.id);
 }
 const magicResource=sustainWithLeech(mpSupply,mpReserve,magicDemand+(profile.kind==='magic'?mpDemand:0),magicOutput,s.autoMixed?(channels.find(c=>c.key==='weapon')?.value||0):0,get('MP_STEAL'));
 if(profile.kind==='magic'){
  mpDemand+=magicDemand;mpSupply+=magicResource.mpPerSecond;metrics.mpLeechPerSecond=magicResource.mpPerSecond;
  output=channel('magic','魔法书施放',magicOutput*magicResource.uptime,'每本书独立冷却；沙漏右侧加速；浇水壶门槛；MP 恢复与伤害吸蓝');
  if(profile.element==='TRI'&&Math.max(el[1],el[2],el[3])-Math.min(el[1],el[2],el[3])>.01)warnings.push('三元素面板未相等，最高属性加成不能同时覆盖火、冰、雷。');
 }
 if(profile.kind==='frost'||profile.kind==='frost-lake'){
  const receivers=activeRecords.filter(r=>profile.kind==='frost-lake'?r.d.id===1248:r.d.id===profile.receiver);
  for(const r of receivers){let raw,rate;
   if(r.d.id===1248){raw=el[2]*r.d.mechanics.damageRatio_IceElemental/100*Math.max(0,maxMP-catalog.constants.PLAYERDEFAULTMP)*r.d.mechanics.damageRatio_Mp/100;rate=r.d.mechanics.swordCount/r.d.mechanics.swordLifeTime;}
   else{raw=(r.d.mechanics.defaultDamage||0)+el[2]*value(r.d.curves.damagePercentByLevel,r.lv)/100;raw*=value(r.d.curves.fireCountByLevel,r.lv,1);rate=Math.min(actions,(1+get('CHARGING_CHARM_BONUS')/100)/Math.max(.1,r.d.charge?.defaultChargeTimer||6))*(1+get('CHARGING_CHARM_AMPLIFY'));}
   const out=damage(raw*pct('FROST_RELIC_DAMAGE')*(1+localDamage[r.c]/100),'ICE')*rate*s.hitRate;itemOutput[r.c]=out;output+=channel('frost-'+r.d.id,r.d.name,out,'原生伤害倍率 × 设定命中率 × 充能触发近似');knownOutput.add(r.d.id);
  }
 }
 if(['cloud','companion-cloud'].includes(profile.kind)){
  let restore=0,leafTriggers=0,bottles=0;
  for(const r of activeRecords){if(r.d.id===1035){restore+=value(r.d.curves.cloudByLevel,r.lv)/Math.max(.1,value(r.d.curves.cooldownByLevel,r.lv,3.5));bottles++;}if(r.d.id===1072)leafTriggers+=Math.min(actions*clamp(value(r.d.curves.lightningPercentByLevel,r.lv)*s.attackWeight/100,0,1),1/Math.max(.1,value(r.d.curves.cooldownTimeByLevel,r.lv,1)));}
  const cb=catalog.categories.find(x=>x.id==='DARKCLOUD').combo;
  const stock=cb.defaultDarkCloud+get('MIN_DARK_CLOUD'),keep=clamp(get('DARK_CLOUD_KEEP'),0,100)/100;
  const specialRequests=profile.bottle?Math.max(1,bottles)*dash:profile.kind==='companion-cloud'?(has(1263)?companionCloudHits*curve(1263,'cloudCountByLevel',1):0):leafTriggers;
  const autoRequests=(1+get('DARK_CLOUD_SPEED')/100+Math.max(0,get('ATTACK_SPEED'))*curve(1245,'cloudAttackSpeedByLevel')/10000)/cb.cloudTimer.time;
  const multishot=Math.max(1,get('DARK_CLOUD_MULTISHOT'));
  const requests=autoRequests*multishot+specialRequests*(profile.bottle?1:multishot);
  restore+=Math.max(1,Math.floor(stock*cb.defaultRestorePercent/100))/5*pct('DARK_CLOUD_RESTORE_DURING_BATTLE');
  const feasible=keep===1?requests:Math.min(requests,(restore+stock/s.duration)/(1-keep));
  const cloudElement=get('DARK_CLOUD_ICE')?'ICE_LIGHTNING':'LIGHTNING';
  const rawCloud=get('DARK_CLOUD_ICE')?(Math.max(el[2],el[3])*catalog.constants.DARKCLOUDDAMAGEPERCENT+Math.min(el[2],el[3])*catalog.constants.DARKCLOUDDAMAGEPERCENTICE)/100:el[3]*catalog.constants.DARKCLOUDDAMAGEPERCENT/100;
  output=(layout.counts.DARKCLOUD||0)>=2?channel('cloud','乌云落雷',damage(rawCloud*pct('DARK_CLOUD_DAMAGE')*(1+clamp(get('DARK_CLOUD_LUCK'),0,100)/100),cloudElement)*feasible*sustain(mpDemand),'原生自动放电 + 分支触发；回复每 5 秒结算，双点射每发分别消耗云'):0;
  metrics.cloudSupply=restore;metrics.cloudRequests=requests;metrics.cloudSpecialRequests=specialRequests;metrics.cloudMultishot=multishot;
  if(profile.kind==='companion-cloud'&&specialRequests===0)warnings.push('同伴乌云没有触发源：需要启用耦合器，并让同伴造成闪电或混沌伤害。');
  if(feasible+1e-6<requests)warnings.push('乌云回复不足，持续触发受库存限制。');
 }
 if(profile.kind==='sun'){
  const extraMagic=get('FLAME_SWORD_ADDITIONAL_ATTACK_FROM_MAGIC'),fromMagic=profile.bookSun?casts*(1+extraMagic)*(profile.adama?2:1):0;
  let frequency=fromMagic||actions*(1+get('FLAME_SWORD_ADDITIONAL_ATTACK_FROM_WEAPON'));
  if(profile.returnMode)frequency=(5+get('FLAME_SWORD_MAX'))/Math.max(.125,curve(1237,'cooldownByLevel',20));
  else if(!has(1236))frequency=Math.min(frequency,(5+get('FLAME_SWORD_MAX'))/20);
  frequency=Math.min(8,frequency)*(1+get('FLAME_SWORD_FAST_FALL')/100);
  let raw=el[1]*1.15*(1+(get('FLAME_SWORD_DAMAGE')+(get('FLAME_SWORD_MAGIC_DAMAGE')?get('MAGIC_DAMAGE_BONUS'):0))/100);
  mpDemand+=profile.bookSun?magicDemand:0;
  output=(layout.counts.FLAMESWORD||0)>=2?channel('sun','太阳剑触发',damage(raw*(1+clamp(get('FLAME_SWORD_LUCK'),0,100)/100*catalog.constants.FLAMESWORDLUCKBONUSDAMAGEPERCENT/100),'FIRE',{sun:true,ignore:get('FLAME_SWORD_IGNORE_DEFENSE')})*frequency*s.hitRate*sustain(mpDemand),'动作或法术触发 × 书库之阳等级门槛；回收循环为频率近似'):0;
  metrics.sunTriggers=frequency;
  if(has(1236)&&profile.returnMode)warnings.push('护肩分支同时启用陨铁耳环，回收路径与攻略假设不同。');
 }
 if(profile.kind==='true')output=channel('true','无视防御附伤',trueDamage*(actions*(has(1245)?2:1)+planetHits+companionHits)*sustain(mpDemand),'固伤 × 已建模命中；台风附伤另计一次命中，暴击与放大不放大固伤');
 if(profile.kind==='guardian'){
  const r=rec(1246),raw=r?get('DEFENSE')*value(r.d.curves.damagePercentByLevel,r.lv)/100:0;
  output=channel('guardian','岩象石矛',damage(raw,'PHYSICAL')*(3+Math.floor(Math.max(0,get('ATTACK_SPEED'))/15))*(1+dash*3)/15*s.hitRate,'防御倍率与每 15% 额外攻速的石矛门槛；15 秒循环为场景近似');
 }
 if(profile.kind==='judge'){
  const stacks=Math.min(2+get('BURN_STACK'),debuffRate('EMBER')*4)+Math.min(2+get('ELECTRIC_STACK'),debuffRate('MAGITECH')*2)+Math.min(4,debuffRate('GLACIER')*2);
  output=channel('judge','裁判官清除爆炸',damage(120*stacks*(1+(get('SPECIAL_ATTACK_DAMAGE')+get('FINAL_WEAPONDAMAGE'))/100),'CHAOS')*actions*sustain(mpDemand),'攻略固定 120 伤害 × 可叠层数；刷新、清除时序尚为平均近似');
 }
 if(profile.kind==='lake'){
  const added=Math.max(0,maxMP-catalog.constants.PLAYERDEFAULTMP),missing=maxMP*(1-s.manaRatio);let raw;
  if(profile.lakeMode==='adama')raw=missing*pct('MAGIC_DAMAGE_BONUS')*multicast;
  else if(profile.lakeMode==='missile')raw=el[1]*(1+added/100)*(1+(get('SPECIAL_ATTACK_DAMAGE')+get('FINAL_WEAPONDAMAGE'))/100);
  else raw=(Math.max(...el)+Math.floor(maxMP/50)*5)*Math.floor(maxMP*s.manaRatio/8)*(1+(get('SPECIAL_ATTACK_DAMAGE')+get('FINAL_WEAPONDAMAGE'))/100);
  output=channel('lake','湖泊武器代理',damage(raw,profile.element||'PHYSICAL',{magic:profile.lakeMode==='adama'})*(profile.lakeMode==='adama'?1:pct('MP_SKILL_DAMAGE'))*actions*sustain(mpDemand),'MP 与武器联动的攻略近似；武器倍率和完整动作时序待校准');
  warnings.push('湖泊独立武器使用攻略近似，当前指标不代表该武器的精确伤害。');
 }
 if(profile.kind==='balanced'){
  if(s.autoMixed){mpDemand+=magicDemand;mpSupply+=magicResource.mpPerSecond;metrics.mpLeechPerSecond=magicResource.mpPerSecond;}
  channel('planet','行星射击',planetOutput,'辅助通道');channel('magic','魔法书',magicOutput*(s.autoMixed?magicResource.uptime:sustain(magicDemand)),'辅助通道');channel('companion','同伴',companionOutput,'辅助通道');
  const nonzero=channels.filter(x=>x.value>0);output=s.autoMixed?nonzero.reduce((v,x)=>v+x.value,0):nonzero.length?Math.expm1(nonzero.reduce((v,x)=>v+Math.log1p(x.value),0)/nonzero.length):0;
 }
 let goals,completion;
 if(context){
  if(!context.goals)context.goals=Object.entries(profile.core).map(([id,target])=>[Number(id),Math.min(defs.get(Number(id))?.maxLevel||0,target)]);
  let sum=0;for(const [id,target] of context.goals){const r=byId.get(id);sum+=r?.active?(target===0?1:clamp(r.lv/target,0,1)):0;}completion=context.goals.length?sum/context.goals.length:1;
 }else{
  goals=Object.entries(profile.core).map(([id,target])=>{id=Number(id);const d=defs.get(id),r=byId.get(id);target=Math.min(d?.maxLevel||0,target);const ratio=r?.active?(target===0?1:clamp(r.lv/target,0,1)):0;return {id,name:d?.name||String(id),target,level:r?.active?r.lv:null,owned:!!r,met:ratio===1,ratio};});
  completion=goals.length?goals.reduce((a,g)=>a+g.ratio,0)/goals.length:1;
 }
 // EHP is an explicit utility proxy, not a claim about the game's defense curve.
 const survival=maxHP/s.baseHP*(1+Math.max(-90,get('DEFENSE'))/100)/(1-clamp(get('EVASION')/100,0,75)/100);
 const resource=sustain(mpDemand);
 if(resource<.999&&mpDemand>0)warnings.push('设定场景下 MP 只能维持 '+Math.round(resource*100)+'% 的循环；可补充背包外回蓝。');
 const duplicateIds=context?[]:[...new Set(records.filter((r,i,a)=>r.d.unique&&a.some((t,j)=>j!==i&&t.d.id===r.d.id)).map(r=>r.d.id))];
 if(duplicateIds.length)warnings.push('唯一神器重复：按方案保存的登记顺序选择首个启用实例；实际游戏登记顺序需另行核对。');
 if(unsupported.length)warnings.push(unsupported.length+' 件神器仍有动态效果未建模；已计入支持的静态属性或基础伤害。');
 if(has(1180)&&profile.kind==='freeze')warnings.push('冰海鸥之足的战斗叠层未纳入面板，冻结指标可能低估。');
 if(profile.weapon!==undefined&&state.weapon!==null&&profile.weapon!==state.weapon)warnings.push('武器筛选与构筑建议不同，请核对所需武器。');
 if(!context)for(const l of links)notes.push(({needle:'金色针 → ',hourglass:'沙漏 → ',cost:'雷伊星碎片 → '}[l.type])+defs.get(cells[l.to].id).name+(l.type==='cost'?' 耗蓝 -':' +')+l.value+'%');
 const breakdown={level:1000*Math.log1p(output/100),combo:80*completion,synergy:90*Math.log(Math.max(.05,survival))+40*Math.log(Math.max(.05,resource)),marks:layout.breakdown.marks};
 const total=breakdown.level*state.weights.level+breakdown.combo*state.weights.combo+breakdown.synergy*state.weights.synergy+breakdown.marks;
 if(context)return {total,breakdown,output,ownerUids:context.scoreOnly?null:Object.fromEntries(owners),notes:[]};
 return {total,breakdown,stats,rawStats,elements:el,elementNames,maxHP,maxMP,critical:critical(profile.element),output,channels,metrics,goals,completion,resource,mpDemand,mpSupply,survival,scenario:s,links,notes,warnings,unsupported,itemOutput,ownerUids:Object.fromEntries(owners),localDamage,localCooldown,localCost,modelVersion:'2.0',scope:'steady-state-surrogate'};
}
const api={evaluate,scenario,scenarioSpec,validateScenario,attackable,needleRoot,value,elementalStats,sustainWithLeech,compileContext};
if(typeof module!=='undefined'&&module.exports)module.exports=api;else root.BackpackMechanics=api;
})(typeof window!=='undefined'?window:globalThis);
