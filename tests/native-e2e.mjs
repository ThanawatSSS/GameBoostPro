import { chromium } from '@playwright/test';
import { mkdir, writeFile, readFile, stat } from 'node:fs/promises';
import path from 'node:path';
import assert from 'node:assert/strict';

// Attach only to a disposable debug build started by the local engineering harness.
const browser=await chromium.connectOverCDP('http://127.0.0.1:9223');
const context=browser.contexts()[0];
const page=context.pages().find(p=>/tauri\.localhost/.test(p.url()));
if(!page)throw new Error('Expected the dedicated embedded Tauri debug page');
const out=path.resolve('artifacts/native-e2e');await mkdir(out,{recursive:true});
await page.waitForFunction(()=>!!window.__TAURI__?.core?.invoke);
const call=(command,args={})=>page.evaluate(({command,args})=>window.__TAURI__.core.invoke(command,args),{command,args});
const status=()=>call('get_status');
const initial=await status();assert.equal(initial.admin,true);assert.equal(initial.session,null);
const original=structuredClone(initial.config);
const report={nativeBackend:true,admin:true,checks:[],captureOnly:process.argv.includes('--capture-only')};
const waitConfig=async(test)=>{for(let n=0;n<100;n++){if(test((await status()).config))return;await new Promise(r=>setTimeout(r,100));}throw new Error('Native settings did not settle');};
try{
 if(report.captureOnly){
  const before=(await status()).frames.length;
  await page.locator('[data-view=frames]').first().click();
  assert.equal(await page.locator('#record').isDisabled(),false,'A rendering game must be detected');
  await page.locator('#record').click();console.log('FRAME_CAPTURE_STARTED');
  for(let n=0;n<90;n++){if((await status()).frames.length>before)break;await new Promise(r=>setTimeout(r,500));}
  const after=await status();const frame=after.frames.at(-1);
  assert.ok(frame.frames>=30);assert.ok(frame.averageFps>0);assert.ok(frame.lowFps>0);
  report.checks.push('Real PresentMon stdout capture');report.frames=frame.frames;report.averageFps=frame.averageFps;report.lowFps=frame.lowFps;
  await page.screenshot({path:path.join(out,'real-frame-lab.png'),fullPage:true});
 }else{
  const c=structuredClone(original);c.language='EN';c.autoMode=false;c.keepPower=true;c.selected=null;c.master='quality';
  await call('save_settings',{config:c});await page.reload();await page.locator('#boost').waitFor();
  await page.locator('#boost').click();await page.waitForFunction(async()=>!!(await window.__TAURI__.core.invoke('get_status')).session);
  assert.equal((await status()).session.profile,'quality');
  await page.screenshot({path:path.join(out,'real-boost.png'),fullPage:true});
  await page.locator('#boost').click();await page.waitForFunction(async()=>!(await window.__TAURI__.core.invoke('get_status')).session);
  report.checks.push('Actual UI → Rust Boost → journal → Restore round trip');
  const fixture=path.resolve('artifacts/GameBoostLaunchFixture.exe');
  const testGame={id:'qa-native-launch',name:'QA standard-user launch',source:'manual',path:fixture,launch:fixture,directory:path.dirname(fixture),profile:null};
  const library=(await status()).config;library.games.push(testGame);library.selected=testGame.id;
  await call('save_settings',{config:library});await page.reload();await page.locator('[data-view=library]').first().click();
  assert.equal(await page.locator('#game-profile').isDisabled(),true);
  await page.locator('#override').check();await waitConfig(c=>c.games.find(g=>g.id===testGame.id)?.profile==='quality');
  await page.locator('#game-profile').fill('2');await page.locator('#game-profile').dispatchEvent('change');
  await waitConfig(c=>c.games.find(g=>g.id===testGame.id)?.profile==='performance');
  const started=Date.now();await page.locator('#launch-library').click();
  const receipt=path.join(process.env.LOCALAPPDATA,'CodexGameBoost','qa-launch.json');
  let launched=null;for(let n=0;n<100;n++){try{if((await stat(receipt)).mtimeMs>=started-1000){launched=JSON.parse(await readFile(receipt,'utf8'));break;}}catch{}await new Promise(r=>setTimeout(r,100));}
  if(!launched){try{await call('launch_game',{id:testGame.id});}catch(e){throw new Error(`Native launch failed: ${e}`);}}
  assert.ok(launched,'The selected fixture must actually launch');assert.equal(launched.elevated,false,'Launched game must not inherit Admin');
  report.checks.push('Per-game Override persists through native IPC','Selected game launches with non-elevated token');
  await page.locator('#override').uncheck();await waitConfig(c=>c.games.find(g=>g.id===testGame.id)?.profile===null);
  for(const view of ['home','library','graphics','frames','settings']){
   await page.locator(`[data-view=${view}]`).first().click();await page.screenshot({path:path.join(out,`real-${view}.png`),fullPage:true});
   assert.equal(await page.evaluate(()=>document.documentElement.scrollWidth>innerWidth+1),false);
  }
  report.checks.push('All five embedded native views render without horizontal overflow');
 }
 report.passed=true;
}catch(e){report.passed=false;report.error=String(e);report.visible=(await page.locator('body').innerText()).slice(-2500);throw e;}
finally{
 try{if((await status()).session)await call('restore');if(!report.captureOnly)await call('save_settings',{config:original});}catch(e){report.cleanupError=String(e);}
 await writeFile(path.join(out,report.captureOnly?'capture-result.json':'controls-result.json'),JSON.stringify(report,null,2));
 await browser.close();
}
console.log(JSON.stringify(report));
