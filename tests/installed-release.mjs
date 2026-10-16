import fs from 'node:fs';
import crypto from 'node:crypto';
import assert from 'node:assert/strict';

const built=fs.readFileSync('src-tauri/target/release/gameboostpro.exe');
const installed=fs.readFileSync('C:/Program Files/Game Boost Pro/gameboostpro.exe');
// Tauri temporarily patches its bundle-type marker while producing NSIS, then
// resets the build output to UNK. Compare every byte against that exact variant.
const marker=Buffer.from('__TAURI_BUNDLE_TYPE_VAR_UNK');
const index=built.indexOf(marker);
assert.ok(index>=0,'Expected the Tauri bundle marker');
assert.equal(built.indexOf(marker,index+1),-1,'Marker must be unique');
const expected=Buffer.from(built);
Buffer.from('__TAURI_BUNDLE_TYPE_VAR_NSS').copy(expected,index);
assert.ok(installed.equals(expected),'Installed image differs beyond the documented NSIS marker');
const sha=b=>crypto.createHash('sha256').update(b).digest('hex');
const result={passed:true,installedSha256:sha(installed),portableSha256:sha(built),difference:'Only __TAURI_BUNDLE_TYPE_VAR_UNK -> __TAURI_BUNDLE_TYPE_VAR_NSS',allOtherBytesEqual:true};
fs.writeFileSync('artifacts/installed-binary-verification.json',JSON.stringify(result,null,2));
console.log(JSON.stringify(result));
