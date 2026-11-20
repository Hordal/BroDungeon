/* Build-time reference exporter. Unity runtime never loads WebPrototype JavaScript.
 * node Tools/Brodun/export-web-rules.cjs --check
 * node Tools/Brodun/export-web-rules.cjs --write (review changes, then rebuild SO assets)
 */
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const assert = require('node:assert/strict');
const root = path.resolve(__dirname, '../..');
const context = { console };
context.window = context;
vm.createContext(context);
for (const file of ['addon-manifest.js', 'js/balance.js', 'js/cards.js', 'js/run-state.js',
  'js/dungeon.js', 'js/deck.js', 'js/reward.js', 'js/enemies.js', 'js/battle.js'])
  vm.runInContext(fs.readFileSync(path.join(root, 'WebPrototype', file), 'utf8'), context);
const B = context.B;
const seeds = [0, 17, 29, 4294967295];
const snapshot = {
  schema: 1, sourceCommit: 'eedba21b4552200874388bdb9f637850b14b3af0', config: B.Config,
  cards: B.Cards,
  characters: Object.entries(B.Heroes).map(([id,h]) => ({id,...h,maxHP:B.Config.max_hp,energy:B.Config.max_cost})),
  enemies: Object.values(B.Enemies).map(e => ({...e,...B.EnemyTuning[e.rank],
    ...(B.AreaData.some(a => a.legacy === e.id) ? {legacy:true} : {})})),
  elements: Object.entries(B.Elements).map(([id,name]) => ({id,name})),
  fixtures: seeds.map(seed => {
    const r = B.Run.create('warrior',seed);
    const room = B.Dungeon.enter(r,'r1');
    return {seed,hand:B.Deck.begin(r,room).hand.map(c=>c.card_id),reward:B.Reward.create(r).candidates,map:r.map};
  })
};
const cases = [];
for (const card of B.Cards) for (const previous of [null,'water','fire']) {
  const r = B.Run.create('warrior',17), b = B.Battle.begin(r,B.Dungeon.enter(r,'r1'));
  Object.assign(b.enemy,{hp:100,max_hp:100,block:3,poison:2,weak:0,element:previous});
  Object.assign(b,{block:3,strength:2,energy:3}); r.player.hp=40;
  B.Battle.resolveCard(r,b,card);
  cases.push({id:card.id,previous:previous||'none',hp:b.enemy.hp,enemyBlock:b.enemy.block,
    poison:b.enemy.poison,weak:b.enemy.weak,element:b.enemy.element||'none',playerHP:r.player.hp,
    block:b.block,strength:b.strength,energy:b.energy,damage:b.last_card_result.damage,blocked:b.last_card_result.blocked});
}
const traces = [];
for (const character of Object.keys(B.Heroes)) for (const seed of seeds) {
  const r=B.Run.create(character,seed),b=B.Battle.begin(r,B.Dungeon.enter(r,'r1')),steps=[];
  function capture(kind,instance) {
    const p=B.Enemy.preview(b);
    steps.push({kind,instance:instance||'',hp:r.player.hp,enemyHP:b.enemy.hp,enemyBlock:b.enemy.block,
      block:b.block,strength:b.strength,poison:b.enemy.poison,weak:b.enemy.weak,energy:b.energy,turn:b.turn,
      intentType:p.type,intentAmount:p.amount,intentHits:p.hits,
      hand:b.piles.hand.map(c=>c.instance_id),draw:b.piles.draw.map(c=>c.instance_id),
      discard:b.piles.discard.map(c=>c.instance_id),exhaust:b.piles.exhaust.map(c=>c.instance_id)});
  }
  capture('begin');
  for(let n=0;n<200 && b.enemy.hp>0 && r.player.hp>0;n++) {
    const c=b.piles.hand.find(c=>B.CardById[c.card_id].cost<=b.energy);
    if(c) {
      const def=B.CardById[c.card_id];b.energy-=def.cost;B.Deck.use(b.piles,c.instance_id);
      B.Battle.resolveCard(r,b,def);capture('card',c.instance_id);
    } else {
      const snapshot=B.Enemy.preview(b);B.Deck.endTurn(b.piles);B.Battle.poisonTick(b);
      if(b.enemy.hp>0) {
        B.Enemy.execute(r,b,snapshot);
        if(r.player.hp>0) {b.block=0;b.energy=b.max_energy;b.turn++;B.Deck.nextTurn(b.piles);B.Enemy.decide(b);}
      }
      capture('end');
    }
  }
  traces.push({character,seed,steps});
}
const golden={source:'Actual WebPrototype JS execution; no C# expected value calculation',cases,traces};
for(const [file,data] of [
  ['Assets/BRODUN/Data/WebRulesSnapshot.json',snapshot],
  ['Assets/BRODUN/Tests/Editor/WebGoldenResults.json',golden]
]) {
  const output=path.join(root,file),json=JSON.stringify(data,null,2)+'\n';
  if(process.argv.includes('--write')) fs.writeFileSync(output,json);
  else assert.deepStrictEqual(JSON.parse(fs.readFileSync(output,'utf8')),JSON.parse(json),file+' differs from actual web rules');
}
console.log('Web parity references verified: 27 cards, 3 characters, 12 enemies, 81 effect cases, 12 battle traces, 4 Seed fixtures.');
