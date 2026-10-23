'use strict';
// Local trial installation only. Original Shinra Meter MIT attribution is retained.
const Fs = require('fs');
const Path = require('path');
const Crypto = require('crypto');

module.exports = function applyLocalUpdate(root, log) {
    const pending = Path.join(root, '.modern-ui-pending');
    const manifestPath = Path.join(pending, 'manifest.json');
    if (!Fs.existsSync(manifestPath)) return true;
    const allowed = ['ShinraMeter.dll', 'DamageMeter.Sniffing.dll', 'ru/Lang.resources.dll'];
    let names = [];
    const hash = file => Crypto.createHash('sha256').update(Fs.readFileSync(file)).digest('hex');
    const changed = [];
    try {
        const manifest = JSON.parse(Fs.readFileSync(manifestPath, 'utf8'));
        names = Object.keys(manifest.files || {});
        if (!names.includes('ShinraMeter.dll') || names.some(name => !allowed.includes(name))) throw new Error('Unexpected pending runtime file list');
        for (const name of names) {
            const entry = manifest.files[name];
            const target = Path.join(root, name);
            if (!entry || hash(Path.join(pending, name)) !== entry.after)
                throw new Error('Pending file verification failed: ' + name);
            const current = hash(target);
            if (current !== entry.before && current !== entry.after)
                throw new Error('Installed file changed since preparation: ' + name);
            // Preflight both files before replacing either. A running meter denies write access.
            const handle = Fs.openSync(target, 'r+');
            Fs.closeSync(handle);
            if (current !== entry.after && hash(Path.join(pending, name + '.before')) !== entry.before)
                throw new Error('Rollback verification failed: ' + name);
        }
        for (const name of names) {
            const target = Path.join(root, name);
            if (hash(target) === manifest.files[name].after) continue;
            changed.push(name);
            Fs.copyFileSync(Path.join(pending, name), target);
            if (hash(target) !== manifest.files[name].after) throw new Error('Installed hash mismatch: ' + name);
        }
        Fs.renameSync(manifestPath, Path.join(pending, 'applied.json'));
        log('Modern Meter update installed.');
        return true;
    } catch (error) {
        for (const name of changed.reverse()) {
            try { Fs.copyFileSync(Path.join(pending, name + '.before'), Path.join(root, name)); }
            catch (rollbackError) { log('Rollback could not complete for ' + name + ': ' + rollbackError.message); }
        }
        log('Modern Meter update is pending: ' + error.message + '. Close the old meter, then reopen Toolbox.');
        return false;
    }
};
