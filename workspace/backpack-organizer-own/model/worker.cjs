'use strict';
const R=require('./runtime-model.js'),resonance=require('./resonance-model.js'),catalog=require('./data/catalog.json');
let input='';process.stdin.setEncoding('utf8');
process.stdin.on('data',text=>{input+=text;if(input.length>16*1024*1024){process.stderr.write('Snapshot too large');process.exit(2);}});
process.stdin.on('end',async()=>{try{
 const started=performance.now(),snapshot=JSON.parse(input),prepared=R.prepare(snapshot,catalog),prepareMs=performance.now()-started;
 const phaseStarted=performance.now(),activation=await resonance.plan(prepared),activationMs=performance.now()-phaseStarted;
 const result=activation.needed?activation.result:await R.search(prepared,{budgetMs:Math.max(0,snapshot.budgetMs||0)});
 const compact=r=>({total:r.total,objective:r.objective,level:r.level,active:r.active,counts:r.counts,freeGain:r.freeGain,
  output:r.combat.output,channels:r.combat.channels.map(c=>({key:c.key,value:c.value})),
  stats:Object.fromEntries(['DEFENSE','FINAL_DAMAGE','CRITICAL_DAMAGE_RATE','MP_REGEN','MP_STEAL'].map(k=>[k,r.combat.rawStats[k]||0])),
  resource:{maxMP:r.combat.maxMP,demand:r.combat.mpDemand,supply:r.combat.mpSupply,uptime:r.combat.resource,leech:r.combat.metrics.mpLeechPerSecond||0},
  unsupported:r.combat.unsupported,warnings:r.combat.warnings});
 process.stdout.write(JSON.stringify({protocol:1,ok:true,requestId:snapshot.requestId,
  detection:prepared.detection,phase:activation.needed?'resonance':'normal',resonance:{...activation,result:undefined},cells:result.cells,before:compact(result.before),after:compact(result.after),
  evaluations:result.evaluations,completedStarts:result.completedStarts,budgetReached:result.budgetReached,freeGains:result.freeGains,
  timings:{prepareMs:Math.round(prepareMs),activationMs:Math.round(activationMs),searchMs:result.searchMs||0,workerMs:Math.round(performance.now()-started)},
  cache:{layout:result.layoutCache,combat:result.cache},calibrationWarnings:prepared.calibrationWarnings,ignoredItems:prepared.ignoredItems}));
}catch(e){process.stdout.write(JSON.stringify({protocol:1,ok:false,error:e.message}));process.exitCode=1;}});
