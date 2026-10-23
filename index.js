'use strict';
const Fs = require('fs');
const Path = require('path');
const { spawn } = require('child_process');

function initializeSettings(root) {
    const target = Path.join(root, 'resources', 'config', 'window.xml');
    if (Fs.existsSync(target)) return false;
    Fs.mkdirSync(Path.dirname(target), { recursive: true });
    try { Fs.copyFileSync(Path.join(root, 'defaults', 'window.xml'), target, Fs.constants.COPYFILE_EXCL); }
    catch (error) { if (error.code !== 'EEXIST') throw error; }
    return true;
}

class ModernMeter {
    constructor(mod) {
        mod.clientInterface.once('ready', () => {
            if (!require('./runtime-update')(__dirname, text => mod.log(text))) return;
            try { initializeSettings(__dirname); }
            catch (error) { mod.log('Could not initialize Modern Meter settings: ' + error.message); return; }
            mod.log('Starting Modern Meter...');
            const child = spawn(Path.join(__dirname, 'ShinraMeter.exe'), ['--toolbox', '--modern-ui'], { cwd: __dirname, stdio: 'ignore', detached: true, windowsHide: true });
            child.on('error', error => mod.log('Could not start Modern Meter: ' + error.message));
            child.unref();
        });
    }
}
exports.ClientMod = ModernMeter;
exports.initializeSettings = initializeSettings;
