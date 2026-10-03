(function(root) {
'use strict';
const nativeQuery = typeof module !== 'undefined' && module.exports ? require('./native-query.js') : root.NativeTabletQuery;
const builds = typeof module !== 'undefined' && module.exports ? require('./builds.js') : root.SephiriaBuilds;
const mechanics = typeof module !== 'undefined' && module.exports ? require('./mechanics.js') : root.BackpackMechanics;
const clamp=(n,a,b)=>Math.max(a,Math.min(b,n));
const copy=cells=>cells.map(x=>x?{...x}:null);
const at=(x,y,n,w=6)=>x<0||x>=w||y<0||y*w+x>=n?-1:y*w+x;
const near8=[[-1,0],[1,0],[0,-1],[0,1],[-1,-1],[1,-1],[-1,1],[1,1]];
const guide=(category,build)=>'https://sephiriadfm05.streamlit.app/build_analysis?category='+encodeURIComponent(category)+'&build='+encodeURIComponent(build);
const profiles=builds.profiles;
function rng(seed){let a=seed>>>0;return()=>{a+=0x6D2B79F5;let t=a;t=Math.imul(t^t>>>15,t|1);t^=t+Math.imul(t^t>>>7,t|61);return((t^t>>>14)>>>0)/4294967296;};}
function createState(capacity=36){return {schemaVersion:1,modelVersion:'2.0',width:6,capacity,profile:'ember',weapon:null,seed:20261002,cells:Array(capacity).fill(null),ground:Array.from({length:capacity},()=>({level:0,multiplier:0,disabled:false,ignore:false})),mysticPositions:[12,21,28].filter(x=>x<capacity),weights:{level:1,combo:1,synergy:1},scenario:{},preservePaper:true};}
function validateState(input,catalog){
 if(!input||input.schemaVersion!==1)throw Error('不是支持的背包方案（需要 schemaVersion 1）。');
 const byId=new Map(catalog.items.map(x=>[x.id,x]));
 if(input.width!==6||!Number.isInteger(input.capacity)||input.capacity<12||input.capacity>60)throw Error('背包必须为 6 列、12–60 格。');
 if(!Array.isArray(input.cells)||input.cells.length!==input.capacity)throw Error('格位数量与容量不一致。');
 const s=createState(input.capacity),seen=new Set();
 s.profile=profiles[input.profile]?input.profile:'balanced';s.seed=Number.isInteger(input.seed)?input.seed>>>0:20261002;
 s.weapon=input.weapon==null?null:clamp(Math.trunc(Number(input.weapon)||0),0,20);
 s.preservePaper=input.preservePaper!==false;
 s.scenario=mechanics.validateScenario(input.scenario);
 s.cells=input.cells.map(x=>{
  if(x===null)return null;
  if(!x||!byId.has(x.id)||typeof x.uid!=='string'||!x.uid||x.uid.length>100||seen.has(x.uid))throw Error('方案中存在未知物品、重复实例或无效实例编号。');
  seen.add(x.uid);const d=byId.get(x.id);
  if(!Number.isInteger(x.rotation)||x.rotation<0||x.rotation>3||(!d.rotatable&&x.rotation!==0))throw Error('方案含有不允许的石板旋转。');
  if(!Number.isInteger(x.enchant)||x.enchant< -9||x.enchant>9)throw Error('强化等级必须是 -9 到 9 的整数。');
  return {uid:x.uid,id:x.id,rotation:x.rotation,enchant:x.enchant,locked:!!x.locked,mark:clamp(Math.trunc(Number(x.mark)||0),0,4)};
 });
 if(input.uniqueOrder!==undefined&&(!Array.isArray(input.uniqueOrder)||input.uniqueOrder.some(x=>typeof x!=='string')||new Set(input.uniqueOrder).size!==input.uniqueOrder.length))throw Error('唯一效果登记顺序无效。');
 s.uniqueOrder=[...(input.uniqueOrder||[]).filter(uid=>seen.has(uid))];
 for(const x of s.cells)if(x&&!s.uniqueOrder.includes(x.uid))s.uniqueOrder.push(x.uid);
 if(input.ground!==undefined){
  if(!Array.isArray(input.ground)||input.ground.length!==s.capacity)throw Error('地块配置数量与容量不一致。');
  s.ground=input.ground.map(g=>{
   if(!g||!Number.isInteger(g.level)||Math.abs(g.level)>20||!Number.isInteger(g.multiplier)||g.multiplier<0||g.multiplier>10)throw Error('地块等级或倍率超出范围。');
   return {level:g.level,multiplier:g.multiplier,disabled:!!g.disabled,ignore:!!g.ignore};
  });
 }
 if(input.mysticPositions!==undefined){
  if(!Array.isArray(input.mysticPositions)||input.mysticPositions.length>3||input.mysticPositions.some(c=>!Number.isInteger(c)||c<0||c>=s.capacity)||new Set(input.mysticPositions).size!==input.mysticPositions.length)throw Error('神秘地块需要最多 3 个不同的有效格位。');
  s.mysticPositions=[...input.mysticPositions];
 }
 for(const key of ['level','combo','synergy'])if(input.weights&&input.weights[key]!==undefined){const v=Number(input.weights[key]);if(!Number.isFinite(v)||v<0||v>5)throw Error('评分权重需要在 0–5 之间。');s.weights[key]=v;}
 return s;
}
function makePreset(id,catalog){
 const s=createState(),available=new Set(catalog.items.map(x=>x.id));s.profile=profiles[id]?id:'balanced';
 const p=profiles[s.profile];s.weapon=p.weapon??null;
 const extra={burn:[1213,1211,1102,1100,1144,1185,1214,1128],freeze:[1145,1177,1180,1212,1176,1178,1150,1144,1139,1268],planet:[1108,1241,1220,1106,1107,1105,1242,1251,1295,1128],companion:[1029,1198,1199,1200,1202,1249,1090,1130,1264],cloud:[1035,1072,1104,1129,1055,1245,1263,1264],magic:[1168,1165,1166,1075,1076,1069,1040,1124,1004],sun:[1236,1229,1240,1232,1233,1230,1193,1185],frost:[1179,1210,1209,1144,1002,1247,1177],weapon:[1000,1262,1221,1011,1080,1025,1155,1274,1173],balanced:[1213,1211,1108,1241,1220,1251,1145,1180,1144,1176]};
 const group=extra[p.kind]||extra[p.kind.split('-').at(-1)]||extra.weapon;
 const core=Object.keys(p.core).map(Number),support=p.kind==='companion-cloud'?extra.companion:p.kind==='plasma'?[1128,1213,1238,1100,1272,1144]:[];
 if(p.bookSun)core.push(3002);if(p.fusion&&p.element==='TRI')core.push(3011,3027);
 const ids=[...new Set([...core,...support,...group,1270,1030,1258,1004,1040])].filter(i=>available.has(i)).slice(0,18);
 if(!ids.includes(1270))ids[ids.length-1]=1270;
 ids.push(...[2003,2004,2005,2017,2028,2035,2042,2051,2054,2008,2056]);
 ids.forEach((v,i)=>s.cells[i]={uid:'demo-'+i,id:v,rotation:0,enchant:0,locked:false,mark:0});
 const random=rng(s.seed);for(let i=s.cells.length-1;i>0;i--){const j=Math.floor(random()*(i+1));[s.cells[i],s.cells[j]]=[s.cells[j],s.cells[i]];}
 s.uniqueOrder=s.cells.filter(Boolean).map(x=>x.uid);return s;
}
function categoriesFor(cells,defs,weapon=null){
 const cats=Array(cells.length),visiting=new Set();let recursive=false;
 function get(c){
  if(c<0||c>=cells.length||!cells[c])return [];
  if(cats[c])return cats[c];if(visiting.has(c)){recursive=true;return [];}
  visiting.add(c);const d=defs.get(cells[c].id);let list=[];
  if(d.kind==='artifact'){
   if(d.class==='Charm_WhitePaper'){const l=at(c%6-1,Math.floor(c/6),cells.length),r=at(c%6+1,Math.floor(c/6),cells.length),left=get(l),right=get(r);list=left.filter(v=>right.includes(v));}
   else if(d.class==='Charm_UpCharmDamage'){const target=mechanics.needleRoot(c,cells,defs,weapon);list=target>=0?get(target):[];}
   else if(d.class==='Charm_3Elemental_ByRow'){const a=d.curves.lineCategory||d.mechanics.lineCategory;list=[a[Math.floor(c/6)%a.length]];}
   else list=d.categories;
  }
  visiting.delete(c);cats[c]=list;return list;
 }
 for(let i=0;i<cells.length;i++)get(i);
 return {cats:cats.map(a=>a||[]),recursive};
}
function activation(d,c,cells,defs){
 const x=c%6,y=Math.floor(c/6),n=cells.length;
 switch(d.criteria){
  case '':return true;
  case 'CharmActivateCriteria_TopInInventory':return y===0;
  case 'CharmActivateCriteria_BottomInInventory':return c>=n-6;
  case 'CharmActivateCriteria_Outlined':return x===0||x===5||y===0||c>=n-6;
  case 'CharmActivateCriteria_Inside':return x>0&&x<5&&y>0&&c+7<n;
  case 'CharmActivateCriteria_BothSidesAreEmpty':return x>0&&x<5&&c+1<n&&!cells[c-1]&&!cells[c+1];
  case 'CharmActivateCriteria_BothSideCharm':return x>0&&x<5&&!!cells[c-1]&&!!cells[c+1]&&defs.get(cells[c-1].id).kind==='artifact'&&defs.get(cells[c+1].id).kind==='artifact';
  default:return false;
 }
}
function pattern(d,c,r,n){
 const convert=q=>nativeQuery(q||'',6,Math.ceil(n/6),n,{x:c%6,y:Math.floor(c/6)},r).map(e=>({cell:at(e.position.x,e.position.y,n),value:e.value}));
 // Conditions outside the inventory remain unsatisfied, matching FindItem in the game.
 return {effects:convert(d.query).filter(e=>e.cell>=0),conditions:convert(d.condition)};
}
function applies(p,c,cells,defs){let placed=false,hit=false;
 for(const e of p.conditions){const x=e.cell>=0?cells[e.cell]:null;
  if(e.value==='PLACED'){placed=true;hit=hit||e.cell===c;}
  if(e.value==='ITEM'&&!x)return false;
  if(e.value==='CHARM'&&(!x||defs.get(x.id).kind!=='artifact'))return false;
 }return !placed||hit;
}
class Model{
 constructor(catalog,state){this.catalog=catalog;this.state=state;this.runtime=state.runtime||{};this.defs=new Map(catalog.items.map(d=>[d.id,d]));this.profile=profiles[state.profile]||profiles.balanced;this.patterns=new Map();this.original=copy(state.cells);
  for(const inst of state.cells){if(!inst)continue;const d=this.defs.get(inst.id);if(d.kind!=='tablet'||this.patterns.has(d.id))continue;this.patterns.set(d.id,Array.from({length:state.capacity},(_,c)=>Array.from({length:4},(_,r)=>pattern(d,c,r,state.capacity))));}
  const base=categoriesFor(state.cells,this.defs,state.weapon);this.paperTargets=new Map();
  if(state.preservePaper)state.cells.forEach((x,c)=>{if(x&&this.defs.get(x.id).class==='Charm_WhitePaper'&&base.cats[c].length)this.paperTargets.set(x.uid,[...base.cats[c]].sort().join('|'));});
  this.recursive=base.recursive;this.fastPatterns=new WeakMap();this.countOffsetEntries=Object.entries(this.runtime.countOffsets||{}).filter(([,v])=>v!==0);
  this.baseGround={level:state.ground.map(g=>g.level),mult:state.ground.map(g=>g.multiplier),disabled:state.ground.map(g=>g.disabled),ignore:state.ground.map(g=>g.ignore)};
  this.mysticCombo=catalog.categories.find(c=>c.id==='MYSTIC').combo;
  this.categoryPlan=new Map();this.staticCounts={};const uniques=new Set();this.distinctUnique=true;
  for(const x of state.cells){if(!x)continue;const d=this.defs.get(x.id),mode=d.kind!=='artifact'?0:d.class==='Charm_WhitePaper'?1:d.class==='Charm_UpCharmDamage'?2:d.class==='Charm_3Elemental_ByRow'?3:0;
   const cats=d.kind==='artifact'?d.categories:[];this.categoryPlan.set(x.id,{mode,cats,rows:d.curves.lineCategory||d.mechanics.lineCategory});
   if(!mode)for(const category of cats)this.staticCounts[category]=(this.staticCounts[category]||0)+1;
   if(d.unique){if(uniques.has(d.id))this.distinctUnique=false;uniques.add(d.id);}
  }
 }
 fastCategories(cells){
  // Most categories depend only on the item set. Recompute only white paper,
  // needle targets and row-dependent categories, including recursive failures.
  const cats=Array(cells.length),dynamic=[],counts={...this.staticCounts},visiting=new Set(),empty=[];let recursive=false;
  for(let c=0;c<cells.length;c++){const x=cells[c];if(!x){cats[c]=empty;continue;}const entry=this.categoryPlan.get(x.id);if(entry.mode)dynamic.push(c);else cats[c]=entry.cats;}
  const get=c=>{if(c<0||c>=cells.length)return empty;if(cats[c])return cats[c];if(visiting.has(c)){recursive=true;return empty;}
   visiting.add(c);const entry=this.categoryPlan.get(cells[c].id);let list;
   if(entry.mode===1){const left=get(at(c%6-1,Math.floor(c/6),cells.length)),right=get(at(c%6+1,Math.floor(c/6),cells.length));list=left.filter(x=>right.includes(x));}
   else if(entry.mode===2)list=get(mechanics.needleRoot(c,cells,this.defs,this.state.weapon));
   else list=[entry.rows[Math.floor(c/6)%entry.rows.length]];
   visiting.delete(c);cats[c]=list;return list;
  };
  for(const c of dynamic)for(const category of get(c))counts[category]=(counts[category]||0)+1;
  return {cats,counts,recursive};
 }
 evaluate(cells=this.state.cells){
  const n=cells.length,defs=this.defs,s=this.state,p=this.profile,details=!this.scoreOnly;
  const level=details?s.ground.map(g=>g.level):this.baseGround.level.slice(),mult=details?s.ground.map(g=>g.multiplier):this.baseGround.mult.slice(),disabled=details?s.ground.map(g=>g.disabled):this.baseGround.disabled.slice(),ignore=details?s.ground.map(g=>g.ignore):this.baseGround.ignore.slice();
  const topology=details?categoriesFor(cells,defs,s.weapon):this.fastCategories(cells),{cats,recursive}=topology,counts=topology.counts||{},tabletActive=Array(n).fill(false),sources=details?Array.from({length:n},()=>[]):[];
  if(details)cats.forEach(a=>a.forEach(k=>counts[k]=(counts[k]||0)+1));
  for(const [k,v] of (details?Object.entries(this.runtime.countOffsets||{}):this.countOffsetEntries))counts[k]=(counts[k]||0)+v;
  const mystic=this.mysticCombo;
  const mysticCount=((counts.MYSTIC||0)>=mystic.first?mystic.firstEngravingCount:0)+((counts.MYSTIC||0)>=mystic.second?mystic.secondEngravingCount:0);
  s.mysticPositions.slice(0,mysticCount).forEach(c=>{mult[c]+=2;if(details)sources[c].push('神秘地块 ×2');});
  const disableCount=disabled.map(Number),ignoreCount=ignore.map(Number);
  const apply=(pat,c,name)=>{if(!applies(pat,c,cells,defs))return false;
   if(!details){let effects=this.fastPatterns.get(pat);if(!effects){effects=pat.effects.map(e=>{const v=String(e.value);return [e.cell,/^[+-]?\d+$/.test(v)?0:v==='X'?1:v==='IGNORECRITERIA'?2:v.startsWith('MUL/')?3:4,Number(v.startsWith('MUL/')?v.slice(4):v)];});this.fastPatterns.set(pat,effects);}
    for(const [k,kind,amount] of effects){if(k<0||k>=n)continue;if(kind===0)level[k]+=amount;else if(kind===1){disabled[k]=true;disableCount[k]++;}else if(kind===2){ignore[k]=true;ignoreCount[k]++;}else if(kind===3)mult[k]+=amount;}return true;}
   for(const e of pat.effects){const k=e.cell,v=String(e.value);if(k<0||k>=n)continue;if(/^[+-]?\d+$/.test(v))level[k]+=Number(v);else if(v==='X'){disabled[k]=true;disableCount[k]++;}else if(v==='IGNORECRITERIA'){ignore[k]=true;ignoreCount[k]++;}else if(v.startsWith('MUL/'))mult[k]+=Number(v.slice(4));sources[k].push(name+' '+v);}return true;};
  cells.forEach((x,c)=>{if(!x)return;const d=defs.get(x.id);if(d.kind==='artifact'){level[c]+=x.enchant;return;}if(d.kind!=='tablet')return;
   const pat=this.runtime.patterns?.[x.uid]?.[c]?.[x.rotation]||this.patterns.get(d.id)[c][x.rotation];tabletActive[c]=apply(pat,c,d.name);
  });
  for(const fixed of this.runtime.fixedPatterns||[])apply(fixed.pattern,fixed.cell,'固定铭刻');
  const rawLevel=details?[...level]:[];
  const active=Array(n).fill(false),effective=Array(n).fill(0),reasons=details?Array(n).fill(''):[],byUid=new Map();
  cells.forEach((x,c)=>{level[c]*=mult[c]||1;if(!x)return;byUid.set(x.uid,c);const d=defs.get(x.id);if(d.kind!=='artifact')return;
   const weaponOk=s.weapon==null||d.weapon==null||d.weapon===s.weapon;
   active[c]=!disabled[c]&&level[c]>=0&&weaponOk&&!!(ignore[c]||activation(d,c,cells,defs));
   if(details)reasons[c]=disabled[c]?'石板禁用':level[c]<0?'等级低于 0':!weaponOk?'武器类型不符':!active[c]?'摆放条件未满足':'';
   effective[c]=active[c]?clamp(level[c],0,d.maxLevel):0;
  });
  const uniqueBest=!details&&this.distinctUnique?null:new Map();if(uniqueBest)for(const uid of s.uniqueOrder||this.original.filter(Boolean).map(x=>x.uid)){const c=byUid.get(uid),x=cells[c];if(x&&active[c]&&defs.get(x.id).unique&&!uniqueBest.has(x.id))uniqueBest.set(x.id,c);}
  const breakdown={level:0,combo:0,synergy:0,marks:0},itemScores=Array(n).fill(0),harmony=Array(n).fill(0),planetTargets=new Set(),synergyNotes=[];
  let full=0,totalLevels=0,activeCount=0,moves=0,valid=!recursive;
  cells.forEach((x,c)=>{if(!x)return;const d=defs.get(x.id);if(d.kind!=='artifact')return;
   if(active[c]){activeCount++;totalLevels+=effective[c];if(effective[c]>=d.maxLevel)full++;}
   const uniqueEffective=!d.unique||uniqueBest===null||uniqueBest.get(d.id)===c;
   if(x.mark===1)breakdown.marks+=active[c]?effective[c]*65+30:0;
   if(x.mark===2)breakdown.marks+=active[c]?effective[c]*100+100+(effective[c]===d.maxLevel?150:0):0;
   if(x.mark===3)breakdown.marks+=active[c]?160-Math.abs(level[c])*55:-150;
   if(x.mark===4)breakdown.marks+=level[c]<0?220: -level[c]*50;
   itemScores[c]=active[c]&&uniqueEffective?1:0;
   if(!active[c]||!uniqueEffective)return;
   if(d.class==='Charm_NearLevelDamage'){
    const coefficient=d.curves.allDamageBonusByLevel[effective[c]];let sum=0;
    for(const [dx,dy] of near8){const k=at(c%6+dx,Math.floor(c/6)+dy,n);if(k>=0&&cells[k]&&defs.get(cells[k].id).kind==='artifact')sum=Math.fround(sum+Math.fround(coefficient*Math.min(level[k],defs.get(cells[k].id).maxLevel)));}
    harmony[c]=Math.floor(sum);
   }
   if(d.class==='Charm_PlanetModule')for(const [dx,dy] of near8){const k=at(c%6+dx,Math.floor(c/6)+dy,n);if(k>=0&&cells[k]&&active[k]&&defs.get(cells[k].id).class==='Charm_SummonGreenBat')planetTargets.add(k);}
   if(details&&d.class==='Charm_WoodenBox'){const count=cells.slice(0,6).filter(z=>z&&defs.get(z.id).kind==='artifact').length;synergyNotes.push('腰带首排 '+count+'/6');}
  });
  if(details&&planetTargets.size)synergyNotes.push('望远镜覆盖 '+planetTargets.size+' 颗启用星球');
  const harmonyTotal=harmony.reduce((a,b)=>a+b,0);if(details&&harmony.some(x=>x!==0))synergyNotes.push('和谐之晶合计 '+harmonyTotal+'%');
  for(const [uid,target] of this.paperTargets){const pos=byUid.get(uid);if(pos===undefined||[...cats[pos]].sort().join('|')!==target)valid=false;}
  this.original.forEach((x,c)=>{if(!x)return;const pos=byUid.get(x.uid);if(pos!==c||cells[pos]?.rotation!==x.rotation)moves++;if(x.locked&&(pos!==c||cells[pos]?.rotation!==x.rotation))valid=false;});
  const result={positions:byUid,breakdown,level,rawLevel,mult,disabled,ignore,disableCount,ignoreCount,active,effective,reasons,counts,cats,tabletActive,sources,itemScores,harmony,harmonyTotal,planetTargets:[...planetTargets],synergyNotes,activeCount,full,totalLevels,moves,valid,recursive,mysticCount};
  // Invalid proposals never participate in acceptance or annealing. Search
  // can reject them before combat; ordinary evaluations retain diagnostics.
  if(this.skipInvalidCombat){if(this.checkLayoutConstraints)this.checkLayoutConstraints(cells,result);if(!result.valid)return {...result,total:0,combat:{total:0,breakdown,notes:[]}};}
  const combat=this.combatEvaluator?this.combatEvaluator(cells,result):mechanics.evaluate(this.catalog,s,cells,result,p,defs);
  return {...result,total:combat.total,breakdown:combat.breakdown,combat,synergyNotes:[...synergyNotes,...combat.notes]};
 }
}
async function optimize(catalog,state,{steps=18000,starts=4,search='hybrid',onProgress=()=>{},cancelled=()=>false}={}){
 const model=new Model(catalog,state);if(model.recursive)throw Error('相邻白纸的递归复制尚未建模，请先将白纸隔开。');
 const original=copy(state.cells),before=model.evaluate(original),free=original.map((x,i)=>!x?.locked?i:-1).filter(i=>i>=0);
 let best=copy(original),bestResult=before,evaluations=0;const random=rng(state.seed);
 const better=(a,b)=>a.valid&&(a.total>b.total+1e-7||(Math.abs(a.total-b.total)<1e-7&&a.moves<b.moves));
 const per=Math.max(1,Math.floor(steps/starts));
 for(let start=0;start<starts;start++){
  let current=copy(start===0?original:best),cur=model.evaluate(current);
  for(let i=0;i<per;i++){
   if(cancelled())return {cancelled:true,before,cells:original,evaluations};
   if(free.length===0)break;
   let a=free[Math.floor(random()*free.length)],b=free[Math.floor(random()*free.length)],third=-1,oldThird;
   // Alternate local random moves with level-targeted swaps and 3-cycles.
   // The directed proposals never bypass legality or use a different objective.
   if(search==='hybrid'&&random()<.35){
    const needy=free.filter(k=>current[k]&&model.defs.get(current[k].id).kind==='artifact'&&(!cur.active[k]||cur.effective[k]<Math.min(model.defs.get(current[k].id).maxLevel,model.profile.core[current[k].id]??1)));
    if(needy.length){a=needy[Math.floor(random()*needy.length)];const ranked=free.filter(k=>k!==a).sort((x,y)=>cur.level[y]-cur.level[x]);if(ranked.length)b=ranked[Math.floor(random()*Math.min(8,ranked.length))];}
   }
   const oldA=current[a],oldB=current[b];
   if(random()<0.25&&oldA&&model.defs.get(oldA.id).rotatable)current[a]={...oldA,rotation:Math.floor(random()*4)};
   else {
    if(search==='hybrid'&&a!==b&&free.length>2&&random()<.22){const options=free.filter(k=>k!==a&&k!==b);third=options[Math.floor(random()*options.length)];oldThird=current[third];[current[a],current[b],current[third]]=[current[third],current[a],current[b]];}
    else [current[a],current[b]]=[current[b],current[a]];
    if(current[a]&&model.defs.get(current[a].id).rotatable&&random()<0.5)current[a]={...current[a],rotation:Math.floor(random()*4)};
   }
   const result=model.evaluate(current);evaluations++;
   const temperature=(1-i/per)*(start===0?18:35)+0.03;
   if(result.valid&&(better(result,cur)||random()<Math.exp((result.total-cur.total)/temperature)))cur=result;
   else {current[a]=oldA;current[b]=oldB;if(third>=0)current[third]=oldThird;}
   if(better(cur,bestResult)){best=copy(current);bestResult=cur;}
   if(i%180===0){onProgress({progress:(start*per+i)/(starts*per),best:bestResult.total,evaluations});await new Promise(r=>setTimeout(r,0));}
  }
 }
 // Final best is never worse than the user's input. The UI applies it explicitly.
 onProgress({progress:1,best:bestResult.total,evaluations});
 return {cancelled:false,cells:best,before,after:model.evaluate(best),evaluations,seed:state.seed};
}
const api={profiles,groups:builds.groups,mechanics,createState,validateState,makePreset,Model,optimize,pattern,activation,categoriesFor,rng,copy};
if(typeof module!=='undefined'&&module.exports)module.exports=api;else root.BackpackModel=api;
})(typeof window!=='undefined'?window:globalThis);
