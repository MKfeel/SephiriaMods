'use strict';
const R=require('./runtime-model.js'),M=require('./model.js');
function movement(prepared,cells,layout){
 let value=prepared.state.runtime.statOffsets?.MOVE_SPEED||0;
 const owners=new Set();
 for(const uid of prepared.state.uniqueOrder){const c=cells.findIndex(x=>x?.uid===uid);if(c<0||!layout.active[c])continue;const d=prepared.model.defs.get(cells[c].id);if(d.unique&&owners.has(d.id))continue;owners.add(d.id);
  for(const stat of d.stats||[])if(stat.statusID==='MOVE_SPEED')value+=M.mechanics.value(stat.valuesByLevel,layout.effective[c]);
 }
 for(const cat of prepared.catalog.categories)for(const tier of cat.combo.addStatByCombo||[])if((layout.counts[cat.id]||0)>=tier.comboCount)for(const token of tier.status){const [key,v]=token.split('/');if(key==='MOVE_SPEED')value+=Number(v)||0;}
 return 100+value+(prepared.model.snapshot.movement?.slowPercent||0);
}
async function plan(prepared){
 const snapshot=prepared.model.snapshot,quest=snapshot.resonance;
 if(!quest||snapshot.skipResonance)return {needed:false,reason:snapshot.skipResonance?'already-attempted':'no-quest'};
 if(!Number.isFinite(snapshot.nativeStats?.MOVE_SPEED)||!Number.isFinite(snapshot.movement?.slowPercent))return {needed:false,reason:'missing-native-movement'};
 const {model,state}=prepared,threshold=quest.threshold,tolerance=.001;
 if(!Number.isFinite(threshold)||threshold<=0||!state.cells.some(x=>x?.uid===quest.uid))throw Error('共鸣石成长快照无效');
 let upper=100+(state.runtime.statOffsets?.MOVE_SPEED||0)+snapshot.movement.slowPercent;
 for(const x of state.cells)if(x)for(const stat of model.defs.get(x.id).stats||[])if(stat.statusID==='MOVE_SPEED')upper+=Math.max(0,...stat.valuesByLevel);
 for(const cat of prepared.catalog.categories)for(const tier of cat.combo.addStatByCombo||[])for(const token of tier.status){const [key,v]=token.split('/');if(key==='MOVE_SPEED')upper+=Math.max(0,Number(v)||0);}
 if(upper<threshold-tolerance)return {needed:false,reason:'upper-bound-below-threshold',upper,threshold};
 const originalCombat=model.combatEvaluator,originalScoreOnly=model.scoreOnly;
 const evaluateMovement=(cells,layout)=>{const c=cells.findIndex(x=>x?.uid===quest.uid),speed=movement(prepared,cells,layout),value=layout.active[c]?Math.min(threshold,speed):0;return {total:value+layout.breakdown.marks,breakdown:{marks:layout.breakdown.marks},notes:[],speed};};
 let out;
 try{
  model.combatEvaluator=evaluateMovement;model.scoreOnly=true;
  const before=model.evaluate(),originalStone=state.cells.findIndex(x=>x?.uid===quest.uid),already=before.active[originalStone]&&before.combat.speed>=threshold-tolerance;
  let reached=already;
  out=already?{cells:M.copy(state.cells),evaluations:0,completedStarts:0,budgetReached:false}:await R.search({...prepared,before},{cache:false,cancelled:()=>reached,onProgress:p=>{if(p.best>=threshold-tolerance)reached=true;}});
 }finally{model.combatEvaluator=originalCombat;model.scoreOnly=originalScoreOnly;}
 const before=model.evaluate(),after=model.evaluate(out.cells),c=out.cells.findIndex(x=>x?.uid===quest.uid),speed=movement(prepared,out.cells,after);
 if(!after.valid||!after.active[c]||speed<threshold-tolerance)return {needed:false,reason:'no-feasible-layout-found',upper,threshold,evaluations:out.evaluations};
 // Full diagnostics are recomputed above. Only the temporary phase objective
 // uses movement; the final normal search uses the unchanged build objective.
 for(const [r,cells] of [[before,state.cells],[after,out.cells]]){const at=cells.findIndex(x=>x?.uid===quest.uid);r.total=r.active[at]?Math.min(threshold,movement(prepared,cells,r)):0;for(let c=0;c<cells.length;c++)if(cells[c]?.mark===3)r.total-=Math.max(0,r.level[c])*100;r.objective[6]=r.total;}
 if(R.compare(after,before)<0)return {needed:false,reason:'explicit-marks-prevent-activation',upper,threshold};
 return {needed:true,reason:'feasible',quest,predictedSpeed:speed,threshold,result:{...out,before,after}};
}
module.exports={plan,movement};
