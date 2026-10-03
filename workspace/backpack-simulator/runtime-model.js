(function(root){
'use strict';
// Plain-data bridge. No Unity objects, screen recognition or network requests.
const M=typeof module!=='undefined'&&module.exports?require('./model.js'):root.BackpackModel;
const profiles=M.profiles;
// A separate Node worker only needs to yield its event loop. setTimeout(0)
// costs a Windows timer tick (~15.6 ms) per batch, even with an idle CPU.
const yieldSearch=()=>new Promise(resolve=>typeof setImmediate==='function'?setImmediate(resolve):setTimeout(resolve,0));
const specialWeapons={judge:[1115],nebolax:[528],'plasma-dagger':[1201],'library-katana':[417],
 'guardian':[1020],'armor-katana':[424],'planet-fixed':[126]};
const physical={0:'sword',1:'greatsword',2:'dagger',3:'crossbow',5:'katana',7:'staff'};
function identify(snapshot,catalog){
 const defs=new Map(catalog.items.map(d=>[d.id,d]));
 const ids=new Set(snapshot.cells.filter(Boolean).map(x=>x.id));
 const counts=snapshot.nativeCounts||{},weapon=snapshot.weapon;
 if(!weapon||!Number.isInteger(weapon.entityId)||!Number.isInteger(weapon.type))throw Error('原生武器尚未就绪');
 const eligible=p=>(p.weapon===undefined||p.weapon===weapon.type)&&
  (!specialWeapons[p.id]||specialWeapons[p.id].includes(weapon.entityId))&&(!p.fusion||weapon.entityId===503);
 if(snapshot.profile&&snapshot.profile!=='auto'){
  const p=profiles[snapshot.profile];if(!p||!eligible(p))throw Error('指定流派与当前武器不匹配');
  return {id:p.id,name:p.name,mode:'manual',confidence:'manual',candidates:[]};
 }
 // A double-cast branch requires the actual enabling artifact. Generic spell
 // supports cannot make it tie the ordinary spell branch without a watering can.
 const candidates=Object.values(profiles).filter(p=>p.id!=='balanced'&&eligible(p)&&(!p.double||ids.has(1292))).map(p=>{
  const core=Object.keys(p.core).map(Number),owned=core.filter(id=>ids.has(id));
  const coverage=core.length?owned.length/core.length:0;
  const exact=!!specialWeapons[p.id]||!!p.fusion;
  // Counts are evidence of a build, never an artifact power ranking.
  const category=Math.min(1,(counts[p.category]||0)/4);
  const score=4*coverage+category+(exact?1.5:0);
  return {id:p.id,name:p.name,score,coverage,category,exact,
   evidence:owned.map(id=>defs.get(id)?.name||String(id))};
 }).sort((a,b)=>b.score-a.score||a.id.localeCompare(b.id));
 const a=candidates[0],b=candidates[1],gap=a&&b?a.score-b.score:0;
 const fallback='physical-'+(physical[weapon.type]||'');
 const enough=a&&(a.exact||a.coverage>=.5)&&gap>=.35;
 const hasSpell=snapshot.cells.some(x=>x&&defs.get(x.id)?.class==='Charm_Magic');
 let id=enough?a.id:(a?.score>=2||hasSpell?'balanced':profiles[fallback]?fallback:'balanced'),confidence=enough?(gap>=1?'high':'medium'):'ambiguous';
 // Ambiguity inside one mechanism family must not discard the whole family.
 if(!enough&&a?.score>=2&&b&&profiles[a.id].kind===profiles[b.id].kind&&profiles[a.id].category===profiles[b.id].category&&profiles[profiles[a.id].kind]){id=profiles[a.id].kind;confidence='family';}
 if(weapon.entityId===503&&[3002,3011,3027].every(x=>ids.has(x))){id='fusion-tri';confidence='family';}
 const mixed=id==='balanced';
 return {id,name:mixed?'当前背包 · 混合收益':profiles[id].name,mode:'auto',confidence,mixed,
  candidates:candidates.slice(0,5),reason:confidence==='family'?'确认机制家族，子分支仍需结合实战判断':enough?'武器兼容、核心组件覆盖与连击数量':mixed?'未形成明确分支，计算当前武器和已持有法术、召唤的合计收益':'证据不足，采用当前武器基础模型并保留候选'};
}
function compare(a,b){for(let i=0;i<a.objective.length;i++){const d=a.objective[i]-b.objective[i];if(Math.abs(d)>1e-8)return d;}return 0;}
function comparePrimary(a,b){for(let i=0;i<7;i++){const d=a.objective[i]-b.objective[i];if(Math.abs(d)>1e-8)return d;}return 0;}
// Native Charm_StatusInstance only replaces its level-indexed status values;
// Charm_HomingMagic only overrides Weaved(). Do not generalize to unknown
// level-dependent effects, conversions, thresholds or "more levels is better".
const freeStatKeys=['DEFENSE','EVASION','MP_REGEN','MP_STEAL'];
const staticStats=d=>!d.attackable&&['Charm_StatusInstance','Charm_HomingMagic'].includes(d.class)&&!Object.values(d.curves||{}).some(Array.isArray);
const sortedEntries=o=>Object.entries(o).sort(([a],[b])=>a<b?-1:a>b?1:0);
function freeGainProof(model,cells,r){
 const combat=r.combat;if(!combat?.rawStats||!r.valid)return null;
 const stats=Object.fromEntries(freeStatKeys.map(k=>[k,combat.rawStats[k]||0]));
 const guards={supply:combat.mpSupply,resource:combat.resource,survival:combat.survival,output:combat.output},records=[];
 for(const x of cells.filter(Boolean).sort((a,b)=>a.uid<b.uid?-1:a.uid>b.uid?1:0)){
  const c=r.positions.get(x.uid),d=model.defs.get(x.id),plain=d.kind==='artifact'&&staticStats(d);
  // Every non-static item and every tablet retains its position, rotation and
  // level. Per-item guards prevent exchanging one item's benefit for another.
  records.push([x.uid,x.id,x.rotation,x.enchant,x.mark,plain?null:c,plain?null:r.level[c],r.active[c],
   [...r.cats[c]].sort(),r.harmony[c],combat.localDamage[c],combat.localCooldown[c],combat.localCost[c],
   plain?d.stats.filter(s=>!freeStatKeys.includes(s.statusID)).map(s=>[s.statusID,M.mechanics.value(s.valuesByLevel,r.effective[c])]):null]);
  if(plain){guards['level:'+x.uid]=r.effective[c];for(const k of freeStatKeys)guards[x.uid+':'+k]=r.active[c]?d.stats.filter(s=>s.statusID===k).reduce((n,s)=>n+M.mechanics.value(s.valuesByLevel,r.effective[c]),0):0;}
  if(x.mark)records.push(['mark',x.uid,x.mark===3?r.level[c]:x.mark===4?+(r.level[c]<0):r.effective[c]]);
 }
 const links=combat.links.map(l=>[cells[l.from]?.uid,cells[l.to]?.uid,l.type,l.value]).sort((a,b)=>JSON.stringify(a).localeCompare(JSON.stringify(b)));
 const invariant=JSON.stringify([records,sortedEntries(r.counts),sortedEntries(combat.rawStats).filter(([k])=>!freeStatKeys.includes(k)),sortedEntries(combat.ownerUids),links,
  r.planetTargets.map(c=>cells[c]?.uid).sort(),combat.elements,combat.maxHP,combat.maxMP,combat.critical,combat.completion,combat.mpDemand]);
 for(const channel of combat.channels)guards['channel:'+channel.key]=channel.value;
 return {schema:1,invariant,stats,guards};
}
function dominatesFreeGain(a,b){
 if(!a||!b||a.schema!==1||b.schema!==1||!a.invariant||a.invariant!==b.invariant||!a.stats||!b.stats||!a.guards||!b.guards)return false;
 if(Object.keys(a.stats).length!==freeStatKeys.length||Object.keys(b.stats).length!==freeStatKeys.length||freeStatKeys.some(k=>!Object.hasOwn(a.stats,k)||!Object.hasOwn(b.stats,k))||!Object.keys(b.guards).length)return false;
 let improved=false;
 for(const field of ['stats','guards']){
  const keys=Object.keys(b[field]);if(keys.length!==Object.keys(a[field]).length)return false;
  for(const k of keys){const x=a[field][k],y=b[field][k];if(!Number.isFinite(x)||!Number.isFinite(y)||x<y-1e-8)return false;if(field==='stats'&&x>y+1e-8)improved=true;}
 }
 return improved;
}
function acceptsResult(after,before){const primary=comparePrimary(after,before);return after.valid!==false&&(primary>0||primary===0&&(compare(after,before)>=0||dominatesFreeGain(after.freeGain,before.freeGain)));}
async function refineFreeGains(prepared,result,options,started){
 const {model}=prepared,cancelled=options.cancelled||(()=>false),expired=()=>options.budgetMs>0&&Date.now()-started>=options.budgetMs;
 const pinned=new Set((model.snapshot.locks?.pins||[]).map(x=>x[0]));
 const eligible=result.cells.filter(x=>x&&!x.locked&&staticStats(model.defs.get(x.id))&&model.defs.get(x.id).stats.some(s=>freeStatKeys.includes(s.statusID)));
 const details={checks:0,moves:[],ms:0},begin=Date.now();let cells=result.cells,r=result.after;
 r.freeGain=freeGainProof(model,cells,r);result.before.freeGain=freeGainProof(model,prepared.state.cells,result.before);
 // A monotone local pass, separate from annealing: Pareto dominance is a partial
 // order and must not replace annealing's transitive scalar/lexicographic order.
 for(let pass=0;pass<Math.min(6,eligible.length+1)&&!cancelled()&&!expired();pass++){
  let improved=false;
  for(const item of eligible){
   const a=r.positions.get(item.uid);if(pinned.has(a)||!r.active[a]||r.effective[a]>=model.defs.get(item.id).maxLevel)continue;
   for(let b=0;b<cells.length&&!cancelled()&&!expired();b++){
    if(a===b||pinned.has(b)||cells[b]?.locked||cells[b]&&!staticStats(model.defs.get(cells[b].id)))continue;
    const candidate=cells.slice();[candidate[a],candidate[b]]=[candidate[b],candidate[a]];
    const next=model.evaluate(candidate);details.checks++;
    if(!next.valid||comparePrimary(next,r)!==0)continue;
    next.freeGain=freeGainProof(model,candidate,next);
    if(!dominatesFreeGain(next.freeGain,r.freeGain))continue;
    details.moves.push({uid:item.uid,id:item.id,from:a,to:b,levelBefore:r.effective[a],levelAfter:next.effective[b],statsBefore:r.freeGain.stats,statsAfter:next.freeGain.stats});
    cells=candidate;r=next;improved=true;break;
   }
  }
  if(!improved)break;await yieldSearch();
 }
 details.ms=Date.now()-begin;result.cells=cells;result.after=r;result.freeGains=details;
 result.budgetReached=result.budgetReached||expired();result.cancelled=result.cancelled||cancelled();
}
class RuntimeModel extends M.Model{
 constructor(catalog,state,snapshot){super(catalog,state);this.snapshot=snapshot;}
 checkLayoutConstraints(cells,r){
  const s=this.snapshot,positions=r.positions;
  for(const [cell,uid] of s.locks?.pins||[])if((cells[cell]?.uid||null)!==uid)r.valid=false;
  for(const [from,to] of s.locks?.compass||[])if(positions.get(from)-6!==positions.get(to))r.valid=false;
  for(const lock of s.locks?.rows||[]){const row=Math.floor(positions.get(lock.uid)/6);if((lock.cycle?row%lock.cycle:row)!==lock.row)r.valid=false;}
  for(const lock of s.locks?.paper||[]){const c=positions.get(lock.uid);if(c===undefined||[...r.cats[c]].sort().join('|')!==[...lock.categories].sort().join('|'))r.valid=false;}
  // Native fixed mystic engravings are captured in ground. Their count must not
  // change during this request; a fresh F8 snapshot observes newly created ones.
  if((r.counts.MYSTIC||0)!==(s.nativeCounts?.MYSTIC||0))r.valid=false;
 }
 evaluate(cells=this.state.cells){
  const r=super.evaluate(cells);if(!this.skipInvalidCombat)this.checkLayoutConstraints(cells,r);
  const q=[0,0,0,0,0,0,r.total-r.breakdown.marks,-r.moves];
  cells.forEach((x,c)=>{if(!x)return;const d=this.defs.get(x.id);if(d.kind!=='artifact')return;
   const lv=r.level[c],effective=r.effective[c],active=r.active[c];
   if(x.mark===2){const deficit=Math.max(0,d.maxLevel-effective);q[0]+=+active;q[1]-=deficit;q[2]-=deficit*deficit;}
   if(x.mark===3){q[3]+=+active;q[6]-=Math.max(0,lv)*100;}
   if(x.mark===1)q[4]+=effective;if(x.mark===4)q[5]+=+(lv<0);
  });
  r.objective=q;r.total=q[6];return r;
 }
}
function prepare(snapshot,baseCatalog){
 if(snapshot.protocol!==1||snapshot.width!==6||snapshot.capacity<12||snapshot.capacity>60||snapshot.cells.length!==snapshot.capacity)throw Error('游戏快照格式或背包尺寸不支持');
 const catalog=structuredClone(baseCatalog),defs=new Map(catalog.items.map(d=>[d.id,d]));
 for(const patch of snapshot.definitions||[]){const d=defs.get(patch.id);if(!d||d.class!==patch.class)throw Error('物品类型未建模: '+patch.id);
  for(const key of ['rarity','maxLevel','unique','weapon','criteria','categories','attackable','stats','rotatable','curves','mechanics'])if(Object.hasOwn(patch,key))
   d[key]=key==='curves'||key==='mechanics'?{...d[key],...patch[key]}:structuredClone(patch[key]);
 }
 const seen=new Set(),unique=new Set();
 for(const x of snapshot.cells){if(!x)continue;const d=defs.get(x.id);if(!d||seen.has(x.uid))throw Error('未知物品或重复实例: '+x.id);seen.add(x.uid);
  if(d.unique&&unique.has(x.id))throw Error('重复唯一神器登记顺序尚不支持，保留原布局: '+d.name);if(d.unique)unique.add(x.id);
  if(!Number.isInteger(x.enchant)||!Number.isInteger(x.rotation)||x.rotation<0||x.rotation>3)throw Error('无效强化或旋转');
 }
 const detection=identify(snapshot,catalog),state=M.createState(snapshot.capacity);
 Object.assign(state,{cells:M.copy(snapshot.cells),weapon:snapshot.weapon.type,profile:detection.id,
  seed:snapshot.seed??20261002,mysticPositions:[],uniqueOrder:snapshot.uniqueOrder||snapshot.cells.filter(Boolean).map(x=>x.uid),
  scenario:{attackWeight:snapshot.weapon.attackWeight||1,autoMixed:!!detection.mixed},runtime:{patterns:snapshot.patterns||{},fixedPatterns:snapshot.fixedPatterns||[]}});
 let model=new RuntimeModel(catalog,state,snapshot),raw=model.evaluate();
 if(raw.recursive)throw Error('相邻白纸的递归连击尚不支持，保留原布局');
 state.runtime.countOffsets={};for(const key of new Set([...Object.keys(raw.counts),...Object.keys(snapshot.nativeCounts||{})]))state.runtime.countOffsets[key]=(snapshot.nativeCounts?.[key]||0)-(raw.counts[key]||0);
 for(let c=0;c<state.capacity;c++){
  const a=snapshot.matrices,mult=a.multiply[c]||1;
  if(a.level[c]%mult!==0)throw Error('原生等级倍率尚未同步');
  const residualMultiply=a.multiply[c]-raw.mult[c],disabled=a.disable[c]-raw.disableCount[c],ignore=a.ignore[c]-raw.ignoreCount[c];
  if(residualMultiply<0||disabled<0||ignore<0)throw Error('石板效果与原生矩阵不一致');
  state.ground[c]={level:a.level[c]/mult-raw.rawLevel[c],multiplier:residualMultiply,disabled:disabled>0,ignore:ignore>0};
 }
 model=new RuntimeModel(catalog,state,snapshot);
 // Native raw stats are calibrated as a residual, including external bonuses.
 // Acyclic stat conversions converge in a few passes. Reject unstable inputs.
 const calibrationWarnings=[],nativeStats={...snapshot.nativeStats};
 // 3.0.0 real captures used status IDs as raw dictionary keys. These fields
 // cannot be recovered from that snapshot; do not turn their spurious zeroes
 // into negative external bonuses. Synthetic/model snapshots are unaffected.
 if(!snapshot.nativeStatsSchema&&(snapshot.definitions||[]).some(d=>Object.hasOwn(d.mechanics||{},'IsEffectEnabled'))){
  const bad=['BASIC_ATTACK_DAMAGE','CRITICAL_DAMAGE_RATE','DASH_ATTACK_DAMAGE','DASH_RECOVERY_SPEED','DEFENSE','FINAL_DAMAGE','FINAL_HP','SPECIAL_ATTACK_DAMAGE'];
  for(const key of bad)delete nativeStats[key];
  calibrationWarnings.push('3.0.0 旧快照含错误原生属性字段；防御、全伤等采用已建模背包值，未知外部加成无法恢复。请用 3.0.1 重新采集。');
 }
 if(snapshot.nativeStats){state.runtime.statOffsets={};for(let pass=0;pass<6;pass++){
  const stats=model.evaluate().combat.rawStats;let error=0;
  for(const [key,value] of Object.entries(nativeStats)){if(!Number.isFinite(value))throw Error('无效原生属性');const delta=value-(stats[key]||0);error=Math.max(error,Math.abs(delta));state.runtime.statOffsets[key]=(state.runtime.statOffsets[key]||0)+delta;}
  if(error<1e-5)break;if(pass===5)throw Error('原生属性残差未收敛，保留原布局');
 }}
 state.runtime.statOffsetEntries=Object.entries(state.runtime.statOffsets||{}).filter(([,value])=>value!==0);
 const before=model.evaluate();
 if(!before.valid)throw Error('原布局约束校验失败');
 for(const e of snapshot.nativeEffects||[]){const c=state.cells.findIndex(x=>x?.uid===e.uid);if(c<0||before.level[c]!==e.level||before.active[c]!==e.active)throw Error('原生启用/等级与模型不一致: '+e.uid);}
 return {catalog,state,model,before,detection,calibrationWarnings};
}
function createCombatCache(model,limit=2048){
 // Compile the dependency key once per search. Combat consumes capped levels;
 // raw levels still participate in marks, harmony and the needle exception.
 const fixed=model.state.cells.filter(Boolean),indices=new Map(fixed.map((x,i)=>[x.uid,i]));
 const counts=new Map();for(const x of fixed)counts.set(x.id,(counts.get(x.id)||0)+1);
 const entries=fixed.map(x=>{const d=model.defs.get(x.id),kind=d.class;return {id:x.id,uid:x.uid,artifact:d.kind==='artifact',maximum:d.maxLevel,
  needle:kind==='Charm_UpCharmDamage',right:kind==='Charm_RightSpellCooldownHelper',left:kind==='Charm_ReduceMPCost',side:kind==='Charm_FireIce',row:kind==='Charm_3Elemental_ByRow',companion:/^Charm_(Summon|Companion)/.test(kind)&&kind!=='Charm_SummonGreenBat'};});
 const categoryKeys=[...new Set([...Object.keys(model.runtime.countOffsets||{}),...fixed.flatMap(x=>{const d=model.defs.get(x.id);return [...d.categories,...(d.curves.lineCategory||d.mechanics.lineCategory||[])];})])].sort();
 const duplicates=[...counts].filter(([,n])=>n>1).map(([id])=>entries.filter(x=>x.artifact&&x.id===id)).filter(x=>x.length>1);
 const cache=new Map(),stats={hits:0,misses:0},scenario=M.mechanics.scenario(model.profile,model.state.scenario),context=M.mechanics.compileContext(model.catalog,model.state,model.defs,model.profile);
 const aggregate=context.aggregate,staticSums=Array(aggregate?.size||0).fill(0);
 for(const entry of entries)entry.aggregate=aggregate?.items.get(entry.uid);
 const fifo=Array(Math.max(1,limit));let eviction=0;
 const numbers=[],codes=[],targets=[];let cursor=0,wide=false;
 const put=value=>{if(value!==Math.trunc(value)||value< -32768||value>32767)wide=true;numbers[cursor]=value;codes[cursor++]=value+32768;};
 const targetIndex=x=>x?indices.get(x.uid)+1:0;
 const materialize=(value,marks)=>({total:value.base+marks,breakdown:{...value.breakdown,marks},ownerUids:value.ownerUids,output:value.output,notes:[]});
 const evaluate=(cells,layout)=>{
  cursor=0;wide=false;let top=0;const combine=!!aggregate&&!!model.scoreOnly;put(+combine);if(combine)staticSums.fill(0);
  for(const item of entries){if(!item.artifact)continue;const c=layout.positions.get(item.uid);
   if(combine&&item.aggregate){if(layout.active[c]){const plan=item.aggregate,values=plan.levels[layout.effective[c]];for(let i=0;i<plan.terms.length;i++){const [index,curve]=plan.terms[i];staticSums[index]+=values?values[i]:M.mechanics.value(curve,layout.effective[c]);}}}
   else{put(item.needle?Math.max(0,Math.min(layout.level[c],item.maximum)):layout.effective[c]);put(+layout.active[c]);}
   if(item.needle)put(targetIndex(cells[M.mechanics.needleRoot(c,cells,model.defs,model.state.weapon)]));
   if(item.right)put(c%6<5?targetIndex(cells[c+1]):0);
   if(item.left)put(c%6>0?targetIndex(cells[c-1]):0);
   if(item.side)put(c%6<3?0:1);
   if(item.row)put(Math.floor(c/6)%4);
   if(item.companion)put(Math.floor(c/6));
   if(c<6)top++;
  }
  if(combine)for(const sum of staticSums)put(sum);
  // byId reads the last active copy in cell order, not the absolute positions
  // of every duplicate. Unique registration order remains fixed separately.
  for(const group of duplicates){let last=-1,winner=0;for(const item of group){const c=layout.positions.get(item.uid);if(layout.active[c]&&c>last){last=c;winner=indices.get(item.uid)+1;}}put(winner);}
  put(top);put(layout.harmonyTotal);targets.length=0;
  for(const c of layout.planetTargets)targets.push(targetIndex(cells[c]));targets.sort((a,b)=>a-b);
  put(targets.length);for(const index of targets)put(index);
  for(const category of categoryKeys)put(layout.counts[category]||0);
  numbers.length=cursor;codes.length=cursor;
  // A complete integer tuple, not a lossy hash. Unusual modded numeric values
  // take a separate exact JSON path instead of truncating to UTF-16 units.
  const key=wide?'j'+JSON.stringify(numbers):'p'+String.fromCharCode(...codes),cached=cache.get(key);
  if(cached&&(model.scoreOnly||cached.ownerUids)){stats.hits++;return materialize(cached,layout.breakdown.marks);}
  context.scoreOnly=!!model.scoreOnly;
  stats.misses++;const result=M.mechanics.evaluate(model.catalog,model.state,cells,layout,model.profile,model.defs,scenario,context);
  const b=result.breakdown,w=model.state.weights,score={base:b.level*w.level+b.combo*w.combo+b.synergy*w.synergy,breakdown:b,ownerUids:result.ownerUids,output:result.output};
  if(!cached){if(cache.size>=fifo.length)cache.delete(fifo[eviction]);fifo[eviction]=key;eviction=(eviction+1)%fifo.length;}cache.set(key,score);return materialize(score,layout.breakdown.marks);
 };
 return {evaluate,stats};
}
async function search(prepared,options={}){
 const model=prepared.model,standard=model.evaluate===RuntimeModel.prototype.evaluate,previous=model.combatEvaluator,previousScoreOnly=model.scoreOnly,previousSkip=model.skipInvalidCombat,cache=options.cache===false||!standard?null:createCombatCache(model),started=Date.now();let result;
 // An external evaluator may consume full diagnostics, even for rejected
 // layouts. Only the built-in evaluator opts into compact search results.
 try{if(cache){model.combatEvaluator=cache.evaluate;model.scoreOnly=true;}if(standard)model.skipInvalidCombat=true;result=await searchInner(prepared,options);}
 finally{model.combatEvaluator=previous;model.scoreOnly=previousScoreOnly;model.skipInvalidCombat=previousSkip;}
 // Public results always contain fresh cell-specific mechanics and diagnostics.
 result.after=model.evaluate(result.cells);result.cache=cache?.stats||{hits:0,misses:result.evaluations};
 // Custom objectives (including temporary resonance speed) keep their own
 // acceptance rules. Zero budget still means all eight normal search starts.
 if(standard&&options.freeGains!==false&&!previous&&!previousScoreOnly&&!result.cancelled)await refineFreeGains(prepared,result,options,started);
 result.searchMs=Date.now()-started;
 return result;
}
function translate(cells,group,target,width=6){
 const root=group[0],dx=target%width-root%width,dy=Math.floor(target/width)-Math.floor(root/width),to=[];
 for(const c of group){const x=c%width+dx,y=Math.floor(c/width)+dy,k=y*width+x;if(x<0||x>=width||y<0||k>=cells.length)return false;to.push(k);}
 const values=group.map(c=>cells[c]),holes=group.filter(c=>!to.includes(c)),displaced=to.filter(c=>!group.includes(c)).map(c=>cells[c]);
 holes.forEach((c,i)=>cells[c]=displaced[i]);to.forEach((c,i)=>cells[c]=values[i]);return true;
}
async function searchInner(prepared,{stepsPerStart=18000,starts=8,passes=64,budgetMs=0,cancelled=()=>false,onProgress=()=>{},proposal=null,proposalRate=.25,cache=true}={}){
 if(proposal!==null&&typeof proposal!=='function')throw Error('无效的候选生成器');
 if(!Number.isFinite(proposalRate)||proposalRate<0||proposalRate>1)throw Error('候选比例必须介于 0 与 1');
 const {state,model,before}=prepared,random=M.rng(state.seed),started=Date.now(),expired=()=>budgetMs>0&&Date.now()-started>=budgetMs;
 const pinned=new Set((model.snapshot.locks?.pins||[]).map(x=>x[0]));
 const free=state.cells.map((x,i)=>!x?.locked&&!pinned.has(i)?i:-1).filter(x=>x>=0);
 let best=M.copy(state.cells),bestResult=before,evaluations=0,completedStarts=0,proposalAttempts=0;
 const layouts=new Map(),tokens=new Map(state.cells.filter(Boolean).map((x,i)=>[x.uid,Array.from({length:4},(_,r)=>String.fromCharCode(33+4*i+r))])),layoutCache={hits:0,misses:0,unchanged:0};
 const layoutFifo=Array(4096);let layoutEviction=0;
 const impossible={valid:false,total:0,objective:Array(8).fill(0)},bindings=model.snapshot.locks?.compass||[];
 const test=cells=>{
  evaluations++;
  // A violated native binding cannot enter the search regardless of its score.
  // Reject it before allocating layout and combat diagnostics.
  if(bindings.some(([from,to])=>cells.findIndex(x=>x?.uid===from)-6!==cells.findIndex(x=>x?.uid===to)))return impossible;
  const key=cache?cells.map(x=>x?tokens.get(x.uid)[x.rotation]:' ').join(''):null;
  if(cache&&layouts.has(key)){layoutCache.hits++;return layouts.get(key);}
  layoutCache.misses++;const r=model.evaluate(cells),score={valid:r.valid,total:r.total,objective:r.objective};
  if(cache){if(layouts.size>=4096)layouts.delete(layoutFifo[layoutEviction]);layoutFifo[layoutEviction]=key;layoutEviction=(layoutEviction+1)&4095;layouts.set(key,score);}
  return score;
 };
 const sameSlot=(a,b)=>a===b||a?.uid===b?.uid&&a?.rotation===b?.rotation;
 const reuse=score=>{evaluations++;layoutCache.unchanged++;return score;};
 if(free.length<2)return {cells:best,before,after:bestResult,evaluations,completedStarts,proposalAttempts,budgetReached:false};
 for(let start=0;start<starts&&!expired()&&!cancelled();start++){
  let current=M.copy(start%2===0?state.cells:best),cur=test(current),loss=1;
  for(let i=0;i<stepsPerStart&&!expired()&&!cancelled();i++){
   // Instances are immutable in the built-in proposer; clone only a rotated
   // instance. External proposal callbacks retain the old fully-owned copy.
   const candidate=proposal?M.copy(current):current.slice(),a=free[Math.floor(random()*free.length)],b=free[Math.floor(random()*free.length)];let simple=!proposal;
   const proposed=proposal&&proposalRate>0&&random()<proposalRate&&proposal(candidate,a,free,random);
   if(proposed){proposalAttempts++;}
   else if(random()<.18){
    const x=candidate[a],d=x&&model.defs.get(x.id);let group=[];
    if(d?.class==='Charm_WhitePaper'&&a%6>0&&a%6<5)group=[a-1,a,a+1];
    else{let c=a;while(c>=6&&model.defs.get(candidate[c]?.id)?.class==='Charm_UpCharmDamage')c-=6;group=[c];while(c+6<candidate.length&&model.defs.get(candidate[c+6]?.id)?.class==='Charm_UpCharmDamage'){c+=6;group.push(c);}}
    if(group.length>1&&!group.some(c=>!free.includes(c))){translate(candidate,group,b);simple=false;}else [candidate[a],candidate[b]]=[candidate[b],candidate[a]];
   }else if(candidate[a]&&model.defs.get(candidate[a].id).rotatable&&random()<.25)candidate[a]={...candidate[a],rotation:Math.floor(random()*4)};
   else{[candidate[a],candidate[b]]=[candidate[b],candidate[a]];if(candidate[b]&&model.defs.get(candidate[b].id).rotatable&&random()<.35)candidate[b]={...candidate[b],rotation:Math.floor(random()*4)};}
   // Keep the proposal and RNG schedule intact, but an empty/empty swap or an
   // unchanged rotation already has exactly the current result. External
   // proposers and multi-cell group moves use the general validation path.
   const r=simple&&sameSlot(candidate[a],current[a])&&sameSlot(candidate[b],current[b])?reuse(cur):test(candidate);if(r.valid){if(compare(r,bestResult)>0){best=M.copy(candidate);bestResult=r;}
    const delta=compare(r,cur);if(delta<0)loss=.95*loss+.05*Math.min(100,-delta);
    const temperature=Math.max(.001,loss*Math.pow(1-i/stepsPerStart,3));
    if(delta>=0||random()<Math.exp(delta/temperature)){current=candidate;cur=r;}
   }
   if(i%256===0){onProgress({start,iteration:i,evaluations,best:bestResult.total});await yieldSearch();}
  }
  if(!expired()&&!cancelled())completedStarts++;
 }
 for(let pass=0;pass<passes&&!expired()&&!cancelled();pass++){
  let improved=false;
  outer:for(const a of free)for(const b of free){if(expired()||cancelled())break outer;if(!best[a])continue;
   const rotations=model.defs.get(best[a].id).rotatable?4:1;
   for(let rotation=0;rotation<rotations;rotation++){
    const candidate=best.slice();[candidate[a],candidate[b]]=[candidate[b],candidate[a]];if(rotations===4)candidate[b]={...candidate[b],rotation};
    const r=sameSlot(candidate[a],best[a])&&sameSlot(candidate[b],best[b])?reuse(bestResult):test(candidate);if(r.valid&&compare(r,bestResult)>0){best=candidate;bestResult=r;improved=true;break outer;}
   }
  }if(!improved)break;await yieldSearch();
 }
 return {cells:best,before,after:bestResult,evaluations,completedStarts,proposalAttempts,layoutCache,budgetReached:expired(),cancelled:cancelled()};
}
const api={identify,prepare,search,compare,comparePrimary,acceptsResult,freeGainProof,dominatesFreeGain,RuntimeModel,translate,createCombatCache};
if(typeof module!=='undefined'&&module.exports)module.exports=api;else root.RuntimeBackpackModel=api;
})(typeof window!=='undefined'?window:globalThis);
