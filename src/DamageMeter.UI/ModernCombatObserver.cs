using System;
using System.IO;
using Data;
using Tera;
using Tera.Game;
using Tera.Sniffing;

namespace DamageMeter.UI;

// Read-only observation of the existing stream; no hooks, injection or duplicate full packet parser.
public sealed class ModernCombatObserver : IDisposable
{
    private readonly ITeraSniffer _sniffer;
    private readonly Func<ulong?> _self;
    private readonly Action<bool> _changed;
    private readonly Func<uint, string, ushort> _opcode;
    private readonly Func<ulong?> _target;
    private readonly object _gate = new();
    private uint _version;
    private ushort _status, _load, _life, _despawn;
    private bool _characterInCombat;
    private ulong? _finishedTarget;
    public bool IsInCombat { get { lock (_gate) return _characterInCombat && (!_finishedTarget.HasValue || _target() != _finishedTarget); } }
    public ModernCombatObserver(ITeraSniffer sniffer, Func<ulong?> self, Action<bool> changed,
        Func<uint, string, ushort>? opcode = null, Func<ulong?>? target = null)
    {
        _sniffer = sniffer; _self = self; _changed = changed;
        _target = target ?? (() => null);
        _opcode = opcode ?? ((version, name) => new OpCodeNamer(Path.Combine(BasicTeraData.Instance.ResourceDirectory, $"data/opcodes/{version}.txt")).GetCode(name));
        sniffer.MessageReceived += Observe; sniffer.EndConnection += Reset; sniffer.NewConnection += OnConnection;
    }
    private void OnConnection(Server server) => Reset();
    private void Reset() { lock (_gate) { _version = 0; _status = _load = _life = _despawn = 0; _characterInCombat = false; _finishedTarget = null; _changed(false); } }
    public void TryConfigure()
    {
        try
        {
        lock (_gate)
        {
            if (_version == 0 || _status != 0) return;
            _status = _opcode(_version, "S_USER_STATUS"); _load = _opcode(_version, "S_LOAD_TOPO");
            _life = _opcode(_version, "S_CREATURE_LIFE"); _despawn = _opcode(_version, "S_DESPAWN_NPC");
        }
        }
        catch { /* Optional status stays unavailable until the map is readable. */ }
    }
    private void Observe(Message message)
    {
        try
        {
            lock (_gate)
            {
            if (message.OpCode == 19900)
            {
                ReadVersion(message);
                return;
            }
            if (_load != 0 && message.OpCode == _load) { _characterInCombat = false; _finishedTarget = null; _changed(false); return; }
            if (message.Data.Count >= 23 && ((_life != 0 && message.OpCode == _life) || (_despawn != 0 && message.OpCode == _despawn)))
            {
                var id = BitConverter.ToUInt64(message.Data.Array!, message.Data.Offset + 2);
                var state = message.Data.Array![message.Data.Offset + 22];
                if (message.OpCode == _life && state == 0 && id == _self()) { _characterInCombat = false; _changed(false); }
                if (((message.OpCode == _life && state == 0) || (message.OpCode == _despawn && (state == 3 || state == 5))) && id == _target())
                { _finishedTarget = id; _changed(false); }
                return;
            }
            if (_status == 0 || message.OpCode != _status || message.Data.Count < 11) return;
            var data = message.Data;
            if (_self() != BitConverter.ToUInt64(data.Array!, data.Offset + 2)) return;
            _characterInCombat = data.Array![data.Offset + 10] == 1; // 0 idle, 1 combat, 2 campfire
            _changed(IsInCombat);
            }
        }
        catch { /* Optional visual status must never interrupt the meter's actual packet processing. */ }
    }
    private void ReadVersion(Message message)
    {
        var data = message.Data;
        if (data.Count < 6) return;
        var count = BitConverter.ToUInt16(data.Array!, data.Offset + 2);
        var offset = BitConverter.ToUInt16(data.Array!, data.Offset + 4);
        for (var i = 0; i < count; i++)
        {
            var index = offset - 2; // Game offsets include the length word omitted by the receiver.
            if (index < 6 || index + 12 > data.Count) return;
            if (BitConverter.ToUInt32(data.Array!, data.Offset + index + 4) == 0)
            {
                _version = BitConverter.ToUInt32(data.Array!, data.Offset + index + 8);
                _status = _load = _life = _despawn = 0; TryConfigure(); return;
            }
            offset = BitConverter.ToUInt16(data.Array!, data.Offset + index + 2);
        }
    }
    public void Dispose()
    {
        _sniffer.MessageReceived -= Observe; _sniffer.EndConnection -= Reset; _sniffer.NewConnection -= OnConnection;
    }
}
