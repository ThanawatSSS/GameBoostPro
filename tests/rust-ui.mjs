import { chromium } from '@playwright/test';
import { mkdir, writeFile } from 'node:fs/promises';
import assert from 'node:assert/strict';
const out=new URL('../artifacts/rust-ui/',import.meta.url);await mkdir(out,{recursive:true});
const browser=await chromium.launch({executablePath:'C:\\Program Files\\BraveSoftware\\Brave-Browser\\Application\\brave.exe',headless:true});
const context=await browser.newContext();
const page=await context.newPage();const errors=[];page.on('pageerror',e=>errors.push(e.message));
await page.addInitScript(()=>{
 const game={id:'steam-730',name:'Counter-Strike 2',source:'steam',path:'C:\\Games\\CS2\\cs2.exe',directory:'C:\\Games\\CS2',launch:'steam://rungameid/730',profile:null};
 const data={admin:true,platform:{manufacturer:'Acer',model:'Nitro AN515',laptop:true,nitroSense:true,supported:true,gpu:['NVIDIA GeForce RTX 4060 Laptop GPU','Intel UHD Graphics'],powerName:'Nezha',ac:true},running:[],session:null,legacyRecovery:false,error:null,cpu:12.4,ramPercent:38,ramUsedGb:12.2,ramTotalGb:32,scanMs:0.4,frames:[],capturing:false,config:{language:'TH',master:'balanced',autoMode:false,gameMode:true,captureOff:true,gpuPreference:true,priority:true,keepPower:true,telemetry:true,selected:game.id,games:[game]}};
 window.testCalls=[];window.testStatus=data;
 window.__TAURI__={core:{invoke:async(cmd,args={})=>{window.testCalls.push({cmd,args});
  switch(cmd){case 'get_status':return structuredClone(data);case 'save_settings':data.config=structuredClone(args.config);return;
   case 'boost':data.session={started:Math.floor(Date.now()/1000),profile:data.config.master,game_name:'Counter-Strike 2',notes:[],changes:[]};return;
   case 'restore':data.session=null;return [];
   case 'scan_games':return 0;case 'launch_game':return;case 'add_game':return null;case 'open_settings':return;
   default:throw new Error('Unexpected IPC command: '+cmd);}
 }}};
});
await page.goto('http://127.0.0.1:1420');await page.locator('#boost').waitFor();
await page.locator('#boost').click();await page.getByRole('button',{name:'คืนค่าปกติ'}).waitFor();await page.locator('#boost').click();await page.getByRole('button',{name:'เปิด Boost',exact:true}).waitFor();
await page.locator('[data-view=library]').first().click();assert.equal(await page.locator('#game-profile').isDisabled(),true);
await page.locator('#override').check();await page.waitForFunction(()=>window.testStatus.config.games[0].profile==='balanced');assert.equal(await page.locator('#game-profile').isDisabled(),false);
await page.locator('#game-profile').fill('2');await page.locator('#game-profile').dispatchEvent('change');await page.waitForFunction(()=>window.testStatus.config.games[0].profile==='performance');
await page.locator('#launch-library').click();await page.waitForFunction(()=>window.testCalls.some(c=>c.cmd==='launch_game'&&c.args.id==='steam-730'));
await page.locator('#override').uncheck();await page.waitForFunction(()=>window.testStatus.config.games[0].profile===null);
await page.locator('#search').fill('<img src=x onerror=alert(1)>');assert.equal(await page.locator('.game-row').count(),0);await page.locator('#search').fill('');
for(const lang of ['TH','EN']){
 await page.locator('#language').selectOption(lang);await page.waitForFunction(l=>window.testStatus.config.language===l,lang);
 for(const size of [{width:1440,height:900},{width:1160,height:790},{width:800,height:650},{width:660,height:540},{width:390,height:844}]){
  await page.setViewportSize(size);
  for(const view of ['home','library','graphics','frames','settings']){
   await page.locator(`[data-view=${view}]`).first().click();
   const issues=await page.evaluate(()=>{const root=document.documentElement;const problems=[];if(root.scrollWidth>innerWidth+1)problems.push('page horizontal overflow '+root.scrollWidth+'/'+innerWidth);
    for(const e of document.querySelectorAll('button,h1,h2,.badge,.effect-row,.hardware-row')){const r=e.getBoundingClientRect();if(r.width&&r.left< -1)problems.push('left clipped: '+e.textContent);if(r.width&&r.right>innerWidth+1)problems.push('right clipped: '+e.textContent);}
    for(const e of document.images)if(!e.complete||e.naturalWidth===0)problems.push('missing image '+e.src);return problems;});
   assert.deepEqual(issues,[],`${lang}/${view}/${size.width}`);
   if(size.width===1160||size.width===660)await page.screenshot({path:new URL(`${lang}-${view}-${size.width}.png`,out).pathname.replace(/^\/(\w:)/,'$1'),fullPage:true});
  }
 }
}
assert.deepEqual(errors,[]);
await writeFile(new URL('result.json',out),JSON.stringify({passed:true,languages:2,viewports:5,views:5,flow:'Boost/Restore, per-game Override/inheritance, selected-game-only launch, search injection, all views',errors},null,2));
await browser.close();console.log('UI flow and 50 layout combinations passed');
