'use strict';
// Local runtime installation. Original Shinra Meter MIT attribution is retained.
const Fs = require('fs');
const Path = require('path');
const Crypto = require('crypto');

module.exports = function applyLocalUpdate(root, log) {
    const pending = Path.join(root, '.modern-ui-pending');
    const manifestPath = Path.join(pending, 'manifest.json');
    const renamed = ['ShinraMeter.dll', 'ShinraMeter.exe', 'ShinraMeter.deps.json', 'ShinraMeter.runtimeconfig.json', 'ShinraMeter.dll.config'];
    const legacy = renamed.concat(['Autoupdate.exe', 'Autoupdate.deps.json', 'Autoupdate.runtimeconfig.json']);
    const allowed = ['ModernMeter.dll', 'ModernMeter.exe', 'ModernMeter.deps.json', 'ModernMeter.runtimeconfig.json', 'ModernMeter.dll.config',
        'resources/img/shinra.ico', 'DamageMeter.dll', 'DamageMeter.Sniffing.dll', 'ru/Lang.resources.dll',
        'resources/data/hotdot/hotdot-EU-EN.tsv', 'resources/data/hotdot/hotdot-RU.tsv', 'resources/data/icons.zip'];
    const hash = file => Crypto.createHash('sha256').update(Fs.readFileSync(file)).digest('hex');
    const fingerprint = file => Fs.existsSync(file) ? hash(file) : null;
    const writable = file => { const handle = Fs.openSync(file, 'r+'); Fs.closeSync(handle); };
    const changed = [];
    const retired = [];
    let manifest;
    try {
        if (!Fs.existsSync(manifestPath)) {
            const oldNames = legacy.filter(name => Fs.existsSync(Path.join(root, name)));
            if (!oldNames.length) return true;
            const newNames = renamed.map(name => name.replace('ShinraMeter', 'ModernMeter'));
            // Toolbox downloads new names without removing obsolete files. Only stage
            // retirement after all five replacement files match this package's manifest.
            if (!newNames.every(name => Fs.existsSync(Path.join(root, name)))) return true;
            const published = JSON.parse(Fs.readFileSync(Path.join(root, 'manifest.json'), 'utf8')).files;
            if (!newNames.every(name => published[name] === hash(Path.join(root, name))))
                throw new Error('Modern runtime verification failed before filename migration');
            Fs.mkdirSync(pending, { recursive: true });
            const migration = { schema: 2, files: {}, retire: {} };
            for (const name of newNames) {
                const current = hash(Path.join(root, name));
                migration.files[name] = { before: current, after: current };
                Fs.copyFileSync(Path.join(root, name), Path.join(pending, name));
                Fs.copyFileSync(Path.join(root, name), Path.join(pending, name + '.before'));
            }
            for (const name of oldNames) {
                migration.retire[name] = hash(Path.join(root, name));
                Fs.copyFileSync(Path.join(root, name), Path.join(pending, name + '.before'));
            }
            Fs.writeFileSync(manifestPath, JSON.stringify(migration, null, 2), { flag: 'wx' });
        }
        manifest = JSON.parse(Fs.readFileSync(manifestPath, 'utf8'));
        const modern = manifest.schema === 2;
        const names = Object.keys(manifest.files || {});
        const retireNames = Object.keys(manifest.retire || {});
        const permitted = modern ? allowed : ['ShinraMeter.dll', 'ShinraMeter.exe', 'resources/img/shinra.ico', 'DamageMeter.Sniffing.dll', 'ru/Lang.resources.dll'];
        if (!names.includes(modern ? 'ModernMeter.dll' : 'ShinraMeter.dll') || names.some(name => !permitted.includes(name))
            || retireNames.some(name => !modern || !legacy.includes(name))) throw new Error('Unexpected pending runtime file list');
        // Verify all payloads and preflight old files too: a running old meter defers the whole migration.
        for (const name of names) {
            const entry = manifest.files[name];
            const target = Path.join(root, name);
            if (!entry || !/^[a-f0-9]{64}$/.test(entry.after) || hash(Path.join(pending, name)) !== entry.after)
                throw new Error('Pending file verification failed: ' + name);
            const current = fingerprint(target);
            if (current !== entry.before && current !== entry.after) throw new Error('Installed file changed since preparation: ' + name);
            if (current !== null) writable(target);
            if (entry.before !== null && hash(Path.join(pending, name + '.before')) !== entry.before)
                throw new Error('Rollback verification failed: ' + name);
        }
        for (const name of retireNames) {
            const expected = manifest.retire[name];
            const target = Path.join(root, name);
            if (!/^[a-f0-9]{64}$/.test(expected) || hash(Path.join(pending, name + '.before')) !== expected)
                throw new Error('Legacy rollback verification failed: ' + name);
            const current = fingerprint(target);
            if (current === null) continue;
            if (current !== expected) throw new Error('Legacy file changed since preparation: ' + name);
            writable(target);
        }
        for (const name of names) {
            const target = Path.join(root, name);
            if (fingerprint(target) === manifest.files[name].after) continue;
            changed.push(name);
            Fs.copyFileSync(Path.join(pending, name), target);
            if (hash(target) !== manifest.files[name].after) throw new Error('Installed hash mismatch: ' + name);
        }
        for (const name of retireNames) {
            const target = Path.join(root, name);
            if (!Fs.existsSync(target)) continue;
            retired.push(name);
            Fs.unlinkSync(target);
        }
        Fs.renameSync(manifestPath, Path.join(pending, 'applied.json'));
        log('Modern Meter update installed.');
        return true;
    } catch (error) {
        for (const name of retired.reverse()) {
            try { Fs.copyFileSync(Path.join(pending, name + '.before'), Path.join(root, name)); }
            catch (rollbackError) { log('Rollback could not complete for ' + name + ': ' + rollbackError.message); }
        }
        for (const name of changed.reverse()) {
            try {
                const target = Path.join(root, name);
                if (manifest.files[name].before === null) { if (Fs.existsSync(target)) Fs.unlinkSync(target); }
                else Fs.copyFileSync(Path.join(pending, name + '.before'), target);
            } catch (rollbackError) { log('Rollback could not complete for ' + name + ': ' + rollbackError.message); }
        }
        log('Modern Meter update is pending: ' + error.message + '. It will retry on the next normal Toolbox start.');
        return false;
    }
};
